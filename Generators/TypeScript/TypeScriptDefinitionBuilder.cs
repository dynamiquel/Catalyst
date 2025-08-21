using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;

namespace Catalyst.Generators.TypeScript;

public class TypeScriptDefinitionBuilder : IDefinitionBuilder<TypeScriptCompiler>
{
    public string Name => Builder.Default;
    public required TypeScriptCompiler Compiler { get; init; }

    public string GetBuiltFileName(BuildContext context, DefinitionNode definitionNode)
    {
        return StringExtensions.FilePathToPascalCase(context.FileNode.FilePath) + ".ts";
    }

    public void Build(BuildContext context, DefinitionNode definitionNode)
    {
        List<BuiltProperty> properties = [];
        foreach (KeyValuePair<string, PropertyNode> propertyNode in definitionNode.Properties)
        {
            BuiltPropertyType propertyType = Compiler.GetCompiledPropertyType(propertyNode.Value.BuiltType!);
            BuiltPropertyValue propertyValue = this.GetCompiledPropertyValue(propertyNode.Value.BuiltType!, propertyNode.Value.Value);

            properties.Add(new(
                Node: propertyNode.Value,
                Name: propertyNode.Value.Name.ToPascalCase(),
                Type: propertyType,
                Value: propertyValue
            ));
        }

        List<BuiltFunction> functions = [];
        functions.AddRange(BuildSerialiseFunctions(context, definitionNode));
        functions.AddRange(BuildDeserialiseFunctions(context, definitionNode));

        BuiltDefinition definition = new(
            Node: definitionNode,
            Name: GetCompiledClassName(definitionNode),
            Properties: properties,
            Functions: functions);

        context.GetOrAddFile(Compiler, GetBuiltFileName(context, definitionNode)).Definitions.Add(definition);
    }

    public BuiltPropertyValue GetCompiledDefaultValueForPropertyType(IPropertyType propertyType)
    {
        return propertyType switch
        {
            IOptionalPropertyType => new NoPropertyValue(),
            AnyType or ObjectType => new SomePropertyValue("{} as any"),
            ListType or MapType or SetType => new SomePropertyValue("[]"),
            StringType => new SomePropertyValue("''"),
            _ => new NoPropertyValue()
        };
    }

    public BuiltPropertyValue GetCompiledDesiredPropertyValue(IPropertyValue propertyValue)
    {
        switch (propertyValue)
        {
            case BooleanValue booleanValue:
                return new SomePropertyValue(booleanValue.Value ? "true" : "false");
            case DateValue dateValue:
                return new SomePropertyValue($"'" + dateValue.Value.ToString("O") + "'");
            case FloatValue floatValue:
                return new SomePropertyValue(floatValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            case IntegerValue integerValue:
                return new SomePropertyValue(integerValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            case ListValue listValue:
                System.Text.StringBuilder sb = new();
                sb.Append('[');
                for (int itemIdx = 0; itemIdx < listValue.Values.Count; itemIdx++)
                {
                    IPropertyValue itemValue = listValue.Values[itemIdx];
                    sb.Append(GetCompiledDesiredPropertyValue(itemValue));
                    if (itemIdx < listValue.Values.Count - 1)
                        sb.Append(", ");
                }
                sb.Append(']');
                return new SomePropertyValue(sb.ToString());
            case EnumValue enumValue:
                string enumPrefix = Compiler.GetCompiledPropertyType(enumValue.Type).Name;
                string value = string.Join(" | ", enumValue.Values.Select(x => $"{enumPrefix}.{x}"));
                return new SomePropertyValue(value);
            case MapValue:
                throw new NotImplementedException();
            case NullValue:
                return new SomePropertyValue("null");
            case ObjectValue:
                throw new NotImplementedException();
            case StringValue stringValue:
                return new SomePropertyValue($"'" + stringValue.Value.Replace("'", "\\'") + "'");
            case TimeValue timeValue:
                return new SomePropertyValue(timeValue.Value.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public string GetCompiledClassName(DefinitionNode definitionNode)
    {
        return definitionNode.Name.ToPascalCase();
    }

    public IEnumerable<BuiltFunction> BuildSerialiseFunctions(BuildContext context, DefinitionNode definitionNode)
    {
        return [
            new BuiltFunction(
                Name: "toBytes",
                ReturnType: "Uint8Array",
                Flags: FunctionFlags.Const,
                Parameters: [],
                BodyInit: "const json = JSON.stringify(this); return new TextEncoder().encode(json);")
        ];
    }

    public IEnumerable<BuiltFunction> BuildDeserialiseFunctions(BuildContext context, DefinitionNode definitionNode)
    {
        return [
            new BuiltFunction(
                Name: "fromBytes",
                ReturnType: $"{definitionNode.Name.ToPascalCase()} | null",
                Flags: FunctionFlags.Static,
                Parameters: ["bytes: Uint8Array"],
                BodyInit: $"try {{ const json = new TextDecoder().decode(bytes); const obj = JSON.parse(json) as {definitionNode.Name.ToPascalCase()}; return Object.assign(new {definitionNode.Name.ToPascalCase()}(), obj); }} catch {{ return null; }}")
        ];
    }
}

