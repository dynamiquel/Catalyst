using Catalyst.Generators.Builders;
using Catalyst.Generators.Unreal;
using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators.CSharp;

public class UnrealEnumBuilder : IEnumBuilder<UnrealCompiler>
{
    public string Name => Builder.Default;
    
    public string GetBuiltFileName(BuildContext context, EnumNode enumNode)
    {
        // One file for all definitions.
        string fileName = Compiler.GetFileName(context.FileNode) + ".h";
        
        return Path.Combine(
            StringExtensions.FilePathToPascalCase(context.FileNode.Directory) ?? string.Empty, 
            fileName);
    }

    public void Build(BuildContext context, EnumNode enumNode)
    {
        BuiltEnum builtEnum = new BuiltEnum(
            Node: enumNode,
            Name: GetCompiledEnumName(enumNode),
            Values: enumNode.Values.Select(x => new BuiltEnumValue(x.Key, x.Value)).ToList()
        );
        
        context.GetOrAddFile(Compiler, GetBuiltFileName(context, enumNode), FileFlags.Header).Enums.Add(builtEnum);
    }
    
    public string GetCompiledEnumName(EnumNode enumNode)
    {
        var compilerOptions = enumNode.FindCompilerOptions<UnrealEnumOptionsNode>()!;
        string? prefix = compilerOptions.Prefix ?? Compiler.GetPrefixFromNamespace(enumNode.GetParentChecked<FileNode>().Namespace);

        return $"F{prefix}{enumNode.Name.ToPascalCase()}";
    }

    public required UnrealCompiler Compiler { get; init; }
}