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

Console.WriteLine($"Building Spec Graph using {compiler.GetType().Name} [{graph.Files.Count}] files]...");

List<LanguageCompiler.File> builtFiles = [];
for (var fileNodeIdx = 0; fileNodeIdx < graph.Files.Count; fileNodeIdx++)
{
    FileNode fileNode = graph.Files[fileNodeIdx];
    Console.WriteLine($"[{fileNodeIdx + 1}] Building Spec File '{fileNode.FullName}'...");
    
    LanguageCompiler.File file = compiler.BuildFile(fileNode);
    builtFiles.Add(file);
    
    Console.WriteLine($"[{fileNodeIdx + 1}] Built Spec File '{fileNode.FullName}':\n{file}");
}

Console.WriteLine($"Compiling Spec Graph using {compiler.GetType().Name} [{graph.Files.Count}] files]...");

CompiledFiles compiledFiles = new();
for (var builtFileIdx = 0; builtFileIdx < builtFiles.Count; builtFileIdx++)
{
    LanguageCompiler.File builtFile = builtFiles[builtFileIdx];
    
    Console.WriteLine($"[{builtFileIdx + 1}] Compiling Built File '{builtFile.Name}'...");

    CompiledFile compiledFile = compiler.CompileFile(builtFile);
    compiledFiles.AddFile(compiledFile);
    
    Console.WriteLine($"[{builtFileIdx + 1}] Compiled Built File '{builtFile.Name}':\n{compiledFile.FileContents}");
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