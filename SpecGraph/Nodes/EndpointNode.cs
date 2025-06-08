using Catalyst.SpecGraph.Properties;

namespace Catalyst.SpecGraph.Nodes;

public enum HttpMethod
{
    Get,
    Post,
    Put,
    Patch,
    Delete,
    Options,
    Trace
}

/// <summary>
/// Represents an Endpoint within a Service.
/// </summary>
public class EndpointNode : Node, INodeDescription, ICompilerOptions
{
    public required HttpMethod Method { get; set; }
    public required string Path { get; set; }
    public required string UnBuiltRequestType { get; set; }
    public required string UnBuiltResponseType { get; set; }
    public string? Description { get; set; }
    public IPropertyType? BuiltRequestType { get; set; }
    public IPropertyType? BuiltResponseType { get; set; }
    public Dictionary<string, GeneratorOptionsNode> CompilerOptions { get; } = [];
}