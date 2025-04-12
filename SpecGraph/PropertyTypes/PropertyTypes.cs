using Catalyst.SpecGraph.Nodes;

namespace Catalyst.SpecGraph.PropertyTypes;

public interface IPropertyType
{
    public string Name { get; }

    public bool Matches(string compare)
    {
        return Name == compare || $"{Name}?" == compare;
    }
}

public interface IPropertyContainerType : IPropertyType
{
    public Tuple<int, int>? GetInnerPropertyTypesRange(string propertyType)
    {
        Tuple<int, int> tuple = new(propertyType.IndexOf('<'), propertyType.LastIndexOf('>'));
        if (tuple.Item1 == -1 || tuple.Item2 == -1)
            return null;

        return new Tuple<int, int>(tuple.Item1 + 1, tuple.Item2);
    }

    public string[] GetInnerPropertyTypes(string propertyType)
    {
        Tuple<int, int>? innerTypesRange = GetInnerPropertyTypesRange(propertyType);
        
        if (innerTypesRange is null)
            throw new ArgumentOutOfRangeException($"Property type '{propertyType}' is not a valid container type");
        
        string innerTypesStr = propertyType[innerTypesRange.Item1..innerTypesRange.Item2];
        
        innerTypesStr = innerTypesStr.Replace(" ", string.Empty);
        
        string[] innerTypes = innerTypesStr.Split(',');
        if (innerTypes.Length == 0)
            throw new InvalidOperationException($"Property type '{propertyType}' is not a valid container type");
        
        return innerTypes;
    }

    bool IPropertyType.Matches(string compare)
    {
        // Containers match when:
        // 1. They have the same number of inner types.
        // 2. They start with the same name.
        
        Tuple<int, int> baseTypesRange = GetInnerPropertyTypesRange(Name) ?? throw new InvalidOperationException();
        Tuple<int, int>? compareTypesRange = GetInnerPropertyTypesRange(compare);
        
        // Compare is not a container.
        if (compareTypesRange is null)
            return false;
        
        string baseName = Name[..baseTypesRange.Item1];
        string compareName = Name[..compareTypesRange.Item1];

        if (baseName != compareName)
            return false;
        
        string[] baseInnerTypes = GetInnerPropertyTypes(Name);
        string[] compareInnerTypes = GetInnerPropertyTypes(compare);
        return baseInnerTypes.Length == compareInnerTypes.Length;
    }
}

public class StringType : IPropertyType
{
    public string Name => "str";
}

public class IntegerType : IPropertyType
{
    public string Name => "i32";
}

public class FloatType : IPropertyType
{
    public string Name => "f64";
}

public class BooleanType : IPropertyType
{
    public string Name => "bool";
}

public class DateType : IPropertyType
{
    public string Name => "date";
}

public class TimespanType : IPropertyType
{
    public string Name => "timespan";
}

public class ListType : IPropertyContainerType
{
    public string Name => "list<T>";
}

public class SetType : IPropertyContainerType
{
    public string Name => "set<T>";
}

public class MapType : IPropertyContainerType
{
    public string Name => "map<T,U>";
}

public class AnyType : IPropertyType
{
    public string Name => "any";
}

public class UserType : IPropertyType
{
    public required string Name { get; set; }
    public required string? Namespace { get; set; }
    public required FileNode OwnedFile { get; set; }

}