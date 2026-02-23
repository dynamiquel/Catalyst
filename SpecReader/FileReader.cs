using System.Text.Json;
using Catalyst.Generators;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using HttpMethod = Catalyst.SpecGraph.Nodes.HttpMethod;

namespace Catalyst.SpecReader;

/// <summary>
/// Responsible for reading a Spec File and converting it to a FileNode, which can be
/// used to create a Spec Graph.
/// </summary>
public class FileReader
{
    public required ILogger<FileReader> Logger { get; init; }
    public required DirectoryInfo BaseDir { get; init; }
    protected List<OptionsReader> LanguageFileReaders = [];

    public string GetBuiltSpecFilePath(FileInfo fileInfo)
    {
        // Converts the actual spec file path to a more suitable built name.
        // i.e. /home/project/specs/common/user.yaml -> common/user
        string fileNodePath = Path.GetRelativePath(BaseDir.FullName, fileInfo.FullName);
        fileNodePath = Path.ChangeExtension(fileNodePath, null);
        return fileNodePath;
    }
    
    public void AddGeneratorOptionsReader<T>() where T : OptionsReader, new()
    {
        if (LanguageFileReaders.Any(x => x.GetType() == typeof(T)))
            throw new InvalidOperationException($"A Language File Reader of type {typeof(T).Name} already exists");
        
        LanguageFileReaders.Add(new T());
    }
    
    public async Task<RawFileNode> ReadRawSpec(FileInfo specFileInfo)
    {
        string fileContent = await File.ReadAllTextAsync(specFileInfo.FullName);
        var deserializer = new DeserializerBuilder().Build();

        // Yaml will deserialise the object as a Dictionary<string, object> if no type specified.
        var deserialisedObject = deserializer.Deserialize(fileContent) as Dictionary<object, object>;
        if (deserialisedObject is null)
        {
            throw new SpecFileDeserialiseException
            {
                FileName = specFileInfo.FullName
            };
        }

        return new RawFileNode(specFileInfo, deserialisedObject);
    }

    public FileNode ReadFileFromSpec(RawFileNode rawFileNode)
    {
        Logger.LogInformation("Reading spec file {FilePath}", rawFileNode.FileInfo.FullName);

        string builtFilePath = GetBuiltSpecFilePath(rawFileNode.FileInfo);
        
        FileNode fileNode = new()
        {
            Parent = null,
            FilePath = builtFilePath,
            Name = builtFilePath
        };
        
        string? format = rawFileNode.ReadPropertyAsStr("format");
        if (format is not null)
            fileNode.Format = format.ToLower();

        fileNode.Namespace = rawFileNode.ReadPropertyAsStr("namespace");
        
        ReadIncludes(rawFileNode, fileNode);
        ReadFileCompilerOptions(rawFileNode, fileNode);
        ReadEnums(rawFileNode, fileNode);
        ReadDefinitions(rawFileNode, fileNode);
        ReadServices(rawFileNode, fileNode);
        
        return fileNode;
    }

    void ReadIncludes(RawFileNode rawFileNode, FileNode fileNode)
    {
        List<object>? includeSpecs = rawFileNode.ReadPropertyAsList("includes");
        if (includeSpecs is null) 
            return;
        
        Logger.LogInformation("Found {Count} include specs in {FilePath}", includeSpecs.Count, fileNode.FullName);
        
        foreach (var includeSpec in includeSpecs)
        {
            var includeSpecStr = includeSpec as string;
            if (includeSpecStr is null)
            {
                throw new UnexpectedTypeException
                {
                    RawNode = rawFileNode,
                    LeafName = "includes",
                    ExpectedType = nameof(String),
                    ReceivedType = includeSpec.GetType().Name
                };
            }
            
            if (!Path.HasExtension(includeSpecStr))
                includeSpecStr += ".yaml";
            
            string relativeIncludePath = Path.Combine(fileNode.Directory ?? string.Empty, includeSpecStr);
            string absoluteIncludePath = Path.Combine(BaseDir.FullName, relativeIncludePath);
            if (!File.Exists(absoluteIncludePath))
            {
                throw new IncludeNotFoundException
                {
                    RawNode = rawFileNode,
                    LeafName = "includes",
                    IncludeFile = $"{includeSpecStr} (Absolute Path: {absoluteIncludePath})"
                };
            }
            
            fileNode.IncludeSpecs.Add(includeSpecStr);
        }
    }
    
    void ReadEnums(RawFileNode rawFileNode, FileNode fileNode)
    {
        Logger.LogDebug("Reading enums in {FilePath}", fileNode.FullName);

        Dictionary<object, object>? enums = rawFileNode.ReadPropertyAsDictionary("enums");
        if (enums is null) 
            return;
        
        Logger.LogInformation("Found {Count} enums in {FilePath}", enums.Count, fileNode.FullName);
        
        RawNode enumsRawNode = rawFileNode.CreateChild(enums, "enums");
            
        foreach (KeyValuePair<object, object> enumKeyValue in enums)
        {
            string enumName = (string)enumKeyValue.Key;
            Dictionary<object, object> enumObject;

            if (enumKeyValue.Value is List<object> enumValuesList)
            {
                // Enums may be specified as a list of values rather than an object containing a values' field.
                enumObject = new Dictionary<object, object>
                {
                    ["values"] = enumValuesList
                };
            }
            else if (enumKeyValue.Value is Dictionary<object, object> enumObjectTest)
            {
                enumObject = enumObjectTest;
            }
            else
            {
                throw new UnexpectedTypeException
                {
                    RawNode = enumsRawNode,
                    LeafName = enumName,
                    ExpectedType = nameof(Dictionary<object, object>),
                    ReceivedType = enumKeyValue.Value.GetType().Name
                };
            }
            
            RawNode enumRawNode = enumsRawNode.CreateChild(enumObject, enumName);
            ReadEnum(fileNode, enumName, enumRawNode);
        }
    }

    void ReadEnum(FileNode fileNode, string enumName, RawNode enumRawNode)
    {
        EnumNode enumNode = new()
        {
            Parent = new WeakReference<Node>(fileNode),
            Name = enumName,
            Description = (enumRawNode.ReadPropertyAsStr("description") ?? enumRawNode.ReadPropertyAsStr("desc"))?.TrimEnd(),
            Flags = enumRawNode.ReadPropertyAsBool("flags")
        };
        
        Logger.LogDebug("Reading enum {EnumName} in {FilePath}", enumNode.Name, fileNode.FullName);

        ReadEnumCompilerOptions(fileNode, enumRawNode, enumNode);
        
        List<object>? enumValues = enumRawNode.ReadPropertyAsList("values");
        if (enumValues is null)
        {
            throw new ExpectedTokenNotFoundException
            {
                RawNode = enumRawNode,
                TokenName = "values"
            };
        }
        
        Logger.LogDebug("Found {Count} values in enum {EnumFullName}", enumValues.Count, enumNode.FullName);
        
        int prevIntegerValue = -1;
        for (var valueIdx = 0; valueIdx < enumValues.Count; valueIdx++)
        {
            object enumValue = enumValues[valueIdx];
            
            string label;
            int numericValue;

            switch (enumValue)
            {
                case string valueStr:
                    label = valueStr;

                    if (valueIdx == enumValues.Count - 1)
                    {
                        if (enumNode.Flags == true && label.Equals("All", StringComparison.OrdinalIgnoreCase))
                        {
                            numericValue = ~0;
                            break;
                        }

                        if (enumNode.Flags == false && label.Equals("Max", StringComparison.OrdinalIgnoreCase))
                        {
                            numericValue = enumNode.Values.OrderBy(x => x.Value).Last().Value;
                            break;
                        }
                    }

                    // Double the value if using flags and the previous value is a power of two.
                    if (enumNode.Flags == true && prevIntegerValue != 0 &&
                        (prevIntegerValue & (prevIntegerValue - 1)) == 0)
                        numericValue = prevIntegerValue * 2;
                    else
                        numericValue = prevIntegerValue + 1;
                    break;
                // Yaml will make the map as a single entry, which will look like:
                // enumValueLabel: enumValueInt.
                case Dictionary<object, object> map when map.Count != 1:
                    throw new UnexpectedTokenException
                    {
                        RawNode = enumRawNode,
                        LeafName = enumName,
                        TokenName = "enumValue"
                    };
                case Dictionary<object, object> map:
                {
                    KeyValuePair<object, object> enumValueKeyValue = map.First();
                    if (enumValueKeyValue.Key is not string potentialLabel)
                    {
                        throw new UnexpectedTypeException
                        {
                            RawNode = enumRawNode,
                            LeafName = enumName,
                            ExpectedType = "string",
                            ReceivedType = enumValueKeyValue.Key.GetType().Name
                        };
                    }

                    if (enumValueKeyValue.Value is not int potentialNumericValue)
                    {
                        string? enumValueStr = enumValueKeyValue.Value as string;
                        if (!string.IsNullOrEmpty(enumValueStr))
                        {
                            if (enumValueStr.StartsWith('^') && int.TryParse(enumValueStr[1..], out int shiftedValue))
                            {
                                // Determine integer value based on bitwise shift syntax.
                                potentialNumericValue = 1 << shiftedValue;

                                // Bitwise shift implies flags.
                                enumNode.Flags ??= true;
                            }
                            else if (int.TryParse(enumValueKeyValue.Value as string, out int value))
                            {
                                // Sometimes Yaml parses an actual integer as a string for some reason.
                                potentialNumericValue = value;
                            }
                            else
                            {
                                // Determine integer value based on string enum values, including flags.
                                potentialNumericValue = 0;
                                string[] enumFlags = enumValueStr.Split('|').Select(x => x.Trim()).ToArray();

                                if (enumFlags.Length == 0)
                                {
                                    throw new UnexpectedTypeException
                                    {
                                        RawNode = enumRawNode,
                                        LeafName = enumName,
                                        ExpectedType = "int",
                                        ReceivedType = enumValueKeyValue.Value.GetType().Name
                                    };
                                }

                                if (enumFlags.Length > 1)
                                {
                                    // Multiple enum values implies flags.
                                    enumNode.Flags ??= true;
                                }

                                foreach (string enumFlag in enumFlags)
                                {
                                    if (!enumNode.Values.TryGetValue(enumFlag, out int flagValue))
                                    {
                                        throw new UnexpectedTokenException
                                        {
                                            RawNode = enumRawNode,
                                            LeafName = potentialLabel,
                                            TokenName = enumFlag
                                        };
                                    }

                                    potentialNumericValue |= flagValue;
                                }
                            }
                        }
                        else
                        {
                            throw new UnexpectedTypeException
                            {
                                RawNode = enumRawNode,
                                LeafName = enumName,
                                ExpectedType = "int",
                                ReceivedType = enumValueKeyValue.Value.GetType().Name
                            };
                        }
                    }

                    label = potentialLabel;
                    numericValue = potentialNumericValue;
                    break;
                }
                default:
                    throw new UnexpectedTypeException
                    {
                        RawNode = enumRawNode,
                        LeafName = enumName,
                        ExpectedType = nameof(Dictionary<object, object>),
                        ReceivedType = enumValue.GetType().Name
                    };
            }

            if (!enumNode.Values.TryAdd(label, numericValue))
            {
                throw new ExistingEnumValueFoundException
                {
                    RawNode = enumRawNode,
                    LeafName = enumName,
                    EnumValueLabel = label
                };
            }

            prevIntegerValue = numericValue;
        }

        fileNode.Enums.Add(enumName, enumNode);
    }
    
    void ReadDefinitions(RawFileNode rawFileNode, FileNode fileNode)
    {
        Logger.LogDebug("Reading definitions in {FilePath}", fileNode.FullName);

        Dictionary<object, object>? definitions = rawFileNode.ReadPropertyAsDictionary("definitions");
        if (definitions is null) 
            return;
        
        Logger.LogInformation("Found {Count} definitions in {FilePath}", definitions.Count, fileNode.FullName);
        
        RawNode definitionsRawNode = rawFileNode.CreateChild(definitions, "definitions");
            
        foreach (KeyValuePair<object, object> definition in definitions)
        {
            string definitionName = ((string)definition.Key);
            Dictionary<object, object>? definitionValue = definition.Value as Dictionary<object, object>;
            if (definitionValue is null)
            {
                throw new UnexpectedTypeException
                {
                    RawNode = definitionsRawNode,
                    LeafName = definitionName,
                    ExpectedType = nameof(Dictionary<object, object>),
                    ReceivedType = definition.Value.GetType().Name
                };
            }

            RawNode definitionRawNode = definitionsRawNode.CreateChild(definitionValue, definitionName);
            ReadDefinition(fileNode, definitionName, definitionRawNode);
        }
    }

    void ReadDefinition(FileNode fileNode, string definitionName, RawNode definitionRawNode)
    {
        DefinitionNode definitionNode = new()
        {
            Parent = new WeakReference<Node>(fileNode),
            Name = definitionName,
            Description = (definitionRawNode.ReadPropertyAsStr("description") ?? definitionRawNode.ReadPropertyAsStr("desc"))?.TrimEnd()
        };
        
        Logger.LogDebug("Reading definition {DefinitionName} in {FilePath}", definitionNode.Name, fileNode.FullName);

        ReadDefinitionCompilerOptions(fileNode, definitionRawNode, definitionNode);

        if (definitionRawNode.Internal.Count > 0)
        {
            Dictionary<object, object>? properties = definitionRawNode.ReadPropertyAsDictionary("properties");
            properties ??= definitionRawNode.ReadPropertyAsDictionary("props");
            if (properties is null)
            {
                throw new ExpectedTokenNotFoundException
                {
                    RawNode = definitionRawNode,
                    TokenName = "properties"
                };
            }

            Logger.LogDebug("Found {Count} properties in definition {DefinitionFullName}", properties.Count, definitionNode.FullName);

            RawNode propertiesRawNode = definitionRawNode.CreateChild(properties, "properties");

            foreach (KeyValuePair<object, object> property in properties)
            {
                string propertyName = ((string)property.Key);
                ReadProperty(definitionNode, propertiesRawNode, propertyName, property.Value);
            }
        }

        Dictionary<object, object>? constants = definitionRawNode.ReadPropertyAsDictionary("constants");
        constants ??= definitionRawNode.ReadPropertyAsDictionary("consts");
        if (constants is not null)
        {
            Logger.LogDebug("Found {Count} constants in definition {DefinitionFullName}", constants.Count, definitionNode.FullName);

            RawNode constantsRawNode = definitionRawNode.CreateChild(constants, "constants");

            foreach (KeyValuePair<object, object> constant in constants)
            {
                string constantName = ((string)constant.Key);
                ReadConstant(definitionNode, constantsRawNode, constantName, constant.Value);
            }
        }

        fileNode.Definitions.Add(definitionName, definitionNode);
    }

    void ReadProperty(DefinitionNode definitionNode, RawNode propertiesRawNode, string propertyName, object propertyValue)
    {
        Logger.LogDebug("Reading property {PropertyName} in {DefinitionFullName}", propertyName, definitionNode.FullName);

        Dictionary<object, object>? propertyObjectValue;
        
        if (propertyValue is string propertyShortValue)
        {
            // Short property declaration was used, where only a type is specified.
            // Convert this to the standard property schema so it's easier to parse.
            propertyShortValue = propertyShortValue.Replace(" ", string.Empty);
            propertyObjectValue = new Dictionary<object, object>
            {
                ["type"] = propertyShortValue
            };
        }
        else
        {
            propertyObjectValue = propertyValue as Dictionary<object, object>;
            if (propertyObjectValue is null)
            {
                throw new UnexpectedTypeException
                {
                    RawNode = propertiesRawNode,
                    LeafName = propertyName,
                    ExpectedType = nameof(Dictionary<object, object>),
                    ReceivedType = propertyValue.GetType().Name
                };
            }
        }
        
        RawNode propertyRawNode = propertiesRawNode.CreateChild(propertyObjectValue, propertyName);

        string? propertyType = propertyRawNode.ReadPropertyAsStr("type");
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            throw new ExpectedTokenNotFoundException
            {
                RawNode = propertyRawNode,
                TokenName = "type"
            };
        }
        
        propertyType = propertyType.Replace(" ", string.Empty);

        UnBuiltValue? unBuiltValue = null;
        object? propertyDefaultValue = propertyRawNode.Internal.GetValueOrDefault("default");
        if (propertyDefaultValue is not null)
        {
            unBuiltValue = new UnBuiltValue(JsonSerializer.Serialize(
                propertyDefaultValue, 
                new JsonSerializerOptions{ WriteIndented = true }));
        }

        PropertyNode propertyNode = new()
        {
            Parent = new WeakReference<Node>(definitionNode),
            Name = propertyName,
            UnBuiltType = propertyType,
            Description = (propertyRawNode.ReadPropertyAsStr("description") ?? propertiesRawNode.ReadPropertyAsStr("desc"))?.TrimEnd(),
            Value = unBuiltValue,
            Validation = ReadValidationAttributes(propertyRawNode)
        };
        
        ReadPropertyCompilerOptions(definitionNode, propertyRawNode, propertyNode);
        
        definitionNode.Properties.Add(propertyName, propertyNode);
    }

    ValidationAttributes? ReadValidationAttributes(RawNode propertyRawNode)
    {
        object? minValue = propertyRawNode.Internal.GetValueOrDefault("min");
        object? maxValue = propertyRawNode.Internal.GetValueOrDefault("max");
        string? patternValue = propertyRawNode.ReadPropertyAsStr("pattern");

        if (minValue is null && maxValue is null && patternValue is null)
            return null;

        double? min = null;
        double? max = null;
        bool minInclusive = true;
        bool maxInclusive = false;

        if (minValue is not null)
        {
            min = ParseDoubleWithSuffix(minValue, out minInclusive);
        }

        if (maxValue is not null)
        {
            max = ParseDoubleWithSuffix(maxValue, out maxInclusive);
        }

        return new ValidationAttributes
        {
            Min = min,
            MinInclusive = minInclusive,
            Max = max,
            MaxInclusive = maxInclusive,
            PatternRaw = patternValue,
            Pattern = GetRegexForPattern(patternValue)
        };
    }

    static double ParseDoubleWithSuffix(object value, out bool inclusive)
    {
        inclusive = true;
        string strValue = value.ToString() ?? string.Empty;
        
        if (strValue.EndsWith('i'))
        {
            inclusive = true;
            strValue = strValue[..^1];
        }
        else if (strValue.EndsWith('e'))
        {
            inclusive = false;
            strValue = strValue[..^1];
        }
        
        return Convert.ToDouble(strValue);
    }
    
    static string? GetRegexForPattern(string? pattern) => pattern switch
    {
        "alpha"        => @"^[a-zA-Z]+$",
        "alphanumeric" => @"^[a-zA-Z0-9]+$",
        "hex"          => @"^[0-9a-fA-F]+$",
        "number"       => @"^-?[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?$",
        
        "ascii"        => @"^[\x00-\x7F]+$",
        "ascii8"       => @"^[\x00-\xFF]+$",
        "vascii"       => @"^[\x20-\x7E]+$",
        "vascii8"      => @"^[\x20-\xFF]+$",
        
        "uuid"         => @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        "url"          => @"^[a-zA-Z][a-zA-Z0-9+.-]*:[^ \s]*$",
        "date"         => @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$",
        "ipv4"         => @"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$",
        "ipv6"         => @"^(?:(?:[a-fA-F\d]{1,4}:){7}(?:[a-fA-F\d]{1,4}|:)|(?:[a-fA-F\d]{1,4}:){1,7}:|(?:[a-fA-F\d]{1,4}:){1,6}:[a-fA-F\d]{1,4}|(?:[a-fA-F\d]{1,4}:){1,5}(?::[a-fA-F\d]{1,4}){1,2}|(?:[a-fA-F\d]{1,4}:){1,4}(?::[a-fA-F\d]{1,4}){1,3}|(?:[a-fA-F\d]{1,4}:){1,3}(?::[a-fA-F\d]{1,4}){1,4}|(?:[a-fA-F\d]{1,4}:){1,2}(?::[a-fA-F\d]{1,4}){1,5}|[a-fA-F\d]{1,4}:(?::[a-fA-F\d]{1,4}){1,6}|:(?:(?::[a-fA-F\d]{1,4}){1,7}|:))$",

        "email"        => @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        "http"         => @"^https?:\/\/(?:www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b(?:[-a-zA-Z0-9()@:%_\+.~#?&\/=]*)$",
        "slug"         => @"^[a-z0-9]+(?:-[a-z0-9]+)*$",
        "phone"        => @"^\+?[1-9]\d{1,14}$",
        _              => pattern
    };

    void ReadConstant(DefinitionNode definitionNode, RawNode constantsRawNode, string constantName, object constantValue)
    {
        Logger.LogDebug("Reading constant {ConstantName} in {DefinitionFullName}", constantName, definitionNode.FullName);

        Dictionary<object, object>? constantObjectValue = constantValue as Dictionary<object, object>;
        if (constantObjectValue is null)
        {
            throw new UnexpectedTypeException
            {
                RawNode = constantsRawNode,
                LeafName = constantName,
                ExpectedType = nameof(Dictionary<object, object>),
                ReceivedType = constantValue.GetType().Name
            };
        }

        RawNode constantRawNode = constantsRawNode.CreateChild(constantObjectValue, constantName);

        string? constantType = constantRawNode.ReadPropertyAsStr("type");
        if (string.IsNullOrWhiteSpace(constantType))
        {
            throw new ExpectedTokenNotFoundException
            {
                RawNode = constantRawNode,
                TokenName = "type"
            };
        }

        constantType = constantType.Replace(" ", string.Empty);

        object? constantDefaultValue = constantRawNode.Internal.GetValueOrDefault("value");
        if (constantDefaultValue is null)
        {
            throw new ExpectedTokenNotFoundException
            {
                RawNode = constantRawNode,
                TokenName = "value"
            };
        }

        UnBuiltValue unBuiltValue = new(JsonSerializer.Serialize(
            constantDefaultValue, 
            new JsonSerializerOptions{ WriteIndented = true }));

        ConstantNode constantNode = new()
        {
            Parent = new WeakReference<Node>(definitionNode),
            Name = constantName,
            UnBuiltType = constantType,
            Description = (constantRawNode.ReadPropertyAsStr("description") ?? constantsRawNode.ReadPropertyAsStr("desc"))?.TrimEnd(),
            Value = unBuiltValue
        };

        ReadConstantCompilerOptions(definitionNode, constantRawNode, constantNode);

        definitionNode.Constants.Add(constantName, constantNode);
    }
    
    void ReadServices(RawFileNode rawFileNode, FileNode fileNode)
    {
        Logger.LogDebug("Reading services in {FilePath}", fileNode.FullName);

        Dictionary<object, object> services = rawFileNode.ReadPropertyAsDictionary("services") ?? [];
        Dictionary<object, object>? defaultEndpoints = rawFileNode.ReadPropertyAsDictionary("endpoints");
        if (defaultEndpoints is not null)
        {
            Logger.LogInformation("Found default service in {FilePath}", fileNode.FullName);
            
            string defaultServiceName = fileNode.FileName;
            services.Add(defaultServiceName, new Dictionary<object, object>
            {
                { "endpoints", defaultEndpoints }
            });
        }
        
        if (services.Count == 0)
            return;
        
        Logger.LogInformation("Found {Count} services in {FilePath}", services.Count, fileNode.FullName);
        
        RawNode servicesRawNode = rawFileNode.CreateChild(services, "services");
            
        foreach (KeyValuePair<object, object> service in services)
        {
            string serviceName = ((string)service.Key);
            Dictionary<object, object>? serviceValue = service.Value as Dictionary<object, object>;
            if (serviceValue is null)
            {
                throw new UnexpectedTypeException
                {
                    RawNode = servicesRawNode,
                    LeafName = serviceName,
                    ExpectedType = nameof(Dictionary<object, object>),
                    ReceivedType = service.Value.GetType().Name
                };
            }

            RawNode serviceRawNode = servicesRawNode.CreateChild(serviceValue, serviceName);
            ReadService(fileNode, serviceName, serviceRawNode);
        }
    }

    void ReadService(FileNode fileNode, string serviceName, RawNode serviceRawNode)
    {
        Logger.LogDebug("Reading service {ServiceName} in {FilePath}", serviceName, fileNode.FullName);

        string path = serviceRawNode.ReadPropertyAsUri("path") ?? $"/{serviceName}";
        if (!path.StartsWith('/'))
            path = '/' + path;
        
        ServiceNode serviceNode = new()
        {
            Parent = new WeakReference<Node>(fileNode),
            Name = serviceName,
            Description = serviceRawNode.ReadDescription(),
            Path = path
        };
        
        ReadServiceCompilerOptions(fileNode, serviceRawNode, serviceNode);
        
        Dictionary<object, object>? endpoints = serviceRawNode.ReadPropertyAsDictionary("endpoints");
        if (endpoints is null)
        {
            throw new ExpectedTokenNotFoundException
            {
                RawNode = serviceRawNode,
                TokenName = "endpoints"
            };
        }

        Logger.LogDebug("Found {Count} endpoints in service {FilePath}", endpoints.Count, fileNode.FullName);
        
        foreach (KeyValuePair<object, object> endpoint in endpoints)
        {
            string endpointName = ((string)endpoint.Key);
            Dictionary<object, object>? endpointValue = endpoint.Value as Dictionary<object, object>;
            if (endpointValue is null)
            {
                throw new UnexpectedTypeException
                {
                    RawNode = serviceRawNode,
                    LeafName = endpointName,
                    ExpectedType = nameof(Dictionary<object, object>),
                    ReceivedType = endpoint.Value.GetType().Name
                };
            }

            RawNode endpointRawNode = serviceRawNode.CreateChild(endpointValue, endpointName);
            ReadEndpoint(serviceNode, endpointName, endpointRawNode);
        }

        fileNode.Services.Add(serviceName, serviceNode);
    }
    
    void ReadEndpoint(ServiceNode serviceNode, string endpointName, RawNode endpointRawNode)
    {
        Logger.LogDebug("Reading endpoint {EndpointName} in service {ServiceFullName}", endpointName, serviceNode.FullName);
        
        string? requestType = endpointRawNode.ReadPropertyAsStr("request") ?? endpointRawNode.ReadPropertyAsStr("req");
        if (string.IsNullOrWhiteSpace(requestType))
        {
            throw new ExpectedTokenNotFoundException
            {
                RawNode = endpointRawNode,
                TokenName = "request"
            };
        }
        requestType = requestType.Replace(" ", string.Empty);
        
        string? responseType = endpointRawNode.ReadPropertyAsStr("response") ?? endpointRawNode.ReadPropertyAsStr("res");
        if (string.IsNullOrWhiteSpace(responseType))
        {
            throw new ExpectedTokenNotFoundException
            {
                RawNode = endpointRawNode,
                TokenName = "response"
            };
        }
        responseType = responseType.Replace(" ", string.Empty);

        string? httpMethodStr = endpointRawNode.ReadPropertyAsStr("method");
        httpMethodStr ??= "POST";

        HttpMethod httpMethod;

        try
        {
            httpMethod = Enum.Parse<HttpMethod>(httpMethodStr, true);
        }
        catch (OverflowException e)
        {
            throw new UnexpectedTokenException
            {
                RawNode = endpointRawNode,
                TokenName = httpMethodStr
            };
        }

        string path = endpointRawNode.ReadPropertyAsUri("path") ?? $"/{endpointName}";
        if (!path.StartsWith('/'))
            path = '/' + path;

        // Traditional REST spec, where the endpoint is just a method with no explicit path.
        if (Enum.TryParse(endpointName, false, out HttpMethod endpointNameMethod))
        {
            httpMethod = endpointNameMethod;
            path = string.Empty;
        }
        
        EndpointNode endpointNode = new()
        {
            Parent = new WeakReference<Node>(serviceNode),
            Name = endpointName,
            Method = httpMethod,
            Path = path,
            UnBuiltRequestType = requestType,
            UnBuiltResponseType = responseType,
            Description = endpointRawNode.ReadDescription(),
        };
        
        // TODO: ReadEndpointCompilerOptions(definitionNode, propertyRawNode, propertyNode);
        
        serviceNode.Endpoints.Add(endpointName, endpointNode);
    }

    void ReadFileCompilerOptions(RawFileNode rawFileNode, FileNode fileNode)
    {
        foreach (OptionsReader languageFileReader in LanguageFileReaders)
        {
            RawNode? rawCompilerOptions = languageFileReader.GetRawCompilerOptions(rawFileNode);
            GeneratorOptionsNode? compilerOptions = languageFileReader.ReadFileOptions(fileNode, rawCompilerOptions);
            if (compilerOptions is not null)
                fileNode.CompilerOptions.Add(compilerOptions.Name, compilerOptions);
        }
    }
    
    void ReadEnumCompilerOptions(FileNode fileNode, RawNode rawEnumNode, EnumNode enumNode)
    {
        foreach (OptionsReader languageFileReader in LanguageFileReaders)
        {
            RawNode? rawCompilerOptions = languageFileReader.GetRawCompilerOptions(rawEnumNode);
            GeneratorOptionsNode? parentCompilerOptions = languageFileReader.GetCompilerOptions(fileNode);
            GeneratorOptionsNode? compilerOptions = languageFileReader.ReadEnumOptions(enumNode, parentCompilerOptions, rawCompilerOptions);
            if (compilerOptions is not null)
                enumNode.CompilerOptions.Add(compilerOptions.Name, compilerOptions);
        }
    }
    
    void ReadDefinitionCompilerOptions(FileNode fileNode, RawNode rawDefinitionNode, DefinitionNode definitionNode)
    {
        foreach (OptionsReader languageFileReader in LanguageFileReaders)
        {
            RawNode? rawCompilerOptions = languageFileReader.GetRawCompilerOptions(rawDefinitionNode);
            GeneratorOptionsNode? parentCompilerOptions = languageFileReader.GetCompilerOptions(fileNode);
            GeneratorOptionsNode? compilerOptions = languageFileReader.ReadDefinitionOptions(definitionNode, parentCompilerOptions, rawCompilerOptions);
            if (compilerOptions is not null)
                definitionNode.CompilerOptions.Add(compilerOptions.Name, compilerOptions);
        }
    }
    
    void ReadPropertyCompilerOptions(DefinitionNode definitionNode, RawNode rawPropertyNode, PropertyNode propertyNode)
    {
        foreach (OptionsReader languageFileReader in LanguageFileReaders)
        {
            RawNode? rawCompilerOptions = languageFileReader.GetRawCompilerOptions(rawPropertyNode);
            GeneratorOptionsNode? parentCompilerOptions = languageFileReader.GetCompilerOptions(definitionNode);
            GeneratorOptionsNode? compilerOptions = languageFileReader.ReadPropertyOptions(propertyNode, parentCompilerOptions, rawCompilerOptions);
            if (compilerOptions is not null)
                propertyNode.CompilerOptions.Add(compilerOptions.Name, compilerOptions);
        }
    }
    
    void ReadConstantCompilerOptions(DefinitionNode definitionNode, RawNode constantRawNode, ConstantNode constantNode)
    {
    }

    
    void ReadServiceCompilerOptions(FileNode fileNode, RawNode rawServiceNode, ServiceNode serviceNode)
    {
        foreach (OptionsReader languageFileReader in LanguageFileReaders)
        {
            RawNode? rawCompilerOptions = languageFileReader.GetRawCompilerOptions(rawServiceNode);
            GeneratorOptionsNode? parentCompilerOptions = languageFileReader.GetCompilerOptions(fileNode);
            GeneratorOptionsNode? compilerOptions = languageFileReader.ReadServiceOptions(serviceNode, parentCompilerOptions, rawCompilerOptions);
            if (compilerOptions is not null)
                serviceNode.CompilerOptions.Add(compilerOptions.Name, compilerOptions);
        }
    }
}