namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents a Definition within a Spec File.
/// </summary>
public class DefinitionNode : Node
{
    public string? Description { get; set; }
    public Dictionary<string, PropertyNode> Properties { get; set; } = [];
}