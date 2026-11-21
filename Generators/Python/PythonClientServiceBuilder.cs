using System.Text;
using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators.Python;

public class PythonClientServiceBuilder : IClientServiceBuilder<PythonCompiler>
{
    public string Name => Builder.Default;
    public required PythonCompiler Compiler { get; init; }

    public string GetBuiltFileName(BuildContext context, ServiceNode serviceNode)
    {
        return Compiler.GetClientModuleName(context.FileNode);
    }

    public void Build(BuildContext context, ServiceNode serviceNode)
    {
        List<BuiltEndpoint> endpoints = [];
        foreach (KeyValuePair<string, EndpointNode> endpointNode in serviceNode.Endpoints)
        {
            BuiltEndpoint endpoint = new(
                Node: endpointNode.Value,
                Name: Compiler.GetCompiledMethodName(endpointNode.Value.Name),
                RequestType: Compiler.GetCompiledDataType(endpointNode.Value.BuiltRequestType!),
                ResponseType: Compiler.GetCompiledDataType(endpointNode.Value.BuiltResponseType!)
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
        if (!file.Name.EndsWith("_client.py", StringComparison.Ordinal))
            return;

        fileStr.AppendLine($"class {service.Name}:");
        Compiler.AppendDocstring(fileStr, service.Node, 1);
        if (!string.IsNullOrWhiteSpace(service.Node.Description))
            fileStr.AppendLine();

        fileStr
            .AppendLine("    def __init__(self, base_url: str, access_token: str | None = None) -> None:")
            .AppendLine("        self.base_url = base_url")
            .AppendLine("        self.access_token = access_token");

        foreach (BuiltEndpoint endpoint in service.Endpoints)
        {
            fileStr.AppendLine();
            AppendEndpoint(fileStr, service, endpoint);
        }

        fileStr.AppendLine();
        fileStr.AppendLine();
    }

    private void AppendEndpoint(StringBuilder fileStr, BuiltService service, BuiltEndpoint endpoint)
    {
        string method = endpoint.Node.Method.ToString().ToUpperInvariant();
        string encodedRequest = Compiler.GetEncodedExpression(endpoint.Node.BuiltRequestType!, "request");
        string decodedResponse = Compiler.GetDecodedExpression(endpoint.Node.BuiltResponseType!, "result");

        if (Compiler.NeedsResponseTypeCast(endpoint.Node.BuiltResponseType!))
            decodedResponse = $"typing.cast({endpoint.ResponseType.Name}, {decodedResponse})";

        fileStr.AppendLine($"    def {endpoint.Name}(self, request: {endpoint.RequestType.Name}) -> {endpoint.ResponseType.Name}:");
        Compiler.AppendDocstring(fileStr, endpoint.Node, 2);
        if (!string.IsNullOrWhiteSpace(endpoint.Node.Description))
            fileStr.AppendLine();

        fileStr
            .AppendLine($"        url = self.base_url + {PythonCompiler.ToPythonStringLiteral(service.Node.Path)} + {PythonCompiler.ToPythonStringLiteral(endpoint.Node.Path)}")
            .AppendLine($"        request_body = json.dumps({encodedRequest}).encode(\"utf-8\")")
            .AppendLine("        headers = {\"Content-Type\": \"application/json; charset=utf-8\"}")
            .AppendLine("        if self.access_token is not None:")
            .AppendLine("            headers[\"Authorization\"] = \"Bearer \" + self.access_token")
            .AppendLine("        http_request = urllib.request.Request(")
            .AppendLine("            url,")
            .AppendLine("            data=request_body,")
            .AppendLine("            headers=headers,")
            .AppendLine($"            method=\"{method}\",")
            .AppendLine("        )")
            .AppendLine("        try:")
            .AppendLine("            with urllib.request.urlopen(http_request) as http_response:")
            .AppendLine("                payload = http_response.read().decode(\"utf-8\")")
            .AppendLine("        except urllib.error.HTTPError as error:")
            .AppendLine("            message = error.read().decode(\"utf-8\", \"replace\")")
            .AppendLine("            raise CatalystHTTPError(error.code, url, message) from error")
            .AppendLine()
            .AppendLine("        result = json.loads(payload)")
            .AppendLine($"        return {decodedResponse}");
    }
}
