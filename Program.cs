using System.Text;
using Catalyst;
using Catalyst.Generators;
using Catalyst.Generators.CSharp;
using Catalyst.Generators.Unreal;
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

CompilerOptions compilerOptions = new(
    EnumBuilderName: config.EnumBuilder,
    DefinitionBuilderName: config.DefinitionBuilder,
    ClientServiceBuilderName: config.ClientBuilder,
    ServerServiceBuilderName: config.ServerBuilder);

FileInfo[] inputFiles = GetInputFiles();

if (inputFiles.Length == 0)
{
    Console.WriteLine("No input files found");
    return 0;
}


Graph graph = new();
FileReader specFileReader = new()
{
    BaseDir = baseInputDir
};
specFileReader.AddGeneratorOptionsReader<CSharpOptionsReader>();
specFileReader.AddGeneratorOptionsReader<UnrealOptionsReader>();

await ReadSpecFilesRecursive(inputFiles);

Console.WriteLine("Spec Graph created");
Console.WriteLine(graph);

graph.Build();

Console.WriteLine("Spec Graph built");
Console.WriteLine(graph);

if (graph.Files.Count == 0)
    return 0;

Compiler compiler;
if (config.Language == CSharp.Name)
    compiler = new CSharpCompiler(compilerOptions);
else if (config.Language == Unreal.Name)
    compiler = new UnrealCompiler(compilerOptions);
else
    throw new InvalidOperationException($"Language {config.Language} is not supported");

Console.WriteLine($"Building Spec Graph using {compiler.GetType().Name} ({graph.Files.Count}] files)...");

List<BuiltFile> builtFiles = [];
for (var fileNodeIdx = 0; fileNodeIdx < graph.Files.Count; fileNodeIdx++)
{
    FileNode fileNode = graph.Files[fileNodeIdx];
    Console.WriteLine($"[{fileNodeIdx + 1}] Building Spec File '{fileNode.FullName}'...");
    
    IEnumerable<BuiltFile> builtFilesForFile = compiler.Build(fileNode);
    builtFiles.AddRange(builtFilesForFile);
    
    StringBuilder sb = new($"[{fileNodeIdx + 1}] Built Spec File '{fileNode.FullName}' into files: ");
    foreach (BuiltFile builtFile in builtFilesForFile)
        sb.Append($"'{builtFile.Name}' ");

    Console.WriteLine(sb.ToString());
}

if (builtFiles.Count == 0)
    throw new InvalidOperationException("No files were built. Something has gone wrong");

Console.WriteLine($"Compiling Spec Graph using {compiler.GetType().Name} ({builtFiles.Count} files)...");

CompiledFiles compiledFiles = new();
for (var builtFileIdx = 0; builtFileIdx < builtFiles.Count; builtFileIdx++)
{
    BuiltFile builtFile = builtFiles[builtFileIdx];
    
    Console.WriteLine($"[{builtFileIdx + 1}] Compiling Built File '{builtFile.Name}'...");

    CompiledFile compiledFile = compiler.Compile(builtFile);
    compiledFiles.AddFile(compiledFile);
    
    Console.WriteLine($"[{builtFileIdx + 1}] Compiled Built File '{builtFile.Name}':\n{compiledFile.FileContents}");
}

await compiledFiles.OutputFiles(baseOutputDir);

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

    // If any Server Builder other than default was specified, implicitly enable server generation.
    if (foundConfig.ServerBuilder is not null && !foundConfig.ServerBuilder.Equals("default"))
        foundConfig.Server = true;
    else if (foundConfig.Server is false)
        foundConfig.ServerBuilder = null;
    
    // If any Client Builder other than default was specified, implicitly enable client generation.
    if (foundConfig.ClientBuilder is not null && !foundConfig.ClientBuilder.Equals("default"))
        foundConfig.Client = true;
    else if (foundConfig.Client is false)
        foundConfig.ClientBuilder = null;
    
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
            if (file.Name.Contains("global") || (file.Extension != ".yaml" && file.Extension != ".yml"))
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
            string includeSpecPath = Path.Combine(baseInputDir.FullName, fileNode.Directory ?? string.Empty, includeSpec);
            FileInfo fileInfo = new FileInfo(includeSpecPath);
            string includeSpecBuiltFilePath = specFileReader.GetBuiltSpecFilePath(fileInfo);

            bool bFileAlreadyAdded = graph.Files.Any(f => f.FilePath == includeSpecBuiltFilePath);
            if (bFileAlreadyAdded)
                continue;
            
            includeFiles.Add(fileInfo);
        }
    }

    if (includeFiles.Count != 0)
        await ReadSpecFilesRecursive(includeFiles.ToArray());
}