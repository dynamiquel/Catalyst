using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecReader;

namespace Catalyst.LanguageCompilers.Unreal;

public class UnrealLanguageReader : LanguageFileReader
{
    public override string SectionName => UnrealLanguage.Name;
    
    public override CompilerOptionsNode? ReadGlobalOptions()
    {
        throw new NotImplementedException();
    }

    public override CompilerOptionsNode? ReadFileOptions(FileNode fileNode, RawNode? rawCompilerOptions)
    {
        string? prefix = rawCompilerOptions?.ReadPropertyAsStr("prefix");

        return new UnrealFileCompilerOptionsNode
        {
            Parent = new WeakReference<Node>(fileNode),
            Name = SectionName,
            Prefix = prefix
        };
    }

    public override CompilerOptionsNode? ReadDefinitionOptions(DefinitionNode definitionNode, CompilerOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        string? prefix = rawCompilerOptions?.ReadPropertyAsStr("prefix");
        prefix ??= ((UnrealFileCompilerOptionsNode?)parentCompilerOptions)?.Prefix;
        
        return new UnrealDefinitionCompilerOptionsNode
        {
            Parent = new WeakReference<Node>(definitionNode),
            Name = SectionName,
            Prefix = prefix
        };
    }

    public override CompilerOptionsNode? ReadPropertyOptions(PropertyNode propertyNode, CompilerOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        return null;
    }
}