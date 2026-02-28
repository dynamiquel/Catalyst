using System.Text;
using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;
using HttpMethod = Catalyst.SpecGraph.Nodes.HttpMethod;

namespace Catalyst.Generators.CSharp;

/// <summary>
/// Server Service Generator for generating bleeding-edge 'Catalyst Services', as well as
/// Minimap API mappings for them.
/// </summary>
public class CSharpMinimalApiServiceBuilder : IServerServiceBuilder<CSharpCompiler>
{
    public string Name => "service";
    public required CSharpCompiler Compiler { get; init; }

    public string GetBuiltFileName(BuildContext context, ServiceNode serviceNode)
    {
        return Helpers.FilePathToPascalCase(context.FileNode.FilePath) + "Services.cs";
    }

    public void Build(BuildContext context, ServiceNode serviceNode)
    {
        // When using service builder, always generate for all services in the file
        // The generator option in the YAML is for backward compatibility with controller builder

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
            Name: Compiler.GetCompiledClassName(serviceNode.Name),
            Endpoints: endpoints
        );

        BuiltFile file = context.GetOrAddFile(Compiler, GetBuiltFileName(context, serviceNode));
        file.Services.Add(service);
        file.Includes.Add(new("Catalyst.Errors"));
        file.Includes.Add(new("System"));
        file.Includes.Add(new("Microsoft.AspNetCore.Builder"));
        file.Includes.Add(new("Microsoft.AspNetCore.Http"));
        file.Includes.Add(new("Microsoft.Extensions.DependencyInjection"));

        string? fileNamespace = Compiler.GetCompiledNamespace(context.FileNode.Namespace);
        if (!string.IsNullOrEmpty(fileNamespace))
            file.Includes.Add(new(fileNamespace));
    }

    public void Compile(BuiltFile file, BuiltService service, StringBuilder fileStr)
    {
        if (!file.Name.EndsWith("Services.cs"))
            return;

        string interfaceName = $"I{service.Name}Service";
        string servicePath = service.Node.Path.TrimStart('/');

        Compiler.AppendDescriptionComment(fileStr, service.Node);

        fileStr.AppendLine($"public interface {interfaceName}");
        fileStr.AppendLine("{");
        fileStr.AppendLine($"    const string ServicePath = \"/{servicePath}\";");

        for (int endpointIndex = 0; endpointIndex < service.Endpoints.Count; endpointIndex++)
        {
            BuiltEndpoint endpoint = service.Endpoints[endpointIndex];
            string endpointPath = endpoint.Node.Path.TrimStart('/');
            fileStr.AppendLine($"    const string {endpoint.Name}Path = \"/{endpointPath}\";");
        }

        fileStr.AppendLine();

        for (int endpointIndex = 0; endpointIndex < service.Endpoints.Count; endpointIndex++)
        {
            BuiltEndpoint endpoint = service.Endpoints[endpointIndex];
            Compiler.AppendDescriptionComment(fileStr, endpoint.Node, 1);

            fileStr.AppendLine(
                $"    ValueTask<CatalystResult<{endpoint.ResponseType.Name}>> {endpoint.Name}({endpoint.RequestType.Name} request);");

            if (endpointIndex < service.Endpoints.Count - 1)
                fileStr.AppendLine();
        }

        fileStr.AppendLine("}");
        fileStr.AppendLine();

        bool serviceRequiresAuth = service.Node.RequiresAuth;

        fileStr.AppendLine($"public static class {service.Name}ServiceMinimalApiExtensions");
        fileStr.AppendLine("{");

        fileStr.AppendLine(
            $"    public static IEndpointRouteBuilder Map{service.Name}Service(this IEndpointRouteBuilder app, Action<RouteGroupBuilder>? configure = null)");
        fileStr.AppendLine("    {");
        fileStr.AppendLine($"        RouteGroupBuilder group = app.MapGroup({interfaceName}.ServicePath)");
        fileStr.AppendLine($"            .WithGroupName(\"{service.Name}\")");

        if (serviceRequiresAuth)
        {
            fileStr.AppendLine("            .RequireAuthorization()");
            fileStr.AppendLine("            .AddEndpointFilter<CatalystErrorEnrichEndpointFilter>();");
        }
        else
        {
            fileStr.AppendLine("            .AddEndpointFilter<CatalystErrorEnrichEndpointFilter>();");
        }

        fileStr.AppendLine();
        fileStr.AppendLine("        configure?.Invoke(group);");
        fileStr.AppendLine();

        for (int endpointIndex = 0; endpointIndex < service.Endpoints.Count; endpointIndex++)
        {
            BuiltEndpoint endpoint = service.Endpoints[endpointIndex];
            string httpMethod = endpoint.Node.Method switch
            {
                HttpMethod.Get => "MapGet",
                HttpMethod.Post => "MapPost",
                HttpMethod.Put => "MapPut",
                HttpMethod.Patch => "MapPatch",
                HttpMethod.Delete => "MapDelete",
                HttpMethod.Options => "MapMethods",
                HttpMethod.Trace => "MapTrace",
                _ => "MapGet"
            };

            string route = endpoint.Node.Path.TrimStart('/');
            bool? endpointRequiresAuth = endpoint.Node.RequiresAuth;

            fileStr.AppendLine($"        group.{httpMethod}({interfaceName}.{endpoint.Name}Path, ");
            fileStr.AppendLine($"            async ({interfaceName} service, {endpoint.RequestType.Name} request) =>");
            fileStr.AppendLine("            {");
            fileStr.AppendLine(
                $"                CatalystResult<{endpoint.ResponseType.Name}> result = await service.{endpoint.Name}(request);");
            fileStr.AppendLine("                return result.ToMinimalResult();");
            fileStr.AppendLine("            })");

            if (endpointRequiresAuth == false)
                fileStr.AppendLine("            .AllowAnonymous()");
            else if (endpointRequiresAuth == true)
                fileStr.AppendLine("            .RequireAuthorization()");

            fileStr.AppendLine($"            .WithName(\"{service.Name}{endpoint.Name}\");");

            if (endpointIndex < service.Endpoints.Count - 1)
                fileStr.AppendLine();
        }

        fileStr.AppendLine();
        fileStr.AppendLine("        return app;");
        fileStr.AppendLine("    }");
        fileStr.AppendLine("}");
    }
}
