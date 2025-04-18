using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecReader;

namespace Catalyst.LanguageCompilers;

public abstract class LanguageFileReader
{
    public abstract string SectionName { get; }

    public abstract CompilerOptionsNode? ReadGlobalOptions();
    public abstract CompilerOptionsNode? ReadFileOptions(FileNode fileNode, RawNode? rawCompilerOptions);
    public abstract CompilerOptionsNode? ReadDefinitionOptions(DefinitionNode definitionNode, CompilerOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions);
    public abstract CompilerOptionsNode? ReadPropertyOptions(PropertyNode propertyNode, CompilerOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions);

    public RawNode? GetRawCompilerOptions(RawNode rawNode)
    {
        Dictionary<object, object>? values = rawNode.ReadPropertyAsDictionary(SectionName);
        return values is null ? null : rawNode.CreateChild(values, SectionName);
    }

    public CompilerOptionsNode? GetCompilerOptions(ICompilerOptions node)
    {
        node.CompilerOptions.TryGetValue(SectionName, out CompilerOptionsNode? compilerOptions);
        return compilerOptions;
    }
}