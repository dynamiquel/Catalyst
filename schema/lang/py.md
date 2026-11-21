# Catalyst: Python Schema Rules
This section details the Python-specific attributes and type conversions used during code generation.

## Compiler Options
### File Options
Currently no file-specific options.

### Enum Options
Currently no enum-specific options.

### Definition Options
Currently no definition-specific options.

### Property Options
Currently no property-specific options.

### Service Options
Currently no service-specific options.

### Endpoint Options
Currently no endpoint-specific options.

## Validator System
The Python generator does not currently include a Validator system that generates validation code. Validation attributes are emitted as comments on the generated properties.

## Property Type Conversions
| Schema Type                  | Python Type          | Notes                                                                   |
|------------------------------|----------------------|-------------------------------------------------------------------------|
| `str`                        | `str`                |                                                                         |
| `i32`                        | `int`                |                                                                         |
| `i64`                        | `int`                | Python integers are arbitrary precision.                                |
| `f64`                        | `float`              |                                                                         |
| `bool`                       | `bool`               |                                                                         |
| `date`                       | `datetime.datetime`  | ISO 8601 date-time string. UTC is written with a `Z` suffix.            |
| `time`                       | `float`              | Seconds.                                                                |
| `uuid`                       | `uuid.UUID`          | String on the wire.                                                     |
| `url`                        | `str`                |                                                                         |
| `list<T>`                    | `list[T]`            | Where `T` is the Python equivalent of the inner schema type.            |
| `set<T>`                     | `set[T]`             | Emitted as a JSON array.                                                |
| `map<K, V>`                  | `dict[K, V]`         | Emitted as a JSON object; non-string keys are converted to strings.     |
| `any`                        | `typing.Any`         |                                                                         |
| `[Namespace.]DefinitionName` | `ClassName`          | Reference to a generated Python dataclass (namespaces are file-scoped).  |

Notes:
- Optional schema types are emitted as `T | None` and default to `None`.
- Properties without a default are required. Properties with a `default`, optional
  properties, and `str`/`list`/`set`/`map` properties are given initialisers. As
  dataclasses require, required properties are declared before defaulted properties.
- Python attributes use `snake_case` while the JSON keys keep the property names exactly
  as written in the spec (usually `camelCase`).
- Generated dataclasses include `to_dict()`, `from_dict(data)`, `to_json()` and
  `from_json(text)` helpers.
- Enums are generated as `enum.IntEnum`; flags enums are generated as `enum.IntFlag`.
  Enum values are serialised as integers, matching the TypeScript compiler. Enum labels
  are converted to `UPPER_SNAKE_CASE`.
- Constants are emitted as `typing.ClassVar` fields so they don't become dataclass fields.

## File Layout
Each spec file is compiled into a module that mirrors the spec file's path. Spec files that
define services also get a matching `_client.py` module:

| Spec file                 | Generated modules                                |
|---------------------------|--------------------------------------------------|
| `user.yaml`               | `user.py`, `user_client.py`                      |
| `common/address.yaml`     | `common/address.py`, `common/address_client.py`  |

Every output directory is given an `__init__.py`, and the output root contains a
`_catalyst.py` module with shared runtime helpers. Imports between generated modules use
relative imports, so the output directory must be imported as a package (for example,
`from py.user import User` when the output directory itself is on `sys.path`).

Generated code targets Python 3.10 and uses only the standard library.

## Client Services
Each service is compiled into an `XxxClient` class in its spec file's `_client.py`
module. Clients take the API's `base_url` and an optional `access_token` in their
constructor, and use `urllib.request` to send JSON requests. A non-2xx response raises
`CatalystHTTPError`.
