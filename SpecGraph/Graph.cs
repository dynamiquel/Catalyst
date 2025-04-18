using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;

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
    public List<FileNode> Files { get; private set; } = [];
    public List<IPropertyType> PropertyTypes { get; private set; } = [];
    
    bool _bBuilt;

    public Graph()
    {
        AddBuiltInPropertyTypes();
    }
    
    public IPropertyType? FindPropertyType(string typeName, string? currentNamespace = null)
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
        
        RegisterFileDefinitions(fileNode);
        
        Files.Add(fileNode);
    }

    public void Build()
    {
        if (_bBuilt)
            throw new GraphAlreadyBuiltException();
        
        // All definitions have been added at this point. It's safe to register container property types now.
        BuildContainerPropertyTypes();
        
        // All Property Types are now defined. Now let's build the Properties themselves with the new type info.
        BuildProperties();

        _bBuilt = true;
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
                IPropertyType? foundInnerPropertyType = FindPropertyType(rawInnerPropertyType, fileNode.Namespace);
                if (foundInnerPropertyType is null)
                {
                    throw new PropertyTypeNotFoundException
                    {
                        ExpectedProperty = rawInnerPropertyType,
                        Node = propertyNode
                    };
                }

                if (IPropertyType.IsOptional(foundInnerPropertyType))
                {
                    PropertyTypes.Add(new OptionalListType
                    {
                        Name = rawContainerPropertyType,
                        InnerType = foundInnerPropertyType
                    });
                }
                else
                {
                    PropertyTypes.Add(new ListType
                    {
                        Name = rawContainerPropertyType,
                        InnerType = foundInnerPropertyType
                    });
                }
            }
        }
        else if (rawPropertyType.StartsWith("set<"))
        {
            string rawInnerPropertyType = IPropertyContainer1InnerType.ExtractRawInnerType(rawPropertyType);
            string rawContainerPropertyType = $"set<{rawInnerPropertyType}>";
            
            if (FindPropertyType(rawContainerPropertyType, fileNode.Namespace) is null)
            {
                IPropertyType? foundInnerPropertyType = FindPropertyType(rawInnerPropertyType, fileNode.Namespace);
                if (foundInnerPropertyType is null)
                {
                    throw new PropertyTypeNotFoundException
                    {
                        ExpectedProperty = rawInnerPropertyType,
                        Node = propertyNode
                    };
                }

                if (IPropertyType.IsOptional(foundInnerPropertyType))
                {
                    PropertyTypes.Add(new OptionalSetType
                    {
                        Name = rawContainerPropertyType,
                        InnerType = foundInnerPropertyType
                    });
                }
                else
                {
                    PropertyTypes.Add(new SetType
                    {
                        Name = rawContainerPropertyType,
                        InnerType = foundInnerPropertyType
                    });
                }
            }
        }
        else if (rawPropertyType.StartsWith("map<"))
        {
            Tuple<string, string> rawInnerPropertyTypes = IPropertyContainer2InnerTypes.ExtractRawInnerTypes(rawPropertyType);
            string rawContainerPropertyType = $"map<{rawInnerPropertyTypes.Item1},{rawInnerPropertyTypes.Item2}>";
            
            if (FindPropertyType(rawContainerPropertyType, fileNode.Namespace) is null)
            {
                IPropertyType? foundInnerAPropertyType = FindPropertyType(rawInnerPropertyTypes.Item1, fileNode.Namespace);
                if (foundInnerAPropertyType is null)
                {
                    throw new PropertyTypeNotFoundException
                    {
                        ExpectedProperty = rawInnerPropertyTypes.Item1,
                        Node = propertyNode
                    };
                }
                
                IPropertyType? foundInnerBPropertyType = FindPropertyType(rawInnerPropertyTypes.Item2, fileNode.Namespace);
                if (foundInnerBPropertyType is null)
                {
                    throw new PropertyTypeNotFoundException
                    {
                        ExpectedProperty = rawInnerPropertyTypes.Item2,
                        Node = propertyNode
                    };
                }

                if (IPropertyType.IsOptional(foundInnerBPropertyType))
                {
                    PropertyTypes.Add(new OptionalMapType
                    {
                        Name = rawContainerPropertyType,
                        InnerTypeA = foundInnerAPropertyType,
                        InnerTypeB = foundInnerBPropertyType
                    });
                }
                else
                {
                    PropertyTypes.Add(new MapType
                    {
                        Name = rawContainerPropertyType,
                        InnerTypeA = foundInnerAPropertyType,
                        InnerTypeB = foundInnerBPropertyType
                    });
                }
            }
        }
    }

    void BuildProperties()
    {
        // Build the types first.
        foreach (FileNode file in Files)
        foreach (KeyValuePair<string, DefinitionNode> definition in file.Definitions)
        foreach (KeyValuePair<string, PropertyNode> property in definition.Value.Properties)
            BuildTypeForProperty(file, property.Value);
        
        // Now types are all build, build the default values.
        foreach (FileNode file in Files)
        foreach (KeyValuePair<string, DefinitionNode> definition in file.Definitions)
        foreach (KeyValuePair<string, PropertyNode> property in definition.Value.Properties)
            BuildValueForProperty(file, property.Value);
    }

    void BuildTypeForProperty(FileNode fileNode, PropertyNode propertyNode)
    {
        IPropertyType? foundPropertyType = FindPropertyType(propertyNode.UnBuiltType, fileNode.Namespace);

        if (foundPropertyType is null)
        {
            throw new PropertyTypeNotFoundException
            {
                ExpectedProperty = propertyNode.UnBuiltType,
                Node = propertyNode
            };
        }
    
        propertyNode.BuiltType = foundPropertyType;
    }

    void BuildValueForProperty(FileNode fileNode, PropertyNode propertyNode)
    {
        if (propertyNode.Value is null)
            return;
        
        UnBuiltValue? unbuiltValue = propertyNode.Value as UnBuiltValue;
        if (unbuiltValue is null)
        {
            throw new PropertyValueAlreadyBuiltException
            {
                PropertyNode = propertyNode
            };
        }
        
        JsonNode? valueJsonNode = JsonNode.Parse(unbuiltValue.ValueJson);
        BuildJsonNode(propertyNode, propertyNode.BuiltType!, valueJsonNode, out IPropertyValue propertyValue);
        propertyNode.Value = propertyValue;
    }

    bool IsDefaultValue(JsonValue value)
    {
        value.TryGetValue(out string? result);
        return result is "" or "default";
    }

    void BuildJsonNode(PropertyNode propertyNode, IPropertyType propertyType, JsonNode? jsonNode, out IPropertyValue propertyValue)
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
                    throw new PropertyTypeMismatchException
                    {
                        Node = propertyNode,
                        ExpectedPropertyTypes = [typeof(ListType), typeof(SetType)],
                    };
                }

                IPropertyType innerType = ((propertyType as IPropertyContainer1InnerType)?.InnerType ?? propertyType as AnyType) ?? throw new InvalidOperationException();

                List<IPropertyValue> childValues = [];
                foreach (JsonNode? childJsonNode in jsonArray)
                {
                    BuildJsonNode(propertyNode, innerType, childJsonNode, out IPropertyValue childValue);
                    childValues.Add(childValue);
                }

                propertyValue = new ListValue(childValues);
                break;
            case JsonObject jsonObject:
                if (propertyNode.BuiltType is MapType mapType)
                {
                    var mapEntries = new Dictionary<IPropertyValue, IPropertyValue>();
                    foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
                    {
                        JsonValue keyNode = JsonValue.Create(property.Key);
                        
                        BuildJsonNode(propertyNode, mapType.InnerTypeA, keyNode, out IPropertyValue keyPropertyValue);
                        BuildJsonNode(propertyNode, mapType.InnerTypeB, property.Value, out IPropertyValue valuePropertyValue);
                        mapEntries.Add(keyPropertyValue, valuePropertyValue);
                    }
                    
                    propertyValue = new MapValue(mapEntries);
                }
                else if (propertyNode.BuiltType is ObjectType objectType)
                {
                    throw new NotImplementedException();
                }
                else if (propertyNode.BuiltType is AnyType)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    throw new PropertyTypeMismatchException
                    {
                        Node = propertyNode,
                        ExpectedPropertyTypes = [typeof(MapType), typeof(ObjectType), typeof(AnyType)],
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
                            throw new InvalidPropertyValueFormatException
                            {
                                PropertyNode = propertyNode,
                                ExpectedPropertyType = propertyType,
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
                            throw new InvalidPropertyValueFormatException
                            {
                                PropertyNode = propertyNode,
                                ExpectedPropertyType = propertyType,
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
                            throw new InvalidPropertyValueFormatException
                            {
                                PropertyNode = propertyNode,
                                ExpectedPropertyType = propertyType,
                                ReceivedValue = jsonValue.ToJsonString()
                            };
                        }
                        
                        propertyValue = new IntegerValue(intValue);
                        break;
                    case StringType:
                        string stringValue;
                        if (IsDefaultValue(jsonValue))
                            stringValue = string.Empty;
                        else
                            stringValue = jsonValue.GetValue<string>();
                        propertyValue = new StringValue(stringValue);
                        break;
                    case TimeType:
                        double seconds;
                        if (IsDefaultValue(jsonValue))
                            seconds = 0;
                        else if (jsonValue.GetValueKind() == JsonValueKind.Number)
                            seconds = jsonValue.GetValue<double>();
                        else if (jsonValue.GetValueKind() == JsonValueKind.String)
                            seconds = double.Parse(jsonValue.GetValue<string>());
                        else
                        {
                            throw new InvalidPropertyValueFormatException
                            {
                                PropertyNode = propertyNode,
                                ExpectedPropertyType = propertyType,
                                ReceivedValue = jsonValue.ToJsonString()
                            };
                        }
                        
                        propertyValue = new TimeValue(TimeSpan.FromSeconds(seconds));
                        break;
                    default:
                        throw new PropertyTypeMismatchException
                        {
                            Node = propertyNode,
                            ExpectedPropertyTypes =
                            [
                                typeof(AnyType), typeof(BooleanType), typeof(DateType), typeof(FloatType),
                                typeof(IntegerType), typeof(StringType), typeof(TimeType)
                            ]
                        };
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
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

            IPropertyType? existingPropertyType = FindPropertyType(newObjectPropertyType.Name);
            if (existingPropertyType is not null)
            {
                throw new PropertyTypeAlreadyExistsException
                {
                    ExistingPropertyType = existingPropertyType,
                    NewPropertyType = newObjectPropertyType
                };
            }
            
            // Ensure there isn't another property type with the same name but with different casing.
            existingPropertyType = PropertyTypes.FirstOrDefault(x =>
                string.Equals(x.Name, newObjectPropertyType.Name, StringComparison.CurrentCultureIgnoreCase));
                
            if (existingPropertyType is not null)
            {
                throw new SimilarPropertyTypeAlreadyExistsException
                {
                    ExistingPropertyType = existingPropertyType,
                    NewPropertyType = newObjectPropertyType
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

    void AddBuiltInPropertyTypes()
    {
        PropertyTypes.Add(new BooleanType());
        PropertyTypes.Add(new OptionalBooleanType());
        PropertyTypes.Add(new IntegerType());
        PropertyTypes.Add(new OptionalIntegerType());
        PropertyTypes.Add(new FloatType());
        PropertyTypes.Add(new OptionalFloatType());
        PropertyTypes.Add(new StringType());
        PropertyTypes.Add(new OptionalStringType());
        PropertyTypes.Add(new DateType());
        PropertyTypes.Add(new OptionalDateType());
        PropertyTypes.Add(new TimeType());
        PropertyTypes.Add(new OptionalTimeType());
        PropertyTypes.Add(new AnyType());
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions{ WriteIndented = true });
    }
}