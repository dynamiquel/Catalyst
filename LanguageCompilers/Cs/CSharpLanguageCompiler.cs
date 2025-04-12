using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.PropertyTypes;

namespace Catalyst.LanguageCompilers.Cs;

// Just raw dog it for now.
public class CSharpLanguageCompiler : LanguageCompiler
{
    public override CompiledFile CompileFile(File file)
    {
        StringBuilder sb = new();

        Include[] includes = file.Includes.Distinct().ToArray();
        foreach (Include include in includes)
            sb.AppendLine($"using {include.Path};");

        sb.AppendLine();
        
        if (!string.IsNullOrWhiteSpace(file.Namespace))
            sb.AppendLine($"namespace {file.Namespace};").AppendLine();

        foreach (Class def in file.Classes)
        {
            sb.AppendLine($"public class {def.Name}").AppendLine("{");

            foreach (Property property in def.Properties)
            {
                if (property.Attributes.Count > 0)
                {
                    sb.Append("    ");
                    foreach (Attribute attribute in property.Attributes)
                    {
                        sb.Append($"[{attribute.Name}");
                        if (!string.IsNullOrWhiteSpace(attribute.Arguments))
                            sb.Append($"({attribute.Arguments})");
                        sb.AppendLine("]");
                    }
                }

                sb.Append("    public");
                if (!property.Type.EndsWith("?") && string.IsNullOrWhiteSpace(property.Value))
                    sb.Append(" required");

                sb.Append($" {property.Type} {property.Name} {{ get; set; }}");
                
                if (!string.IsNullOrWhiteSpace(property.Value))
                    sb.Append($" = {property.Value};");
                
                sb.AppendLine();
            }

            foreach (Function function in def.Functions)
            {
                sb.AppendLine();
                
                sb.Append("    public");
                
                if (function.Static)
                    sb.Append(" static");

                sb.Append($" {function.ReturnType} {function.Name}(");

                for (int parameterIdx = 0; parameterIdx < function.Parameters.Count; parameterIdx++)
                {
                    sb.Append(function.Parameters[parameterIdx]);
                    if (parameterIdx < function.Parameters.Count - 1)
                        sb.Append(", ");
                }
                
                sb.AppendLine(")");
                
                sb.AppendLine("    {");

                sb.AppendLine($"        {function.Body}");
                
                sb.AppendLine("    }");
            }

            sb.AppendLine("}").AppendLine();
        }

        return new CompiledFile(file.Name, sb.ToString());
    }

    public override File CreateFile(FileNode fileNode)
    {
        string newFileName = fileNode.FileInfo.Name.Replace(fileNode.FileInfo.Extension, string.Empty).ToPascalCase() + ".cs";
        string newFilePath = Path.Combine(fileNode.FileInfo.DirectoryName ?? string.Empty, newFileName);
        
        File file = new(
            Name: newFilePath,
            Includes: [],
            Namespace: fileNode.Namespace.ToPascalCase(),
            Classes: []);

        return file;
    }

    protected override void AddPropertyType(File file, string propertyTypeName, IPropertyType propertyType)
    {
        switch (propertyType)
        {
            case ListType:
            case MapType:
            case SetType:
                file.Includes.Add(new("System.Collections.Generic"));
                break;
            case UserType userType:
                if (userType.Namespace is not null) 
                    file.Includes.Add(new(userType.Namespace.ToPascalCase()));
                break;
        }
    }

    protected override void AddDefinition(File file, DefinitionNode definition)
    {
        List<Property> properties = [];
        foreach (KeyValuePair<string, PropertyNode> propertyNode in definition.Properties)
        {
            PropertyType propertyType = GetPropertyType(propertyNode.Value.Type, propertyNode.Value.PropertyType!);

            string? defaultValue = null;
            if (propertyNode.Value.DefaultValue is not null)
                defaultValue = GetDefaultValueForProperty(propertyNode.Value);
            
            Property property = new Property(
                Name: propertyNode.Value.Name.ToPascalCase(),
                Type: propertyType.Name,
                Value: defaultValue,
                Attributes: [/* TODO */]);
            
            properties.Add(property);
        }

        Function serialiseFunc = new Function(
            Name: "ToBytes",
            ReturnType: "byte[]",
            Static: true,
            Parameters: [$"{definition.Name.ToPascalCase()} obj"],
            Body: "return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(obj);");
        
        Function deserialiseFunc = new Function(
            Name: "FromBytes",
            ReturnType: $"{definition.Name.ToPascalCase()}?",
            Static: true,
            Parameters: ["byte[] bytes"],
            Body: $"return System.Text.Json.JsonSerializer.Deserialize<{definition.Name.ToPascalCase()}>(bytes);");
        
        Class def = new Class(
            Name: definition.Name.ToPascalCase(),
            Properties: properties,
            Functions: [serialiseFunc, deserialiseFunc]);
        
        file.Classes.Add(def);
    }

    protected override PropertyType GetPropertyType(string propertyTypeName, IPropertyType propertyType)
    {
        PropertyType genPropertyType;
        switch (propertyType)
        {
            case AnyType:
                genPropertyType = new PropertyType("object");
                break;
            case BooleanType:
                genPropertyType = new PropertyType("bool");
                break;
            case DateType:
                genPropertyType = new PropertyType("DateTime");
                break;
            case FloatType:
                genPropertyType = new PropertyType("double");
                break;
            case IntegerType:
                genPropertyType = new PropertyType("int");
                break;
            case ListType listType:
                string innerType = ((IPropertyContainerType)listType).GetInnerPropertyTypes(propertyTypeName).First();
                genPropertyType = new PropertyType($"List<{innerType.ToPascalCase()}>");
                break;
            case MapType mapType:
                string[] mapInnerTypes = ((IPropertyContainerType)mapType).GetInnerPropertyTypes(propertyTypeName);
                genPropertyType = new PropertyType($"Dictionary<{mapInnerTypes[0].ToPascalCase()}, {mapInnerTypes[1].ToPascalCase()}>");
                break;
            case SetType setType:
                string setInnerType = ((IPropertyContainerType)setType).GetInnerPropertyTypes(propertyTypeName).First();
                genPropertyType = new PropertyType($"HashSet<{setInnerType.ToPascalCase()}>");
                break;
            case StringType:
                genPropertyType = new PropertyType("string");
                break;
            case TimespanType:
                genPropertyType = new PropertyType("TimeSpan");
                break;
            case UserType userType:
                genPropertyType = new PropertyType(userType.Name.ToPascalCase());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(propertyType));
        }

        if (propertyTypeName.EndsWith('?'))
            genPropertyType = new PropertyType($"{genPropertyType.Name}?");

        return genPropertyType;
    }
    
    protected override string GetDefaultValueForProperty(PropertyNode propertyNode)
    {
        switch (propertyNode.PropertyType)
        {
            case AnyType:
                throw new NotSupportedException("Default Value with Any is not supported");
            case BooleanType:
                return propertyNode.DefaultValue!;
            case DateType:
                return $"DateTime.Parse(\"{propertyNode.DefaultValue!}\")";
            case FloatType:
                return propertyNode.DefaultValue!;
            case IntegerType integerType:
                return propertyNode.DefaultValue!;
            case ListType listType:
                break;
            case MapType mapType:
                break;
            case SetType setType:
                break;
            case StringType:
                return $"\"{propertyNode.DefaultValue!}\"";
            case TimespanType:
                return $"TimeSpan.Parse(\"{propertyNode.DefaultValue!}\")";
            case UserType:
                return $"new {GetPropertyType(propertyNode.Type, propertyNode.PropertyType)}()";
            default:
                throw new ArgumentOutOfRangeException();
        }

        return string.Empty;
    }
}