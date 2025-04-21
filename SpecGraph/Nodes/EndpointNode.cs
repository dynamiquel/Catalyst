using Catalyst.SpecGraph.Properties;

namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents an Endpoint within a Service.
/// </summary>
public class EndpointNode : Node, ICompilerOptions
{
    public required string Method { get; set; }
    public required string Path { get; set; }
    public required string UnBuiltRequestType { get; set; }
    public required string UnBuiltResponseType { get; set; }
    public string? Description { get; set; }
    public IPropertyType? BuiltRequestType { get; set; }
    public IPropertyType? BuiltResponseType { get; set; }
    public Dictionary<string, CompilerOptionsNode> CompilerOptions { get; } = [];
}