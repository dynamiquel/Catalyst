using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators.Unreal;

public class UnrealClientServiceBuilder : IClientServiceBuilder<UnrealCompiler>
{
    public string Name => "default";
    public required UnrealCompiler Compiler { get; init; }
    
    public string GetBuiltFileName(BuildContext context, ServiceNode serviceNode)
    {
        // One file for all definitions.
        string? filePrefix = context.FileNode.FindCompilerOptions<UnrealFileOptionsNode>()?.Prefix;
        string fileName = filePrefix + StringExtensions.FilePathToPascalCase(context.FileNode.FileName) + ".h";
        return StringExtensions.FilePathToPascalCase(context.FileNode.Directory) + fileName;
    }

    public void Build(BuildContext context, ServiceNode serviceNode)
    {
        throw new NotImplementedException();
    }
}