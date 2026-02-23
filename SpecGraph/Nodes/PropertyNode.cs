namespace Catalyst.SpecGraph.Nodes;

public class ValidationAttributes
{
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string? PatternRaw { get; set; }
    public string? Pattern { get; set; }
    public bool MinInclusive { get; set; } = true;
    public bool MaxInclusive { get; set; } = false;
}

/// <summary>
/// Represents a Property within a Definition.
/// </summary>
public class PropertyNode : DataMemberNode
{
    public ValidationAttributes? Validation { get; set; }
}