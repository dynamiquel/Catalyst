namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents an Endpoint within a Service.
/// </summary>
public class EndpointNode : Node
{
    public string Method { get; set; } = HttpMethod.Post.Method;
    public string? Route { get; set; }
    public required string RequestType { get; set; }
    public required string ResponseType { get; set; }
    public string? Description { get; set; }
}