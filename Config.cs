using Catalyst.Generators.Builders;

namespace Catalyst;

public class Config
{
    public string BaseInputDir { get; set; } = Directory.GetCurrentDirectory();
    public List<string> Files { get; set; } = ["*.yaml"];
    public string BaseOutputDir { get; set; } = "";
    public required string Language { get; set; }
    public bool Client { get; set; } = true;
    public bool Server { get; set; } = false;
    public string EnumBuilder { get; set; } = Builder.Default;
    public string DefinitionBuilder { get; set; } = Builder.Default;
    public string? ClientBuilder { get; set; } = Builder.Default;
    public string? ServerBuilder { get; set; } = Builder.Default;
}