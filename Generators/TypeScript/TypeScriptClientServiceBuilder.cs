using System.Text;
using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;

namespace Catalyst.Generators.TypeScript;

public class TypeScriptClientServiceBuilder : IClientServiceBuilder<TypeScriptCompiler>
{
    public string Name => Builder.Default;
    public required TypeScriptCompiler Compiler { get; init; }

    public string GetBuiltFileName(BuildContext context, ServiceNode serviceNode)
    {
        return Helpers.FilePathToPascalCase(context.FileNode.FilePath) + "Client.ts";
    }

    public void Build(BuildContext context, ServiceNode serviceNode)
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
            Name: Compiler.GetCompiledClassName(serviceNode.Name) + "Client",
            Endpoints: endpoints
        );

        context.GetOrAddFile(Compiler, GetBuiltFileName(context, serviceNode)).Services.Add(service);
    }

    public void Compile(BuiltFile file, BuiltService service, StringBuilder fileStr)
    {
        if (!file.Name.EndsWith("Client.ts"))
            return;

        fileStr
            .AppendLine($"export interface {service.Name}Options {{ baseUrl: string }}")
            .AppendLine()
            .AppendLine($"export class {service.Name} {{")
            .AppendLine("    public constructor(private options: "+service.Name+"Options) {}")
            .AppendLine();

        for (int endpointIndex = 0; endpointIndex < service.Endpoints.Count; endpointIndex++)
        {
            BuiltEndpoint endpoint = service.Endpoints[endpointIndex];

            fileStr
                .AppendLine($"    /** {endpoint.Node.Description} */")
                .AppendLine($"    public async {endpoint.Name}(request: {endpoint.RequestType.Name}): Promise<{endpoint.ResponseType.Name}> {{")
                .AppendLine("        const url = this.options.baseUrl + \"" + service.Node.Path + "\" + \"" + endpoint.Node.Path + "\";")
                .AppendLine("        const bodyBytes = new TextEncoder().encode(JSON.stringify(request));")
                .AppendLine("        const res = await fetch(url, { method: '" + endpoint.Node.Method.ToString().ToUpper() + "', headers: { 'Content-Type': 'application/json; charset=utf-8' }, body: bodyBytes } as RequestInit);")
                .AppendLine("        if (!res.ok) throw new Error('HTTP ' + res.status);")
                .AppendLine("        const bytes = new Uint8Array(await res.arrayBuffer());")
                .AppendLine("        const json = new TextDecoder().decode(bytes);")
                .AppendLine($"        return JSON.parse(json) as {endpoint.ResponseType.Name};")
                .AppendLine("    }");

            if (endpointIndex < service.Endpoints.Count - 1)
                fileStr.AppendLine();
        }

        fileStr.AppendLine("}");
    }
}

