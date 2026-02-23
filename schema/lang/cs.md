# Catalyst: C# Schema Rules
This section details the C#-specific attributes and type conversions used during code generation.

## Compiler Options
### File Options
| Option        | Type    | Description                                                                                   |
|---------------|---------|-----------------------------------------------------------------------------------------------|
| `type`        | string  | The type to generate for definitions in this file. `record` or `class`.                       |
| `useRequired` | boolean | Whether the `required` keyword should be added to applicable Properties. Defaults to `false`. |

### Enum Options
| Option | Type   | Description                   |
|--------|--------|-------------------------------|
| `type` | string | Not currently used for enums. |

### Definition Options
| Option        | Type    | Description                                                                                 |
|---------------|---------|---------------------------------------------------------------------------------------------|
| `type`        | string  | The type to generate for definitions in this file. `record` or `class`.                     |
| `useRequired` | boolean | Whether the `required` keyword should be added to applicable Properties in this definition. |

### Property Options
| Option     | Type    | Description                                                                                         |
|------------|---------|-----------------------------------------------------------------------------------------------------|
| `required` | boolean | Whether the `required` keyword should be added to this Property. Overrides inherited `useRequired`. |

### Service Options
| Option | Type   | Description                      |
|--------|--------|----------------------------------|
| `type` | string | Not currently used for services. |

### Endpoint Options
| Option | Type   | Description                       |
|--------|--------|-----------------------------------|
| `type` | string | Not currently used for endpoints. |

## Validator System
The C# generator includes a Validator system that generates validation code based on property attributes. This is automatically enabled when validation attributes are specified in the schema.

## Property Type Conversions
| Schema Type                  | C# Type                 | Notes                                                                       |
|------------------------------|-------------------------|-----------------------------------------------------------------------------|
| `str`                        | `string`                |                                                                             |
| `i32`                        | `int`                   |                                                                             |
| `i64`                        | `long`                  |                                                                             |
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
