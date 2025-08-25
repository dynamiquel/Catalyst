using System.Text;
using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators.CSharp;

public class CSharpClientServiceBuilder : IClientServiceBuilder<CSharpCompiler>
{
    public string Name => "default";
    public required CSharpCompiler Compiler { get; init; }

    public string GetBuiltFileName(BuildContext context, ServiceNode serviceNode)
    {
        // One file for all services.
        return StringExtensions.FilePathToPascalCase(context.FileNode.FilePath) + "Client.cs";
    }

    public void Build(BuildContext context, ServiceNode serviceNode)
    {
        List<BuiltEndpoint> endpoints = [];
        foreach (KeyValuePair<string, EndpointNode> endpointNode in serviceNode.Endpoints)
        {
            BuiltEndpoint endpoint = new(
                Node: endpointNode.Value,
                Name: endpointNode.Value.Name.ToPascalCase(),
                RequestType: Compiler.GetCompiledPropertyType(endpointNode.Value.BuiltRequestType!),
                ResponseType: Compiler.GetCompiledPropertyType(endpointNode.Value.BuiltResponseType!)
            );
            
            endpoints.Add(endpoint);
        }

        BuiltService service = new(
            Node: serviceNode,
            Name: Compiler.GetCompiledClassName(serviceNode.Name) + "Client",
            Endpoints: endpoints
        );

        context.GetOrAddFile(Compiler, GetBuiltFileName(context, serviceNode)).Services.Add(service);
    }

    public void Compile(BuiltFile file, BuiltService service, StringBuilder fileStr)
    {
        // TODO: Remove this Compile stage all together and make it part of the Build stage.
        
        // Hackyyyyy.
        if (!file.Name.EndsWith("Client.cs"))
            return;
        
        fileStr
            .AppendLine($"public class {service.Name}Options")
            .AppendLine("{")
            .AppendLine("    public required string Url { get; set; }")
            .AppendLine("}")
            .AppendLine();

        Compiler.AppendDescriptionComment(fileStr, service.Node);

        fileStr
            .AppendLine($"public class {service.Name}(")
            .AppendLine("    HttpClient httpClient,")
            .AppendLine($"    {service.Name}Options options)")
            .AppendLine("{");

        for (int endpointIndex = 0; endpointIndex < service.Endpoints.Count; endpointIndex++)
        {
            BuiltEndpoint endpoint = service.Endpoints[endpointIndex];
            
            string nullableResponseType = endpoint.ResponseType.Name;
            string notNullableResponseType = endpoint.ResponseType.Name;
            bool isResponseTypeNullable = endpoint.ResponseType.Name.EndsWith('?');
            
            if (isResponseTypeNullable)
                notNullableResponseType = endpoint.ResponseType.Name[..^1];
            else
                nullableResponseType += '?';

            Compiler.AppendDescriptionComment(fileStr, endpoint.Node, 1);
            
            fileStr
                .AppendLine($"    public async Task<{endpoint.ResponseType.Name}> {endpoint.Name}({endpoint.RequestType.Name} request)")
                .AppendLine("    {")
                .AppendLine("        ByteArrayContent requestContent = new(request.ToBytes());")
                .AppendLine("        requestContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(\"application/json;\", \"utf-8\");")
                .AppendLine()
                .AppendLine("        HttpRequestMessage httpRequest = new()")
                .AppendLine("        {")
                .AppendLine($"            Method = HttpMethod.{endpoint.Node.Method},")
                .AppendLine($"            RequestUri = new Uri(options.Url + \"{service.Node.Path}\" + \"{endpoint.Node.Path}\"),")
                .AppendLine("            Content = requestContent")
                .AppendLine("        };")
                .AppendLine()
                .AppendLine("        HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest).ConfigureAwait(false);")
                .AppendLine("        httpResponse.EnsureSuccessStatusCode();")
                .AppendLine()
                .AppendLine("        byte[] responseBytes = await httpResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);")
                .AppendLine($"        {nullableResponseType} response = {notNullableResponseType}.FromBytes(responseBytes);");

            if (!isResponseTypeNullable)
            {
                fileStr
                    .AppendLine()
                    .AppendLine("        if (response is null)")
                    .AppendLine($"            throw new NullReferenceException(\"Response of '{service.Name}/{endpoint.Name}' is null\");");
            }
            
            fileStr
                .AppendLine()
                .AppendLine("        return response;")
                .AppendLine("    }");
                
            if (endpointIndex < service.Endpoints.Count - 1)
                fileStr.AppendLine();
        }

        fileStr.AppendLine("}");
    }
}