using System.Text;
using System.Text.Json;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.PropertyTypes;

namespace Catalyst.SpecGraph;

public class Graph
{
    public List<FileNode> Files { get; private set; } = [];
    public List<IPropertyType> PropertyTypes { get; private set; } = [];

    public Graph()
    {
        AddBuiltInPropertyTypes();
    }

    public void AddFileNode(FileNode fileNode)
    {
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
        foreach (FileNode file in Files)
        {
            foreach (KeyValuePair<string, DefinitionNode> definition in file.Definitions)
            {
                foreach (KeyValuePair<string, PropertyNode> property in definition.Value.Properties)
                {
                    bool bPropertyTypeFound = PropertyTypes.Any(p => p.Name == property.Value.Type);
                    if (!bPropertyTypeFound)
                    {
                        if (!string.IsNullOrWhiteSpace(file.Namespace))
                        {
                            // This file is in a namespace, check if there's a Property Type of this name
                            // in the same namespace as this file.
                            string sameNameSpacePropertyTypeName = $"{file.Namespace}.{property.Value.Type}";
                            bool bSameNameSpacePropertyFound = PropertyTypes.Any(p => p.Name == sameNameSpacePropertyTypeName);
                            if (bSameNameSpacePropertyFound)
                            {
                                property.Value.Type = sameNameSpacePropertyTypeName;
                                bPropertyTypeFound = true;
                            }
                        }
                    }

                    if (!bPropertyTypeFound)
                    {
                        throw new PropertyTypeNotFoundException
                        {
                            ExpectedProperty = property.Value.Type,
                            Node = property.Value
                        };
                    }
                }
            }
        }
    }

    void RegisterFileDefinitions(FileNode fileNode)
    {
        foreach (KeyValuePair<string, DefinitionNode> definitionPair in fileNode.Definitions)
        {
            UserType newUserPropertyType = new()
            {
                Name = string.IsNullOrEmpty(fileNode.Namespace) ? definitionPair.Key : $"{fileNode.Namespace}.{definitionPair.Key}",
                OwnedFile = fileNode
            };
            
            IPropertyType? existingPropertyType = PropertyTypes.Find(p => p.Name == newUserPropertyType.Name);
            if (existingPropertyType is not null)
            {
                throw new PropertyTypeAlreadyExistsException
                {
                    ExistingPropertyType = existingPropertyType,
                    NewPropertyType = newUserPropertyType
                };
            }
            
            PropertyTypes.Add(newUserPropertyType);
        }
    }
    
    void AddBuiltInPropertyTypes()
    {
        PropertyTypes.Add(new StringType());
        PropertyTypes.Add(new IntegerType());
        PropertyTypes.Add(new FloatType());
        PropertyTypes.Add(new BooleanType());
        PropertyTypes.Add(new DateType());
        PropertyTypes.Add(new TimespanType());
        PropertyTypes.Add(new ListType());
        PropertyTypes.Add(new SetType());
        PropertyTypes.Add(new MapType());
        PropertyTypes.Add(new AnyType());
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions{ WriteIndented = true });
    }
}