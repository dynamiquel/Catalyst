# Catalyst: TypeScript Schema Rules
This section details the TypeScript-specific attributes and type conversions used during code generation.

## Compiler Options
### File Options
- None currently.

### Definition Options
- None currently.

### Property Options
- None currently.

## Property Type Conversions
| Schema Type                  | TypeScript Type | Notes                                                           |
|------------------------------|-----------------|-----------------------------------------------------------------|
| `str`                        | `string`        |                                                                 |
| `i32`                        | `number`        |                                                                 |
| `i64`                        | `bigint`        |                                                                 |
| `f64`                        | `number`        |                                                                 |
| `bool`                       | `boolean`       |                                                                 |
| `date`                       | `string`        | ISO 8601 string                                                 |
| `time`                       | `number`        | Seconds                                                         |
| `uuid`                       | `string`        |                                                                 |
| `list<T>`                    | `Array<T>`      | Where `T` is the TS equivalent of the inner schema type.        |
| `set<T>`                     | `Array<T>`      | Emitted as arrays in TS.                                        |
| `map<K, V>`                  | `Record<K, V>`  | Where `K` and `V` are the TS equivalents of the schema types.   |
| `any`                        | `any`           |                                                                 |
| `[Namespace.]DefinitionName` | `ClassName`     | Reference to a generated TypeScript class (no namespaces).      |

Notes:
- Optional schema types are emitted as `T | null`, and properties use `?` in classes.
- Generated classes include `toBytes()`/`fromBytes(bytes)` helpers using JSON and TextEncoder/TextDecoder.
- Client services generate `XxxClient` with `fetch`-based calls.