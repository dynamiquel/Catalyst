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
    
    public enum FunctionFlags
    {
        None,
        Const,
        Static,
    }
    
    public record Function(string Name, string ReturnType, FunctionFlags Flags, List<string> Parameters, string Body);

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
                
                switch (propertyNode.Value.BuiltType)
                {
                    case IPropertyContainer1InnerType propertyContainer1InnerType:
                        usedPropertyTypes.Add(propertyContainer1InnerType.InnerType);
                        break;
                    case IPropertyContainer2InnerTypes propertyContainer2InnerTypes:
                        usedPropertyTypes.Add(propertyContainer2InnerTypes.InnerTypeA);
                        usedPropertyTypes.Add(propertyContainer2InnerTypes.InnerTypeB);
                        break;
                }
            }
        }

        foreach (IPropertyType usedPropertyType in usedPropertyTypes)
        {
            Include? include = GetCompiledIncludeForPropertyType(file, usedPropertyType);
            if (include is not null && !file.Includes.Contains(include))
                file.Includes.Add(include);
        }
        
        file.Includes.Sort((x, y) => string.Compare(x.Path, y.Path, StringComparison.OrdinalIgnoreCase));
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
        IEnumerable<Function> serialiseFunctions = CreateSerialiseFunction(file, definitionNode);
        IEnumerable<Function> deserializeFunctions = CreateDeserialiseFunction(file, definitionNode);
        functions.AddRange(serialiseFunctions);
        functions.AddRange(deserializeFunctions);
        
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
    
    protected abstract IEnumerable<Function> CreateSerialiseFunction(File file, DefinitionNode definitionNode);
    protected abstract IEnumerable<Function> CreateDeserialiseFunction(File file, DefinitionNode definitionNode);
}