namespace Catalyst.SpecGraph.Nodes;

public class FileNode : Node
{
    public required FileInfo FileInfo;
    public string Format = Formats.Json;
    public string? Namespace { get; set; }
    public List<string> IncludeSpecs { get; set; } = [];
    public Dictionary<string, DefinitionNode> Definitions { get; set; } = [];
    public Dictionary<string, ServiceNode> Services { get; set; } = [];
}