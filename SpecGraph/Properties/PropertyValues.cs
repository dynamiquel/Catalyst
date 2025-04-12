using System.Text.Json.Serialization;

namespace Catalyst.SpecGraph.Properties;

// Annoying. Maybe better way to solve.
[JsonDerivedType(typeof(UnBuiltValue))]
[JsonDerivedType(typeof(BooleanValue))]
[JsonDerivedType(typeof(IntegerValue))]
[JsonDerivedType(typeof(FloatValue))]
[JsonDerivedType(typeof(StringValue))]
[JsonDerivedType(typeof(DateValue))]
[JsonDerivedType(typeof(TimeValue))]
[JsonDerivedType(typeof(ListValue))]
[JsonDerivedType(typeof(MapValue))]
[JsonDerivedType(typeof(ObjectValue))]
public interface IPropertyValue;

public record UnBuiltValue(string ValueJson) : IPropertyValue;
public record NullValue : IPropertyValue;
public record BooleanValue(bool Value) : IPropertyValue;
public record IntegerValue(int Value) : IPropertyValue;
public record FloatValue(double Value) : IPropertyValue;
public record StringValue(string Value) : IPropertyValue;
public record DateValue(DateTime Value) : IPropertyValue;
public record TimeValue(TimeSpan Value) : IPropertyValue;
public record ListValue(List<IPropertyValue> Values) : IPropertyValue;
public record MapValue(Dictionary<IPropertyValue, IPropertyValue> Values) : IPropertyValue;
public record ObjectValue(IPropertyType Type, Dictionary<string, IPropertyValue> Values) : IPropertyValue;