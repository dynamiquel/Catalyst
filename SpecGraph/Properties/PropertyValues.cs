using System.Text.Json.Serialization;

namespace Catalyst.SpecGraph.Properties;

/// <summary>
/// Data Value represents the value assigned to a Definition's DataMember.
/// It must match the DataMember's Type.
/// </summary>
// Annoying. Maybe better way to solve this JsonDerivedType stuff.
[JsonDerivedType(typeof(UnBuiltValue))]
[JsonDerivedType(typeof(BooleanValue))]
[JsonDerivedType(typeof(IntegerValue))]
[JsonDerivedType(typeof(Integer64Value))]
[JsonDerivedType(typeof(FloatValue))]
[JsonDerivedType(typeof(StringValue))]
[JsonDerivedType(typeof(DateValue))]
[JsonDerivedType(typeof(TimeValue))]
[JsonDerivedType(typeof(UuidValue))]
[JsonDerivedType(typeof(ListValue))]
[JsonDerivedType(typeof(MapValue))]
[JsonDerivedType(typeof(ObjectValue))]
[JsonDerivedType(typeof(EnumValue))]
public interface IDataValue;

public record UnBuiltValue(string ValueJson) : IDataValue;

public record NullValue : IDataValue;
public record BooleanValue(bool Value) : IDataValue;
public record IntegerValue(int Value) : IDataValue;
public record Integer64Value(long Value) : IDataValue;
public record FloatValue(double Value) : IDataValue;
public record StringValue(string Value) : IDataValue;
public record DateValue(DateTime Value) : IDataValue;
public record TimeValue(TimeSpan Value) : IDataValue;
public record UuidValue(Guid Value) : IDataValue;
public record ListValue(List<IDataValue> Values) : IDataValue;
public record MapValue(Dictionary<IDataValue, IDataValue> Values) : IDataValue;

public record ObjectValue(IDataType Type, Dictionary<string, IDataValue> Values) : IDataValue;

public record EnumValue(IDataType Type, string[] Values) : IDataValue;
