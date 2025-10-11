# Catalyst: C# Schema Rules
This section details the C#-specific attributes and type conversions used during code generation.

## Compiler Options
### File Options
- `type`: The type to generate for definitions in this file. `record` or `class`
- `useRequired`: Whether the required keyword should be added to applicable Properties

### Definition Options
- `type`: The type to generate for definitions in this file. `record` or `class`
- `useRequired`: Whether the required keyword should be added to applicable Properties

### Property Options
- `required`: Whether the required keyword should be added to the Property

## Property Type Conversions
| Schema Type                  | C# Type                 | Notes                                                                       |
|------------------------------|-------------------------|-----------------------------------------------------------------------------|
| `str`                        | `string`                |                                                                             |
| `i32`                        | `int`                   |                                                                             |
| `f64`                        | `double`                |                                                                             |
| `bool`                       | `bool`                  |                                                                             |
| `date`                       | `DateTime`              |                                                                             |
| `time`                       | `TimeSpan`              |                                                                             |
| `uuid`                       | `Guid`                  |                                                                             |
| `list<T>`                    | `List<T>`               | Where `T` is the C# equivalent of the inner schema type.                    |
| `set<T>`                     | `HashSet<T>`            | Where `T` is the C# equivalent of the inner schema type.                    |
| `map<K, V>`                  | `Dictionary<K, V>`      | Where `K` and `V` are the C# equivalents of the schema key and value types. |
| `any`                        | `object?`               |                                                                             |
| `[Namespace.]DefinitionName` | `[Namespace.]ClassName` | Reference to a generated C# class or record.                                |
