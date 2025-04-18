using System.Text.Json.Serialization;
using Catalyst.LanguageCompilers.CSharp;
using Catalyst.LanguageCompilers.Unreal;

namespace Catalyst.SpecGraph.Nodes;

// Annoying. Maybe better way to solve this JsonDerivedType stuff.
// This is only used for debugging any ways.
[JsonDerivedType(typeof(CSharpGlobalCompilerOptionsNode))]
[JsonDerivedType(typeof(CSharpFileCompilerOptionsNode))]
[JsonDerivedType(typeof(CSharpDefinitionCompilerOptionsNode))]
[JsonDerivedType(typeof(CSharpPropertyCompilerOptionsNode))]
[JsonDerivedType(typeof(UnrealGlobalCompilerOptionsNode))]
[JsonDerivedType(typeof(UnrealFileCompilerOptionsNode))]
[JsonDerivedType(typeof(UnrealDefinitionCompilerOptionsNode))]
[JsonDerivedType(typeof(UnrealPropertyCompilerOptionsNode))]
public abstract class CompilerOptionsNode : Node;

public interface ICompilerOptions
{
    Dictionary<string, CompilerOptionsNode> CompilerOptions { get; }
}

public static class CompilerOptionsExtensions
{
    public static CompilerOptionsNode? FindCompilerOptions(this ICompilerOptions compilerOptionsContainer, string compilerName)
    {
        compilerOptionsContainer.CompilerOptions.TryGetValue(compilerName, out CompilerOptionsNode? compilerOptions);
        return compilerOptions;
    }
}