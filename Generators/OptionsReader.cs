using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecReader;

namespace Catalyst.Generators;

public abstract class OptionsReader
{
    public abstract string SectionName { get; }

    public abstract GeneratorOptionsNode? ReadGlobalOptions();
    public abstract GeneratorOptionsNode? ReadFileOptions(FileNode fileNode, RawNode? rawCompilerOptions);
    public abstract GeneratorOptionsNode? ReadDefinitionOptions(DefinitionNode definitionNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions);
    public abstract GeneratorOptionsNode? ReadPropertyOptions(PropertyNode propertyNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions);
    public abstract GeneratorOptionsNode? ReadServiceOptions(ServiceNode serviceNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions);

    public RawNode? GetRawCompilerOptions(RawNode rawNode)
    {
        Dictionary<object, object>? values = rawNode.ReadPropertyAsDictionary(SectionName);
        return values is null ? null : rawNode.CreateChild(values, SectionName);
    }

    public GeneratorOptionsNode? GetCompilerOptions(ICompilerOptions node)
    {
        node.CompilerOptions.TryGetValue(SectionName, out GeneratorOptionsNode? compilerOptions);
        return compilerOptions;
    }
}