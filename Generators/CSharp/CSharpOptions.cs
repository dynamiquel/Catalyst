using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators.CSharp;

public enum CSharpClassType
{
    Class,
    Record
}


public class CSharpGlobalOptionsNode : GeneratorOptionsNode
{
    public required CSharpClassType ClassType { get; set; }
    public required bool UseRequired { get; set; }
}

public class CSharpFileOptionsNode : GeneratorOptionsNode
{
    public required CSharpClassType ClassType { get; set; }
    public required bool UseRequired { get; set; }
}

public class CSharpEnumOptionsNode : GeneratorOptionsNode;

public class CSharpDefinitionOptionsNode : GeneratorOptionsNode
{
    public required CSharpClassType Type { get; set; }
    public required bool UseRequired { get; set; }
}

public class CSharpPropertyOptionsNode : GeneratorOptionsNode
{
    public required bool UseRequired { get; set; }
}
