using System.Globalization;
using System.Text;
using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;

namespace Catalyst.Generators.Unreal;

public class UnrealDefinitionBuilder : IDefinitionBuilder<UnrealCompiler>
{
    public string Name => "default";
    public required UnrealCompiler Compiler { get; init; }
    
    public string GetBuiltFileName(BuildContext context, DefinitionNode definitionNode)
    {
        // One file for all definitions.
        string fileName = Compiler.GetFileName(context.FileNode) + ".h";
        
        return Path.Combine(
            Helpers.FilePathToPascalCase(context.FileNode.Directory) ?? string.Empty, 
            fileName);
    }

    public string GetBuiltSourceFileName(BuildContext context, DefinitionNode definitionNode)
    {
        // One file for all definitions.
        string fileName = Compiler.GetFileName(context.FileNode) + ".cpp";
        
        return Path.Combine(
            Helpers.FilePathToPascalCase(context.FileNode.Directory) ?? string.Empty, 
            fileName);
    }

    public void Build(BuildContext context, DefinitionNode definitionNode)
    {
        BuildHeader(context, definitionNode);
        BuildSource(context, definitionNode);
    }

    public BuiltDataValue GetCompiledDefaultValueForDataType(IDataType dataType)
    {
        return dataType switch
        {
            IOptionalDataType => new NoDataValue(),
            BooleanType => new SomeDataValue("false"),
            IntegerType or Integer64Type or FloatType => new SomeDataValue("0"),
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
                // Unreal doesn't have a DateTime initialiser for ISO, so need to convert to unix.
                long unixMs = new DateTimeOffset(dateValue.Value).ToUnixTimeMilliseconds();
                double unixS = unixMs / 1000d;
                return new SomeDataValue($"FDateTime::FromUnixTimestamp({unixS})");
            case FloatValue floatValue:
                return new SomeDataValue(floatValue.Value.ToString(CultureInfo.InvariantCulture));
            case IntegerValue integerValue:
                return new SomeDataValue(integerValue.Value.ToString(CultureInfo.InvariantCulture));
            case Integer64Value integer64Value:
                return new SomeDataValue(integer64Value.Value.ToString(CultureInfo.InvariantCulture));
            case EnumValue enumValue:
                string enumPrefix = Compiler.GetCompiledDataType(enumValue.Type).Name;
                string value = string.Join(" | ", enumValue.Values.Select(x => $"{enumPrefix}.{x}"));
                return new SomeDataValue(value);
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
            case MapValue mapValue:
                throw new NotImplementedException();
            case NullValue nullValue:
                return new SomeDataValue("null");
            case ObjectValue objectValue:
                throw new NotImplementedException();
            case StringValue stringValue:
                return new SomeDataValue($"\"{stringValue.Value}\"");
            case TimeValue timeValue:
                return new SomeDataValue($"FTimespan::FromSeconds({timeValue.Value.TotalSeconds.ToString(CultureInfo.InvariantCulture)})");
            case UuidValue uuidValue:
                return new SomeDataValue($"FGuid(\"{uuidValue.Value}\")");
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public IEnumerable<BuiltFunction> BuildSerialiseFunctions(BuildContext context, DefinitionNode definitionNode)
    {
        StringBuilder mainSerialiseFunc = new();
        mainSerialiseFunc
            .AppendLine("TSharedPtr<FJsonObject> JsonObject = Catalyst::Json::StructToJsonObject(Object);")
            .Append("return JsonObject ? Catalyst::Json::JsonObjectToBytes(JsonObject.ToSharedRef()) : TArray<uint8>();");
        
        return [
            new BuiltFunction(
                Name: "ToBytes",
                ReturnType: "TArray<uint8>",
                Flags: FunctionFlags.Static,
                Parameters: [$"const {GetCompiledClassName(definitionNode)}& Object"],
                BodyInit: mainSerialiseFunc.ToString()),
            new BuiltFunction(
                Name: "ToBytes",
                ReturnType: "TArray<uint8>",
                Flags: FunctionFlags.Const,
                Parameters: [],
                BodyInit: "return ToBytes(*this);"
            )
        ];
    }

    public IEnumerable<BuiltFunction> BuildDeserialiseFunctions(BuildContext context, DefinitionNode definitionNode)
    {
        StringBuilder sb = new();
        sb
            .AppendLine("TSharedPtr<FJsonObject> JsonObject = Catalyst::Json::BytesToJsonObject(Bytes);")
            .Append($"return JsonObject ? Catalyst::Json::JsonObjectToStruct<{GetCompiledClassName(definitionNode)}>(JsonObject.ToSharedRef()) : TOptional<{GetCompiledClassName(definitionNode)}>();");

        return [
            new BuiltFunction(
                Name: "FromBytes",
                ReturnType: $"TOptional<{GetCompiledClassName(definitionNode)}>",
                Flags: FunctionFlags.Static,
                Parameters: ["const TArray<uint8>& Bytes"],
                BodyInit: sb.ToString())
        ];
    }

    void BuildHeader(BuildContext context, DefinitionNode definitionNode)
    {
        // Properties will be defined in header.
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

        List<BuiltFunction> fullFunctions = [];
        fullFunctions.AddRange(BuildSerialiseFunctions(context, definitionNode));
        fullFunctions.AddRange(BuildDeserialiseFunctions(context, definitionNode));
        
        List<BuiltFunction> headerFunctions = [];
        foreach (BuiltFunction function in fullFunctions)
        {
            // Remove the body from the function.
            function.Body = null;
            headerFunctions.Add(function);
        }
        
        BuiltDefinition headerDefinition = new(
            Node: definitionNode,
            Name: GetCompiledClassName(definitionNode),
            Properties: properties,
            Constants: constants,
            Functions: headerFunctions);
        
        BuiltFile headerFile = context.GetOrAddFile(Compiler, GetBuiltFileName(context, definitionNode), FileFlags.Header);
        headerFile.Definitions.Add(headerDefinition);
    }

    void BuildSource(BuildContext context, DefinitionNode definitionNode)
    {
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
        
        BuiltDefinition sourceDefinition = new(
            Node: definitionNode,
            Name: GetCompiledClassName(definitionNode),
            Properties: [],
            Constants: constants,
            Functions: functions);
        
        BuiltFile sourceFile = context.GetOrAddFile(Compiler, GetBuiltSourceFileName(context, definitionNode));
        sourceFile.Definitions.Add(sourceDefinition);
        sourceFile.Includes.AddRange([
            new("CatalystJson"), 
            new("Templates/SharedPointer")
        ]);
    }

    public string GetCompiledClassName(DefinitionNode definitionNode)
    {
        var compilerOptions = definitionNode.FindCompilerOptions<UnrealDefinitionOptionsNode>()!;
        string? prefix = compilerOptions.Prefix ?? Compiler.GetPrefixFromNamespace(definitionNode.GetParentChecked<FileNode>().Namespace);

        return $"F{prefix}{definitionNode.Name.ToPascalCase()}";
    }
}