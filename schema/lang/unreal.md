# Catalyst: Unreal Engine Schema Rules
This section details the Unreal Engine-specific attributes and type conversions used during code generation.

## Compiler Options
### File Options
| Option   | Type   | Description                                                                                  |
|----------|--------|----------------------------------------------------------------------------------------------|
| `prefix` | string | The prefix to assign to the file and any definitions. Useful as an alternative to Namespace. |

### Enum Options
| Option   | Type   | Description                                                              |
|----------|--------|--------------------------------------------------------------------------|
| `prefix` | string | The prefix to assign to the enum. Useful as an alternative to Namespace. |

### Definition Options
| Option   | Type   | Description                                                                    |
|----------|--------|--------------------------------------------------------------------------------|
| `prefix` | string | The prefix to assign to the definition. Useful as an alternative to Namespace. |
| `type`   | string | The type to generate for definitions. `class` or `struct`.                     |

### Property Options
Currently no property-specific options.

### Service Options
Currently no service-specific options.

### Endpoint Options
Currently no endpoint-specific options.

## Validator System
The Unreal Engine generator does not currently include a Validator system that generates validation code.

## Property Type Conversions
| Schema Type                  | C++ Type                 | Notes                                                                        |
|------------------------------|--------------------------|------------------------------------------------------------------------------|
| `str`                        | `FString`                | Constant strings will use TCHAR*                                             |
| `i32`                        | `int32`                  |                                                                              |
| `i64`                        | `int64`                  |                                                                              |
| `f64`                        | `double`                 |                                                                              |
| `bool`                       | `bool`                   |                                                                              |
| `date`                       | `FDateTime`              |                                                                              |
| `time`                       | `FTimespan`              |                                                                              |
| `uuid`                       | `FGuid`                  |                                                                              |
| `list<T>`                    | `TArray<T>`              | Where `T` is the C++ equivalent of the inner schema type.                    |
| `set<T>`                     | `TSet<T>`                | Where `T` is the C++ equivalent of the inner schema type.                    |
| `map<K, V>`                  | `TMap<K, V>`             | Where `K` and `V` are the C++ equivalents of the schema key and value types. |
| `any`                        | `FInstancedStruct`       |                                                                              |
| `[Namespace.]DefinitionName` | `F[Namespace.]ClassName` | Reference to a generated C++ class or record.                                |
