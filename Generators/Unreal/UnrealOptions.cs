using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators.Unreal;

public class UnrealGlobalOptionsNode : GeneratorOptionsNode
{
    public string? Prefix { get; set; }
}

public class UnrealFileOptionsNode : GeneratorOptionsNode
{
    public string? Prefix { get; set; }
}

public class UnrealDefinitionOptionsNode : GeneratorOptionsNode
{
    public string? Prefix { get; set; }
}

public class UnrealPropertyOptionsNode : GeneratorOptionsNode
{
    
}