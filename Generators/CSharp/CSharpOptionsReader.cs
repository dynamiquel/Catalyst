using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecReader;

namespace Catalyst.Generators.CSharp;

public class CSharpOptionsReader : OptionsReader
{
    public override string SectionName => CSharp.Name;
    
    public override GeneratorOptionsNode? ReadGlobalOptions()
    {
        throw new NotImplementedException();
    }

    public override GeneratorOptionsNode? ReadFileOptions(FileNode fileNode, RawNode? rawCompilerOptions)
    {
        CSharpClassType classType = ParseClassType(rawCompilerOptions?.ReadPropertyAsStr("classType")) ?? CSharpClassType.Record;
        bool useRequires = rawCompilerOptions?.ReadPropertyAsBool("useRequired") ?? true;
        bool useOptions = rawCompilerOptions?.ReadPropertyAsBool("useOptions") ?? true;

        return new CSharpFileOptionsNode
        {
            Parent = new WeakReference<Node>(fileNode),
            Name = SectionName,
            ClassType = classType,
            UseRequired = useRequires,
            UseOptions = useOptions
        };
    }

    public override GeneratorOptionsNode? ReadEnumOptions(EnumNode enumNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
    {
        return new CSharpEnumOptionsNode
        {
            Parent = new WeakReference<Node>(enumNode),
            Name = SectionName,
        };
    }

    public override GeneratorOptionsNode? ReadDefinitionOptions(DefinitionNode definitionNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
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
            classType ??= ((CSharpFileOptionsNode)parentCompilerOptions).ClassType;
            useRequires ??= ((CSharpFileOptionsNode)parentCompilerOptions).UseRequired;
        }
        
        return new CSharpDefinitionOptionsNode
        {
            Parent = new WeakReference<Node>(definitionNode),
            Name = SectionName,
            Type = classType ?? CSharpClassType.Record,
            UseRequired = useRequires ?? true
        };
    }

    public override GeneratorOptionsNode? ReadPropertyOptions(PropertyNode propertyNode, GeneratorOptionsNode? parentCompilerOptions, RawNode? rawCompilerOptions)
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
            useRequires ??= ((CSharpDefinitionOptionsNode)parentCompilerOptions).UseRequired;
        }
        
        return new CSharpPropertyOptionsNode
        {
            Parent = new WeakReference<Node>(propertyNode),
            Name = SectionName,
            UseRequired = useRequires ?? true
        };
    }

    public override GeneratorOptionsNode? ReadServiceOptions(ServiceNode serviceNode, GeneratorOptionsNode? parentCompilerOptions,
        RawNode? rawCompilerOptions)
    {
        bool useOptions = 
            rawCompilerOptions?.ReadPropertyAsBool("useOptions") ?? 
            ((CSharpFileOptionsNode?)parentCompilerOptions)?.UseOptions ??
            true;
        
        return new CSharpServiceOptionsNode
        {
            Parent = new WeakReference<Node>(serviceNode),
            Name = SectionName,
            UseOptions = useOptions
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