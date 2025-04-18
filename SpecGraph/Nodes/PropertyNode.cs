using Catalyst.SpecGraph.Properties;

namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents a Property within a Definition.
/// </summary>
public class PropertyNode : Node, ICompilerOptions
{
    public required string UnBuiltType { get; set; }
    public IPropertyType? BuiltType { get; set; }
    public string? Description { get; set; }
    public IPropertyValue? Value { get; set; }
    public Dictionary<string, CompilerOptionsNode> CompilerOptions { get; } = [];
}