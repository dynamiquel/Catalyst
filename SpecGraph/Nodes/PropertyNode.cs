using Catalyst.SpecGraph.PropertyTypes;

namespace Catalyst.SpecGraph.Nodes;

public class PropertyNode : Node
{
    public required string Type { get; set; }
    public string? Description { get; set; }
    public string? DefaultValue { get; set; }
    public IPropertyType? PropertyType { get; set; }
}