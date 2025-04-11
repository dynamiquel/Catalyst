namespace Catalyst.SpecGraph.Nodes;

public class ServiceNode : Node
{
    public string? Path { get; set; }
    public Dictionary<string, EndpointNode> Endpoints { get; set; } = [];
}