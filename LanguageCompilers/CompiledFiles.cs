namespace Catalyst.LanguageCompilers;

public record CompiledFile(string FileName, string FileContents);

public class CompiledFiles
{
    List<CompiledFile> Files { get; set; } = [];

    public void AddFile(CompiledFile file)
    {
        if (Files.Any(f => f.FileName == file.FileName))
            throw new Exception($"File {file.FileName} already exists");
        
        Files.Add(file);
    }

    public async Task OutputFiles(DirectoryInfo outputDir, CancellationToken cancelToken = default)
    {
        foreach (CompiledFile file in Files)
        {
            string outputFilePath = Path.Combine(outputDir.FullName, file.FileName);
            string? outputFileDir = Path.GetDirectoryName(outputFilePath);
            if (outputFileDir is not null)
                Directory.CreateDirectory(outputFileDir);
            
            await File.WriteAllTextAsync(outputFilePath, file.FileContents, cancelToken);
        }
    }
}