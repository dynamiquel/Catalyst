using System.Text;

namespace Catalyst.SpecGraph.Nodes;

public class EndpointNode : Node
{
    public string Method { get; set; } = HttpMethod.Post.Method;
    public string? Route { get; set; }
    public required string RequestType { get; set; }
    public required string ResponseType { get; set; }
    public string? Description { get; set; }
}