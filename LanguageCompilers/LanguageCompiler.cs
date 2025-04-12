using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.PropertyTypes;

namespace Catalyst.LanguageCompilers;


public abstract class LanguageCompiler
{
    public record PropertyType(string Name);
    public record Include(string Path);
    public record Attribute(string Name, string? Arguments);
    public record Property(string Name, string Type, string? Value, List<Attribute> Attributes);
    public record Function(string Name, string ReturnType, bool Static, List<string> Parameters, string Body);
    public record Class(string Name, List<Property> Properties, List<Function> Functions);
    public record File(string Name, List<Include> Includes, string? Namespace, List<Class> Classes);

    public abstract CompiledFile CompileFile(File file);
    public abstract File CreateFile(FileNode fileNode);

    public void BuildFile(File file, FileNode fileNode)
    {
        HashSet<Tuple<string, IPropertyType>> usedPropertyTypes = [];
        
        foreach (KeyValuePair<string, DefinitionNode> definitionNode in fileNode.Definitions)
        {
            AddDefinition(file, definitionNode.Value);
            
            foreach (KeyValuePair<string, PropertyNode> propertyNode in definitionNode.Value.Properties)
            {
                if (propertyNode.Value.PropertyType is null)
                    throw new NullReferenceException("Property Type should not be null at this point");
                
                usedPropertyTypes.Add(new(propertyNode.Value.Type, propertyNode.Value.PropertyType));
            }
        }

        foreach (Tuple<string, IPropertyType> usedPropertyType in usedPropertyTypes)
            AddPropertyType(file, usedPropertyType.Item1, usedPropertyType.Item2);
    }
    
    protected abstract void AddPropertyType(File file, string propertyTypeName, IPropertyType propertyType);
    protected abstract void AddDefinition(File file, DefinitionNode definition);
    protected abstract PropertyType GetPropertyType(string propertyTypeName, IPropertyType propertyType);
    protected abstract string GetDefaultValueForProperty(PropertyNode propertyNode);
}