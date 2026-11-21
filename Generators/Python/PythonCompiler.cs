using System.Globalization;
using System.Text;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;

namespace Catalyst.Generators.Python;

public class PythonCompiler : Compiler
{
    public const string RuntimeFileName = "_catalyst.py";
    private const int IndentWidth = 4;

    private static readonly HashSet<string> PythonKeywords = new(StringComparer.Ordinal)
    {
        "False", "None", "True", "and", "as", "assert", "async", "await", "break", "class",
        "continue", "def", "del", "elif", "else", "except", "finally", "for", "from", "global",
        "if", "import", "in", "is", "lambda", "nonlocal", "not", "or", "pass", "raise",
        "return", "try", "while", "with", "yield"
    };

    private readonly HashSet<string> _emittedSupportFiles = new(StringComparer.Ordinal);

    [Flags]
    private enum UsageKind
    {
        None = 0,
        Encode = 1,
        Decode = 2
    }

    private sealed class FileUsage
    {
        public bool UsesDatetime { get; set; }
        public bool UsesUuid { get; set; }
        public bool UsesTyping { get; set; }
        public bool EncodesDatetime { get; set; }
        public bool DecodesDatetime { get; set; }
        public Dictionary<string, HashSet<string>> UserImports { get; } = new(StringComparer.Ordinal);

        public void AddUserImport(string module, string symbol)
        {
            if (!UserImports.TryGetValue(module, out HashSet<string>? symbols))
            {
                symbols = new HashSet<string>(StringComparer.Ordinal);
                UserImports[module] = symbols;
            }

            symbols.Add(symbol);
        }
    }

    public PythonCompiler(CompilerOptions options) : base(options)
    {
    }

    public override string Name => Python.Name;

    public override IEnumerable<CompiledFile> CompileFiles(BuiltFile file)
    {
        yield return Compile(file);

        // Support files are shared between modules, so only emit each one once.
        foreach (CompiledFile supportFile in BuildSupportFiles(file.Name))
            yield return supportFile;
    }

    public override CompiledFile Compile(BuiltFile file)
    {
        StringBuilder sb = new();

        AppendFileHeader(sb);
        sb.AppendLine("from __future__ import annotations");
        sb.AppendLine();

        FileUsage usage = new();
        CollectUsage(file, usage);

        AppendImports(sb, file, usage);
        sb.AppendLine();
        sb.AppendLine();

        foreach (BuiltEnum builtEnum in file.Enums)
        {
            AppendEnum(sb, builtEnum);
            sb.AppendLine();
            sb.AppendLine();
        }

        foreach (BuiltDefinition definition in file.Definitions)
        {
            AppendDefinition(sb, definition);
            sb.AppendLine();
            sb.AppendLine();
        }

        if (ClientServiceBuilder is not null)
            foreach (BuiltService service in file.Services)
                ClientServiceBuilder.Compile(file, service, sb);

        return new CompiledFile(file.Name, sb.ToString().TrimEnd() + "\n");
    }

    public override BuiltInclude? GetCompiledIncludeForType(BuiltFile file, IDataType dataType)
    {
        // Imports are collected by Compile from the types that are actually used.
        return null;
    }

    public override BuiltDataType GetCompiledDataType(IDataType dataType)
    {
        BuiltDataType compiledDataType;
        switch (dataType)
        {
            case AnyType:
                compiledDataType = new("typing.Any");
                break;
            case BooleanType:
                compiledDataType = new("bool");
                break;
            case DateType:
                compiledDataType = new("datetime.datetime");
                break;
            case FloatType:
                compiledDataType = new("float");
                break;
            case IntegerType:
                compiledDataType = new("int");
                break;
            case Integer64Type:
                compiledDataType = new("int");
                break;
            case ListType listType:
                BuiltDataType innerListDataType = GetCompiledDataType(listType.InnerType);
                compiledDataType = new($"list[{innerListDataType.Name}]");
                break;
            case MapType mapType:
                BuiltDataType innerKeyDataType = GetCompiledDataType(mapType.InnerTypeA);
                BuiltDataType innerValueDataType = GetCompiledDataType(mapType.InnerTypeB);
                compiledDataType = new($"dict[{innerKeyDataType.Name}, {innerValueDataType.Name}]");
                break;
            case SetType setType:
                BuiltDataType innerSetDataType = GetCompiledDataType(setType.InnerType);
                compiledDataType = new($"set[{innerSetDataType.Name}]");
                break;
            case StringType:
                compiledDataType = new("str");
                break;
            case TimeType:
                compiledDataType = new("float");
                break;
            case UuidType:
                compiledDataType = new("uuid.UUID");
                break;
            case UrlType:
                compiledDataType = new("str");
                break;
            case IUserDataType userType:
                compiledDataType = new(GetSimpleUserTypeName(userType));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(dataType));
        }

        // typing.Any already includes None, so it doesn't need the union.
        if (dataType is IOptionalDataType and not AnyType)
            compiledDataType = new($"{compiledDataType.Name} | None");

        return compiledDataType;
    }

    public override string? GetCompiledNamespace(string? namespaceName)
    {
        // Python modules are file-scoped, so namespaces don't map to anything.
        return null;
    }

    public override string GetCompiledClassName(string className)
    {
        return className.ToPascalCase();
    }

    public string GetModuleName(FileNode fileNode)
    {
        return GetModulePath(fileNode) + ".py";
    }

    public string GetClientModuleName(FileNode fileNode)
    {
        string modulePath = GetModulePath(fileNode);
        int lastSeparatorIdx = modulePath.LastIndexOf('/');
        if (lastSeparatorIdx < 0)
            return EnsureModuleName(modulePath + "_client") + ".py";

        return modulePath[..(lastSeparatorIdx + 1)] + EnsureModuleName(modulePath[(lastSeparatorIdx + 1)..] + "_client") + ".py";
    }

    public string GetCompiledPropertyName(string propertyName)
    {
        string snakeCaseName = GetSnakeCaseName(propertyName);
        if (snakeCaseName is "to_dict" or "to_json" or "from_dict" or "from_json" or "__init__")
            snakeCaseName += "_";
        return EnsureIdentifier(snakeCaseName);
    }

    public string GetCompiledMethodName(string endpointName)
    {
        string snakeCaseName = GetSnakeCaseName(endpointName);
        if (snakeCaseName == "__init__")
            snakeCaseName += "_";
        return EnsureIdentifier(snakeCaseName);
    }

    public string GetCompiledEnumValueName(string enumValueLabel)
    {
        return EnsureIdentifier(enumValueLabel.ToSnakeCase()!.ToUpperInvariant());
    }

    public string GetSimpleUserTypeName(IUserDataType userType)
    {
        string typeName = userType.Name;
        if (!string.IsNullOrEmpty(typeName) && typeName.EndsWith('?'))
            typeName = typeName[..^1];

        int lastDotIdx = typeName.LastIndexOf('.');
        if (lastDotIdx >= 0 && lastDotIdx < typeName.Length - 1)
            typeName = typeName[(lastDotIdx + 1)..];

        return typeName.ToPascalCase();
    }

    public string GetEncodedExpression(IDataType dataType, string expression, int depth = 1)
    {
        if (dataType is AnyType)
            return expression;

        if (dataType is IOptionalDataType)
        {
            if (IsEncodedIdentityType(dataType))
                return expression;

            return $"None if {expression} is None else {EncodeRequired(dataType, expression, depth)}";
        }

        return EncodeRequired(dataType, expression, depth);
    }

    public string GetDecodedExpression(IDataType dataType, string expression, int depth = 1)
    {
        if (dataType is AnyType)
            return expression;

        if (dataType is IOptionalDataType)
        {
            if (IsDecodedIdentityType(dataType))
                return expression;

            return $"None if {expression} is None else {DecodeRequired(dataType, expression, depth)}";
        }

        return DecodeRequired(dataType, expression, depth);
    }

    private static bool IsEncodedIdentityType(IDataType dataType)
    {
        return dataType is AnyType or BooleanType or IntegerType or Integer64Type or FloatType or StringType or UrlType or TimeType;
    }

    private static bool IsDecodedIdentityType(IDataType dataType)
    {
        return dataType is AnyType or BooleanType or IntegerType or Integer64Type or StringType or UrlType;
    }

    private static bool IsStringMapKeyType(IDataType dataType)
    {
        return dataType is AnyType or StringType or UrlType;
    }

    public bool NeedsResponseTypeCast(IDataType dataType)
    {
        return dataType is not AnyType && IsDecodedIdentityType(dataType);
    }

    public void AppendDocstring(StringBuilder sb, INodeDescription node, int indentation)
    {
        if (string.IsNullOrWhiteSpace(node.Description))
            return;

        string indent = GetIndentation(indentation);
        string[] lines = node.Description.Split('\n');
        string firstLine = EscapeDocstringLine(lines[0]);

        if (lines.Length == 1)
        {
            sb.Append(indent).Append("\"\"\"").Append(firstLine).AppendLine("\"\"\"");
            return;
        }

        sb.Append(indent).AppendLine("\"\"\"" + firstLine);
        for (int lineIdx = 1; lineIdx < lines.Length; lineIdx++)
        {
            string line = EscapeDocstringLine(lines[lineIdx]);
            if (line.Length == 0)
                sb.AppendLine();
            else
                sb.Append(indent).AppendLine(line);
        }
        sb.Append(indent).AppendLine("\"\"\"");
    }

    public static string ToPythonStringLiteral(string value)
    {
        StringBuilder sb = new(value.Length + 2);
        sb.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(character);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    public static string ToPythonFloatLiteral(double value)
    {
        string text = value.ToString("R", CultureInfo.InvariantCulture);
        bool hasDecimalPoint = text.Contains('.') || text.Contains('e') || text.Contains('E');
        return hasDecimalPoint ? text : text + ".0";
    }

    public static string ToPythonDatetimeLiteral(DateTime value)
    {
        string text = value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

        if (value.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            string fraction = value.ToString("ffffff", CultureInfo.InvariantCulture);
            text += "." + fraction;
        }

        if (value.Kind == DateTimeKind.Utc)
        {
            text += "+00:00";
        }
        else if (value.Kind == DateTimeKind.Local)
        {
            TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(value);
            text += offset < TimeSpan.Zero ? "-" : "+";
            text += offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }

        return $"datetime.datetime.fromisoformat({ToPythonStringLiteral(text)})";
    }

    private string GetModulePath(FileNode fileNode)
    {
        string[] segments = (Helpers.FilePathToSnakeCase(fileNode.FilePath) ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        return string.Join("/", segments.Select(EnsureModuleName));
    }

    private static string EnsureModuleName(string moduleName)
    {
        if (PythonKeywords.Contains(moduleName))
            return "_" + moduleName;
        return EnsureIdentifier(moduleName);
    }

    private string GetSnakeCaseName(string name)
    {
        string snakeCaseName = name.ToSnakeCase()!;
        if (PythonKeywords.Contains(snakeCaseName))
            snakeCaseName += "_";
        return snakeCaseName;
    }

    private static string EnsureIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "_";
        if (char.IsLetter(name[0]) || name[0] == '_')
            return name;
        return "_" + name;
    }

    private string EscapeDocstringLine(string line)
    {
        return line.TrimEnd().Replace("\"\"\"", "\\\"\\\"\\\"");
    }

    private static string GetIndentation(int indentation)
    {
        return new string(' ', indentation * IndentWidth);
    }

    private string EncodeRequired(IDataType dataType, string expression, int depth)
    {
        switch (dataType)
        {
            case BooleanType or IntegerType or Integer64Type or FloatType or StringType or UrlType or TimeType:
                return expression;
            case DateType:
                return $"encode_datetime({expression})";
            case UuidType:
                return $"str({expression})";
            case EnumType:
                return $"int({expression})";
            case ObjectType:
                return $"{expression}.to_dict()";
            case ListType listType:
            {
                if (IsEncodedIdentityType(listType.InnerType))
                    return $"list({expression})";

                string itemName = GetComprehensionName("item", depth);
                string itemExpression = GetEncodedExpression(listType.InnerType, itemName, depth + 1);
                return $"[{itemExpression} for {itemName} in {expression}]";
            }
            case SetType setType:
            {
                if (IsEncodedIdentityType(setType.InnerType))
                    return $"list({expression})";

                string itemName = GetComprehensionName("item", depth);
                string itemExpression = GetEncodedExpression(setType.InnerType, itemName, depth + 1);
                return $"[{itemExpression} for {itemName} in {expression}]";
            }
            case MapType mapType:
            {
                if (IsStringMapKeyType(mapType.InnerTypeA) && IsEncodedIdentityType(mapType.InnerTypeB))
                    return $"dict({expression})";

                string keyName = GetComprehensionName("key", depth);
                string valueName = GetComprehensionName("value", depth);
                string keyExpression = GetEncodedMapKeyExpression(mapType.InnerTypeA, keyName, depth + 1);
                string valueExpression = GetEncodedExpression(mapType.InnerTypeB, valueName, depth + 1);
                return $"{{{keyExpression}: {valueExpression} for {keyName}, {valueName} in {expression}.items()}}";
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(dataType));
        }
    }

    private string DecodeRequired(IDataType dataType, string expression, int depth)
    {
        switch (dataType)
        {
            case BooleanType or IntegerType or Integer64Type or StringType or UrlType:
                return expression;
            case DateType:
                return $"decode_datetime({expression})";
            case FloatType:
                return $"float({expression})";
            case TimeType:
                return $"float({expression})";
            case UuidType:
                return $"uuid.UUID({expression})";
            case EnumType enumType:
                return $"{GetSimpleUserTypeName(enumType)}({expression})";
            case ObjectType objectType:
                return $"{GetSimpleUserTypeName(objectType)}.from_dict({expression})";
            case ListType listType:
            {
                if (IsDecodedIdentityType(listType.InnerType))
                    return $"list({expression})";

                string itemName = GetComprehensionName("item", depth);
                string itemExpression = GetDecodedExpression(listType.InnerType, itemName, depth + 1);
                return $"[{itemExpression} for {itemName} in {expression}]";
            }
            case SetType setType:
            {
                if (IsDecodedIdentityType(setType.InnerType))
                    return $"set({expression})";

                string itemName = GetComprehensionName("item", depth);
                string itemExpression = GetDecodedExpression(setType.InnerType, itemName, depth + 1);
                return "{" + itemExpression + " for " + itemName + " in " + expression + "}";
            }
            case MapType mapType:
            {
                if (IsStringMapKeyType(mapType.InnerTypeA) && IsDecodedIdentityType(mapType.InnerTypeB))
                    return $"dict({expression})";

                string keyName = GetComprehensionName("key", depth);
                string valueName = GetComprehensionName("value", depth);
                string keyExpression = GetDecodedMapKeyExpression(mapType.InnerTypeA, keyName, depth + 1);
                string valueExpression = GetDecodedExpression(mapType.InnerTypeB, valueName, depth + 1);
                return "{" + keyExpression + ": " + valueExpression + " for " + keyName + ", " + valueName +
                       " in " + expression + ".items()}";
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(dataType));
        }
    }

    private string GetEncodedMapKeyExpression(IDataType dataType, string expression, int depth)
    {
        return dataType switch
        {
            StringType or UrlType or AnyType => expression,
            DateType => $"encode_datetime({expression})",
            EnumType => $"str(int({expression}))",
            BooleanType => $"(\"true\" if {expression} else \"false\")",
            _ => $"str({GetEncodedExpression(dataType, expression, depth)})"
        };
    }

    private string GetDecodedMapKeyExpression(IDataType dataType, string expression, int depth)
    {
        return dataType switch
        {
            StringType or UrlType or AnyType => expression,
            IntegerType or Integer64Type => $"int({expression})",
            FloatType or TimeType => $"float({expression})",
            BooleanType => $"{expression} == \"true\"",
            UuidType => $"uuid.UUID({expression})",
            EnumType enumType => $"{GetSimpleUserTypeName(enumType)}(int({expression}))",
            _ => GetDecodedExpression(dataType, expression, depth)
        };
    }

    private static string GetComprehensionName(string baseName, int depth)
    {
        return depth <= 1 ? baseName : baseName + depth.ToString(CultureInfo.InvariantCulture);
    }

    private void CollectUsage(BuiltFile file, FileUsage usage)
    {
        foreach (BuiltDefinition definition in file.Definitions)
        {
            foreach (BuiltConstant constant in definition.Constants)
                CollectTypeUsage(file, constant.Node.BuiltType!, usage, UsageKind.None);

            foreach (BuiltProperty property in definition.Properties)
                CollectTypeUsage(file, property.Node.BuiltType!, usage, UsageKind.Encode | UsageKind.Decode);
        }

        foreach (BuiltService service in file.Services)
        foreach (BuiltEndpoint endpoint in service.Endpoints)
        {
            CollectTypeUsage(file, endpoint.Node.BuiltRequestType!, usage, UsageKind.Encode);
            CollectTypeUsage(file, endpoint.Node.BuiltResponseType!, usage, UsageKind.Decode);

            // Primitive responses are returned straight from json.loads, so they need a cast.
            if (NeedsResponseTypeCast(endpoint.Node.BuiltResponseType!))
                usage.UsesTyping = true;
        }
    }

    private void CollectTypeUsage(BuiltFile file, IDataType dataType, FileUsage usage, UsageKind usageKind)
    {
        switch (dataType)
        {
            case DateType:
                usage.UsesDatetime = true;
                if (usageKind.HasFlag(UsageKind.Encode))
                    usage.EncodesDatetime = true;
                if (usageKind.HasFlag(UsageKind.Decode))
                    usage.DecodesDatetime = true;
                break;
            case UuidType:
                usage.UsesUuid = true;
                break;
            case AnyType:
                usage.UsesTyping = true;
                break;
            case ListType listType:
                CollectTypeUsage(file, listType.InnerType, usage, usageKind);
                break;
            case SetType setType:
                CollectTypeUsage(file, setType.InnerType, usage, usageKind);
                break;
            case MapType mapType:
                CollectTypeUsage(file, mapType.InnerTypeA, usage, usageKind);
                CollectTypeUsage(file, mapType.InnerTypeB, usage, usageKind);
                break;
            case IUserDataType userType:
            {
                string targetModule = GetModuleNameForUserType(userType);
                if (!string.Equals(targetModule, file.Name, StringComparison.Ordinal))
                    usage.AddUserImport(targetModule, GetSimpleUserTypeName(userType));
                break;
            }
        }
    }

    private string GetModuleNameForUserType(IUserDataType userType)
    {
        return userType switch
        {
            ObjectType objectType => DefinitionBuilder.GetBuiltFileName(
                new BuildContext(objectType.OwnedFile, []), objectType.OwnedDefinition),
            EnumType enumType => EnumBuilder.GetBuiltFileName(
                new BuildContext(enumType.OwnedFile, []), enumType.OwnedEnum),
            _ => string.Empty
        };
    }

    private string ComputeRelativeImport(string fromFile, string toFile)
    {
        string fromDirectory = Path.GetDirectoryName(fromFile)?.Replace('\\', '/') ?? string.Empty;
        string toPath = toFile.Replace('\\', '/');
        if (toPath.EndsWith(".py", StringComparison.Ordinal))
            toPath = toPath[..^3];

        string relativePath = Path.GetRelativePath(
                string.IsNullOrEmpty(fromDirectory) ? "." : fromDirectory,
                toPath)
            .Replace('\\', '/');

        int parentCount = 0;
        List<string> moduleParts = [];
        foreach (string part in relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
                parentCount++;
            else if (part != ".")
                moduleParts.Add(part);
        }

        return new string('.', parentCount + 1) + string.Join(".", moduleParts);
    }

    private void AppendImports(StringBuilder sb, BuiltFile file, FileUsage usage)
    {
        List<string> imports = [];
        if (file.Definitions.Count > 0)
            imports.Add("import dataclasses");
        if (usage.UsesDatetime)
            imports.Add("import datetime");
        if (file.Enums.Count > 0)
            imports.Add("import enum");
        if (file.Definitions.Count > 0 || file.Services.Count > 0)
            imports.Add("import json");
        if (usage.UsesTyping || file.Definitions.Count > 0)
            imports.Add("import typing");
        if (file.Services.Count > 0)
        {
            imports.Add("import urllib.error");
            imports.Add("import urllib.request");
        }
        if (usage.UsesUuid)
            imports.Add("import uuid");

        imports.Sort(StringComparer.Ordinal);
        foreach (string import in imports)
            sb.AppendLine(import);

        List<string> localImports = [];
        List<string> runtimeSymbols = [];
        if (file.Services.Count > 0)
            runtimeSymbols.Add("CatalystHTTPError");
        if (usage.DecodesDatetime)
            runtimeSymbols.Add("decode_datetime");
        if (usage.EncodesDatetime)
            runtimeSymbols.Add("encode_datetime");
        runtimeSymbols.Sort(StringComparer.Ordinal);

        if (runtimeSymbols.Count > 0)
        {
            localImports.Add(
                $"from {ComputeRelativeImport(file.Name, RuntimeFileName)} import {string.Join(", ", runtimeSymbols)}");
        }

        foreach (KeyValuePair<string, HashSet<string>> userImport in
                 usage.UserImports.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            string symbols = string.Join(", ", userImport.Value.OrderBy(x => x, StringComparer.Ordinal));
            localImports.Add($"from {ComputeRelativeImport(file.Name, userImport.Key)} import {symbols}");
        }

        if (imports.Count > 0 && localImports.Count > 0)
            sb.AppendLine();

        foreach (string localImport in localImports)
            sb.AppendLine(localImport);
    }

    private void AppendEnum(StringBuilder sb, BuiltEnum builtEnum)
    {
        string baseType = builtEnum.Node.Flags == true ? "enum.IntFlag" : "enum.IntEnum";
        sb.AppendLine($"class {builtEnum.Name}({baseType}):");
        AppendDocstring(sb, builtEnum.Node, 1);
        if (!string.IsNullOrWhiteSpace(builtEnum.Node.Description))
            sb.AppendLine();

        foreach (BuiltEnumValue builtEnumValue in builtEnum.Values)
        {
            string label = GetCompiledEnumValueName(builtEnumValue.Label);
            sb.AppendLine($"    {label} = {builtEnumValue.Value}");
        }
    }

    private void AppendDefinition(StringBuilder sb, BuiltDefinition definition)
    {
        sb.AppendLine("@dataclasses.dataclass");
        sb.AppendLine($"class {definition.Name}:");
        AppendDocstring(sb, definition.Node, 1);
        if (!string.IsNullOrWhiteSpace(definition.Node.Description))
            sb.AppendLine();

        if (definition.Constants.Count > 0)
        {
            foreach (BuiltConstant constant in definition.Constants)
            {
                AppendComment(sb, constant.Node.Description, 1);
                sb.AppendLine($"    {constant.Name}: typing.ClassVar[{constant.Type.Name}] = {constant.Value.Value}");
            }

            sb.AppendLine();
        }

        // Python requires fields without defaults to be declared before fields with defaults.
        IEnumerable<BuiltProperty> orderedProperties = definition.Properties
            .Where(property => property.Value is NoDataValue)
            .Concat(definition.Properties.Where(property => property.Value is not NoDataValue));

        foreach (BuiltProperty property in orderedProperties)
        {
            AppendPropertyComment(sb, (PropertyNode)property.Node, 1);

            sb.Append($"    {property.Name}: {property.Type.Name}");
            if (property.Value is SomeDataValue someValue)
                sb.Append($" = {someValue.Value}");
            sb.AppendLine();
        }

        if (definition.Properties.Count > 0)
            sb.AppendLine();

        for (var functionIdx = 0; functionIdx < definition.Functions.Count; functionIdx++)
        {
            AppendFunction(sb, definition.Functions[functionIdx]);

            if (functionIdx < definition.Functions.Count - 1)
                sb.AppendLine();
        }
    }

    private void AppendFunction(StringBuilder sb, BuiltFunction function)
    {
        if (function.Flags == FunctionFlags.Static)
            sb.AppendLine("    @classmethod");

        sb.Append($"    def {function.Name}({string.Join(", ", function.Parameters)})");
        if (!string.IsNullOrWhiteSpace(function.ReturnType))
            sb.Append($" -> {function.ReturnType}");
        sb.AppendLine(":");

        if (string.IsNullOrWhiteSpace(function.Body))
        {
            sb.AppendLine("        pass");
            return;
        }

        foreach (string line in function.Body.Split('\n'))
        {
            if (line.Length == 0)
                sb.AppendLine();
            else
                sb.AppendLine($"        {line}");
        }
    }

    private void AppendComment(StringBuilder sb, string? description, int indentation)
    {
        if (string.IsNullOrWhiteSpace(description))
            return;

        string indent = GetIndentation(indentation);
        foreach (string line in description.Split('\n'))
        {
            string trimmedLine = line.TrimEnd();
            if (trimmedLine.Length == 0)
                sb.AppendLine();
            else
                sb.Append(indent).Append("# ").AppendLine(trimmedLine);
        }
    }

    private void AppendPropertyComment(StringBuilder sb, PropertyNode propertyNode, int indentation)
    {
        List<string> lines = [];
        if (!string.IsNullOrWhiteSpace(propertyNode.Description))
            lines.AddRange(propertyNode.Description.Split('\n').Select(line => line.TrimEnd()));

        if (propertyNode.Validation is { } validation)
        {
            if (validation.Min is { } min)
                lines.Add($"Minimum value: {min} ({(validation.MinInclusive ? "inclusive" : "exclusive")}).");
            if (validation.Max is { } max)
                lines.Add($"Maximum value: {max} ({(validation.MaxInclusive ? "inclusive" : "exclusive")}).");
            if (!string.IsNullOrEmpty(validation.PatternRaw))
                lines.Add($"Pattern: {validation.PatternRaw}.");
        }

        string indent = GetIndentation(indentation);
        foreach (string line in lines)
        {
            if (line.Length == 0)
                sb.AppendLine();
            else
                sb.Append(indent).Append("# ").AppendLine(line);
        }
    }

    private IEnumerable<CompiledFile> BuildSupportFiles(string moduleName)
    {
        List<string> supportFileNames = [RuntimeFileName];
        supportFileNames.AddRange(GetPackageFileNames(moduleName));

        foreach (string supportFileName in supportFileNames)
        {
            if (!_emittedSupportFiles.Add(supportFileName))
                continue;

            string contents = supportFileName == RuntimeFileName
                ? BuildRuntimeFile()
                : BuildPackageFile();

            yield return new CompiledFile(supportFileName, contents);
        }
    }

    private static IEnumerable<string> GetPackageFileNames(string moduleName)
    {
        string? directory = Path.GetDirectoryName(moduleName);
        while (!string.IsNullOrEmpty(directory))
        {
            yield return Path.Combine(directory, "__init__.py").Replace('\\', '/');
            directory = Path.GetDirectoryName(directory);
        }

        yield return "__init__.py";
    }

    private void AppendFileHeader(StringBuilder sb)
    {
        sb.AppendLine("#");
        sb.AppendLine($"# This file was generated by Catalyst's Python compiler at {DateTime.Now}.");
        sb.AppendLine("# It is recommended not to modify this file. Modify the source spec file instead.");
        sb.AppendLine("#");
        sb.AppendLine();
    }

    private string BuildPackageFile()
    {
        StringBuilder sb = new();
        sb.AppendLine("#");
        sb.AppendLine($"# This package was generated by Catalyst's Python compiler at {DateTime.Now}.");
        sb.AppendLine("# It is recommended not to modify these files. Modify the source spec file instead.");
        sb.AppendLine("#");
        return sb.ToString();
    }

    private string BuildRuntimeFile()
    {
        StringBuilder sb = new();
        AppendFileHeader(sb);
        sb.Append(""""
                  from __future__ import annotations

                  import datetime
                  import re


                  class CatalystHTTPError(Exception):
                      """Raised when the server responds with a non-success HTTP status code."""

                      def __init__(self, status: int, url: str, body: str) -> None:
                          self.status = status
                          self.url = url
                          self.body = body
                          super().__init__(f"HTTP {status} for {url}: {body}")


                  _DATETIME_PATTERN = re.compile(
                      r"(?P<seconds>\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2})"
                      r"(?:\.(?P<fraction>\d+))?"
                      r"(?P<offset>Z|[+-]\d{2}:?\d{2})?"
                  )


                  def encode_datetime(value: datetime.datetime) -> str:
                      """Encodes a datetime as an ISO 8601 string, using Z for UTC."""
                      text = value.isoformat()
                      if text.endswith("+00:00"):
                          return text[:-6] + "Z"
                      return text


                  def decode_datetime(value: str) -> datetime.datetime:
                      """Decodes an ISO 8601 date-time string.

                      Python 3.10 only accepts three or six fractional digits, so other lengths are
                      normalised before parsing. A trailing Z is accepted and treated as UTC.
                      """
                      match = _DATETIME_PATTERN.fullmatch(value.strip())
                      if match is None:
                          raise ValueError(f"Invalid ISO 8601 date-time: {value!r}")

                      seconds = match.group("seconds").replace(" ", "T")
                      fraction = match.group("fraction") or ""
                      offset = match.group("offset") or ""
                      if offset == "Z":
                          offset = "+00:00"

                      if fraction:
                          seconds = f"{seconds}.{fraction[:6].ljust(6, '0')}"

                      return datetime.datetime.fromisoformat(seconds + offset)

                  """");
        return sb.ToString();
    }
}
