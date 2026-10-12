using System.Text.RegularExpressions;

namespace GermanTutorBot;

public static partial class TextFold
{
    public static string Fold(string text) =>
        text.Trim().ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);

    public static string CanonForm(string text)
    {
        var folded = Fold(text);
        foreach (var pronoun in Pronouns)
        {
            if (folded.StartsWith(pronoun + " ", StringComparison.Ordinal))
            {
                folded = folded[(pronoun.Length + 1)..].TrimStart();
                break;
            }
        }

        return Letters().Replace(folded, "");
    }

    public static IEnumerable<string> LatinTokens(string text)
    {
        foreach (Match match in TokenRegex().Matches(Fold(text)))
            yield return match.Value;
    }

    private static readonly string[] Pronouns = ["ihr", "ich", "wir", "sie", "es", "du", "er"];

    [GeneratedRegex("[^a-z]+", RegexOptions.CultureInvariant)]
    private static partial Regex Letters();

    [GeneratedRegex("[a-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
