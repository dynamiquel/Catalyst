# Catalyst: Unreal Engine Schema Rules
This section details the Unreal Engine-specific attributes and type conversions used during code generation.

## Compiler Options
### File Options
- `prefix`: The prefix to assign to the file and any definitions. Useful as an alternative to Namespace.

### Definition Options
- `prefix`: The prefix to assign to the definition. Useful as an alternative to Namespace.

### Property Options

## Property Type Conversions
| Schema Type                  | C# Type                  | Notes                                                                        |
|------------------------------|--------------------------|------------------------------------------------------------------------------|
| `str`                        | `FString`                |                                                                              |
| `i32`                        | `int32`                  |                                                                              |
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
