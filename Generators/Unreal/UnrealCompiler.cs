using System.Text;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;

namespace Catalyst.Generators.Unreal;

public class UnrealCompiler : Compiler
{
    public override string Name => Unreal.Name;
    
    public UnrealCompiler(CompilerOptions options) : base(options)
    {
    }

    public override CompiledFile Compile(BuiltFile file)
    {
        return file.Flags == FileFlags.Header ? CompileHeaderFile(file) : CompileSourceFile(file);
    }

    public override BuiltInclude? GetCompiledIncludeForType(BuiltFile file, IPropertyType propertyType)
    {
        switch (propertyType)
        {
            case AnyType:
                return new("StructUtils/InstancedStruct");
            case DateType:
                return new ("Misc/DateTime");
            case TimeType:
                return new("Misc/Timespan");
            case ObjectType objectType:
                string compiledFileName = DefinitionBuilder.GetBuiltFileName(
                    new BuildContext(objectType.OwnedFile, []), 
                    objectType.OwnedDefinition);
                
                if (compiledFileName != file.Name)
                    return new (compiledFileName);
                break;
        }
        
        return null;
    }

    public override BuiltPropertyType GetCompiledPropertyType(IPropertyType propertyType)
    {
        BuiltPropertyType genPropertyType;
        switch (propertyType)
        {
            case AnyType:
                genPropertyType = new("FInstancedStruct");
                break;
            case BooleanType:
                genPropertyType = new("bool");
                break;
            case DateType:
                genPropertyType = new("FDateTime");
                break;
            case FloatType:
                genPropertyType = new("double");
                break;
            case IntegerType:
                genPropertyType = new("int32");
                break;
            case ListType listType:
                BuiltPropertyType innerListPropertyType = GetCompiledPropertyType(listType.InnerType);
                genPropertyType = new($"TArray<{innerListPropertyType.Name}>");
                break;
            case MapType mapType:
                BuiltPropertyType innerKeyPropertyType = GetCompiledPropertyType(mapType.InnerTypeA);
                BuiltPropertyType innerValuePropertyType = GetCompiledPropertyType(mapType.InnerTypeB);
                genPropertyType = new($"TMap<{innerKeyPropertyType.Name}, {innerValuePropertyType.Name}>");
                break;
            case SetType setType:
                BuiltPropertyType innerSetPropertyType = GetCompiledPropertyType(setType.InnerType);
                genPropertyType = new($"TSet<{innerSetPropertyType.Name}>");
                break;
            case StringType:
                genPropertyType = new("FString");
                break;
            case TimeType:
                genPropertyType = new ("FTimespan");
                break;
            case ObjectType objectType:
                genPropertyType = new (DefinitionBuilder.GetCompiledClassName(objectType.OwnedDefinition));
                break;
            case EnumType enumType:
                genPropertyType = new(EnumBuilder.GetCompiledEnumName(enumType.OwnedEnum));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(propertyType));
        }

        // Unreal Reflection doesn't support Containers as a template, i.e. TOptional<TArray<T>>
        // Instead, we have to treat it as an empty container.
        // The alternative would be to generate our own serialisers in Unreal
        // without the help of reflection, but that would be a lot of work.
        if (propertyType is IOptionalPropertyType and not IPropertyContainerType)
            genPropertyType = new($"TOptional<{genPropertyType.Name}>");

        return genPropertyType;
    }

    public override string? GetCompiledNamespace(string? namespaceName)
    {
        return namespaceName.ToPascalCase()?.Replace(".", "::");
    }

    public override string GetCompiledClassName(string className)
    {
        // Not so useful tho since it doesn't have the capacity to support prefixes.
        return className.ToPascalCase();
    }
    
    public string? GetPrefixFromNamespace(string? namespaceName)
    {
        return namespaceName.ToPascalCase()?.Replace(".", "");
    }

    public string GetFileName(FileNode fileNode)
    {
        /*
         * Looks a bit messy but essentially maps a file with:
         *    namespace: Hello.World
         *    fileName: World
         *
         * to Hello.World, rather than Hello.World.World
         */
        
        string? filePrefix = fileNode.FindCompilerOptions<UnrealFileOptionsNode>()?.Prefix ?? GetPrefixFromNamespace(fileNode.Namespace);
        string fileName = filePrefix ?? string.Empty;
        string desiredFileName = StringExtensions.FilePathToPascalCase(fileNode.FileName);
        if (!fileName.EndsWith(desiredFileName))
            fileName += desiredFileName;
        
        return fileName;
    }
    
    public StringBuilder AppendDescriptionComment(StringBuilder sb, INodeDescription node, int indentation = 0)
    {
        if (string.IsNullOrEmpty(node.Description)) 
            return sb;

        for (int indent = 0; indent < indentation; indent++)
            sb.Append("    ");
        sb.AppendLine("/**");
        
        string[] descLines = node.Description.Split('\n');
        foreach (string descLine in descLines)
        {
            for (int indent = 0; indent < indentation; indent++)
                sb.Append("    ");
            sb.AppendLine($" * {descLine}");
        }

        for (int indent = 0; indent < indentation; indent++)
            sb.Append("    ");
        sb.AppendLine(" */");

        return sb;
    }
    
    CompiledFile CompileHeaderFile(BuiltFile file)
    {
        StringBuilder sb = new();
        
        AddFileComment(file, sb);
        
        sb.AppendLine("#pragma once");
        sb.AppendLine();
        
        AddIncludes(file, sb);
        sb.AppendLine($"#include \"{Path.GetFileNameWithoutExtension(file.Name)}.generated.h\"");
        sb.AppendLine();

        foreach (BuiltEnum builtEnum in file.Enums)
        {
            AppendDescriptionComment(sb, builtEnum.Node);

            sb.Append("UENUM(BlueprintType");
            if (builtEnum.Node.Flags == true)
                sb.Append(", Meta=(Bitflags)");
            
            sb
                .AppendLine(")")
                .AppendLine($"enum class {builtEnum.Name} : int32")
                .AppendLine("{");
            
            for (var enumValueIdx = 0; enumValueIdx < builtEnum.Values.Count; enumValueIdx++)
            {
                BuiltEnumValue builtEnumValue = builtEnum.Values[enumValueIdx];
                sb.Append($"    {builtEnumValue.Label} = {builtEnumValue.Value}");

                if (enumValueIdx < builtEnum.Values.Count - 1)
                    sb.Append(',');
                
                sb.AppendLine();
            }

            sb.AppendLine("};");
            
            if (builtEnum.Node.Flags == true)
                sb.AppendLine($"ENUM_CLASS_FLAGS({builtEnum.Name})");
            
            sb.AppendLine();
        }
        
        foreach (BuiltDefinition def in file.Definitions)
        {
            AppendDescriptionComment(sb, def.Node);
            
            sb
                .AppendLine("USTRUCT(BlueprintType)")
                .AppendLine($"struct {def.Name}")
                .AppendLine("{")
                .AppendLine("    GENERATED_BODY()")
                .AppendLine();

            for (int propertyIndex = 0; propertyIndex < def.Properties.Count; propertyIndex++)
            {
                BuiltProperty property = def.Properties[propertyIndex];
                
                AppendDescriptionComment(sb, property.Node, 1);
                
                sb.Append("    UPROPERTY(EditAnywhere");
                /*if (property.Attributes.Count > 0)
                {
                    foreach (Attribute attribute in property.Attributes)
                    {
                        sb.Append($", {attribute.Name}");
                        if (!string.IsNullOrWhiteSpace(attribute.Arguments))
                            sb.Append($" = {attribute.Arguments}");
                    }
                }*/
                sb.AppendLine(")");

                sb.Append($"    {property.Type.Name} {property.Name}");

                if (property.Value is not NoPropertyValue)
                    sb.Append($" = {property.Value.Value};");
                else
                    sb.Append(";");
                
                sb.AppendLine();

                if (propertyIndex < def.Properties.Count - 1)
                    sb.AppendLine();
            }

            foreach (BuiltFunction function in def.Functions)
            {
                sb.AppendLine();
                sb.Append("    ");
                
                if (function.Flags is FunctionFlags.Static)
                    sb.Append("static ");

                sb.Append($"{function.ReturnType} {function.Name}(");

                for (int parameterIdx = 0; parameterIdx < function.Parameters.Count; parameterIdx++)
                {
                    sb.Append(function.Parameters[parameterIdx]);
                    if (parameterIdx < function.Parameters.Count - 1)
                        sb.Append(", ");
                }
                
                sb.Append(")");

                if (function.Flags is FunctionFlags.Const)
                    sb.Append(" const");

                if (function.Body is null)
                    sb.AppendLine(";");
                else
                {
                    sb.AppendLine();
                    sb.AppendLine("    {");
                    string[] bodyLines = function.Body.Split(Environment.NewLine);
                    foreach (string line in bodyLines)
                        sb.AppendLine($"        {line}");

                    sb.AppendLine("    }");
                }
            }

            sb.AppendLine("};").AppendLine();
        }
        
        // TODO: Redo BuiltService and BuiltEndpoint. The implementation details should be generated
        // in the Build stage, not the Compile stage.
        if (ClientServiceBuilder is not null)
            foreach (BuiltService service in file.Services)
                ClientServiceBuilder.Compile(file, service, sb);
        
        return new CompiledFile(file.Name, sb.ToString());
    }
    
    CompiledFile CompileSourceFile(BuiltFile file)
    {
        StringBuilder sb = new();
        
        AddFileComment(file, sb);
        
        sb.AppendLine($"#include \"{Path.GetFileNameWithoutExtension(file.Name)}.h\"");
        sb.AppendLine();
        AddIncludes(file, sb);

        foreach (BuiltDefinition def in file.Definitions)
        {
            foreach (BuiltFunction function in def.Functions)
            {
                if (function.Body is null)
                    throw new InvalidOperationException("Function body is null within a source file definition");
                
                sb
                    .AppendLine()
                    .Append($"{function.ReturnType} {def.Name}::{function.Name}(");
                
                for (int parameterIdx = 0; parameterIdx < function.Parameters.Count; parameterIdx++)
                {
                    sb.Append(function.Parameters[parameterIdx]);
                    if (parameterIdx < function.Parameters.Count - 1)
                        sb.Append(", ");
                }
                
                sb.Append(")");
                
                if (function.Flags is FunctionFlags.Const)
                    sb.Append(" const");

                sb.AppendLine();
                sb.AppendLine("{");
                string[] bodyLines = function.Body.Split(Environment.NewLine);
                foreach (string line in bodyLines)
                    sb.AppendLine($"    {line}");

                sb.AppendLine("}");
            }
        }
        
        // TODO: Redo BuiltService and BuiltEndpoint. The implementation details should be generated
        // in the Build stage, not the Compile stage.
        if (ClientServiceBuilder is not null)
            foreach (BuiltService service in file.Services)
                ClientServiceBuilder.Compile(file, service, sb);
        
        return new CompiledFile(file.Name, sb.ToString());
    }

    void AddFileComment(BuiltFile file, StringBuilder sb)
    {
        sb.AppendLine("//");
        sb.AppendLine($"// This file was generated by Catalyst's Unreal compiler at {DateTime.Now}.");
        sb.AppendLine("// It is recommended not to modify this file. Modify the source spec file instead.");
        sb.AppendLine("//");
        sb.AppendLine();
    }
    
    void AddIncludes(BuiltFile file, StringBuilder sb)
    {
        if (file.Includes.Count > 0)
        {
            foreach (BuiltInclude include in file.Includes)
            {
                string includePath = Path.ChangeExtension(include.Path, ".h");
                sb.AppendLine($"#include \"{includePath}\"");
            }
            sb.AppendLine();
        }
    }
}