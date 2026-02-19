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
            StringExtensions.FilePathToPascalCase(context.FileNode.Directory) ?? string.Empty, 
            fileName);
    }

    public string GetBuiltSourceFileName(BuildContext context, DefinitionNode definitionNode)
    {
        // One file for all definitions.
        string fileName = Compiler.GetFileName(context.FileNode) + ".cpp";
        
        return Path.Combine(
            StringExtensions.FilePathToPascalCase(context.FileNode.Directory) ?? string.Empty, 
            fileName);
    }

    public void Build(BuildContext context, DefinitionNode definitionNode)
    {
        BuildHeader(context, definitionNode);
        BuildSource(context, definitionNode);
    }

    public BuiltPropertyValue GetCompiledDefaultValueForPropertyType(IPropertyType propertyType)
    {
        return propertyType switch
        {
            IOptionalPropertyType => new NoPropertyValue(),
            BooleanType => new SomePropertyValue("false"),
            IntegerType or FloatType => new SomePropertyValue("0"),
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
                // Unreal doesn't have a DateTime initialiser for ISO, so need to convert to unix.
                long unixMs = new DateTimeOffset(dateValue.Value).ToUnixTimeMilliseconds();
                double unixS = unixMs / 1000d;
                return new SomePropertyValue($"FDateTime::FromUnixTimestamp({unixS})");
            case FloatValue floatValue:
                return new SomePropertyValue(floatValue.Value.ToString(CultureInfo.InvariantCulture));
            case IntegerValue integerValue:
                return new SomePropertyValue(integerValue.Value.ToString(CultureInfo.InvariantCulture));
            case EnumValue enumValue:
                string enumPrefix = Compiler.GetCompiledPropertyType(enumValue.Type).Name;
                string value = string.Join(" | ", enumValue.Values.Select(x => $"{enumPrefix}.{x}"));
                return new SomePropertyValue(value);
            case ListValue listValue:
                StringBuilder sb = new();
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
            case MapValue mapValue:
                throw new NotImplementedException();
            case NullValue nullValue:
                return new SomePropertyValue("null");
            case ObjectValue objectValue:
                throw new NotImplementedException();
            case StringValue stringValue:
                return new SomePropertyValue($"\"{stringValue.Value}\"");
            case TimeValue timeValue:
                return new SomePropertyValue($"FTimespan::FromSeconds({timeValue.Value.TotalSeconds.ToString(CultureInfo.InvariantCulture)})");
            case UuidValue uuidValue:
                return new SomePropertyValue($"FGuid(\"{uuidValue.Value}\")");
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
            BuiltPropertyType propertyType = Compiler.GetCompiledPropertyType(propertyNode.Value.BuiltType!);
            BuiltPropertyValue propertyValue = this.GetCompiledPropertyValue(propertyNode.Value.BuiltType!, propertyNode.Value.Value);
            
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
            BuiltPropertyType constantType = Compiler.GetCompiledPropertyType(constantNode.Value.BuiltType!);
            BuiltPropertyValue constantValue = this.GetCompiledPropertyValue(constantNode.Value.BuiltType!, constantNode.Value.Value);

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
            BuiltPropertyType constantType = Compiler.GetCompiledPropertyType(constantNode.Value.BuiltType!);
            BuiltPropertyValue constantValue = this.GetCompiledPropertyValue(constantNode.Value.BuiltType!, constantNode.Value.Value);

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