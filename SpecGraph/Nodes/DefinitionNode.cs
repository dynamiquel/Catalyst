namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents a Definition within a Spec File.
/// </summary>
public class DefinitionNode : Node, INodeDescription, ICompilerOptions
{
    public string? Description { get; set; }
    public Dictionary<string, PropertyNode> Properties { get; set; } = [];
    public Dictionary<string, ConstantNode> Constants { get; set; } = [];
    public Dictionary<string, GeneratorOptionsNode> CompilerOptions { get; } = [];
}