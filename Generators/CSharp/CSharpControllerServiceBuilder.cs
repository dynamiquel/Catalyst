using System.Text;
using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;
using HttpMethod = Catalyst.SpecGraph.Nodes.HttpMethod;

namespace Catalyst.Generators.CSharp;

public class CSharpControllerServiceBuilder : IServerServiceBuilder<CSharpCompiler>
{
    public string Name => "default;controller";
    public required CSharpCompiler Compiler { get; init; }

    public string GetBuiltFileName(BuildContext context, ServiceNode serviceNode)
    {
        // One file for all services.
        return StringExtensions.FilePathToPascalCase(context.FileNode.FilePath) + "ControllerBase.cs";
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
            Name: Compiler.GetCompiledClassName(serviceNode.Name),
            Endpoints: endpoints
        );

        BuiltFile file = context.GetOrAddFile(Compiler, GetBuiltFileName(context, serviceNode));
        file.Services.Add(service);
        file.Includes.Add(new("Microsoft.AspNetCore.Mvc"));
    }
    
    public void Compile(BuiltFile file, BuiltService service, StringBuilder fileStr)
    {
        // TODO: Remove this Compile stage all together and make it part of the Build stage.

        // Hackyyyyy.
        if (!file.Name.EndsWith("ControllerBase.cs"))
            return;
        
        Compiler.AppendDescriptionComment(fileStr, service.Node);
        
        // Generate Server Controller
        fileStr
            .AppendLine("[ApiController]")
            .AppendLine($"[Route(\"{service.Node.Path.TrimStart('/')}\")]")
            .AppendLine($"public abstract class {service.Name}ControllerBase : ControllerBase")
            .AppendLine("{");

        for (int endpointIndex = 0; endpointIndex < service.Endpoints.Count; endpointIndex++)
        {
            BuiltEndpoint endpoint = service.Endpoints[endpointIndex];
            
            string httpMethodAttribute = endpoint.Node.Method switch
            {
                HttpMethod.Get => "HttpGet",
                HttpMethod.Post => "HttpPost",
                HttpMethod.Put => "HttpPut",
                HttpMethod.Patch => "HttpPatch",
                HttpMethod.Delete => "HttpDelete",
                HttpMethod.Options => "HttpOptions",
                HttpMethod.Trace => "HttpTrace",
                _ => throw new ArgumentOutOfRangeException()
            };

            Compiler.AppendDescriptionComment(fileStr, endpoint.Node, 1);
            
            fileStr
                .AppendLine($"    [{httpMethodAttribute}(\"{endpoint.Node.Path.TrimStart('/')}\", Name = \"{service.Name}{endpoint.Name}\")]")
                .AppendLine($"    public abstract Task<ActionResult<{endpoint.ResponseType.Name}>> {endpoint.Name.ToPascalCase()}({endpoint.RequestType.Name} request);");
                
                if (endpointIndex < service.Endpoints.Count - 1)
                    fileStr.AppendLine();
        }

        fileStr.AppendLine("}");
    }
}