using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;
using Microsoft.Extensions.Logging;

namespace Catalyst.SpecGraph;

/// <summary>
/// Represents the entire Spec File Repository as a 'graph' full of Nodes.
///
/// FileNodes must first be added to the Graph and then the Graph must be
/// 'built', at which point the Spec File Repository is ready to be compiled
/// into desired program languages.
/// </summary>
public class Graph
{
    public required ILogger<Graph> Logger { get; init; }
    public List<FileNode> Files { get; private set; } = [];
    public List<IDataType> PropertyTypes { get; private set; } = [];
    
    bool _bBuilt;

    public Graph()
    {
        AddBuiltInPropertyTypes();
    }
    
    public IDataType? FindPropertyType(string typeName, string? currentNamespace = null)
    {
        if (string.IsNullOrEmpty(currentNamespace))
            return PropertyTypes.FirstOrDefault(x => x.Name == typeName);

        string[] namespaceParts = currentNamespace.Split('.');
    
        // Check from most specific to the least specific namespace
        for (int namespacePartIdx = namespaceParts.Length; namespacePartIdx >= 0; namespacePartIdx--)
        {
            var testNamespace = string.Join(".", namespaceParts.Take(namespacePartIdx));
            var fullName = string.IsNullOrEmpty(testNamespace) 
                ? typeName 
                : $"{testNamespace}.{typeName}";

            var type = PropertyTypes.FirstOrDefault(x => x.Name == fullName);
            if (type is not null)
                return type;
        }

        // Final check without any namespace
        return PropertyTypes.FirstOrDefault(x => x.Name == typeName);
    }
    
    public void AddFileNode(FileNode fileNode)
    {
        if (_bBuilt)
            throw new GraphAlreadyBuiltException();
        
        if (Files.Contains(fileNode))
        {
            throw new FileAlreadyAddedException
            {
                FileNode = fileNode
            };
        }
        
        RegisterFileEnums(fileNode);
        RegisterFileDefinitions(fileNode);
        RegisterFileServices(fileNode);
        
        Files.Add(fileNode);
    }

    public void Build()
    {
        if (_bBuilt)
            throw new GraphAlreadyBuiltException();
        
        Logger.LogInformation("Building spec graph...");
        
        // All definitions have been added at this point. It's safe to register container property types now.
        BuildContainerPropertyTypes();
        
        // All Property Types are now defined. Now let's build the Properties themselves with the new type info.
        BuildProperties();

        _bBuilt = true;
        
        Logger.LogInformation("Spec graph built successfully");
    }

    void BuildContainerPropertyTypes()
    {
        foreach (FileNode file in Files)
        foreach (KeyValuePair<string, DefinitionNode> definition in file.Definitions)
        foreach (KeyValuePair<string, PropertyNode> property in definition.Value.Properties)
            BuildContainerPropertyType(file, property.Value, property.Value.UnBuiltType);
    }

    void BuildContainerPropertyType(FileNode fileNode, PropertyNode propertyNode, string rawPropertyType)
    {
        // TODO: handle recursion. probably requires in -> out approach.
        
        if (rawPropertyType.StartsWith("list<"))
        {
            string rawInnerPropertyType = IPropertyContainer1InnerType.ExtractRawInnerType(rawPropertyType);
            string rawContainerPropertyType = $"list<{rawInnerPropertyType}>";
            
            if (FindPropertyType(rawContainerPropertyType, fileNode.Namespace) is null)
            {
                IDataType? foundInnerPropertyType = FindPropertyType(rawInnerPropertyType, fileNode.Namespace);
                if (foundInnerPropertyType is null)
                {
                    throw new DataTypeNotFoundException
                    {
                        ExpectedDataType = rawInnerPropertyType,
                        Node = propertyNode
                    };
                }
                
                PropertyTypes.AddRange([
                    new ListType
                    {
                        Name = rawContainerPropertyType,
                        InnerType = foundInnerPropertyType
                    },
                    new OptionalListType
                    {
                        Name = $"{rawContainerPropertyType}?",
                        InnerType = foundInnerPropertyType
                    }
                ]);
            }
        }
        else if (rawPropertyType.StartsWith("set<"))
        {
            string rawInnerPropertyType = IPropertyContainer1InnerType.ExtractRawInnerType(rawPropertyType);
            string rawContainerPropertyType = $"set<{rawInnerPropertyType}>";
            
            if (FindPropertyType(rawContainerPropertyType, fileNode.Namespace) is null)
            {
                IDataType? foundInnerPropertyType = FindPropertyType(rawInnerPropertyType, fileNode.Namespace);
                if (foundInnerPropertyType is null)
                {
                    throw new DataTypeNotFoundException
                    {
                        ExpectedDataType = rawInnerPropertyType,
                        Node = propertyNode
                    };
                }

                PropertyTypes.AddRange([
                    new SetType
                    {
                        Name = rawContainerPropertyType,
                        InnerType = foundInnerPropertyType
                    },
                    new OptionalSetType
                    {
                        Name = $"{rawContainerPropertyType}?",
                        InnerType = foundInnerPropertyType
                    }
                ]);
            }
        }
        else if (rawPropertyType.StartsWith("map<"))
        {
            Tuple<string, string> rawInnerPropertyTypes = IPropertyContainer2InnerTypes.ExtractRawInnerTypes(rawPropertyType);
            string rawContainerPropertyType = $"map<{rawInnerPropertyTypes.Item1},{rawInnerPropertyTypes.Item2}>";
            
            if (FindPropertyType(rawContainerPropertyType, fileNode.Namespace) is null)
            {
                IDataType? foundInnerAPropertyType = FindPropertyType(rawInnerPropertyTypes.Item1, fileNode.Namespace);
                if (foundInnerAPropertyType is null)
                {
                    throw new DataTypeNotFoundException
                    {
                        ExpectedDataType = rawInnerPropertyTypes.Item1,
                        Node = propertyNode
                    };
                }
                
                IDataType? foundInnerBPropertyType = FindPropertyType(rawInnerPropertyTypes.Item2, fileNode.Namespace);
                if (foundInnerBPropertyType is null)
                {
                    throw new DataTypeNotFoundException
                    {
                        ExpectedDataType = rawInnerPropertyTypes.Item2,
                        Node = propertyNode
                    };
                }
                
                PropertyTypes.AddRange([
                    new MapType
                    {
                        Name = rawContainerPropertyType,
                        InnerTypeA = foundInnerAPropertyType,
                        InnerTypeB = foundInnerBPropertyType
                    },
                    new OptionalMapType
                    {
                        Name = $"{rawContainerPropertyType}?",
                        InnerTypeA = foundInnerAPropertyType,
                        InnerTypeB = foundInnerBPropertyType
                    }
                ]);
            }
        }
    }

    void BuildProperties()
    {
        // Build the types first.
        foreach (FileNode file in Files)
        foreach (KeyValuePair<string, DefinitionNode> definition in file.Definitions)
        foreach (KeyValuePair<string, PropertyNode> property in definition.Value.Properties)
            BuildTypeForDataMember(file, property.Value);
        
        // Build types for constants.
        foreach (FileNode file in Files)
        foreach (KeyValuePair<string, DefinitionNode> definition in file.Definitions)
        foreach (KeyValuePair<string, ConstantNode> constant in definition.Value.Constants)
            BuildTypeForConstant(file, constant.Value);

        // Now types are all build, build the default values.
        foreach (FileNode file in Files)
        foreach (KeyValuePair<string, DefinitionNode> definition in file.Definitions)
        foreach (KeyValuePair<string, PropertyNode> property in definition.Value.Properties)
            BuildValueForDataMember(file, property.Value);
        
        foreach (FileNode file in Files)
        foreach (KeyValuePair<string, DefinitionNode> definition in file.Definitions)
        foreach (KeyValuePair<string, ConstantNode> constant in definition.Value.Constants)
            BuildValueForConstant(file, constant.Value);
        
        foreach (FileNode file in Files)
        foreach (KeyValuePair<string, ServiceNode> service in file.Services)
        foreach (KeyValuePair<string, EndpointNode> endpoint in service.Value.Endpoints)
            BuildTypesForEndpoint(file, endpoint.Value);
    }

    void BuildTypeForConstant(FileNode fileNode, ConstantNode constantNode)
    {
        IDataType? foundPropertyType = FindPropertyType(constantNode.UnBuiltType, fileNode.Namespace);

        if (foundPropertyType is null)
        {
            throw new DataTypeNotFoundException
            {
                ExpectedDataType = constantNode.UnBuiltType,
                Node = constantNode
            };
        }

        constantNode.BuiltType = foundPropertyType;
    }

    void BuildTypeForDataMember(FileNode fileNode, PropertyNode propertyNode)
    {
        IDataType? foundPropertyType = FindPropertyType(propertyNode.UnBuiltType, fileNode.Namespace);

        if (foundPropertyType is null)
        {
            throw new DataTypeNotFoundException
            {
                ExpectedDataType = propertyNode.UnBuiltType,
                Node = propertyNode
            };
        }
    
        propertyNode.BuiltType = foundPropertyType;
    }

    void BuildValueForDataMember(FileNode fileNode, PropertyNode propertyNode)
    {
        if (propertyNode.Value is null)
            return;
        
        UnBuiltValue? unbuiltValue = propertyNode.Value as UnBuiltValue;
        if (unbuiltValue is null)
        {
            throw new DataMemberValueAlreadyBuiltException
            {
                DataMemberNode = propertyNode
            };
        }
        
        JsonNode? valueJsonNode = JsonNode.Parse(unbuiltValue.ValueJson);
        BuildJsonNode(propertyNode, propertyNode.BuiltType!, valueJsonNode, out IDataValue propertyValue);
        propertyNode.Value = propertyValue;
    }

    void BuildValueForConstant(FileNode fileNode, ConstantNode constantNode)
    {
        if (constantNode.Value is null)
            return;
        
        UnBuiltValue? unbuiltValue = constantNode.Value as UnBuiltValue;
        if (unbuiltValue is null)
        {
            throw new DataMemberValueAlreadyBuiltException
            {
                DataMemberNode = constantNode
            };
        }
        
        JsonNode? valueJsonNode = JsonNode.Parse(unbuiltValue.ValueJson);
        BuildJsonNode(constantNode, constantNode.BuiltType!, valueJsonNode, out IDataValue propertyValue);
        constantNode.Value = propertyValue;
    }

    void BuildTypesForEndpoint(FileNode fileNode, EndpointNode endpointNode)
    {
        IDataType? foundRequestType = FindPropertyType(endpointNode.UnBuiltRequestType, fileNode.Namespace);
        IDataType? foundResponseType = FindPropertyType(endpointNode.UnBuiltResponseType, fileNode.Namespace);

        if (foundRequestType is null)
        {
            throw new DataTypeNotFoundException
            {
                ExpectedDataType = endpointNode.UnBuiltRequestType,
                Node = endpointNode
            };
        }
        
        if (foundResponseType is null)
        {
            throw new DataTypeNotFoundException
            {
                ExpectedDataType = endpointNode.UnBuiltResponseType,
                Node = endpointNode
            };
        }
    
        endpointNode.BuiltRequestType = foundRequestType;
        endpointNode.BuiltResponseType = foundResponseType;
    }

    bool IsDefaultValue(JsonValue value)
    {
        value.TryGetValue(out string? result);
        return result is "" or "default" and not "@default";
    }

    void BuildJsonNode(DataMemberNode dataMemberNode, IDataType propertyType, JsonNode? jsonNode, out IDataValue propertyValue)
    {
        // TODO: Support recursion properly. Cba doing it rn. Too complicated for what it's worth.
        
        if (jsonNode is null)
        {
            propertyValue = new NullValue();
            return;
        }
        
        switch (jsonNode)
        {
            case JsonArray jsonArray:
                if (propertyType is not ListType or SetType or AnyType)
                {
                    throw new DataMemberTypeMismatchException
                    {
                        DataMemberNode = dataMemberNode,
                        ExpectedDataTypes = [typeof(ListType), typeof(SetType)],
                    };
                }

                IDataType innerType = ((propertyType as IPropertyContainer1InnerType)?.InnerType ?? propertyType as AnyType) ?? throw new InvalidOperationException();

                List<IDataValue> childValues = [];
                foreach (JsonNode? childJsonNode in jsonArray)
                {
                    BuildJsonNode(dataMemberNode, innerType, childJsonNode, out IDataValue childValue);
                    childValues.Add(childValue);
                }

                propertyValue = new ListValue(childValues);
                break;
            case JsonObject jsonObject:
                if (dataMemberNode.BuiltType is MapType mapType)
                {
                    var mapEntries = new Dictionary<IDataValue, IDataValue>();
                    foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
                    {
                        JsonValue keyNode = JsonValue.Create(property.Key);
                        
                        BuildJsonNode(dataMemberNode, mapType.InnerTypeA, keyNode, out IDataValue keyPropertyValue);
                        BuildJsonNode(dataMemberNode, mapType.InnerTypeB, property.Value, out IDataValue valuePropertyValue);
                        mapEntries.Add(keyPropertyValue, valuePropertyValue);
                    }
                    
                    propertyValue = new MapValue(mapEntries);
                }
                else if (dataMemberNode.BuiltType is ObjectType objectType)
                {
                    throw new NotImplementedException();
                }
                else if (dataMemberNode.BuiltType is AnyType)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    throw new DataMemberTypeMismatchException
                    {
                        DataMemberNode = dataMemberNode,
                        ExpectedDataTypes = [typeof(MapType), typeof(ObjectType), typeof(AnyType)],
                    };
                }
                break;
            case JsonValue jsonValue:
                switch (propertyType)
                {
                    case AnyType:
                        switch (jsonValue.GetValueKind())
                        {
                            case JsonValueKind.String:
                                propertyValue = new StringValue(jsonValue.GetValue<string>());
                                break;
                            case JsonValueKind.Number:
                                propertyValue = new FloatValue(jsonValue.GetValue<double>());
                                break;
                            case JsonValueKind.True:
                                propertyValue = new BooleanValue(false);
                                break;
                            case JsonValueKind.False:
                                propertyValue = new BooleanValue(false);
                                break;
                            case JsonValueKind.Null:
                                propertyValue = new NullValue();
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        break;
                    case BooleanType:
                        bool boolValue;
                        if (IsDefaultValue(jsonValue))
                            boolValue = false;
                        else if (jsonValue.GetValueKind() == JsonValueKind.Number)
                            boolValue = jsonValue.GetValue<bool>();
                        else if (jsonValue.GetValueKind() == JsonValueKind.String)
                            boolValue = bool.Parse(jsonValue.GetValue<string>());
                        else
                        {
                            throw new InvalidDataMemberValueFormatException
                            {
                                DataMemberNode = dataMemberNode,
                                ExpectedDataType = propertyType,
                                ReceivedValue = jsonValue.ToJsonString()
                            };
                        }
                        
                        propertyValue = new BooleanValue(boolValue);
                        break;
                    case DateType:
                        DateTime dateValue;
                        if (IsDefaultValue(jsonValue))
                            dateValue = default;
                        else
                            dateValue = jsonValue.GetValue<DateTime>();
                        
                        propertyValue = new DateValue(dateValue);
                        break;
                    case FloatType:
                        double floatValue;
                        if (IsDefaultValue(jsonValue))
                            floatValue = 0;
                        else if (jsonValue.GetValueKind() == JsonValueKind.Number)
                            floatValue = jsonValue.GetValue<double>();
                        else if (jsonValue.GetValueKind() == JsonValueKind.String)
                            floatValue = double.Parse(jsonValue.GetValue<string>());
                        else
                        {
                            throw new InvalidDataMemberValueFormatException
                            {
                                DataMemberNode = dataMemberNode,
                                ExpectedDataType = propertyType,
                                ReceivedValue = jsonValue.ToJsonString()
                            };
                        }
                        
                        propertyValue = new FloatValue(floatValue);
                        break;
                    case IntegerType:
                        int intValue;
                        if (IsDefaultValue(jsonValue))
                            intValue = 0;
                        else if (jsonValue.GetValueKind() == JsonValueKind.Number)
                            intValue = jsonValue.GetValue<int>();
                        else if (jsonValue.GetValueKind() == JsonValueKind.String)
                            intValue = int.Parse(jsonValue.GetValue<string>());
                        else
                        {
                            throw new InvalidDataMemberValueFormatException
                            {
                                DataMemberNode = dataMemberNode,
                                ExpectedDataType = propertyType,
                                ReceivedValue = jsonValue.ToJsonString()
                            };
                        }
                        
                        propertyValue = new IntegerValue(intValue);
                        break;
                    case Integer64Type:
                        long int64Value;
                        if (IsDefaultValue(jsonValue))
                            int64Value = 0;
                        else if (jsonValue.GetValueKind() == JsonValueKind.Number)
                            int64Value = jsonValue.GetValue<long>();
                        else if (jsonValue.GetValueKind() == JsonValueKind.String)
                            int64Value = long.Parse(jsonValue.GetValue<string>());
                        else
                        {
                            throw new InvalidDataMemberValueFormatException
                            {
                                DataMemberNode = dataMemberNode,
                                ExpectedDataType = propertyType,
                                ReceivedValue = jsonValue.ToJsonString()
                            };
                        }
                        
                        propertyValue = new Integer64Value(int64Value);
                        break;
                    case StringType:
                        string stringValue;
                        if (IsDefaultValue(jsonValue))
                            stringValue = string.Empty;
                        else
                        {
                            stringValue = jsonValue.GetValue<string>();
                            // Sometimes a user wants to specify "default" as a string. Allow this if the
                            // user prefixed it with @.
                            if (stringValue == "@default")
                                stringValue = "default";
                        }

                        propertyValue = new StringValue(stringValue);
                        break;
                    case TimeType:
                        double seconds;
                        if (IsDefaultValue(jsonValue))
                            seconds = 0;
                        else if (jsonValue.GetValueKind() == JsonValueKind.Number)
                            seconds = jsonValue.GetValue<double>();
                        else if (jsonValue.GetValueKind() == JsonValueKind.String)
                            seconds = ParseTimespanString(jsonValue.GetValue<string>());
                        else
                        {
                            throw new InvalidDataMemberValueFormatException
                            {
                                DataMemberNode = dataMemberNode,
                                ExpectedDataType = propertyType,
                                ReceivedValue = jsonValue.ToJsonString()
                            };
                        }
                        
                        propertyValue = new TimeValue(TimeSpan.FromSeconds(seconds));
                        break;
                    case UuidType:
                        Guid guid;
                        if (IsDefaultValue(jsonValue))
                            guid = Guid.Empty;
                        else if (jsonValue.GetValueKind() == JsonValueKind.String)
                            guid = Guid.Parse(jsonValue.GetValue<string>());
                        else
                        {
                            throw new InvalidDataMemberValueFormatException
                            {
                                DataMemberNode = dataMemberNode,
                                ExpectedDataType = propertyType,
                                ReceivedValue = jsonValue.ToJsonString()
                            };
                        }
                        
                        propertyValue = new UuidValue(guid);
                        break;
                    case EnumType enumType:
                        string enumValueName;
                        if (IsDefaultValue(jsonValue))
                        {
                            // Get the lowest value enum.
                            enumValueName = enumType.OwnedEnum.Values.OrderBy(x => x.Value).First().Key;
                            propertyValue = new EnumValue(enumType, [enumValueName]);
                        }
                        else if (jsonValue.GetValueKind() == JsonValueKind.String)
                        {
                            // Supports the use of flags.
                            var enumValueStr = jsonValue.GetValue<string>();
                            string[] enumFlags = enumValueStr.Split('|').Select(x => x.Trim()).ToArray();
                            if (enumFlags.Any(enumFlag => !enumType.OwnedEnum.Values.ContainsKey(enumFlag)))
                            {
                                throw new InvalidEnumValueException
                                {
                                    DataMemberNode = dataMemberNode,
                                    ExpectedDataType = enumType,
                                    ReceivedValue = jsonValue.ToJsonString()
                                };
                            }
                            
                            propertyValue = new EnumValue(enumType, enumFlags);
                        }
                        else
                        {
                            throw new InvalidDataMemberValueFormatException
                            {
                                DataMemberNode = dataMemberNode,
                                ExpectedDataType = enumType,
                                ReceivedValue = jsonValue.ToJsonString()
                            };
                        }
                        break;
                    default:
                        throw new DataMemberTypeMismatchException
                        {
                            DataMemberNode = dataMemberNode,
                            ExpectedDataTypes =
                            [
                                typeof(AnyType), typeof(BooleanType), typeof(DateType), typeof(FloatType),
                                typeof(IntegerType), typeof(Integer64Type), typeof(StringType), typeof(TimeType), typeof(UuidType),
                                typeof(EnumType),
                            ]
                        };
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    void RegisterFileEnums(FileNode fileNode)
    {
        foreach (KeyValuePair<string, EnumNode> enumPair in fileNode.Enums)
        {
            EnumType newEnumPropertyType = new()
            {
                Name = string.IsNullOrEmpty(fileNode.Namespace)
                    ? enumPair.Key
                    : $"{fileNode.Namespace}.{enumPair.Key}",
                Namespace = fileNode.Namespace,
                OwnedEnum = enumPair.Value,
                OwnedFile = fileNode
            };

            IDataType? existingPropertyType = FindPropertyType(newEnumPropertyType.Name);
            if (existingPropertyType is not null)
            {
                throw new DataTypeAlreadyExistsException
                {
                    ExistingDataType = existingPropertyType,
                    NewDataType = newEnumPropertyType
                };
            }
            
            // Ensure there isn't another property type with the same name but with different casing.
            existingPropertyType = PropertyTypes.FirstOrDefault(x =>
                string.Equals(x.Name, newEnumPropertyType.Name, StringComparison.CurrentCultureIgnoreCase));
                
            if (existingPropertyType is not null)
            {
                throw new SimilarDataTypeAlreadyExistsException
                {
                    ExistingDataType = existingPropertyType,
                    NewDataType = newEnumPropertyType
                };
            }
            
            OptionalEnumType newOptionalEnumPropertyType = new()
            {
                Name = string.IsNullOrEmpty(fileNode.Namespace)
                    ? $"{enumPair.Key}?"
                    : $"{fileNode.Namespace}.{enumPair.Key}?",
                Namespace = fileNode.Namespace,
                OwnedEnum = enumPair.Value,
                OwnedFile = fileNode
            };

            PropertyTypes.AddRange([newEnumPropertyType, newOptionalEnumPropertyType]);
        }
    }

    void RegisterFileDefinitions(FileNode fileNode)
    {
        foreach (KeyValuePair<string, DefinitionNode> definitionPair in fileNode.Definitions)
        {
            ObjectType newObjectPropertyType = new()
            {
                Name = string.IsNullOrEmpty(fileNode.Namespace)
                    ? definitionPair.Key
                    : $"{fileNode.Namespace}.{definitionPair.Key}",
                Namespace = fileNode.Namespace,
                OwnedDefinition = definitionPair.Value,
                OwnedFile = fileNode
            };

            IDataType? existingPropertyType = FindPropertyType(newObjectPropertyType.Name);
            if (existingPropertyType is not null)
            {
                throw new DataTypeAlreadyExistsException
                {
                    ExistingDataType = existingPropertyType,
                    NewDataType = newObjectPropertyType
                };
            }
            
            // Ensure there isn't another property type with the same name but with different casing.
            existingPropertyType = PropertyTypes.FirstOrDefault(x =>
                string.Equals(x.Name, newObjectPropertyType.Name, StringComparison.CurrentCultureIgnoreCase));
                
            if (existingPropertyType is not null)
            {
                throw new SimilarDataTypeAlreadyExistsException
                {
                    ExistingDataType = existingPropertyType,
                    NewDataType = newObjectPropertyType
                };
            }
            
            OptionalObjectType newOptionalObjectPropertyType = new()
            {
                Name = string.IsNullOrEmpty(fileNode.Namespace)
                    ? $"{definitionPair.Key}?"
                    : $"{fileNode.Namespace}.{definitionPair.Key}?",
                Namespace = fileNode.Namespace,
                OwnedDefinition = definitionPair.Value,
                OwnedFile = fileNode
            };

            PropertyTypes.AddRange([newObjectPropertyType, newOptionalObjectPropertyType]);
        }
    }
    
    void RegisterFileServices(FileNode fileNode)
    {
        foreach (KeyValuePair<string, ServiceNode> servicePair in fileNode.Services)
        {
            foreach (FileNode file in Files)
            {
                foreach (KeyValuePair<string, ServiceNode> otherService in file.Services)
                {
                    // Ensure there isn't another service with the same name.
                    if (otherService.Key == servicePair.Key)
                    {
                        throw new SimilarServiceAlreadyExistsException
                        {
                            ExistingService = otherService.Value,
                            NewService = servicePair.Value,
                        };
                    }

                    // Ensure there isn't another Endpoint with the same path.
                    foreach (KeyValuePair<string, EndpointNode> otherEndpoint in otherService.Value.Endpoints)
                    {
                        EndpointNode? conflictingEndpoint = servicePair.Value.Endpoints.FirstOrDefault(e => e.Value.Path == otherEndpoint.Key).Value;
                        if (conflictingEndpoint is not null)
                        {
                            throw new SimilarEndpointAlreadyExistsException
                            {
                                NewEndpoint = conflictingEndpoint,
                                ExistingEndpoint = otherEndpoint.Value
                            };
                        }
                    }
                }
            }
        }
    }

    static readonly Regex TimespanRegex = new(
        @"^\s*(?:(\d+(?:\.\d+)?)y)?\s*(?:(\d+(?:\.\d+)?)w)?\s*(?:(\d+(?:\.\d+)?)d)?\s*(?:(\d+(?:\.\d+)?)h)?\s*(?:(\d+(?:\.\d+)?)m)?\s*(?:(\d+(?:\.\d+)?)s)?\s*(?:(\d+(?:\.\d+)?)ms)?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    double ParseTimespanString(string value)
    {
        var match = TimespanRegex.Match(value.Trim());
        if (!match.Success)
            return double.Parse(value);

        double totalSeconds = 0;

        if (match.Groups[1].Success && double.TryParse(match.Groups[1].Value, out var years))
            totalSeconds += years * 31536000;

        if (match.Groups[2].Success && double.TryParse(match.Groups[2].Value, out var weeks))
            totalSeconds += weeks * 604800;

        if (match.Groups[3].Success && double.TryParse(match.Groups[3].Value, out var days))
            totalSeconds += days * 86400;

        if (match.Groups[4].Success && double.TryParse(match.Groups[4].Value, out var hours))
            totalSeconds += hours * 3600;

        if (match.Groups[5].Success && double.TryParse(match.Groups[5].Value, out var seconds))
            totalSeconds += seconds;

        if (match.Groups[6].Success && double.TryParse(match.Groups[6].Value, out var ms))
            totalSeconds += ms / 1000.0;

        return totalSeconds;
    }

    void AddBuiltInPropertyTypes()
    {
        PropertyTypes.Add(new BooleanType());
        PropertyTypes.Add(new OptionalBooleanType());
        PropertyTypes.Add(new IntegerType());
        PropertyTypes.Add(new OptionalIntegerType());
        PropertyTypes.Add(new Integer64Type());
        PropertyTypes.Add(new OptionalInteger64Type());
        PropertyTypes.Add(new FloatType());
        PropertyTypes.Add(new OptionalFloatType());
        PropertyTypes.Add(new StringType());
        PropertyTypes.Add(new OptionalStringType());
        PropertyTypes.Add(new DateType());
        PropertyTypes.Add(new OptionalDateType());
        PropertyTypes.Add(new TimeType());
        PropertyTypes.Add(new OptionalTimeType());
        PropertyTypes.Add(new UuidType());
        PropertyTypes.Add(new OptionalUuidType());
        PropertyTypes.Add(new AnyType());
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions{ WriteIndented = true });
    }
}