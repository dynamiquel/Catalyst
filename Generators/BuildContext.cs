using System.Text.Json;
using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators;

public abstract record BuiltDataValue(string? Value);
public record NoDataValue() : BuiltDataValue(Value: null);
public record SomeDataValue(string Value) : BuiltDataValue(Value);

public record BuiltDataType(string Name);
public record BuiltInclude(string Path);
public record BuiltProperty(DataMemberNode Node, string Name, BuiltDataType Type, BuiltDataValue Value);
public record BuiltConstant(ConstantNode Node, string Name, BuiltDataType Type, BuiltDataValue Value);
    
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
public record BuiltDefinition(DefinitionNode Node, string Name, List<BuiltProperty> Properties, List<BuiltConstant> Constants, List<BuiltFunction> Functions);
public record BuiltEndpoint(EndpointNode Node, string Name, BuiltDataType RequestType, BuiltDataType ResponseType);
public record BuiltService(ServiceNode Node, string Name, List<BuiltEndpoint> Endpoints);
public record BuiltValidator(DefinitionNode Node, string Name, string TargetName, List<BuiltValidatorProperty> Properties);
public record BuiltValidatorProperty(PropertyNode Node, string Name, ValidationAttributes Validation);
    
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
    List<BuiltService> Services,
    List<BuiltValidator> Validators)
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
                Services: [],
                Validators: []
            ));

            file = Files[^1];
        }
        
        return file;
    }
}
