using Catalyst.SpecGraph.Properties;

namespace Catalyst.SpecGraph.Nodes;

public abstract class DataMemberNode : Node, INodeDescription, ICompilerOptions
{
    public required string UnBuiltType { get; set; }
    public IDataType? BuiltType { get; set; }
    public IDataValue? Value { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, GeneratorOptionsNode> CompilerOptions { get; } = [];
}
