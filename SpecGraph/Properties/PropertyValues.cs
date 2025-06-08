using System.Text.Json.Serialization;

namespace Catalyst.SpecGraph.Properties;

/// <summary>
/// Property Value represents the value assigned to a Definition's Property.
/// It must match the Property's Type.
/// </summary>
// Annoying. Maybe better way to solve this JsonDerivedType stuff.
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
[JsonDerivedType(typeof(EnumValue))]
public interface IPropertyValue;

/// <summary>
/// Represents a Property Value that is yet to be built via JSON.
/// </summary>
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

/// <summary>
/// Represents a Property Value of a non-built-in Property Type, typically those generated from Spec Files.
/// </summary>
public record ObjectValue(IPropertyType Type, Dictionary<string, IPropertyValue> Values) : IPropertyValue;

public record EnumValue(IPropertyType Type, string[] Values) : IPropertyValue;
