# Catalyst: General Schema Rules
This document outlines the general rules and structure of the API specification schema. This schema aims to be language-agnostic, defining the core structure and semantics of your API. Language-specific details are handled in dedicated sections.

## Top-Level Structure
The root of the schema is a YAML document that can contain the following top-level keys:
- `format`: Specifies the serialisation format the definitions will use.
- `namespace`: A logical grouping for the definitions within this file. The interpretation of the namespace depends on the target language.
- `includes`: A list of paths to other schema files that should be included and processed. Paths are relative to the current file.
- `definitions`: Contains definitions of data structures (objects, entities).
- `services`: Defines the API endpoints and operations
- Language-Specific Sections (e.g. cs): Allow for language-specific configurations for this document.

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

## Definitions
Each entry under definitions defines a data structure with a unique name.
A definition can have the following keys:
- `description` (alt: `desc`): A human-readable description of the data structure.
- `properties` (alt: `props`): A map where each key is the name of a property and the value is a property definition.
- `constants` (alt: `consts`): A map where each key is the name of a constant and the value is a constant definition.
- Language-Specific Sections (e.g. cs): Allow for language-specific configurations for this definition.
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
  - Every type (except `any`) can also be **optional**  by suffixing it with `?` (e.g. `str?`)
- `description` (alt: `desc`): A human-readable description of the property.
- `default`: Default value to assign to the property. A value of `default` means to use the default value for the property type
- Language-Specific Sections (e.g. cs): Allow for language-specific configurations for this property

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
  - Every type can also be **optional**  by suffixing it with `?` (e.g. `str?`)
- `description` (alt: `desc`): A human-readable description of the property.
- `value`: Value to assign to the constant. A value of `default` means to use the default value for the constant type
- Language-Specific Sections (e.g. cs): Allow for language-specific configurations for this constant

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

## Language-Specific Sections
The schema is split into a couple of scopes, generally defined with every indentation, these primarily include:
- global (_not yet implemented_)
- document (file)
- definition
- property

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
      # Reputation will noy be given the required keyword as it has a Default value.
      reputation:
        type: f64
        default: 0.5
      credentials: Credentials
```

## More examples
You can find more examples, including alternate methods, under [TestData](./../TestData).