namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents a Service within a Spec File.
/// </summary>
public class ServiceNode : Node
{
    public string? Path { get; set; }
    public Dictionary<string, EndpointNode> Endpoints { get; set; } = [];
}