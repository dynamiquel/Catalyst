using Catalyst.SpecGraph.Properties;

namespace Catalyst.SpecGraph.Nodes;

public class PropertyNode : Node
{
    public required string UnBuiltType { get; set; }
    public IPropertyType? BuiltType { get; set; }
    public string? Description { get; set; }
    public IPropertyValue? Value { get; set; }
}