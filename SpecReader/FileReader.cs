using System.Text.Json;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;
using YamlDotNet.Serialization;

namespace Catalyst.SpecReader;

public class FileReader
{
    public async Task<RawFileNode> ReadRawSpec(FileInfo specFileInfo)
    {
        var fileContent = await File.ReadAllTextAsync(specFileInfo.FullName);
        var deserializer = new DeserializerBuilder().Build();

        // Yaml will deserialise the object as a Dictionary<string, object> if no type specified.
        var deserialisedObject = deserializer.Deserialize(fileContent) as Dictionary<object, object>;
        if (deserialisedObject is null)
            throw new Exception($"Could not deserialise spec file at {specFileInfo.FullName}");
        
        return new RawFileNode(specFileInfo, deserialisedObject);
    }

    public FileNode ReadFileFromSpec(RawFileNode rawNode)
    {
        Console.WriteLine($"[{rawNode.FileInfo.FullName}] Reading Spec File");
        
        FileNode fileNode = new()
        {
            Parent = null,
            FileInfo = rawNode.FileInfo,
            Name = rawNode.FileName
        };
        
        string? format = rawNode.ReadPropertyAsStr("format");
        if (format is not null)
            fileNode.Format = format;
        
        fileNode.Namespace = rawNode.ReadPropertyAsStr("namespace");
        
        ReadIncludes(rawNode, fileNode);
        ReadDefinitions(rawNode, fileNode);

        return fileNode;
    }

    void ReadIncludes(RawFileNode rawNode, FileNode fileNode)
    {
        List<object>? includeSpecs = rawNode.ReadPropertyAsList("includes");
        if (includeSpecs is null) 
            return;
        
        Console.WriteLine($"[{fileNode.FullName}] Found {includeSpecs.Count} Include Specs");
        
        foreach (var includeSpec in includeSpecs)
        {
            var includeSpecStr = includeSpec as string;
            if (includeSpecStr is null)
            {
                throw new UnexpectedTypeException
                {
                    RawNode = rawNode,
                    LeafName = "includes",
                    ExpectedType = nameof(String),
                    ReceivedType = includeSpec.GetType().Name
                };
            }
            
            if (!Path.HasExtension(includeSpecStr))
                includeSpecStr += ".yaml";
            
            string relativeIncludePath = Path.Combine(fileNode.FileInfo.DirectoryName ?? throw new InvalidOperationException(), includeSpecStr);
            
            if (!File.Exists(relativeIncludePath))
            {
                throw new IncludeNotFoundException
                {
                    RawNode = rawNode,
                    LeafName = "includes",
                    IncludeFile = $"{includeSpecStr} (Full Path: {relativeIncludePath}"
                };
            }
            
            fileNode.IncludeSpecs.Add(includeSpecStr);
        }
    }
    
    void ReadDefinitions(RawFileNode rawNode, FileNode fileNode)
    {
        Console.WriteLine($"[{fileNode.FullName}] Reading Definitions");

        Dictionary<object, object>? definitions = rawNode.ReadPropertyAsDictionary("definitions");
        if (definitions is null) 
            return;
        
        Console.WriteLine($"[{fileNode.FullName}] Found {definitions.Count} Definitions");
        
        RawNode definitionsRawNode = rawNode.CreateChild(definitions, "definitions");
            
        foreach (var definition in definitions)
        {
            string definitionName = ((string)definition.Key);
            Dictionary<object, object>? definitionValue = definition.Value as Dictionary<object, object>;
            if (definitionValue is null)
            {
                throw new UnexpectedTypeException
                {
                    RawNode = definitionsRawNode,
                    LeafName = definitionName,
                    ExpectedType = nameof(Dictionary<object, object>),
                    ReceivedType = definition.Value.GetType().Name
                };
            }

            RawNode definitionRawNode = definitionsRawNode.CreateChild(definitionValue, definitionName);
            ReadDefinition(fileNode, definitionName, definitionRawNode);
        }
    }

    void ReadDefinition(FileNode fileNode, string definitionName, RawNode definitionRawNode)
    {
        DefinitionNode definitionNode = new()
        {
            Parent = fileNode,
            Name = definitionName,
            Description = definitionRawNode.ReadPropertyAsStr("description")
        };
        
        Console.WriteLine($"[{fileNode.FullName}] Reading Definition '{definitionNode.Name}'");
        
        Dictionary<object, object>? properties = definitionRawNode.ReadPropertyAsDictionary("properties");
        if (properties is null)
        {
            throw new ExpectedTokenNotFoundException
            {
                RawNode = definitionRawNode,
                TokenName = "properties"
            };
        }
        
        Console.WriteLine($"[{fileNode.FullName}] Found {properties.Count} Properties");
        
        RawNode propertiesRawNode = definitionRawNode.CreateChild(properties, "properties");
        
        foreach (var property in properties)
        {
            string propertyName = (string)property.Key;
            ReadProperty(definitionNode, propertiesRawNode, propertyName, property.Value);
        }
        
        fileNode.Definitions.Add(definitionName, definitionNode);
    }

    void ReadProperty(DefinitionNode definitionNode, RawNode propertiesRawNode, string propertyName, object propertyValue)
    {
        Console.WriteLine($"[{definitionNode.FullName}] Reading Property '{propertyName}'");

        // Short property declaration
        if (propertyValue is string propertyShortValue)
        {
            definitionNode.Properties.Add(propertyName, new PropertyNode
            {
                Parent = definitionNode,
                Name = propertyName,
                UnBuiltType = propertyShortValue
            });
        }
        else
        {
            Dictionary<object, object>? propertyFullValue = propertyValue as Dictionary<object, object>;
            if (propertyFullValue is null)
            {
                throw new UnexpectedTypeException
                {
                    RawNode = propertiesRawNode,
                    LeafName = propertyName,
                    ExpectedType = nameof(Dictionary<object, object>),
                    ReceivedType = propertyValue.GetType().Name
                };
            }
            
            RawNode propertyRawNode = propertiesRawNode.CreateChild(propertyFullValue, propertyName);
            
            var propertyType = propertyRawNode.ReadPropertyAsStr("type");
            if (string.IsNullOrWhiteSpace(propertyType))
            {
                throw new ExpectedTokenNotFoundException
                {
                    RawNode = propertyRawNode,
                    TokenName = "type"
                };
            }

            UnBuiltValue? unBuiltValue = null;
            object? propertyDefaultValue = propertyRawNode.Internal.GetValueOrDefault("default");
            if (propertyDefaultValue is not null)
            {
                unBuiltValue = new UnBuiltValue(JsonSerializer.Serialize(
                    propertyDefaultValue, 
                    new JsonSerializerOptions{ WriteIndented = true }));
            }
            
            definitionNode.Properties.Add(propertyName, new PropertyNode
            {
                Parent = definitionNode,
                Name = propertyName,
                UnBuiltType = propertyType,
                Description = propertyRawNode.ReadPropertyAsStr("description"),
                Value = unBuiltValue
            });
        }
    }
}