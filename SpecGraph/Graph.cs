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
                    ResolveProperty(file, property.Value);
                }
            }
        }
    }

    void ResolveProperty(FileNode fileNode, PropertyNode propertyNode)
    {
        IPropertyType? foundPropertyType = PropertyTypes.Find(p => p.Matches(propertyNode.Type));
        if (foundPropertyType is not null)
        {
            propertyNode.PropertyType = foundPropertyType;
            
            if (foundPropertyType is IPropertyContainerType propertyContainerType)
                ResolveContainerProperty(fileNode, propertyNode, propertyContainerType);
            return;
        }

        if (!string.IsNullOrWhiteSpace(fileNode.Namespace))
        {
            // This file is in a namespace, check if there's a Property Type of this name
            // in the same namespace as this file.
            string sameNameSpacePropertyTypeName = $"{fileNode.Namespace}.{propertyNode.Type}";
            foundPropertyType = PropertyTypes.Find(p => p.Matches(sameNameSpacePropertyTypeName));
            if (foundPropertyType is not null)
            {
                propertyNode.Type = sameNameSpacePropertyTypeName;
                propertyNode.PropertyType = foundPropertyType;
                if (foundPropertyType is IPropertyContainerType propertyContainerType)
                    ResolveContainerProperty(fileNode, propertyNode, propertyContainerType);
                return;
            }
        }
        
        throw new PropertyTypeNotFoundException
        {
            ExpectedProperty = propertyNode.Type,
            Node = propertyNode
        };
    }

    void ResolveContainerProperty(FileNode fileNode, PropertyNode propertyNode, IPropertyContainerType propertyContainerType)
    {
        // Ensure the inner types are valid.
        string[] innerPropertyTypes = propertyContainerType.GetInnerPropertyTypes(propertyNode.Type);
        foreach (var innerPropertyType in innerPropertyTypes)
        {
            IPropertyType? foundPropertyType = PropertyTypes.Find(p => p.Matches(innerPropertyType));
            if (foundPropertyType is not null)
            {
                if (foundPropertyType is IPropertyContainerType innerPropertyContainerType)
                    throw new NotImplementedException("Containers within Containers is not yet supported");
                
                continue;
            }
            
            if (!string.IsNullOrWhiteSpace(fileNode.Namespace))
            {
                // This file is in a namespace, check if there's a Property Type of this name
                // in the same namespace as this file.
                string sameNameSpacePropertyTypeName = $"{fileNode.Namespace}.{innerPropertyType}";
                foundPropertyType = PropertyTypes.Find(p => p.Matches(sameNameSpacePropertyTypeName));
                if (foundPropertyType is not null)
                {
                    // Update the inner type name to match the namespace property type name.
                    propertyNode.Type = propertyNode.Type.Replace(innerPropertyType, sameNameSpacePropertyTypeName);
                    
                    if (foundPropertyType is IPropertyContainerType innerPropertyContainerType)
                        throw new NotImplementedException("Containers within Containers is not yet supported");
                    
                    continue;
                }
            }
            
            throw new PropertyTypeNotFoundException
            {
                ExpectedProperty = innerPropertyType,
                Node = propertyNode
            };
        }
    }

    void RegisterFileDefinitions(FileNode fileNode)
    {
        foreach (KeyValuePair<string, DefinitionNode> definitionPair in fileNode.Definitions)
        {
            UserType newUserPropertyType = new()
            {
                Name = string.IsNullOrEmpty(fileNode.Namespace)
                    ? definitionPair.Key
                    : $"{fileNode.Namespace}.{definitionPair.Key}",
                Namespace = fileNode.Namespace,
                OwnedFile = fileNode
            };

            IPropertyType? existingPropertyType = PropertyTypes.Find(p => p.Matches(newUserPropertyType.Name));
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