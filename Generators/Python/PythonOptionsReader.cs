using Catalyst.SpecReader;
using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators.Python;

public class PythonOptionsReader : OptionsReader
{
    public override string SectionName => Python.Name;

    public override GeneratorOptionsNode? ReadGlobalOptions()
    {
        return null;
    }

    public override GeneratorOptionsNode? ReadFileOptions(FileNode fileNode, RawNode? rawCompilerOptions)
    {
        return null;
    }

    public override GeneratorOptionsNode? ReadEnumOptions(EnumNode enumNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        return null;
    }

    public override GeneratorOptionsNode? ReadDefinitionOptions(DefinitionNode definitionNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        return null;
    }

    public override GeneratorOptionsNode? ReadPropertyOptions(PropertyNode propertyNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        return null;
    }

    public override GeneratorOptionsNode? ReadServiceOptions(ServiceNode serviceNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        return null;
    }
}
