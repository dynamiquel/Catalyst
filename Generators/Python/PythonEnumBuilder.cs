using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators.Python;

public class PythonEnumBuilder : IEnumBuilder<PythonCompiler>
{
    public string Name => Builder.Default;
    public required PythonCompiler Compiler { get; init; }

    public string GetBuiltFileName(BuildContext context, EnumNode enumNode)
    {
        return Compiler.GetModuleName(context.FileNode);
    }

    public void Build(BuildContext context, EnumNode enumNode)
    {
        BuiltEnum builtEnum = new BuiltEnum(
            Node: enumNode,
            Name: GetCompiledEnumName(enumNode),
            Values: enumNode.Values.Select(x => new BuiltEnumValue(x.Key, x.Value)).ToList()
        );

        context.GetOrAddFile(Compiler, GetBuiltFileName(context, enumNode)).Enums.Add(builtEnum);
    }

    public string GetCompiledEnumName(EnumNode enumNode)
    {
        return enumNode.Name.ToPascalCase();
    }
}
