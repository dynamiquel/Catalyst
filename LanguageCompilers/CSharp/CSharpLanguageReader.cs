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
        bool useRequires = rawCompilerOptions?.ReadPropertyAsBool("useRequired") ?? false;
        
        return new CSharpFileCompilerOptionsNode
        {
            Parent = new WeakReference<Node>(fileNode),
            Name = SectionName,
            ClassType = classType,
            UseRequired = useRequires
        };
    }

    public override CompilerOptionsNode? ReadDefinitionOptions(DefinitionNode definitionNode, CompilerOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        CSharpClassType? classType = null;
        bool? useRequires = null;
        
        // Read from current options
        if (rawCompilerOptions is not null)
        {
            classType = ParseClassType(rawCompilerOptions.ReadPropertyAsStr("type"));
            useRequires = rawCompilerOptions.ReadPropertyAsBool("useRequired");
        }

        // Read from parent options
        if (parentCompilerOptions is not null)
        {
            classType ??= ((CSharpFileCompilerOptionsNode)parentCompilerOptions).ClassType;
            useRequires ??= ((CSharpFileCompilerOptionsNode)parentCompilerOptions).UseRequired;
        }
        
        return new CSharpDefinitionCompilerOptionsNode
        {
            Parent = new WeakReference<Node>(definitionNode),
            Name = SectionName,
            Type = classType ?? CSharpClassType.Record,
            UseRequired = useRequires ?? false
        };
    }

    public override CompilerOptionsNode? ReadPropertyOptions(PropertyNode propertyNode, CompilerOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        bool? useRequires = null;

        // If property has been assigned a value then it can't be 'required'.
        if (propertyNode.Value is not null)
            useRequires = false;
        
        // Read from current options
        if (rawCompilerOptions is not null)
        {
            useRequires ??= rawCompilerOptions.ReadPropertyAsBool("required");
        }

        // Read from parent options
        if (parentCompilerOptions is not null)
        {
            useRequires ??= ((CSharpDefinitionCompilerOptionsNode)parentCompilerOptions).UseRequired;
        }
        
        return new CSharpPropertyCompilerOptionsNode
        {
            Parent = new WeakReference<Node>(propertyNode),
            Name = SectionName,
            UseRequired = useRequires ?? false
        };
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