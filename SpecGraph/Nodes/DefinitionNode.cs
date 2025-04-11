using System.Text;

namespace Catalyst.SpecGraph.Nodes;

public class DefinitionNode : Node
{
    public string? Description { get; set; }
    public Dictionary<string, PropertyNode> Properties { get; set; } = [];
}