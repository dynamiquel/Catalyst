# Catalyst: API IDL

Catalyst is an opinionated API development tool and **Interface Definition Language (IDL)** designed to simplify building and consuming APIs across multiple languages. It solves the problem of repetitive boilerplate by generating consistent client and server code from a single, clear YAML specification.

## Why Catalyst?

Building APIs for multiple platforms often means writing repetitive data structures, client libraries, and error handling, which is time-consuming and error-prone.

While tools like **gRPC** are high-performance, they can introduce significant build complexity and library dependencies. Traditional **REST** with **OpenAPI** is flexible, but the specifications can be verbose and code generation results are often inconsistent.

Catalyst provides a solution by offering a simple IDL that generates consistent code, leveraging standard HTTP and JSON for easy integration without the complexity of heavier RPC frameworks.

## Catalyst vs. Other Technologies

| Feature | Catalyst | gRPC | Traditional REST (OpenAPI) |
| :--- | :--- | :--- | :--- |
| **IDL** | Simple YAML | Protocol Buffers (.proto) | OpenAPI (YAML/JSON) |
| **Transport** | HTTP/2 | HTTP/2 | HTTP (any version) |
| **Data Format** | JSON (default) | Binary (Proto) | JSON/XML (flexible) |
| **Strengths** | Simple, "human-readable" API, low integration overhead. | High-performance, low-latency, streaming. | Flexible, mature ecosystem, wide support. |
| **Challenges** | Opinionated design. | Build complexity, library dependencies, less "human-readable". | Verbose spec, can lead to inconsistent code-gen. |

## Core Features

* **Define Once, Generate Everywhere:** Use the Catalyst IDL to generate consistent client and server code in multiple languages.
* **Reduce Boilerplate:** Automates the creation of data structures, serialization, and client communication logic.
* **Simple Integration:** Built on standard HTTP and JSON, it's easy to debug and compatible with existing web infrastructure.
* **Avoids RPC Complexity:** Get the benefits of code generation without the heavy library dependencies or build process changes required by gRPC.
* **Human-Readable REST API:** The underlying API remains a standard REST API, accessible with tools like `curl` or standard HTTP clients.

> **An Opinionated Tool**
>
> Catalyst is intentionally opinionated to enforce consistency. Its structured nature is designed for simple, predictable code generation. If your project requires extreme flexibility or you prefer manual implementation, Catalyst may feel restrictive.

## Examples

Here is a simple example of an API defined with Catalyst:
```yaml
definitions:
  Credentials:
    properties:
      email: str
      password: str? 
  UserResponse:
    description: Represents a user in the system.
    properties:
      username: str
      dateOfBirth: date
      status: str?
      reputation: f64
  CreateUserRequest:
    properties:
      username: str
      credentials: Credentials  

endpoints:
  createUser:
    description: Creates a new user.
    request: CreateUserRequest
    response: UserResponse
  getUser:
    description: Gets a user by username.
    request: str
    response: UserResponse
```

### Property Details
The simple format can be expanded to include more detail:
```yaml
definitions:
  Credentials:
    properties:
      email:
        description: Email address of the user.
        type: str
        default: default@email.com
      password: 
        description: Password of the user.
        type: str? 
```

### Service Categories
Endpoints can be grouped into a **Service**;
```yaml
services:
  Users:
    endpoints:
      createUser:
        description: Creates a new user.
        request: CreateUserRequest
        response: UserResponse
      getUser:
        description: Gets a user by username.
        request: str
        response: UserResponse
```

### Custom Endpoint Routing
By default, Catalyst structures your API (HTTP method, URL), but this can be overridden:
```yaml
services:
  Users:
    path: /users
    endpoints:
      createUser:
        path: /create
        description: Creates a new user.
        method: POST
        request: CreateUserRequest
        response: UserResponse
      getUser:
        path: /get
        description: Gets a user by username.
        method: GET
        request: str
        response: UserResponse
```
This now explicitly maps:
- createUser to `POST /users/create`
- getUser to `GET /users/get`

Alternatively, if you want to have more traditional REST, where you simply map:
- createUser to `POST /user`
- getUser to `GET /user`

you can do:
```yaml
services:
  User:
    endpoints:
      POST:
        description: Creates a new user.
        request: CreateUserRequest
        response: UserResponse
      GET:
        description: Gets a user by username.
        request: str
        response: UserResponse
```


This is only a subset of what Catalyst can do, you can find more examples under [TestData](./TestData).

## Learn More About the Schema

For a detailed understanding of how to define your API using the Catalyst IDL, please refer to the [Schema Rules](./schema/general.md).

## Supported tools
### C#
C# Client, C# Server via ASP.NET Controllers
### Unreal Engine 5
Unreal Engine 5 Client (requires the [Unreal Engine Catalyst Plugin](https://github.com/dynamiquel/Catalyst-UEPlugin)
### TypeScript
TypeScript Client
