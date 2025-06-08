namespace Catalyst.SpecReader;

public class SpecFileDeserialiseException : Exception
{
    public required string FileName { get; set; }
    public override string Message => $"Failed to derserialise spec file at {FileName}";

}

public class CatalystReaderException : Exception
{
    public required RawNode RawNode;
    public string? LeafName;

    protected string FileBreadcrumbString
    {
        get
        {
            var str = string.Join(":", RawNode.Breadcrumbs);
            if (!string.IsNullOrEmpty(LeafName))
                str += $":{LeafName}";

            return str;
        }
    }
}

public class UnexpectedTokenException : CatalystReaderException
{
    public required string TokenName { get; set; }
    public override string Message => $"Found unexpected token '{TokenName}' in '{RawNode.FileName}'. Breadcrumbs: {FileBreadcrumbString}";
}

public class UnexpectedTypeException : CatalystReaderException
{
    public required string ExpectedType { get; set; }
    public required string ReceivedType { get; set; }
    public override string Message => $"Found unexpected type '{ReceivedType}' in '{RawNode.FileName}'. Expected type '{ExpectedType}'. Breadcrumbs: {FileBreadcrumbString}";
}

public class IncludeNotFoundException : CatalystReaderException
{
    public required string IncludeFile { get; set; }
    public override string Message => $"Could not find Include File '{IncludeFile}' defined in '{RawNode.FileName}'. Breadcrumbs: {FileBreadcrumbString}";
}

public class ExpectedTokenNotFoundException : CatalystReaderException
{
    public required string TokenName { get; set; }
    public override string Message => $"Could not find expected token '{TokenName}' in '{RawNode.FileName}'. Breadcrumbs: {FileBreadcrumbString}";
}

public class ExistingEnumValueFoundException : CatalystReaderException
{
    public string EnumValueLabel { get; set; }

    public override string Message => $"Enum '{LeafName}' already contains a value called '{EnumValueLabel}' in '{RawNode.FileName}. Breadcrumbs: {FileBreadcrumbString}";
}