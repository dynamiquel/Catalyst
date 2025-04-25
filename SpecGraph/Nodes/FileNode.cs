namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents a Spec File.
/// </summary>
public class FileNode : Node, ICompilerOptions
{
    public required string FilePath;
    public string? Directory => Path.GetDirectoryName(FilePath);
    public string FileName => Path.GetFileName(FilePath);
    
    public string Format = Formats.Json;
    public string? Namespace { get; set; }
    public List<string> IncludeSpecs { get; set; } = [];
    public Dictionary<string, DefinitionNode> Definitions { get; set; } = [];
    public Dictionary<string, ServiceNode> Services { get; set; } = [];
    public Dictionary<string, GeneratorOptionsNode> CompilerOptions { get; } = [];
}