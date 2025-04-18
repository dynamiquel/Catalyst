using Catalyst.SpecGraph.Nodes;

namespace Catalyst.LanguageCompilers.CSharp;

public enum CSharpClassType
{
    Class,
    Record
}


public class CSharpGlobalCompilerOptionsNode : CompilerOptionsNode
{
    public required CSharpClassType ClassType { get; set; }
}

public class CSharpFileCompilerOptionsNode : CompilerOptionsNode
{
    public required CSharpClassType ClassType { get; set; }
}

public class CSharpDefinitionCompilerOptionsNode : CompilerOptionsNode
{
    public required CSharpClassType Type { get; set; }
}

public class CSharpPropertyCompilerOptionsNode : CompilerOptionsNode
{
    
}