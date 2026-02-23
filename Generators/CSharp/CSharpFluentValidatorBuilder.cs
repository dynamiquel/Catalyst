using System.Text;
using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;

namespace Catalyst.Generators.CSharp;

public class CSharpFluentValidatorBuilder : IValidatorBuilder<CSharpCompiler>
{
    public string Name => "default;fluent";
    public required CSharpCompiler Compiler { get; init; }

    public string GetBuiltFileName(BuildContext context, DefinitionNode definitionNode)
    {
        // One file for all validators.
        return Helpers.FilePathToPascalCase(context.FileNode.FilePath) + "Validators.cs";
    }

    public void Build(BuildContext context, DefinitionNode definitionNode)
    {
        var propertiesWithValidation = definitionNode.Properties
            .Where(p => p.Value.Validation is { } && (p.Value.Validation.Min.HasValue || p.Value.Validation.Max.HasValue || !string.IsNullOrEmpty(p.Value.Validation.Pattern)))
            .ToList();

        if (propertiesWithValidation.Count == 0)
            return;

        var validator = new BuiltValidator(
            Node: definitionNode,
            Name: definitionNode.Name.ToPascalCase() + "Validator",
            TargetName: definitionNode.Name.ToPascalCase(),
            Properties: propertiesWithValidation.Select(p => new BuiltValidatorProperty(
                Node: p.Value,
                Name: p.Value.Name.ToPascalCase(),
                Validation: p.Value.Validation!
            )).ToList()
        );

        BuiltFile file = context.GetOrAddFile(Compiler, GetBuiltFileName(context, definitionNode));
        file.Validators.Add(validator);
        file.Includes.Add(new BuiltInclude("FluentValidation"));
    }

    public void Compile(BuiltFile file, BuiltValidator validator, StringBuilder sb)
    {
        sb.AppendLine($"public class {validator.Name} : AbstractValidator<{validator.TargetName}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public {validator.Name}()");
        sb.AppendLine("    {");

        foreach (var prop in validator.Properties)
        {
            BuiltDataType propertyType = Compiler.GetCompiledDataType(prop.Node.BuiltType!);
            string csharpType = propertyType.Name;
            ValidationAttributes validation = prop.Validation;

            bool isCollection = csharpType.StartsWith("List<") || csharpType.StartsWith("HashSet<");
            bool isNumeric = csharpType is "int" or "long" or "double" or "decimal" 
                          or "int?" or "long?" or "double?" or "decimal?";
            bool isTimeSpan = csharpType is "TimeSpan" or "TimeSpan?";
            bool isNullable = prop.Node.BuiltType is IOptionalDataType;
            bool hasMin = validation.Min.HasValue;
            bool hasMax = validation.Max.HasValue;
            bool hasRange = hasMin && hasMax && isNumeric;
            bool bothInclusive = hasRange && validation.MinInclusive && validation.MaxInclusive;
            bool hasValidation = hasMin || hasMax || validation.Pattern is not null;

            if (!hasValidation)
                continue;

            string propertyAccess = $"x => x.{prop.Name}";
            string indent = "        ";
            if (isNullable)
            {
                sb.AppendLine($"{indent}When(x => x.{prop.Name} is not null, () =>");
                sb.AppendLine($"{indent}{{");
                indent = "            ";
                propertyAccess = $"x => x.{prop.Name}!";
            }

            List<string> rules = [];

            if (bothInclusive)
            {
                rules.Add($".InclusiveBetween({validation.Min!.Value:F0}, {validation.Max!.Value:F0})");
            }
            else if (hasRange)
            {
                rules.Add(validation.MinInclusive
                    ? $".GreaterThanOrEqualTo({validation.Min!.Value:F0})"
                    : $".GreaterThan({validation.Min!.Value:F0})");
                
                rules.Add(validation.MaxInclusive
                    ? $".LessThanOrEqualTo({validation.Max!.Value:F0})"
                    : $".LessThan({validation.Max!.Value:F0})");
            }
            else if ((csharpType == "string" || csharpType == "string?") && hasRange)
            {
                rules.Add($".MinimumLength({(int)validation.Min!.Value})");
                rules.Add($".MaximumLength({(int)validation.Max!.Value})");
                
                if (validation.Min.Value == 1)
                    rules.Add(".NotEmpty()");
            }
            else
            {
                if (hasMin && isNumeric)
                {
                    rules.Add(validation.MinInclusive
                        ? $".GreaterThanOrEqualTo({validation.Min!.Value:F0})"
                        : $".GreaterThan({validation.Min!.Value:F0})");
                }
                else if (hasMin && (csharpType == "string" || csharpType == "string?"))
                {
                    rules.Add($".MinimumLength({(int)validation.Min!.Value})");
                    if (validation.Min.Value == 1)
                        rules.Add(".NotEmpty()");
                }
                else if (hasMin && csharpType == "DateTime")
                {
                    rules.Add(validation.MinInclusive
                        ? $".GreaterThanOrEqualTo(DateTime.Parse(\"1970-01-01\").AddSeconds({validation.Min!.Value}))"
                        : $".GreaterThan(DateTime.Parse(\"1970-01-01\").AddSeconds({validation.Min!.Value}))");
                }
                else if (hasMin && isTimeSpan)
                {
                    rules.Add(validation.MinInclusive
                        ? $".GreaterThanOrEqualTo(TimeSpan.FromSeconds({validation.Min!.Value}))"
                        : $".GreaterThan(TimeSpan.FromSeconds({validation.Min!.Value}))");
                }
                else if (hasMin && isCollection)
                {
                    rules.Add(validation.MinInclusive
                        ? $".Count().GreaterThanOrEqualTo({(int)validation.Min!.Value})"
                        : $".Count().GreaterThan({(int)validation.Min!.Value})");
                }

                if (hasMax && !hasRange && isNumeric)
                {
                    rules.Add(validation.MaxInclusive
                        ? $".LessThanOrEqualTo({validation.Max!.Value:F0})"
                        : $".LessThan({validation.Max!.Value:F0})");
                }
                else if (hasMax && !hasRange && (csharpType == "string" || csharpType == "string?"))
                {
                    rules.Add($".MaximumLength({(int)validation.Max!.Value})");
                }
                else if (hasMax && csharpType == "DateTime")
                {
                    rules.Add(validation.MaxInclusive
                        ? $".LessThanOrEqualTo(DateTime.Parse(\"1970-01-01\").AddSeconds({validation.Max!.Value}))"
                        : $".LessThan(DateTime.Parse(\"1970-01-01\").AddSeconds({validation.Max!.Value}))");
                }
                else if (hasMax && isTimeSpan)
                {
                    rules.Add(validation.MaxInclusive
                        ? $".LessThanOrEqualTo(TimeSpan.FromSeconds({validation.Max!.Value}))"
                        : $".LessThan(TimeSpan.FromSeconds({validation.Max!.Value}))");
                }
                else if (hasMax && isCollection)
                {
                    if (!hasMin)
                        rules.Add(".Count()");
                    
                    rules.Add(validation.MaxInclusive
                        ? $".LessThanOrEqualTo({(int)validation.Max!.Value})"
                        : $".LessThan({(int)validation.Max!.Value})");
                }
            }
            
            if (validation.Pattern is not null)
                rules.Add($".Matches(@\"{validation.Pattern}\")");

            sb.AppendLine($"{indent}RuleFor({propertyAccess})");

            for (int i = 0; i < rules.Count; i++)
            {
                if (i == rules.Count - 1)
                    sb.AppendLine($"{indent}    {rules[i]};");
                else
                    sb.AppendLine($"{indent}    {rules[i]}");
            }

            if (isNullable)
                sb.AppendLine($"        }});");

            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
    }
}
