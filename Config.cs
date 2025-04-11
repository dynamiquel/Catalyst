namespace Catalyst;

public class Config
{
    public string BaseInputDir { get; set; } = Directory.GetCurrentDirectory();
    public List<string> Files { get; set; } = ["*.yaml"];
    public string BaseOutputDir { get; set; } = "";
    public required string Language { get; set; }
    public bool Client { get; set; } = true;
    public bool Server { get; set; } = false;
}