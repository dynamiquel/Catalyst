# Catalyst: General Schema Rules
This document outlines the general rules and structure of the API specification schema. This schema aims to be language-agnostic, defining the core structure and semantics of your API. Language-specific details are handled in dedicated sections.

## Top-Level Structure
The root of the schema is a YAML document that can contain the following top-level keys:
- `format`: Specifies the serialisation format the definitions will use.
- `namespace`: A logical grouping for the definitions within this file. The interpretation of the namespace depends on the target language.
- `includes`: A list of paths to other schema files that should be included and processed. Paths are relative to the current file.
- `enums`: Contains enum definitions.
- `definitions`: Contains definitions of data structures (objects, entities).
- `services`: Defines the API endpoints and operations grouped by service.
- `endpoints`: Shorthand for defining endpoints in a default service.
- Language-Specific Sections (e.g. `cs`, `unreal`, `ts`): Allow for language-specific configurations for this document.

## Format
The format key specifies the serialisation format that will be used to serialise and deserialise definitions.

It defaults to `json`. Currently, no other formats are supported.

## Includes
The includes section is a list of file paths. The content of these files is merged into the current schema during processing. This allows for modularizing your API specification.

Included files without a file extension specified will implicity use the `.yaml` extension.
### Examples
```yaml
includes:
  - Sibling.yaml
  - AnotherSibling
  - Children/Child.yaml
  - ./Children/AnotherChild
  - ../Parent.yaml
```

## Enums
Each entry under enums defines an enumerated type with a unique name.
An enum can have the following keys:
- `description` (alt: `desc`): A human-readable description of the enum.
- `flags`: Boolean indicating if this is a flags enum (bitwise).
- `values`: A list of enum values.

Enum values can be specified in multiple ways:
- As a simple string (value auto-assigned): `ValueName`
- As a key-value pair with explicit numeric value: `ValueName: 5`
- As a key-value pair with bitwise shift: `ValueName: ^2` (means 1 << 2 = 4)
- As a key-value pair combining flags: `ValueName: FlagA | FlagB`

### Examples
```yaml
enums:
  # Simple enum
  Color:
    - Red
    - Green
    - Blue
  
  # Verbose enum with description
  UserRole:
    desc: Roles for user access control
    values:
      - User
      - Moderator: ^0
      - Admin: ^2
  
  # Flags enum
  Permissions:
    flags: true
    values:
      - None
      - Read: ^0
      - Write: ^1
      - Execute: ^2
      - All: Read | Write | Execute
```

## Definitions
Each entry under definitions defines a data structure with a unique name.
A definition can have the following keys:
- `description` (alt: `desc`): A human-readable description of the data structure.
- `properties` (alt: `props`): A map where each key is the name of a property and the value is a property definition.
- `constants` (alt: `consts`): A map where each key is the name of a constant and the value is a constant definition.
- Language-Specific Sections (e.g. `cs`, `unreal`, `ts`): Allow for language-specific configurations for this definition.
### Examples
```yaml
definitions:
  Credentials:
    properties:
      email: str
      password: str?
  User:
    description: Represents a user in the system.
    properties:
      username:
        type: str
        description: The display name the user chose for themselves.
      dateOfBirth:
        type: date
        description: The date the user was born.
      status:
        type: str?
        description: The current status the user has provided themselves.
      reputation:
        type: f64
        default: 0.5
      credentials: Credentials
    constants:
      onlineStatus:
        type: str
        value: Online
```

## Properties
Each entry under properties defines a property of a data structure.
A property definition can have the following keys:
- `type` (required): The data type of the property. Supported types include:
  - `str`: string of characters
  - `i32`: 32-bit integer
  - `i64`: 64-bit integer
  - `f64`: 64-bit floating-point number
  - `bool`: boolean
  - `date`: ISO 8601 date
  - `time`: timespan
  - `uuid`: 128-bit UUID
  - `list<T>`: resizable array of T elements
  - `set<T>`: unique container of T elements
  - `map<K, V>`: hash map of T elements keyed using K
  - `any`: any type
  - `[Namespace.]DefinitionName`: reference to another defined data structure
  - Every type (except `any`) can also be **optional** by suffixing it with `?` (e.g. `str?`)
- `description` (alt: `desc`): A human-readable description of the property.
- `default`: Default value to assign to the property. A value of `default` means to use the default value for the property type.
- `min`: Minimum value for validation (numeric types, strings, lists).
- `max`: Maximum value for validation (numeric types, strings, lists).
- `pattern`: Regex pattern for string validation. See [Built-in Patterns](#built-in-patterns) below.
- Language-Specific Sections (e.g. `cs`, `unreal`, `ts`): Allow for language-specific configurations for this property.

### Validation Attributes
Properties can include validation attributes to enforce constraints:

| Attribute | Types                                         | Description                                                                         |
|-----------|-----------------------------------------------|-------------------------------------------------------------------------------------|
| `min`     | `i32`, `i64`, `f64`, `str`, `list<T>`, `time` | Minimum value/length. Use suffix `i` or `e` for exclusive, no suffix for inclusive. |
| `max`     | `i32`, `i64`, `f64`, `str`, `list<T>`, `time` | Maximum value/length. Use suffix `i` or `e` for exclusive, no suffix for inclusive. |
| `pattern` | `str`                                         | Regular expression pattern for validation.                                          |

### Validation Suffixes
- No suffix: Inclusive boundary (e.g., `max: 100` means ≤ 100)
- `i` suffix: Inclusive boundary (e.g., `max: 100i` means ≤ 100)
- `e` suffix: Exclusive boundary (e.g., `max: 100e` means < 100)

#### Examples
```yaml
properties:
  username:
    type: str
    min: 3
    max: 50e
    pattern: ascii
  email:
    type: str
    pattern: email
  age:
    type: i32
    min: 0
    max: 150i
  tags:
    type: list<str>
    min: 1
    max: 10e
```

### Built-in Patterns
The following built-in patterns are available for string validation:

| Pattern        | Description                                   | Regex                                                                                                         |
|----------------|-----------------------------------------------|---------------------------------------------------------------------------------------------------------------|
| `alpha`        | Alphabetic characters only                    | `^[a-zA-Z]+$`                                                                                                 |
| `alphanumeric` | Alphanumeric characters                       | `^[a-zA-Z0-9]+$`                                                                                              |
| `hex`          | Hexadecimal characters                        | `^[0-9a-fA-F]+$`                                                                                              |
| `number`       | Numeric values (integer, decimal, scientific) | `^-?[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?$`                                                                     |
| `ascii`        | ASCII characters                              | `^[\x00-\x7F]+$`                                                                                              |
| `ascii8`       | Extended ASCII (0-255)                        | `^[\x00-\xFF]+$`                                                                                              |
| `vascii`       | Visible ASCII (32-126)                        | `^[\x20-\x7E]+$`                                                                                              |
| `vascii8`      | Visible extended ASCII                        | `^[\x20-\xFF]+$`                                                                                              |
| `uuid`         | UUID format                                   | `^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$`                               |
| `url`          | URL format                                    | `^[a-zA-Z][a-zA-Z0-9+.-]*:[^ \s]*$`                                                                           |
| `date`         | ISO 8601 date-time                            | `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z                                                           |[+-]\d{2}:\d{2})$` |
| `ipv4`         | IPv4 address                                  | `^(?:(?:25[0-5]                                                                                               |2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$` |
| `ipv6`         | IPv6 address                                  | Complex IPv6 regex                                                                                            |
| `email`        | Email address                                 | `^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$`                                                            |
| `http`         | HTTP/HTTPS URL                                | `^https?:\/\/(?:www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b(?:[-a-zA-Z0-9()@:%_\+.~#?&\/=]*)$` |
| `slug`         | URL slug                                      | `^[a-z0-9]+(?:-[a-z0-9]+)*$`                                                                                  |
| `phone`        | E.164 phone number                            | `^\+?[1-9]\d{1,14}$`                                                                                          |

Custom patterns can also be specified as regular expressions directly.

## Constants
Each entry under constants defines a constant field. These are read-only and static
fields that can be accessed without creating an instance of a definition.
A constant definition can have the following keys:
- `type` (required): The data type of the constant. Supported types include:
  - `str`: string of characters
  - `i32`: 32-bit integer
  - `i64`: 64-bit integer
  - `f64`: 64-bit floating-point number
  - `bool`: boolean
  - `date`: ISO 8601 date
  - `time`: timespan
  - `uuid`: 128-bit UUID
  - Every type can also be **optional** by suffixing it with `?` (e.g. `str?`)
- `description` (alt: `desc`): A human-readable description of the property.
- `value`: Value to assign to the constant. A value of `default` means to use the default value for the constant type
- Language-Specific Sections (e.g. `cs`, `unreal`, `ts`): Allow for language-specific configurations for this constant

### Examples
```yaml
properties:
  username:
    type: str
    description: The display name the user chose for themselves.
  dateOfBirth:
    type: date
    description: The date the user was born.
  status:
    type: str?
    description: The current status the user has provided themselves.
  reputation:
    type: f64
    default: 0.5
```

#### Alternate schema
Properties also have an alternate shorter schema, which is useful when you only care about defining the Property Type.
```yaml
properties:
  username: str
  dateOfBirth: date
  status: str?
  reputation:
    type: f64
    default: 0.5
```

## Services and Endpoints
Services group related API endpoints together. Each service has a base path and contains endpoints.

### Service Properties
- `description` (alt: `desc`): A human-readable description of the service.
- `path`: Base URL path for all endpoints in this service.
- `endpoints`: A map of endpoint names to endpoint definitions.
- Language-Specific Sections: Allow for language-specific configurations.

### Endpoint Properties
- `description` (alt: `desc`): A human-readable description of the endpoint.
- `request` (alt: `req`): The request type (required).
- `response` (alt: `res`): The response type (required).
- `method`: HTTP method (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS). Defaults to POST.
- `path`: Endpoint-specific path (appended to service path).
- Language-Specific Sections: Allow for language-specific configurations.

### Examples
```yaml
services:
  Users:
    path: /users
    endpoints:
      getUser:
        request: GetUserRequest
        response: UserResponse
      createUser:
        path: /create
        method: POST
        request: CreateUserRequest
        response: UserResponse

# Shorthand - endpoints at file level become a default service
endpoints:
  getUser:
    request: User
    response: User
  createUser:
    path: /create
    req: User
    res: User
```

### Traditional REST Style
You can also use HTTP method names directly as endpoint names for traditional REST APIs:
```yaml
services:
  Users:
    endpoints:
      GET:
        request: str
        response: User
      POST:
        request: CreateUserRequest
        response: User
```

## Language-Specific Sections
The schema is split into several scopes, generally defined with every indentation, these primarily include:
- global (_not yet implemented_)
- document (file)
- enum
- definition
- property
- service
- endpoint

Each scope can have its own options assigned to them, which is then propagated to any child scopes.

Currently, scopes are primarily used to set **Language-Specific Compiler Options**, which are set by creating a new object with the target **Language Key**.
Each language will have its own set of **Compiler Options**.

### Examples
For this example, we will reference the **UseRequired** option within the **C# Compiler Options**, which adds the `required` keyword to any **Property** that is a non-**Optional** type and isn't assigned a **Default** value.
The C# language uses `cs` as its **Language Key**.


```yaml
# Document-scoped C# Compiler Options
# Will propagate to all child scopes unless explicitly overriden.
# All Properties will have the required keyword.
cs:
  useRequired: true

definitions:
  Credentials:
    properties:
      email: str
      password: str?
    # Definition-scoped C# Compiler Options. Overrides the inherited Compiler Options.
    # No Properties in Credentials will have the required keyword.
    cs:
      useRequired: false
  User:
    description: Represents a user in the system.
    properties:
      # Username will have the required keyword.
      username:
        type: str
        description: The display name the user chose for themselves.
      dateOfBirth:
        type: date
        description: The date the user was born.
        # Property-scoped C# Compiler Options. Overrides the inherited Compiler Options.
        # Date Of Birth will not have the required keyword.
        cs:
          required: false
      # Status will not be given the required keyword as it has an Optional type.
      status:
        type: str?
        description: The current status the user has provided themselves.
      # Reputation will not be given the required keyword as it has a Default value.
      reputation:
        type: f64
        default: 0.5
      credentials: Credentials
```

## More examples
You can find more examples, including alternate methods, under [TestData](./../TestData).
