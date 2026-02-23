using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators.TypeScript;

public class TypeScriptEnumBuilder : IEnumBuilder<TypeScriptCompiler>
{
    public string Name => Builder.Default;

    public string GetBuiltFileName(BuildContext context, EnumNode enumNode)
    {
        return Helpers.FilePathToPascalCase(context.FileNode.FilePath) + ".ts";
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

    public required TypeScriptCompiler Compiler { get; init; }
}

