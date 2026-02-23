using System.Globalization;
using System.Text;
using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;

namespace Catalyst.Generators.CSharp;

public class CSharpDefinitionBuilder : IDefinitionBuilder<CSharpCompiler>
{
    public string Name => Builder.Default;
    public required CSharpCompiler Compiler { get; init; }
    
    public string GetBuiltFileName(BuildContext context, DefinitionNode definitionNode)
    {
        // One file for all definitions.
        return Helpers.FilePathToPascalCase(context.FileNode.FilePath) + ".cs";
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
        
        BuiltFile file = context.GetOrAddFile(Compiler, GetBuiltFileName(context, definitionNode));
        file.Definitions.Add(definition);
        file.Includes.AddRange([
            new("System.Text.Json"),
            new("System.Text.Json.Serialization")
        ]);
    }

    public BuiltDataValue GetCompiledDefaultValueForDataType(IDataType dataType)
    {
        return dataType switch
        {
            IOptionalDataType => new NoDataValue(),
            AnyType or ObjectType => new SomeDataValue("new()"),
            ListType or MapType or SetType => new SomeDataValue("[]"),
            StringType => new SomeDataValue("string.Empty"),
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
                return new SomeDataValue($"DateTime.Parse(\"{dateValue.Value:O}\")");
            case FloatValue floatValue:
                return new SomeDataValue(floatValue.Value.ToString(CultureInfo.InvariantCulture));
            case IntegerValue integerValue:
                return new SomeDataValue(integerValue.Value.ToString(CultureInfo.InvariantCulture));
            case Integer64Value integer64Value:
                return new SomeDataValue($"{integer64Value.Value.ToString(CultureInfo.InvariantCulture)}L");
            case ListValue listValue:
                StringBuilder sb = new();
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
                string enumPrefix = Compiler.GetCompiledDataType(enumValue.Type).Name;
                string value = string.Join(" | ", enumValue.Values.Select(x => $"{enumPrefix}.{x}"));
                return new SomeDataValue(value);
            case MapValue mapValue:
                throw new NotImplementedException();
            case NullValue nullValue:
                return new SomeDataValue("null");
            case ObjectValue objectValue:
                throw new NotImplementedException();
            case StringValue stringValue:
                return new SomeDataValue($"\"{stringValue.Value}\"");
            case TimeValue timeValue:
                return new SomeDataValue($"TimeSpan.FromSeconds({timeValue.Value.TotalSeconds.ToString(CultureInfo.InvariantCulture)})");
            case UuidValue uuidValue:
                return new SomeDataValue($"Guid.Parse(\"{uuidValue.Value}\")");
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
                Name: "ToBytes",
                ReturnType: "byte[]",
                Flags: FunctionFlags.Const,
                Parameters: [],
                BodyInit: $"return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this, {GetJsonContextName(context, definitionNode)});")
        ];
    }

    public IEnumerable<BuiltFunction> BuildDeserialiseFunctions(BuildContext context, DefinitionNode definitionNode)
    {
        return [
            new BuiltFunction(
                Name: "FromBytes",
                ReturnType: $"{definitionNode.Name.ToPascalCase()}?",
                Flags: FunctionFlags.Static,
                Parameters: ["ReadOnlySpan<byte> bytes"],
                BodyInit: $"return System.Text.Json.JsonSerializer.Deserialize<{definitionNode.Name.ToPascalCase()}>(bytes, {GetJsonContextName(context, definitionNode)});")
        ];
    }

    private string GetJsonContextName(BuildContext context, DefinitionNode definitionNode) =>
        $"{context.FileNode.FileName.ToPascalCase()}JsonContext.Default.{definitionNode.Name.ToPascalCase()}";
}