namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents a Service within a Spec File.
/// </summary>
public class ServiceNode : Node, ICompilerOptions
{
    public required string Path { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, EndpointNode> Endpoints { get; set; } = [];
    public Dictionary<string, GeneratorOptionsNode> CompilerOptions { get; } = [];
}