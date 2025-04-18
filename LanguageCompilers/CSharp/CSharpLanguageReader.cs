using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecReader;

namespace Catalyst.LanguageCompilers.CSharp;

public class CSharpLanguageReader : LanguageFileReader
{
    public override string SectionName => CSharpLanguage.Name;
    
    public override CompilerOptionsNode? ReadGlobalOptions()
    {
        throw new NotImplementedException();
    }

    public override CompilerOptionsNode? ReadFileOptions(FileNode fileNode, RawNode? rawCompilerOptions)
    {
        CSharpClassType classType = ParseClassType(rawCompilerOptions?.ReadPropertyAsStr("classType")) ?? CSharpClassType.Record;
        
        return new CSharpFileCompilerOptionsNode
        {
            Parent = new WeakReference<Node>(fileNode),
            Name = SectionName,
            ClassType = classType
        };
    }

    public override CompilerOptionsNode? ReadDefinitionOptions(DefinitionNode definitionNode, CompilerOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        CSharpClassType? classType = null;
        
        // Read from current options
        if (rawCompilerOptions is not null)
            classType = ParseClassType(rawCompilerOptions.ReadPropertyAsStr("type"));
        
        // Read from parent options
        if (classType is null && parentCompilerOptions is not null)
            classType = ((CSharpFileCompilerOptionsNode)parentCompilerOptions).ClassType;
        
        return new CSharpDefinitionCompilerOptionsNode
        {
            Parent = new WeakReference<Node>(definitionNode),
            Name = SectionName,
            Type = classType ?? CSharpClassType.Record
        };
    }

    public override CompilerOptionsNode? ReadPropertyOptions(PropertyNode propertyNode, CompilerOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        return null;
    }

    static CSharpClassType? ParseClassType(string? classTypeStr)
    {
        if (string.IsNullOrEmpty(classTypeStr))
            return null;
        
        if (classTypeStr.Equals("class", StringComparison.OrdinalIgnoreCase))
            return CSharpClassType.Class;
        
        if (classTypeStr.Equals("record", StringComparison.OrdinalIgnoreCase))
            return CSharpClassType.Record;
        
        throw new InvalidOperationException("Expected class or record");
    }
}