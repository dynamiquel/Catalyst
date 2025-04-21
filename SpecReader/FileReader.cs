using System.Text.Json;
using Catalyst.LanguageCompilers;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;
using YamlDotNet.Serialization;
using HttpMethod = Catalyst.SpecGraph.Nodes.HttpMethod;

namespace Catalyst.SpecReader;

/// <summary>
/// Responsible for reading a Spec File and converting it to a FileNode, which can be
/// used to create a Spec Graph.
/// </summary>
public class FileReader
{
    public required DirectoryInfo BaseDir { get; init; }
    protected List<LanguageFileReader> LanguageFileReaders = [];

    public string GetBuiltSpecFilePath(FileInfo fileInfo)
    {
        // Converts the actual spec file path to a more suitable built name.
        // i.e. /home/project/specs/common/user.yaml -> common/user
        string fileNodePath = Path.GetRelativePath(BaseDir.FullName, fileInfo.FullName);
        fileNodePath = Path.ChangeExtension(fileNodePath, null);
        return fileNodePath;
    }
    
    public void AddLanguageFileReader<T>() where T : LanguageFileReader, new()
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
        Console.WriteLine($"[{rawFileNode.FileInfo.FullName}] Reading Spec File");

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
        ReadDefinitions(rawFileNode, fileNode);
        ReadServices(rawFileNode, fileNode);
        
        return fileNode;
    }

    void ReadIncludes(RawFileNode rawFileNode, FileNode fileNode)
    {
        List<object>? includeSpecs = rawFileNode.ReadPropertyAsList("includes");
        if (includeSpecs is null) 
            return;
        
        Console.WriteLine($"[{fileNode.FullName}] Found {includeSpecs.Count} Include Specs");
        
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
    
    void ReadDefinitions(RawFileNode rawFileNode, FileNode fileNode)
    {
        Console.WriteLine($"[{fileNode.FullName}] Reading Definitions");

        Dictionary<object, object>? definitions = rawFileNode.ReadPropertyAsDictionary("definitions");
        if (definitions is null) 
            return;
        
        Console.WriteLine($"[{fileNode.FullName}] Found {definitions.Count} Definitions");
        
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
        
        Console.WriteLine($"[{fileNode.FullName}] Reading Definition '{definitionNode.Name}'");

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

            Console.WriteLine($"[{fileNode.FullName}] Found {properties.Count} Properties");

            RawNode propertiesRawNode = definitionRawNode.CreateChild(properties, "properties");

            foreach (KeyValuePair<object, object> property in properties)
            {
                string propertyName = ((string)property.Key);
                ReadProperty(definitionNode, propertiesRawNode, propertyName, property.Value);
            }
        }

        fileNode.Definitions.Add(definitionName, definitionNode);
    }

    void ReadProperty(DefinitionNode definitionNode, RawNode propertiesRawNode, string propertyName, object propertyValue)
    {
        Console.WriteLine($"[{definitionNode.FullName}] Reading Property '{propertyName}'");

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
            Value = unBuiltValue
        };
        
        ReadPropertyCompilerOptions(definitionNode, propertyRawNode, propertyNode);
        
        definitionNode.Properties.Add(propertyName, propertyNode);
    }
    
    void ReadServices(RawFileNode rawFileNode, FileNode fileNode)
    {
        Console.WriteLine($"[{fileNode.FullName}] Reading Services");

        Dictionary<object, object>? services = rawFileNode.ReadPropertyAsDictionary("services");
        if (services is null) 
            return;
        
        Console.WriteLine($"[{fileNode.FullName}] Found {services.Count} Services");
        
        RawNode servicesRawNode = rawFileNode.CreateChild(services, "services");
            
        foreach (KeyValuePair<object, object> service in services)
        {
            string seriveName = ((string)service.Key);
            Dictionary<object, object>? serviceValue = service.Value as Dictionary<object, object>;
            if (serviceValue is null)
            {
                throw new UnexpectedTypeException
                {
                    RawNode = servicesRawNode,
                    LeafName = seriveName,
                    ExpectedType = nameof(Dictionary<object, object>),
                    ReceivedType = service.Value.GetType().Name
                };
            }

            RawNode serviceRawNode = servicesRawNode.CreateChild(serviceValue, seriveName);
            ReadService(fileNode, seriveName, serviceRawNode);
        }
    }

    void ReadService(FileNode fileNode, string serviceName, RawNode serviceRawNode)
    {
        Console.WriteLine($"[{fileNode.FullName}] Reading Service '{serviceName}'");

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
        
        //TODO ReadServiceCompilerOptions(fileNode, serviceRawNode, serviceNode);
        
        Dictionary<object, object>? endpoints = serviceRawNode.ReadPropertyAsDictionary("endpoints");
        if (endpoints is null)
        {
            throw new ExpectedTokenNotFoundException
            {
                RawNode = serviceRawNode,
                TokenName = "endpoints"
            };
        }

        Console.WriteLine($"[{fileNode.FullName}] Found {endpoints.Count} Endpoints");
        
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
        Console.WriteLine($"[{serviceNode.FullName}] Reading Endpoint '{endpointName}'");
        
        string? requestType = endpointRawNode.ReadPropertyAsStr("request");
        if (string.IsNullOrWhiteSpace(requestType))
        {
            throw new ExpectedTokenNotFoundException
            {
                RawNode = endpointRawNode,
                TokenName = "request"
            };
        }
        requestType = requestType.Replace(" ", string.Empty);
        
        string? responseType = endpointRawNode.ReadPropertyAsStr("response");
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
        if (Enum.TryParse(endpointName, true, out HttpMethod endpointNameMethod))
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
        foreach (LanguageFileReader languageFileReader in LanguageFileReaders)
        {
            RawNode? rawCompilerOptions = languageFileReader.GetRawCompilerOptions(rawFileNode);
            CompilerOptionsNode? compilerOptions = languageFileReader.ReadFileOptions(fileNode, rawCompilerOptions);
            if (compilerOptions is not null)
                fileNode.CompilerOptions.Add(compilerOptions.Name, compilerOptions);
        }
    }
    
    void ReadDefinitionCompilerOptions(FileNode fileNode, RawNode rawDefinitionNode, DefinitionNode definitionNode)
    {
        foreach (LanguageFileReader languageFileReader in LanguageFileReaders)
        {
            RawNode? rawCompilerOptions = languageFileReader.GetRawCompilerOptions(rawDefinitionNode);
            CompilerOptionsNode? parentCompilerOptions = languageFileReader.GetCompilerOptions(fileNode);
            CompilerOptionsNode? compilerOptions = languageFileReader.ReadDefinitionOptions(definitionNode, parentCompilerOptions, rawCompilerOptions);
            if (compilerOptions is not null)
                definitionNode.CompilerOptions.Add(compilerOptions.Name, compilerOptions);
        }
    }
    
    void ReadPropertyCompilerOptions(DefinitionNode definitionNode, RawNode rawPropertyNode, PropertyNode propertyNode)
    {
        foreach (LanguageFileReader languageFileReader in LanguageFileReaders)
        {
            RawNode? rawCompilerOptions = languageFileReader.GetRawCompilerOptions(rawPropertyNode);
            CompilerOptionsNode? parentCompilerOptions = languageFileReader.GetCompilerOptions(definitionNode);
            CompilerOptionsNode? compilerOptions = languageFileReader.ReadPropertyOptions(propertyNode, parentCompilerOptions, rawCompilerOptions);
            if (compilerOptions is not null)
                propertyNode.CompilerOptions.Add(compilerOptions.Name, compilerOptions);
        }
    }
}