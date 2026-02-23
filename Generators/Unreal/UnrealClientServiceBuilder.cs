using System.Text;
using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators.Unreal;

public class UnrealClientServiceBuilder : IClientServiceBuilder<UnrealCompiler>
{
    public string Name => "default";
    public required UnrealCompiler Compiler { get; init; }
    
    public string GetBuiltFileName(BuildContext context, ServiceNode serviceNode)
    {
        // One file for all services.
        string fileName = Compiler.GetFileName(context.FileNode) + "Client.h";
        
        return Path.Combine(
            Helpers.FilePathToPascalCase(context.FileNode.Directory) ?? string.Empty, 
            fileName);
    }
    
    public string GetBuiltSourceFileName(BuildContext context, ServiceNode serviceNode)
    {
        // One file for all services.
        string fileName = Compiler.GetFileName(context.FileNode) + "Client.cpp";

        return Path.Combine(
            Helpers.FilePathToPascalCase(context.FileNode.Directory) ?? string.Empty, 
            fileName);
    }


    public void Build(BuildContext context, ServiceNode serviceNode)
    {
        BuildHeader(context, serviceNode);
        BuildSource(context, serviceNode);
    }

    void BuildHeader(BuildContext context, ServiceNode serviceNode)
    {
        List<BuiltEndpoint> endpoints = [];
        foreach (KeyValuePair<string, EndpointNode> endpointNode in serviceNode.Endpoints)
        {
            BuiltEndpoint endpoint = new(
                Node: endpointNode.Value,
                Name: endpointNode.Value.Name.ToPascalCase(),
                RequestType: Compiler.GetCompiledDataType(endpointNode.Value.BuiltRequestType!),
                ResponseType: Compiler.GetCompiledDataType(endpointNode.Value.BuiltResponseType!)
            );
            
            endpoints.Add(endpoint);
        }

        BuiltService service = new(
            Node: serviceNode,
            Name: GetCompiledClassName(serviceNode) + "Client",
            Endpoints: endpoints
        );
        
        BuiltFile headerFile = context.GetOrAddFile(Compiler, GetBuiltFileName(context, serviceNode), FileFlags.Header);
        headerFile.Services.Add(service);
        headerFile.Includes.AddRange([
            new("Templates/SharedPointer"),
            new("CatalystClient"),
            new("CatalystOperation"),
        ]);
    }
    
    void BuildSource(BuildContext context, ServiceNode serviceNode)
    {
        List<BuiltEndpoint> endpoints = [];
        foreach (KeyValuePair<string, EndpointNode> endpointNode in serviceNode.Endpoints)
        {
            BuiltEndpoint endpoint = new(
                Node: endpointNode.Value,
                Name: endpointNode.Value.Name.ToPascalCase(),
                RequestType: Compiler.GetCompiledDataType(endpointNode.Value.BuiltRequestType!),
                ResponseType: Compiler.GetCompiledDataType(endpointNode.Value.BuiltResponseType!)
            );
            
            endpoints.Add(endpoint);
        }

        BuiltService service = new(
            Node: serviceNode,
            Name: GetCompiledClassName(serviceNode) + "Client",
            Endpoints: endpoints
        );
        
        BuiltFile sourceFile = context.GetOrAddFile(Compiler, GetBuiltSourceFileName(context, serviceNode));
        sourceFile.Services.Add(service);
        sourceFile.Includes.Add(new("CatalystSubsystem"));
    }

    record struct OperationDeclaration(string Name, BuiltDataType Type);
    
    public void Compile(BuiltFile file, BuiltService service, StringBuilder fileStr)
    {
        // TODO: Remove this Compile stage all together and make it part of the Build stage.
        
        // Hackyyyyy.
        if (file.Name.EndsWith("Client.h"))
            CompileHeader(file, service, fileStr);
        else
            CompileSource(file, service, fileStr);
    }

    void CompileHeader(BuiltFile file, BuiltService service, StringBuilder fileStr)
    {
        string serviceNamespace = GetServiceNamespace(file, service);

        fileStr
            .AppendLine()
            .AppendLine($"namespace {serviceNamespace}")
            .AppendLine("{");
        foreach (BuiltEndpoint endpoint in service.Endpoints)
            fileStr.AppendLine($"    using F{endpoint.Name} = TCatalystOperation<{endpoint.ResponseType.Name}>;");
        fileStr
            .AppendLine("}")
            .AppendLine();

        Compiler.AppendDescriptionComment(fileStr, service.Node);

        fileStr
            .AppendLine("UCLASS(Config=Catalyst, DefaultConfig)")
            .AppendLine($"class {service.Name} : public UCatalystClient")
            .AppendLine("{")
            .AppendLine("    GENERATED_BODY()")
            .AppendLine()
            .AppendLine("public:")
            .AppendLine($"    {service.Name}();")
            .AppendLine();

        for (int endpointIdx = 0; endpointIdx < service.Endpoints.Count; endpointIdx++)
        {
            BuiltEndpoint endpoint = service.Endpoints[endpointIdx];
            string operationRef = $"TSharedRef<{serviceNamespace}::F{endpoint.Name}>";
            
            Compiler.AppendDescriptionComment(fileStr, endpoint.Node, 1);

            fileStr.AppendLine($"    {operationRef} {endpoint.Name}(")
                .AppendLine($"        const {endpoint.RequestType.Name}& Request,")
                .AppendLine("        float Timeout = Timeout::Default);");
            
            if (endpointIdx < service.Endpoints.Count - 1)
                fileStr.AppendLine();
        }
        
        fileStr.AppendLine("};");
    }

    void CompileSource(BuiltFile file, BuiltService service, StringBuilder fileStr)
    {
        string serviceNamespace = GetServiceNamespace(file, service);

        fileStr
            .AppendLine($"{service.Name}::{service.Name}()")
            .AppendLine("{")
            .AppendLine("    BaseUrl = TEXT(\"https://YouNeedToSet.Me\");")
            .AppendLine("}")
            .AppendLine();

        for (int endpointIdx = 0; endpointIdx < service.Endpoints.Count; endpointIdx++)
        {
            BuiltEndpoint endpoint = service.Endpoints[endpointIdx];
            string operationName = $"{serviceNamespace}::F{endpoint.Name}";

            fileStr
                .AppendLine($"TSharedRef<{operationName}> {service.Name}::{endpoint.Name}(")
                .AppendLine($"    const {endpoint.RequestType.Name}& Request,")
                .AppendLine("    float Timeout)")
                .AppendLine("{")
                .AppendLine("    TArray<uint8> RequestBytes = Request.ToBytes();")
                .AppendLine()
                .AppendLine($"    auto Operation = UCatalystSubsystem::Get().CreateOperation<{operationName}>(")
                .AppendLine($"        BaseUrl + TEXT(\"{service.Node.Path}\") + TEXT(\"{endpoint.Node.Path}\"),")
                .AppendLine($"        Catalyst::Verbs::{endpoint.Node.Method.ToString().ToUpper()},")
                .AppendLine("        MoveTemp(RequestBytes),")
                .AppendLine("        Timeout == Timeout::Default ? DefaultTimeout : Timeout")
                .AppendLine("    );")
                .AppendLine()
                .AppendLine("    return Operation;")
                .AppendLine("}");

            if (endpointIdx < service.Endpoints.Count - 1)
                fileStr.AppendLine();
        }
    }
    
    public string GetCompiledClassName(ServiceNode serviceNode)
    {
        var compilerOptions = serviceNode.FindCompilerOptions<UnrealServiceOptionsNode>()!;
        string? prefix = compilerOptions.Prefix ?? Compiler.GetPrefixFromNamespace(serviceNode.GetParentChecked<FileNode>().Namespace);
       
        string desiredServiceName = serviceNode.Name.ToPascalCase();

        if (prefix is null)
            return $"U{desiredServiceName}";

        if (prefix.EndsWith(desiredServiceName))
            return $"U{prefix}";

        return $"U{prefix}{desiredServiceName}";
    }

    public string GetServiceNamespace(BuiltFile file, BuiltService service)
    {
        string serviceName = service.Node.Name.ToPascalCase();
        if (string.IsNullOrEmpty(file.Namespace))
            return serviceName;
        
        if (file.Namespace.EndsWith(serviceName))
            return file.Namespace;
        
        return file.Namespace + "::" + service.Node.Name.ToPascalCase();
    }
}