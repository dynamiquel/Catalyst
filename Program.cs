// See https://aka.ms/new-console-template for more information

using Catalyst;
using Catalyst.LanguageCompilers;
using Catalyst.LanguageCompilers.CSharp;
using Catalyst.SpecGraph;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecReader;
using Microsoft.Extensions.Configuration;

Console.WriteLine("Running Catalyst!");

Config config = ReadConfiguration();

if (string.IsNullOrWhiteSpace(config.Language))
    throw new Exception("Language not set!");

DirectoryInfo baseInputDir = new DirectoryInfo(config.BaseInputDir);
DirectoryInfo baseOutputDir = new DirectoryInfo(config.BaseOutputDir);

Console.WriteLine($"BaseInputDir: {baseInputDir.FullName}");
Console.WriteLine($"BaseOutputDir: {baseOutputDir.FullName}");

FileInfo[] inputFiles = GetInputFiles();

if (inputFiles.Length == 0)
{
    Console.WriteLine("No input files found");
    return 0;
}

var graph = new Graph();
var specFileReader = new FileReader();

await ReadSpecFilesRecursive(inputFiles);

Console.WriteLine("Spec Graph created");
Console.WriteLine(graph);

graph.Build();

Console.WriteLine("Spec Graph built");
Console.WriteLine(graph);

LanguageCompiler compiler;
switch (config.Language)
{
    case "cs":
        compiler = new CSharpLanguageCompiler();
        break;
    default:
        throw new InvalidOperationException($"Language {config.Language} is not supported");
}

CompiledFiles compiledFiles = new();
foreach (FileNode fileNode in graph.Files)
{
    LanguageCompiler.File file = compiler.CreateFile(fileNode);
    compiler.BuildFile(file, fileNode);
    CompiledFile compiledFile = compiler.CompileFile(file);
    compiledFiles.AddFile(compiledFile);
}

await compiledFiles.OutputFiles(baseInputDir, baseOutputDir);

return 0;

Config ReadConfiguration()
{
    Console.WriteLine("Reading configuration");
    
    IConfiguration configuration = new ConfigurationBuilder()
        .AddJsonFile("config.json", optional: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args)
        .Build();

    var foundConfig = configuration.Get<Config>();
    
    if (foundConfig is null)
        throw new NullReferenceException("Could not deserialise configuration");
    
    if (string.IsNullOrEmpty(foundConfig.BaseOutputDir))
        foundConfig.BaseOutputDir = Path.Combine(foundConfig.BaseInputDir, "output");
    
    Console.WriteLine("Read configuration");
    
    return foundConfig;
}

FileInfo[] GetInputFiles()
{
    HashSet<FileInfo> files = [];
    foreach (string fileFilter in config.Files)
    {
        IEnumerable<FileInfo> filesMatchingFilter = baseInputDir.EnumerateFiles(fileFilter, SearchOption.AllDirectories);
        foreach (var file in filesMatchingFilter)
        {
            if (file.Extension != ".yaml" && file.Extension != ".yml")
                continue;
            
            files.Add(file);
        }
    }

    return files.ToArray();
}

async Task ReadSpecFilesRecursive(FileInfo[] specFiles)
{
    IEnumerable<Task<RawFileNode>> readSpecFilesTask = specFiles.Select(file => specFileReader.ReadRawSpec(file));
    RawFileNode[] rawSpecNodes = await Task.WhenAll(readSpecFilesTask);
    FileNode[] fileNodes = rawSpecNodes.Select(raw => specFileReader.ReadFileFromSpec(raw)).ToArray();

    foreach (FileNode fileNode in fileNodes)
        graph.AddFileNode(fileNode);

    HashSet<FileInfo> includeFiles = [];
    foreach (FileNode fileNode in fileNodes)
    {
        foreach (string includeSpec in fileNode.IncludeSpecs)
        {
            string includeSpecPath = Path.Combine(fileNode.FileInfo.DirectoryName!, includeSpec);
            FileInfo fileInfo = new FileInfo(includeSpecPath);
            if (graph.Files.Any(f => f.Name == fileInfo.FullName))
                continue;
            
            includeFiles.Add(fileInfo);
        }
    }

    if (includeFiles.Count != 0)
        await ReadSpecFilesRecursive(includeFiles.ToArray());
}