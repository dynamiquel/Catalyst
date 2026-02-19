using Catalyst.SpecGraph.Properties;

namespace Catalyst.SpecGraph.Nodes;

public class ConstantNode : Node, INodeDescription, ICompilerOptions
{
    public required string UnBuiltType { get; set; }
    public IPropertyType? BuiltType { get; set; }
    public string? Description { get; set; }
    public required IPropertyValue Value { get; set; }
    public Dictionary<string, GeneratorOptionsNode> CompilerOptions { get; } = [];
}
