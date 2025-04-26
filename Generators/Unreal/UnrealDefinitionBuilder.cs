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
        string? filePrefix = context.FileNode.FindCompilerOptions<UnrealFileOptionsNode>()?.Prefix;
        string fileName = filePrefix + StringExtensions.FilePathToPascalCase(context.FileNode.FileName) + ".h";
        
        return Path.Combine(
            StringExtensions.FilePathToPascalCase(context.FileNode.Directory) ?? string.Empty, 
            fileName);
    }

    public string GetBuiltSourceFileName(BuildContext context, DefinitionNode definitionNode)
    {
        // One file for all definitions.
        string? filePrefix = context.FileNode.FindCompilerOptions<UnrealFileOptionsNode>()?.Prefix;
        string fileName = filePrefix + StringExtensions.FilePathToPascalCase(context.FileNode.FileName) + ".cpp";
        
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
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public IEnumerable<BuiltFunction> BuildSerialiseFunctions(BuildContext context, DefinitionNode definitionNode)
    {
        // This implementation could easily be templated for every definition but that 
        // requires dependencies for the project, making it less portable.
        
        StringBuilder mainSerialiseFunc = new();
        mainSerialiseFunc
            .AppendLine("TRACE_CPUPROFILER_EVENT_SCOPE(Catalyst::ToJsonBytes)")
            .AppendLine()
            .AppendLine("TSharedPtr<FJsonObject> JsonObject = FJsonObjectConverter::UStructToJsonObject(Object);")
            .AppendLine("if (!JsonObject)")
            .AppendLine("{")
            .AppendLine($"    UE_LOG(LogSerialization, Error, TEXT(\"Could not serialise object '{GetCompiledClassName(definitionNode)}' into a JSON object\"));")
            .AppendLine("    return {};")
            .AppendLine("}")
            .AppendLine()
            .AppendLine("TArray<uint8> Buffer;")
            .AppendLine("FMemoryWriter MemoryWriter(Buffer);")
            .AppendLine("TSharedRef<TJsonWriter<UTF8CHAR>> JsonWriter = TJsonWriterFactory<UTF8CHAR>::Create(&MemoryWriter);")
            .AppendLine("if (!FJsonSerializer::Serialize(JsonObject.ToSharedRef(), JsonWriter))")
            .AppendLine("{")
            .AppendLine($"    UE_LOG(LogSerialization, Error, TEXT(\"Could not serialise JSON object '{GetCompiledClassName(definitionNode)}' into bytes\"));")
            .AppendLine("    return {};")
            .AppendLine("}")
            .AppendLine()
            .Append("return Buffer;");
        
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
        // This implementation could easily be templated for every definition but that 
        // requires dependencies for the project, making it less portable.
        
        StringBuilder sb = new();
        sb
            .AppendLine("TRACE_CPUPROFILER_EVENT_SCOPE(Catalyst::FromJsonBytes)")
            .AppendLine()
            .AppendLine("FMemoryReader MemoryReader(Bytes);")
            .AppendLine()
            .AppendLine("TSharedPtr<FJsonObject> JsonObject;")
            .AppendLine("TSharedRef<TJsonReader<UTF8CHAR>> JsonReader = TJsonReaderFactory<UTF8CHAR>::Create(&MemoryReader);")
            .AppendLine("if (!FJsonSerializer::Deserialize(JsonReader, JsonObject) || !JsonObject)")
            .AppendLine("{")
            .AppendLine($"    UE_LOG(LogSerialization, Error, TEXT(\"Could not deserialise the given bytes for '{GetCompiledClassName(definitionNode)}' into a JSON object\"));")
            .AppendLine("    return {};")
            .AppendLine("}")
            .AppendLine()
            .AppendLine($"{GetCompiledClassName(definitionNode)} DeserialisedObject;")
            .AppendLine("FText FailReason;")
            .AppendLine()
            .AppendLine("bool bConvertedToStruct;")
            .AppendLine("{")
            .AppendLine("    FGCScopeGuard LockGC;")
            .AppendLine($"    bConvertedToStruct = FJsonObjectConverter::JsonObjectToUStruct<{GetCompiledClassName(definitionNode)}>(")
            .AppendLine("        JsonObject.ToSharedRef(),")
            .AppendLine("        &DeserialisedObject,")
            .AppendLine("        /* CheckFlags */ 0,")
            .AppendLine("        /* SkipFlags */ 0,")
            .AppendLine("        /* bStrictMode */ false,")
            .AppendLine("        OUT &FailReason);")
            .AppendLine("}")
            .AppendLine()
            .AppendLine("if (!bConvertedToStruct)")
            .AppendLine("{")
            .AppendLine($"    UE_LOG(LogSerialization, Error, TEXT(\"Could not deserialise the JSON object into an '{GetCompiledClassName(definitionNode)}' object. Reason: %s\"), *FailReason.ToString());")
            .AppendLine("    return {};")
            .AppendLine("}")
            .AppendLine()
            .Append("return DeserialisedObject;");

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
            Functions: headerFunctions);
        
        BuiltFile headerFile = context.GetOrAddFile(Compiler, GetBuiltFileName(context, definitionNode), FileFlags.Header);
        headerFile.Definitions.Add(headerDefinition);
    }

    void BuildSource(BuildContext context, DefinitionNode definitionNode)
    {
        List<BuiltFunction> functions = [];
        functions.AddRange(BuildSerialiseFunctions(context, definitionNode));
        functions.AddRange(BuildDeserialiseFunctions(context, definitionNode));
        
        BuiltDefinition sourceDefinition = new(
            Node: definitionNode,
            Name: GetCompiledClassName(definitionNode),
            Properties: [],
            Functions: functions);
        
        BuiltFile sourceFile = context.GetOrAddFile(Compiler, GetBuiltSourceFileName(context, definitionNode));
        sourceFile.Definitions.Add(sourceDefinition);
        sourceFile.Includes.AddRange([
            new("JsonObjectConverter"), 
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