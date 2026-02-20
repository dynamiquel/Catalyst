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
            BuiltDataType propertyType = Compiler.GetCompiledDataType(propertyNode.Value.BuiltType!);
            BuiltDataValue propertyValue = this.GetCompiledDataValue(propertyNode.Value.BuiltType!, propertyNode.Value.Value);

            properties.Add(new(
                Node: propertyNode.Value,
                Name: propertyNode.Value.Name.ToPascalCase(),
                Type: propertyType,
                Value: propertyValue
            ));
        }

        List<BuiltConstant> constants = [];
        foreach (KeyValuePair<string, ConstantNode> constantNode in definitionNode.Constants)
        {
            BuiltDataType constantType = Compiler.GetCompiledDataType(constantNode.Value.BuiltType!);
            BuiltDataValue constantValue = this.GetCompiledDataValue(constantNode.Value.BuiltType!, constantNode.Value.Value);

            constants.Add(new(
                Node: constantNode.Value,
                Name: constantNode.Value.Name.ToPascalCase(),
                Type: constantType,
                Value: constantValue
            ));
        }

        List<BuiltFunction> functions = [];
        functions.AddRange(BuildSerialiseFunctions(context, definitionNode));
        functions.AddRange(BuildDeserialiseFunctions(context, definitionNode));

        BuiltDefinition definition = new(
            Node: definitionNode,
            Name: GetCompiledClassName(definitionNode),
            Properties: properties,
            Constants: constants,
            Functions: functions);

        context.GetOrAddFile(Compiler, GetBuiltFileName(context, definitionNode)).Definitions.Add(definition);
    }

    public BuiltDataValue GetCompiledDefaultValueForDataType(IDataType dataType)
    {
        return dataType switch
        {
            IOptionalDataType => new NoDataValue(),
            AnyType or ObjectType => new SomeDataValue("{} as any"),
            ListType or SetType => new SomeDataValue("[]"),
            MapType => new SomeDataValue("{}"),
            StringType => new SomeDataValue("''"),
            _ => new NoDataValue()
        };
    }

    public BuiltDataValue GetCompiledDesiredDataValue(IDataValue dataValue)
    {
        switch (dataValue)
        {
            case BooleanValue booleanValue:
                return new SomeDataValue(booleanValue.Value ? "true" : "false");
            case DateValue dateValue:
                return new SomeDataValue($"'" + dateValue.Value.ToString("O") + "'");
            case FloatValue floatValue:
                return new SomeDataValue(floatValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            case IntegerValue integerValue:
                return new SomeDataValue(integerValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            case Integer64Value integer64Value:
                return new SomeDataValue($"{integer64Value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}n");
            case ListValue listValue:
                System.Text.StringBuilder sb = new();
                sb.Append('[');
                for (int itemIdx = 0; itemIdx < listValue.Values.Count; itemIdx++)
                {
                    IDataValue itemValue = listValue.Values[itemIdx];
                    sb.Append(GetCompiledDesiredDataValue(itemValue));
                    if (itemIdx < listValue.Values.Count - 1)
                        sb.Append(", ");
                }
                sb.Append(']');
                return new SomeDataValue(sb.ToString());
            case EnumValue enumValue:
                // Ensure enum references use simple identifier (no namespaces)
                string enumPrefix = Compiler.GetCompiledDataType(enumValue.Type).Name;
                int lastDotIdx = enumPrefix.LastIndexOf('.');
                if (lastDotIdx >= 0 && lastDotIdx < enumPrefix.Length - 1)
                    enumPrefix = enumPrefix[(lastDotIdx + 1)..];
                string value = string.Join(" | ", enumValue.Values.Select(x => $"{enumPrefix}.{x}"));
                return new SomeDataValue(value);
            case MapValue:
                throw new NotImplementedException();
            case NullValue:
                return new SomeDataValue("null");
            case ObjectValue:
                throw new NotImplementedException();
            case StringValue stringValue:
                return new SomeDataValue($"'" + stringValue.Value.Replace("'", "\\'") + "'");
            case TimeValue timeValue:
                return new SomeDataValue(timeValue.Value.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            case UuidValue uuidValue:
                return new SomeDataValue($"'{uuidValue.Value}'");
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

