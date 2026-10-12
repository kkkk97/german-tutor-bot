using System.Text.RegularExpressions;
using GermanTutorBot.Data;

namespace GermanTutorBot.Services;

public static partial class AnswerGuard
{
    public const string LastResort =
        "Я не можу показати цю репліку, бо в ній була б готова форма. Продовжуй поточний крок або надішли /practice.";

    private static readonly HashSet<string> Allowed = BuildAllowed();

    public static bool Leaks(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return false;

        if (TranslationLeak().IsMatch(reply))
            return true;

        var folded = TextFold.Fold(reply);
        if (ShortEnding().IsMatch(folded))
            return true;

        foreach (var token in TextFold.LatinTokens(folded))
        {
            if (token.Length >= 3 && !Allowed.Contains(token))
                return true;
        }

        return false;
    }

    public static string Sanitize(string? reply, string fallback)
    {
        if (!Leaks(reply) && !string.IsNullOrWhiteSpace(reply))
            return reply.Trim();

        if (!Leaks(fallback))
            return fallback.Trim();

        return LastResort;
    }

    private static HashSet<string> BuildAllowed()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "ich", "wir", "ihr", "sie",
            "praesens", "presens",
            "practice", "reset", "help", "start", "status", "types"
        };

        foreach (var verb in VerbCatalog.All)
            allowed.Add(TextFold.Fold(verb.Infinitive));

        return allowed;
    }

    [GeneratedRegex(@"(?<![a-z])-?(st|et)(?![a-z])", RegexOptions.CultureInvariant)]
    private static partial Regex ShortEnding();

    [GeneratedRegex(@"переклад\w*\s*[:—-]\s*\S|перекладається|правильн\w+\s+форм\w*\s*[:—-]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TranslationLeak();
}
