using System.Text.Json;
using Catalyst.SpecGraph;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;

namespace Catalyst.LanguageCompilers;

public abstract class LanguageCompiler
{
    public abstract record PropertyValue(string? Value);
    protected record NoPropertyValue() : PropertyValue(Value: null);
    protected record SomePropertyValue(string Value) : PropertyValue(Value);

    public record PropertyType(string Name);

    public record Include(string Path);
    
    public record Property(string Name, PropertyType Type, PropertyValue Value, CompilerOptionsNode? CompilerOptions);

    public record Function(string Name, string ReturnType, bool Static, List<string> Parameters, string Body);

    public record Class(string Name, List<Property> Properties, List<Function> Functions, CompilerOptionsNode? CompilerOptions);

    public record File(string Name, List<Include> Includes, string? Namespace, List<Class> Classes, CompilerOptionsNode? CompilerOptions)
    {
        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions{ WriteIndented = true });
        }
    }
    
    public abstract string CompilerName { get; }

    
    public File BuildFile(FileNode fileNode)
    {
        File file = CreateFile(fileNode);
        BuildFile(file, fileNode);
        return file;
    }
    
    public abstract CompiledFile CompileFile(File file);
    
    protected File CreateFile(FileNode fileNode)
    {
        File file = new(
            Name: GetCompiledFilePath(fileNode),
            Includes: [],
            Namespace: GetCompiledNamespace(fileNode),
            Classes: [],
            CompilerOptions: fileNode.FindCompilerOptions(CompilerName));
        
        return file;
    }

    protected void BuildFile(File file, FileNode fileNode)
    {
        HashSet<IPropertyType> usedPropertyTypes = [];
        
        foreach (KeyValuePair<string, DefinitionNode> definitionNode in fileNode.Definitions)
        {
            file.Classes.Add(CreateClass(file, definitionNode.Value));
            
            foreach (KeyValuePair<string, PropertyNode> propertyNode in definitionNode.Value.Properties)
            {
                if (propertyNode.Value.BuiltType is null)
                    throw new NullReferenceException("Property Type should not be null at this point");
                
                usedPropertyTypes.Add(propertyNode.Value.BuiltType);
            }
        }

        foreach (IPropertyType usedPropertyType in usedPropertyTypes)
        {
            Include? include = GetCompiledIncludeForPropertyType(file, usedPropertyType);
            if (include is not null && !file.Includes.Contains(include))
                file.Includes.Add(include);
        }
    }
    
    protected Class CreateClass(File file, DefinitionNode definitionNode)
    {
        List<Property> properties = [];
        foreach (KeyValuePair<string, PropertyNode> propertyNode in definitionNode.Properties)
        {
            Property property = new Property(
                Name: GetCompiledPropertyName(propertyNode.Value),
                Type: GetCompiledPropertyType(propertyNode.Value.BuiltType!),
                Value: GetCompiledPropertyValue(propertyNode.Value.BuiltType!, propertyNode.Value.Value),
                CompilerOptions: propertyNode.Value.FindCompilerOptions(CompilerName));
            
            properties.Add(property);
        }
        
        List<Function> functions = [];
        Function? serialiseFunction = CreateSerialiseFunction(file, definitionNode);
        Function? deserializeFunction = CreateDeserialiseFunction(file, definitionNode);
        if (serialiseFunction is not null)
            functions.Add(serialiseFunction);
        if (deserializeFunction is not null)
            functions.Add(deserializeFunction);
        
        Class def = new Class(
            Name: GetCompiledClassName(definitionNode),
            Properties: properties,
            Functions: functions,
            CompilerOptions: definitionNode.FindCompilerOptions(CompilerName));
        
        return def;
    }

    protected abstract string GetCompiledFilePath(FileNode fileNode);
    protected abstract string? GetCompiledNamespace(FileNode fileNode);
    protected abstract string GetCompiledClassName(DefinitionNode definitionNode);
    protected abstract string GetCompiledPropertyName(PropertyNode propertyNode);

    protected abstract Include? GetCompiledIncludeForPropertyType(File file, IPropertyType propertyType);
    
    protected abstract PropertyType GetCompiledPropertyType(IPropertyType propertyType);

    protected PropertyValue GetCompiledPropertyValue(IPropertyType propertyType, IPropertyValue? propertyValue)
    {
        return propertyValue is null 
            ? GetCompiledDefaultValueForPropertyType(propertyType) 
            : GetCompiledDesiredPropertyValue(propertyValue);
    }

    protected abstract PropertyValue GetCompiledDefaultValueForPropertyType(IPropertyType propertyType);
    protected abstract PropertyValue GetCompiledDesiredPropertyValue(IPropertyValue propertyValue);
    
    protected abstract Function? CreateSerialiseFunction(File file, DefinitionNode definitionNode);
    protected abstract Function? CreateDeserialiseFunction(File file, DefinitionNode definitionNode);
}