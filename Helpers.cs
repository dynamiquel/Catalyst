using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

public static class Helpers
{
    [return: NotNullIfNotNull(nameof(input))]
    public static string? ToPascalCase(this string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var segments = input.Split(new[] { '.' }, StringSplitOptions.None)
            .Select(ProcessSegment)
            .ToArray();

        return string.Join(".", segments);
    }

    private static string ProcessSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment))
            return segment;

        // Split camelCase and replace non-alphanumerics with spaces
        string spaced = Regex.Replace(segment, @"([a-z0-9])([A-Z])", "$1 $2");
        spaced = Regex.Replace(spaced, @"[^a-zA-Z0-9]+", " ");

        var words = spaced.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        var capitalizedWords = words.Select(CapitalizeFirstLetter);
        
        return string.Concat(capitalizedWords);
    }

    private static string CapitalizeFirstLetter(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        return char.ToUpper(word[0]) + word[1..];
    }

    [return: NotNullIfNotNull(nameof(filePath))]
    public static string? FilePathToPascalCase(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return filePath;
        
        IEnumerable<string> split = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(ToPascalCase)!;
        return string.Join("/", split);
    }
    
    private static readonly Regex TimespanRegex = new(
        @"^\s*(?:(\d+(?:\.\d+)?)y)?\s*(?:(\d+(?:\.\d+)?)w)?\s*(?:(\d+(?:\.\d+)?)d)?\s*(?:(\d+(?:\.\d+)?)h)?\s*(?:(\d+(?:\.\d+)?)m)?\s*(?:(\d+(?:\.\d+)?)s)?\s*(?:(\d+(?:\.\d+)?)ms)?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool ContainsTimeUnits(string value) =>
        value.Contains('y') || value.Contains('w') || value.Contains('d') ||
        value.Contains('h') || value.Contains('m') || value.Contains('s');

    public static double ParseTimespan(string value)
    {
        Match match = TimespanRegex.Match(value.Trim());
        if (!match.Success)
            return double.Parse(value);

        double totalSeconds = 0;

        if (match.Groups[1].Success && double.TryParse(match.Groups[1].Value, out var years))
            totalSeconds += years * 31536000;

        if (match.Groups[2].Success && double.TryParse(match.Groups[2].Value, out var weeks))
            totalSeconds += weeks * 604800;

        if (match.Groups[3].Success && double.TryParse(match.Groups[3].Value, out var days))
            totalSeconds += days * 86400;

        if (match.Groups[4].Success && double.TryParse(match.Groups[4].Value, out var hours))
            totalSeconds += hours * 3600;

        if (match.Groups[5].Success && double.TryParse(match.Groups[5].Value, out var minutes))
            totalSeconds += minutes * 60;

        if (match.Groups[6].Success && double.TryParse(match.Groups[6].Value, out var seconds))
            totalSeconds += seconds;

        if (match.Groups[7].Success && double.TryParse(match.Groups[7].Value, out var ms))
            totalSeconds += ms / 1000.0;

        return totalSeconds;
    }
}