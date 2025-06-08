using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecReader;

namespace Catalyst.Generators.Unreal;

public class UnrealOptionsReader : OptionsReader
{
    public override string SectionName => Unreal.Name;
    
    public override GeneratorOptionsNode? ReadGlobalOptions()
    {
        throw new NotImplementedException();
    }

    public override GeneratorOptionsNode? ReadFileOptions(FileNode fileNode, RawNode? rawCompilerOptions)
    {
        string? prefix = rawCompilerOptions?.ReadPropertyAsStr("prefix");

        return new UnrealFileOptionsNode
        {
            Parent = new WeakReference<Node>(fileNode),
            Name = SectionName,
            Prefix = prefix
        };
    }

    public override GeneratorOptionsNode? ReadEnumOptions(EnumNode enumNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        string? prefix = rawCompilerOptions?.ReadPropertyAsStr("prefix");
        prefix ??= ((UnrealFileOptionsNode?)parentCompilerOptions)?.Prefix;
        
        return new UnrealEnumOptionsNode
        {
            Parent = new WeakReference<Node>(enumNode),
            Name = SectionName,
            Prefix = prefix
        };
    }

    public override GeneratorOptionsNode? ReadDefinitionOptions(DefinitionNode definitionNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        string? prefix = rawCompilerOptions?.ReadPropertyAsStr("prefix");
        prefix ??= ((UnrealFileOptionsNode?)parentCompilerOptions)?.Prefix;
        
        return new UnrealDefinitionOptionsNode
        {
            Parent = new WeakReference<Node>(definitionNode),
            Name = SectionName,
            Prefix = prefix
        };
    }

    public override GeneratorOptionsNode? ReadPropertyOptions(PropertyNode propertyNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        return null;
    }
    
    public override GeneratorOptionsNode? ReadServiceOptions(ServiceNode serviceNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        string? prefix = rawCompilerOptions?.ReadPropertyAsStr("prefix");
        prefix ??= ((UnrealFileOptionsNode?)parentCompilerOptions)?.Prefix;
        
        return new UnrealServiceOptionsNode
        {
            Parent = new WeakReference<Node>(serviceNode),
            Name = SectionName,
            Prefix = prefix
        };
    }
}