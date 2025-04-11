using Catalyst.SpecGraph.Nodes;

namespace Catalyst.SpecGraph.PropertyTypes;

public interface IPropertyType
{
    public string Name { get; }
}

public class StringType : IPropertyType
{
    public string Name => "str";
}

public class IntegerType : IPropertyType
{
    public string Name => "int";
}

public class FloatType : IPropertyType
{
    public string Name => "float";
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

public class ListType : IPropertyType
{
    public string Name => "list<T>";
}

public class SetType : IPropertyType
{
    public string Name => "set<T>";
}

public class MapType : IPropertyType
{
    public string Name => "map<T>";
}

public class AnyType : IPropertyType
{
    public string Name => "any";
}

public class UserType : IPropertyType
{
    public required string Name { get; set; }
    
    public required FileNode OwnedFile { get; set; }
}