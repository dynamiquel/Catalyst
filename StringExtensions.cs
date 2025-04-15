using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

public static class StringExtensions
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

        return char.ToUpper(word[0]) + word.Substring(1);
    }
}