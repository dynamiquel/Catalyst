using Catalyst.SpecGraph.Nodes;

namespace Catalyst.LanguageCompilers.Unreal;

public class UnrealGlobalCompilerOptionsNode : CompilerOptionsNode
{
    public string? Prefix { get; set; }
}

public class UnrealFileCompilerOptionsNode : CompilerOptionsNode
{
    public string? Prefix { get; set; }
}

public class UnrealDefinitionCompilerOptionsNode : CompilerOptionsNode
{
    public string? Prefix { get; set; }
}

public class UnrealPropertyCompilerOptionsNode : CompilerOptionsNode
{
    
}