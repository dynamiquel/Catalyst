namespace Catalyst.SpecReader;

public record RawNode(Dictionary<object, object> Internal, string FileName, RawNode? ParentNode, string[] Breadcrumbs);
public record RawFileNode(FileInfo FileInfo, Dictionary<object, object> Internal) : RawNode(Internal, FileInfo.FullName, null, [FileInfo.Name]);

public static class RawNodeExtensions
{
    public static RawNode CreateChild(this RawNode rawNode, Dictionary<object, object> rawNodeObject, string nodeName)
    {
        var childBreadcrumbs = new string[rawNode.Breadcrumbs.Length + 1];
        Array.Copy(rawNode.Breadcrumbs, childBreadcrumbs, rawNode.Breadcrumbs.Length);
        childBreadcrumbs[^1] = nodeName;
        
        var childRawNode = new RawNode(rawNodeObject, rawNode.FileName, rawNode, childBreadcrumbs);
        return childRawNode;
    }
    
    public static string? ReadPropertyAsStr(this RawNode rawNode, string propertyName)
    {
        if (rawNode.Internal.TryGetValue(propertyName, out object? obj))
        {
            if (obj is string objStr)
                return objStr;
            
            throw new UnexpectedTypeException
            {
                RawNode = rawNode,
                LeafName = propertyName,
                ExpectedType = nameof(String),
                ReceivedType = obj.GetType().Name
            };
        }

        return null;
    }
    
    public static int? ReadPropertyAsInt(this RawNode rawNode, string propertyName)
    {
        if (rawNode.Internal.TryGetValue(propertyName, out object? obj))
        {
            if (obj is int objStr)
                return objStr;
            
            throw new UnexpectedTypeException
            {
                RawNode = rawNode,
                LeafName = propertyName,
                ExpectedType = nameof(Int32),
                ReceivedType = obj.GetType().Name
            };
        }

        return null;
    }
    
    public static double? ReadPropertyAsFloat(this RawNode rawNode, string propertyName)
    {
        if (rawNode.Internal.TryGetValue(propertyName, out object? obj))
        {
            if (obj is double objStr)
                return objStr;
            
            throw new UnexpectedTypeException
            {
                RawNode = rawNode,
                LeafName = propertyName,
                ExpectedType = nameof(Double),
                ReceivedType = obj.GetType().Name
            };
        }

        return null;
    }
    
    public static List<object>? ReadPropertyAsList(this RawNode rawNode, string propertyName)
    {
        if (rawNode.Internal.TryGetValue(propertyName, out object? obj))
        {
            if (obj is List<object> objStr)
                return objStr;
            
            throw new UnexpectedTypeException
            {
                RawNode = rawNode,
                LeafName = propertyName,
                ExpectedType = nameof(List<object>),
                ReceivedType = obj.GetType().Name
            };
        }

        return null;
    }

    public static Dictionary<object, object>? ReadPropertyAsDictionary(this RawNode rawNode, string propertyName)
    {
        if (rawNode.Internal.TryGetValue(propertyName, out object? obj))
        {
            if (obj is Dictionary<object, object> objStr)
                return objStr;
            
            throw new UnexpectedTypeException
            {
                RawNode = rawNode,
                LeafName = propertyName,
                ExpectedType = nameof(Dictionary<object, object>),
                ReceivedType = obj.GetType().Name
            };
        }

        return null;
    }
}