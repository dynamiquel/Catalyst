using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Catalyst;
using Catalyst.Generators;
using Catalyst.Generators.CSharp;
using Catalyst.Generators.Unreal;
using Catalyst.Generators.TypeScript;
using Catalyst.SpecGraph;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecReader;

Config config = ReadConfiguration();

ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(config.LogLevel);
});

ILogger<Program> logger = loggerFactory.CreateLogger<Program>();

logger.LogInformation("Starting Catalyst");

if (string.IsNullOrWhiteSpace(config.Language))
    throw new Exception("Language not set!");

DirectoryInfo baseInputDir = new DirectoryInfo(config.BaseInputDir);
DirectoryInfo baseOutputDir = new DirectoryInfo(config.BaseOutputDir);

logger.LogInformation("BaseInputDir: {BaseInputDir}", baseInputDir.FullName);
logger.LogInformation("BaseOutputDir: {BaseOutputDir}", baseOutputDir.FullName);

CompilerOptions compilerOptions = new(
    EnumBuilderName: config.EnumBuilder,
    DefinitionBuilderName: config.DefinitionBuilder,
    ClientServiceBuilderName: config.ClientBuilder,
    ServerServiceBuilderName: config.ServerBuilder);

FileInfo[] inputFiles = GetInputFiles();

if (inputFiles.Length == 0)
{
    logger.LogWarning("No input files found");
    return 0;
}

FileReader specFileReader = new()
{
    BaseDir = baseInputDir,
    Logger = loggerFactory.CreateLogger<FileReader>()
};
specFileReader.AddGeneratorOptionsReader<CSharpOptionsReader>();
specFileReader.AddGeneratorOptionsReader<UnrealOptionsReader>();
specFileReader.AddGeneratorOptionsReader<TypeScriptOptionsReader>();

Graph graph = new()
{
    Logger = loggerFactory.CreateLogger<Graph>()
};

await ReadSpecFilesRecursive(inputFiles);

logger.LogInformation("Spec Graph created");
logger.LogDebug("{Graph}", graph);

graph.Build();

logger.LogInformation("Spec Graph built");
logger.LogDebug("{Graph}", graph);

if (graph.Files.Count == 0)
    return 0;

ILogger<Compiler> compilerLogger = loggerFactory.CreateLogger<Compiler>();
Compiler compiler;

if (config.Language == CSharp.Name)
    compiler = new CSharpCompiler(compilerOptions) { Logger = compilerLogger };
else if (config.Language == Unreal.Name)
    compiler = new UnrealCompiler(compilerOptions) { Logger = compilerLogger };
else if (config.Language == TypeScript.Name)
    compiler = new TypeScriptCompiler(compilerOptions) { Logger = compilerLogger };
else
    throw new InvalidOperationException($"Language {config.Language} is not supported");

compilerLogger.LogInformation("Building Spec Graph using {CompilerName} ({FileCount} files)...", compiler.GetType().Name, graph.Files.Count);

List<BuiltFile> builtFiles = [];
for (var fileNodeIdx = 0; fileNodeIdx < graph.Files.Count; fileNodeIdx++)
{
    FileNode fileNode = graph.Files[fileNodeIdx];
    compilerLogger.LogInformation("[{CurrentIndex}] Building Spec File '{FileName}'...", fileNodeIdx + 1, fileNode.FullName);

    BuiltFile[] builtFilesForFile = compiler.Build(fileNode).ToArray();
    builtFiles.AddRange(builtFilesForFile);
    
    StringBuilder sb = new($"[{fileNodeIdx + 1}] Built Spec File '{fileNode.FullName}' into files: ");
    foreach (BuiltFile builtFile in builtFilesForFile)
        sb.Append($"'{builtFile.Name}' ");

    compilerLogger.LogInformation("{Message}", sb.ToString());
}

if (builtFiles.Count == 0)
    throw new InvalidOperationException("No files were built. Something has gone wrong");

compilerLogger.LogInformation("Compiling Spec Graph using {CompilerName} ({FileCount} files)...", compiler.GetType().Name, builtFiles.Count);

CompiledFiles compiledFiles = new();
for (var builtFileIdx = 0; builtFileIdx < builtFiles.Count; builtFileIdx++)
{
    BuiltFile builtFile = builtFiles[builtFileIdx];
    
    compilerLogger.LogInformation("[{CurrentIndex}] Compiling Built File '{FileName}'...", builtFileIdx + 1, builtFile.Name);

    CompiledFile compiledFile = compiler.Compile(builtFile);
    compiledFiles.AddFile(compiledFile);
    
    compilerLogger.LogInformation("[{CurrentIndex}] Compiled Built File '{FileName}'", builtFileIdx + 1, builtFile.Name);
    compilerLogger.LogDebug("':{FileContents}", compiledFile.FileContents);
}

await compiledFiles.OutputFiles(baseOutputDir);

logger.LogInformation("Catalyst completed successfully");

return 0;

Config ReadConfiguration()
{
    IConfiguration configuration = new ConfigurationBuilder()
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
