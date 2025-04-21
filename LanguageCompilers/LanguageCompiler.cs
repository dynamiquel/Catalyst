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
    
    public record Property(string Name, string? Description, PropertyType Type, PropertyValue Value, CompilerOptionsNode? CompilerOptions);
    
    public enum FunctionFlags
    {
        None,
        Const,
        Static,
    }
    
    public record Function(string Name, string ReturnType, FunctionFlags Flags, List<string> Parameters, string Body);

    public record Class(string Name, string? Description, List<Property> Properties, List<Function> Functions, CompilerOptionsNode? CompilerOptions);

    public record Endpoint(string Name, string? Description, string Path, string Method, PropertyType RequestType, PropertyType ResponseType, CompilerOptionsNode? CompilerOptions);
    public record Service(string Name, string? Description, string Path, List<Endpoint> Endpoints, CompilerOptionsNode? CompilerOptions);
    
    public record File(string Name, List<Include> Includes, string? Namespace, List<Class> Definitions, List<Service> Services, CompilerOptionsNode? CompilerOptions)
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
            Definitions: [],
            Services: [],
            CompilerOptions: fileNode.FindCompilerOptions(CompilerName));
        
        return file;
    }

    protected void BuildFile(File file, FileNode fileNode)
    {
        HashSet<IPropertyType> usedPropertyTypes = [];
        
        foreach (KeyValuePair<string, DefinitionNode> definitionNode in fileNode.Definitions)
        {
            file.Definitions.Add(CreateDefinition(file, definitionNode.Value));
            
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

        foreach (KeyValuePair<string, ServiceNode> serviceNode in fileNode.Services)
        {
            file.Services.Add(CreateService(file, serviceNode.Value));
        }
        
        file.Includes.Sort((x, y) => string.Compare(x.Path, y.Path, StringComparison.OrdinalIgnoreCase));
    }
    
    protected Class CreateDefinition(File file, DefinitionNode definitionNode)
    {
        List<Property> properties = [];
        foreach (KeyValuePair<string, PropertyNode> propertyNode in definitionNode.Properties)
        {
            Property property = new(
                Name: GetCompiledPropertyName(propertyNode.Value),
                Description: GetCompiledPropertyDescription(file, propertyNode.Value),
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
        
        Class def = new(
            Name: GetCompiledClassName(definitionNode),
            Description: GetCompiledDefinitionDescription(file, definitionNode),
            Properties: properties,
            Functions: functions,
            CompilerOptions: definitionNode.FindCompilerOptions(CompilerName));
        
        return def;
    }

    protected Service CreateService(File file, ServiceNode serviceNode)
    {
        List<Endpoint> endpoints = [];
        foreach (KeyValuePair<string, EndpointNode> endpointNode in serviceNode.Endpoints)
        {
            Endpoint endpoint = new(
                Name: GetCompiledEndpointName(endpointNode.Value),
                Description: endpointNode.Value.Description,
                Method: endpointNode.Value.Method,
                Path: endpointNode.Value.Path,
                RequestType: GetCompiledPropertyType(endpointNode.Value.BuiltRequestType!),
                ResponseType: GetCompiledPropertyType(endpointNode.Value.BuiltResponseType!),
                CompilerOptions: endpointNode.Value.FindCompilerOptions(CompilerName));
            
            endpoints.Add(endpoint);
        }

        Service service = new(
            Name: GetCompiledServiceName(serviceNode),
            Description: serviceNode.Description,
            Path: serviceNode.Path,
            Endpoints: endpoints,
            CompilerOptions: serviceNode.FindCompilerOptions(CompilerName)
        );
        
        return service;
    }

    protected abstract string GetCompiledFilePath(FileNode fileNode);
    protected abstract string? GetCompiledNamespace(FileNode fileNode);
    protected abstract string GetCompiledClassName(DefinitionNode definitionNode);
    protected abstract string GetCompiledPropertyName(PropertyNode propertyNode);
    protected abstract string GetCompiledServiceName(ServiceNode serviceNode);
    protected abstract string GetCompiledEndpointName(EndpointNode endpointNode);

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

    protected virtual string? GetCompiledDefinitionDescription(File file, DefinitionNode definitionNode) =>
        definitionNode.Description;
    protected virtual string? GetCompiledPropertyDescription(File file, PropertyNode propertyNode) =>
        propertyNode.Description;
    protected virtual string? GetCompiledEndpointDescription(File file, EndpointNode endpointNode) =>
        endpointNode.Description;
}