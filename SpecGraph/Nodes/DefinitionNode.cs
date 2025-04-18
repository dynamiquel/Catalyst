namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents a Definition within a Spec File.
/// </summary>
public class DefinitionNode : Node, ICompilerOptions
{
    public string? Description { get; set; }
    public Dictionary<string, PropertyNode> Properties { get; set; } = [];
    public Dictionary<string, CompilerOptionsNode> CompilerOptions { get; } = [];
}