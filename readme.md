# Catalyst: Streamlining API Development Through Code Generation

Catalyst is an opinionated API development tool and **Interface Definition Language (IDL)** designed to simplify the process of building and consuming APIs across multiple programming languages. It aims to alleviate the pain points of repetitive code writing and the complexities of integrating disparate technologies.

## The Problem Catalyst Solves

Many developers find themselves writing similar data structures, client libraries, and error handling logic repeatedly when building APIs for different platforms or languages. This is time-consuming, error-prone, and can lead to inconsistencies. Furthermore, integrating powerful but often heavy and complex frameworks like gRPC can be a significant hurdle, especially in environments with limited library support or intricate build processes.

Catalyst was born out of the frustration of this repetitive work. The goal is to define an API once, in a clear and structured specification (using the Catalyst IDL), and then automatically generate the necessary code for various target languages, ensuring consistency and reducing development overhead.

## Catalyst's Approach Compared to Other API Technologies

### gRPC

gRPC is a high-performance, language-agnostic RPC framework developed by Google. It uses Protocol Buffers as its Interface Definition Language (IDL) and typically relies on HTTP/2 for transport.

**Comparison:**

* **IDL:** Both Catalyst and gRPC utilize an IDL to define the API contract. Catalyst's IDL is based on YAML, aiming for readability and ease of use. gRPC uses Protocol Buffers, which is a binary serialization format with its own definition language.
* **Performance:** gRPC often boasts higher performance due to its binary serialization (Protocol Buffers). Catalyst, by default uses JSON, which reduces some of its performance, although binary serialisation formats could be added in the future.
* **Code Generation:** Both Catalyst and gRPC heavily rely on code generation from their respective IDLs.
* **Complexity:** gRPC can introduce significant complexity in build processes and library integration, especially for languages with less mature gRPC support. Catalyst aims for simpler integration by leveraging standard HTTP/1.1 or HTTP/2 and common data formats.
* **Transport:** gRPC primarily uses HTTP/2. Catalyst is designed to work with standard HTTP (both 1.1 and 2), making it more readily compatible with existing infrastructure and easier to debug with standard HTTP tools.
* **"Human-Readable" API:** While gRPC's Protocol Buffers are efficient, the underlying API calls are not as inherently "human-readable" as typical REST APIs using JSON. Catalyst's design allows the API to still be interacted with as a standard REST API when direct code generation isn't used or desired.

### Traditional REST

Traditional REST APIs often use specifications like OpenAPI (Swagger) for documentation and sometimes for code generation. 
However, the level of code generation and the consistency achieved can vary. Manual client creation is a common source of the problems Catalyst aims to solve.

OpenAPI is also a fairly complex specification and is likely overkill and too verbose for many modern APIs.

**Comparison:**

* **IDL:** OpenAPI serves as a description format and can be considered an IDL in a broader sense. Catalyst's IDL is specifically designed for code generation with a focus on simplicity and consistency.
* **Code Generation:** Catalyst mandates a structured specification for comprehensive code generation, aiming for more complete and consistent output than some OpenAPI workflows.
* **Consistency:** Catalyst enforces a specific structure in its IDL, leading to more predictable and maintainable generated code across languages. Traditional REST with OpenAPI can be more flexible but potentially less consistent in generated clients.
* **Learning Curve:** Developers familiar with basic YAML and REST principles should find Catalyst's IDL relatively easy to pick up. gRPC's Protocol Buffers and even the intricacies of OpenAPI can have steeper learning curves.
* **"Human-Readable" API:** Catalyst builds upon the well-understood principles of REST, ensuring that the API remains accessible and understandable even without the generated clients.

## Advantages of Catalyst

* **Simplified Cross-Language Development:** Define your API once using the Catalyst IDL and generate consistent client and server code in multiple languages, eliminating the need for manual reimplementation.
* **Reduced Boilerplate:** Automates the creation of data structures, serialization/deserialization logic, and basic client communication, freeing up developers to focus on core business logic.
* **Leverages Standard REST:** Built upon standard HTTP and common data formats, making it inherently compatible with existing web infrastructure and easier to debug.
* **Easier Integration:** Avoids the complexities and potential headaches of integrating large, language-specific RPC libraries like gRPC, especially in less well-supported environments.
* **Still a REST API:** Even when using generated code, the underlying API remains a standard REST API, allowing for direct interaction with tools like `curl` or standard HTTP clients when needed.
* **Opinionated for Consistency:** Catalyst's structured IDL enforces a degree of consistency that leads to more predictable and maintainable generated code.

## Important Note: Catalyst is Opinionated

Catalyst is designed with specific principles and preferences in mind. Its structured nature and focus on code generation for consistency mean it might not be the ideal solution for every API development scenario. If you require extreme flexibility in your API design or prefer a more "hands-on" approach to client and server implementation across all languages, Catalyst's opinionated nature might feel restrictive.

## How about an example?

Sure, here's a simple example of an API defined with Catalyst:
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

### More property info?
Sure, what was shown above was just the simple way to do it, but you can further elaborate on your Properties by expanding it to the full spec.
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

### Categorise Endpoints?
Of course, Endpoints can be grouped into a **Service**, which has its own properties and collection of Endpoints.
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

### More control over Endpoints?
Catalyst is simple by design, and thus it automatically structures your API for you, this includes the HTTP method it uses as well as the URLs. But of course, you can override this behaviour.
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
