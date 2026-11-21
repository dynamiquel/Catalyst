using System.Globalization;
using System.Text;
using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;

namespace Catalyst.Generators.Python;

public class PythonDefinitionBuilder : IDefinitionBuilder<PythonCompiler>
{
    public string Name => Builder.Default;
    public required PythonCompiler Compiler { get; init; }

    public string GetBuiltFileName(BuildContext context, DefinitionNode definitionNode)
    {
        return Compiler.GetModuleName(context.FileNode);
    }

    public void Build(BuildContext context, DefinitionNode definitionNode)
    {
        List<BuiltProperty> properties = BuildProperties(definitionNode);

        List<BuiltConstant> constants = [];
        foreach (KeyValuePair<string, ConstantNode> constantNode in definitionNode.Constants)
        {
            BuiltDataType constantType = Compiler.GetCompiledDataType(constantNode.Value.BuiltType!);
            BuiltDataValue constantValue = this.GetCompiledDataValue(constantNode.Value.BuiltType!, constantNode.Value.Value);

            constants.Add(new(
                Node: constantNode.Value,
                Name: Compiler.GetCompiledPropertyName(constantNode.Value.Name),
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
            Functions: functions
        );

        context.GetOrAddFile(Compiler, GetBuiltFileName(context, definitionNode)).Definitions.Add(definition);
    }

    public BuiltDataValue GetCompiledDefaultValueForDataType(IDataType dataType)
    {
        return dataType switch
        {
            IOptionalDataType => new SomeDataValue("None"),
            StringType => new SomeDataValue("\"\""),
            ListType => new SomeDataValue("dataclasses.field(default_factory=list)"),
            SetType => new SomeDataValue("dataclasses.field(default_factory=set)"),
            MapType => new SomeDataValue("dataclasses.field(default_factory=dict)"),
            _ => new NoDataValue()
        };
    }

    public BuiltDataValue GetCompiledDesiredDataValue(IDataValue dataValue)
    {
        switch (dataValue)
        {
            case BooleanValue booleanValue:
                return new SomeDataValue(booleanValue.Value ? "True" : "False");
            case DateValue dateValue:
                return new SomeDataValue(PythonCompiler.ToPythonDatetimeLiteral(dateValue.Value));
            case FloatValue floatValue:
                return new SomeDataValue(PythonCompiler.ToPythonFloatLiteral(floatValue.Value));
            case IntegerValue integerValue:
                return new SomeDataValue(integerValue.Value.ToString(CultureInfo.InvariantCulture));
            case Integer64Value integer64Value:
                return new SomeDataValue(integer64Value.Value.ToString(CultureInfo.InvariantCulture));
            case ListValue listValue:
                StringBuilder sb = new();
                sb.Append('[');
                for (int itemIdx = 0; itemIdx < listValue.Values.Count; itemIdx++)
                {
                    IDataValue itemValue = listValue.Values[itemIdx];
                    sb.Append(GetCompiledDesiredDataValue(itemValue).Value);
                    if (itemIdx < listValue.Values.Count - 1)
                        sb.Append(", ");
                }
                sb.Append(']');
                return new SomeDataValue(sb.ToString());
            case EnumValue enumValue:
                string enumName = Compiler.GetCompiledDataType(enumValue.Type).Name;
                string value = string.Join(
                    " | ",
                    enumValue.Values.Select(x => $"{enumName}.{Compiler.GetCompiledEnumValueName(x)}"));
                return new SomeDataValue(value);
            case MapValue:
                throw new NotImplementedException();
            case NullValue:
                return new SomeDataValue("None");
            case ObjectValue:
                throw new NotImplementedException();
            case StringValue stringValue:
                return new SomeDataValue(PythonCompiler.ToPythonStringLiteral(stringValue.Value));
            case TimeValue timeValue:
                return new SomeDataValue(PythonCompiler.ToPythonFloatLiteral(timeValue.Value.TotalSeconds));
            case UuidValue uuidValue:
                return new SomeDataValue($"uuid.UUID({PythonCompiler.ToPythonStringLiteral(uuidValue.Value.ToString())})");
            case UrlValue urlValue:
                return new SomeDataValue(PythonCompiler.ToPythonStringLiteral(urlValue.Value.OriginalString));
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
        List<BuiltProperty> properties = BuildProperties(definitionNode);
        return [BuildToDictFunction(properties), BuildToJsonFunction()];
    }

    public IEnumerable<BuiltFunction> BuildDeserialiseFunctions(BuildContext context, DefinitionNode definitionNode)
    {
        List<BuiltProperty> properties = BuildProperties(definitionNode);
        return [BuildFromDictFunction(definitionNode, properties), BuildFromJsonFunction(definitionNode)];
    }

    private List<BuiltProperty> BuildProperties(DefinitionNode definitionNode)
    {
        List<BuiltProperty> properties = [];
        foreach (KeyValuePair<string, PropertyNode> propertyNode in definitionNode.Properties)
        {
            BuiltDataType propertyType = Compiler.GetCompiledDataType(propertyNode.Value.BuiltType!);
            BuiltDataValue propertyValue = this.GetCompiledDataValue(propertyNode.Value.BuiltType!, propertyNode.Value.Value);

            properties.Add(new(
                Node: propertyNode.Value,
                Name: Compiler.GetCompiledPropertyName(propertyNode.Value.Name),
                Type: propertyType,
                Value: propertyValue
            ));
        }

        return properties;
    }

    private BuiltFunction BuildToDictFunction(List<BuiltProperty> properties)
    {
        StringBuilder body = new();
        body.AppendLine("data: dict[str, typing.Any] = {}");

        foreach (BuiltProperty property in properties)
        {
            string key = PythonCompiler.ToPythonStringLiteral(property.Node.Name);
            string value = Compiler.GetEncodedExpression(property.Node.BuiltType!, $"self.{property.Name}");
            body.AppendLine($"data[{key}] = {value}");
        }

        body.Append("return data");

        return new BuiltFunction(
            Name: "to_dict",
            ReturnType: "dict[str, typing.Any]",
            Flags: FunctionFlags.Const,
            Parameters: ["self"],
            BodyInit: body.ToString());
    }

    private BuiltFunction BuildToJsonFunction()
    {
        return new BuiltFunction(
            Name: "to_json",
            ReturnType: "str",
            Flags: FunctionFlags.Const,
            Parameters: ["self"],
            BodyInit: "return json.dumps(self.to_dict())");
    }

    private BuiltFunction BuildFromDictFunction(DefinitionNode definitionNode, List<BuiltProperty> properties)
    {
        StringBuilder body = new();
        body.AppendLine("values: dict[str, typing.Any] = {}");

        foreach (BuiltProperty property in properties)
        {
            string key = PythonCompiler.ToPythonStringLiteral(property.Node.Name);
            string name = PythonCompiler.ToPythonStringLiteral(property.Name);
            string value = Compiler.GetDecodedExpression(property.Node.BuiltType!, $"data[{key}]");

            if (property.Value is NoDataValue)
            {
                body.AppendLine($"values[{name}] = {value}");
                continue;
            }

            // Defaulted properties are left out of the constructor so the dataclass default applies.
            body.AppendLine($"if {key} in data:");
            body.AppendLine($"    values[{name}] = {value}");
        }

        body.Append("return cls(**values)");

        return new BuiltFunction(
            Name: "from_dict",
            ReturnType: GetCompiledClassName(definitionNode),
            Flags: FunctionFlags.Static,
            Parameters: ["cls", "data: dict[str, typing.Any]"],
            BodyInit: body.ToString());
    }

    private BuiltFunction BuildFromJsonFunction(DefinitionNode definitionNode)
    {
        return new BuiltFunction(
            Name: "from_json",
            ReturnType: GetCompiledClassName(definitionNode),
            Flags: FunctionFlags.Static,
            Parameters: ["cls", "text: str"],
            BodyInit: "return cls.from_dict(json.loads(text))");
    }
}
