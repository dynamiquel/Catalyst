using System.Text.Json.Serialization;
using Catalyst.Generators.CSharp;
using Catalyst.Generators.Unreal;

namespace Catalyst.SpecGraph.Nodes;

// Annoying. Maybe better way to solve this JsonDerivedType stuff.
// This is only used for debugging any ways.
[JsonDerivedType(typeof(CSharpGlobalOptionsNode))]
[JsonDerivedType(typeof(CSharpFileOptionsNode))]
[JsonDerivedType(typeof(CSharpDefinitionOptionsNode))]
[JsonDerivedType(typeof(CSharpPropertyOptionsNode))]
[JsonDerivedType(typeof(UnrealGlobalOptionsNode))]
[JsonDerivedType(typeof(UnrealFileOptionsNode))]
[JsonDerivedType(typeof(UnrealDefinitionOptionsNode))]
[JsonDerivedType(typeof(UnrealPropertyOptionsNode))]
public abstract class GeneratorOptionsNode : Node;

public interface ICompilerOptions
{
    Dictionary<string, GeneratorOptionsNode> CompilerOptions { get; }
}

public static class CompilerOptionsExtensions
{
    public static GeneratorOptionsNode? FindCompilerOptions(this ICompilerOptions compilerOptionsContainer, string compilerName)
    {
        compilerOptionsContainer.CompilerOptions.TryGetValue(compilerName, out GeneratorOptionsNode? compilerOptions);
        return compilerOptions;
    }

    public static T? FindCompilerOptions<T>(this ICompilerOptions compilerOptionsContainer) where T : GeneratorOptionsNode
    {
        return (T?)compilerOptionsContainer.CompilerOptions.FirstOrDefault(x => x.Value is T).Value;
    }
}