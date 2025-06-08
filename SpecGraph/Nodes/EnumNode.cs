namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents an Enum within a Spec File.
/// </summary>
public class EnumNode : Node, INodeDescription, ICompilerOptions
{
    public string? Description { get; set; }
    public Dictionary<string, int> Values { get; set; } = [];
    public bool? Flags { get; set; }
    public Dictionary<string, GeneratorOptionsNode> CompilerOptions { get; } = [];
}