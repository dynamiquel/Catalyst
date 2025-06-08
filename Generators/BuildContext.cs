using System.Text.Json;
using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators;

public abstract record BuiltPropertyValue(string? Value);
public record NoPropertyValue() : BuiltPropertyValue(Value: null);
public record SomePropertyValue(string Value) : BuiltPropertyValue(Value);

public record BuiltPropertyType(string Name);
public record BuiltInclude(string Path);
public record BuiltProperty(PropertyNode Node, string Name, BuiltPropertyType Type, BuiltPropertyValue Value);
    
public enum FunctionFlags
{
    None,
    Const,
    Static,
    Async
}

public record BuiltFunction(string Name, string ReturnType, FunctionFlags Flags, List<string> Parameters, string? BodyInit)
{
    public string? Body { get; set; } = BodyInit;
}

public record BuiltEnumValue(string Label, int Value);
public record BuiltEnum(EnumNode Node, string Name, List<BuiltEnumValue> Values);
public record BuiltDefinition(DefinitionNode Node, string Name, List<BuiltProperty> Properties, List<BuiltFunction> Functions);
public record BuiltEndpoint(EndpointNode Node, string Name, BuiltPropertyType RequestType, BuiltPropertyType ResponseType);
public record BuiltService(ServiceNode Node, string Name, List<BuiltEndpoint> Endpoints);
    
public enum FileFlags
{
    Source,
    Header
}

public record BuiltFile(
    FileNode Node, 
    string Name, 
    FileFlags Flags, 
    List<BuiltInclude> Includes, 
    string? Namespace, 
    List<BuiltEnum> Enums,
    List<BuiltDefinition> Definitions, 
    List<BuiltService> Services)
{
    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions{ WriteIndented = true });
    }
}

public record BuildContext(FileNode FileNode, List<BuiltFile> Files)
{
    public BuiltFile GetOrAddFile(Compiler compiler, string fileName, FileFlags fileFlags = FileFlags.Source)
    {
        BuiltFile? file = Files.FirstOrDefault(f => f.Name == fileName);

        if (file is null)
        {
            Files.Add(new BuiltFile(
                Node: FileNode,
                Name: fileName,
                Enums: [],
                Definitions: [],
                Flags: fileFlags,
                Includes: [],
                Namespace: compiler.GetCompiledNamespace(FileNode.Namespace),
                Services: []
            ));

            file = Files[^1];
        }
        
        return file;
    }
}
