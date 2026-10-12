using System.Text.RegularExpressions;

namespace GermanTutorBot.Data;

public enum VerbClass
{
    Weak,
    Strong,
    Irregular
}

public sealed class VerbCard
{
    public required string Infinitive { get; init; }
    public required string Level { get; init; }
    public required VerbClass Class { get; init; }
    public string? Stem { get; init; }
    public string? ChangedStem { get; init; }
    public bool VowelChanges { get; init; }
    public bool NeedsLinkingE { get; init; }
    public bool HasStandardEndings { get; init; }
    public required IReadOnlyDictionary<string, string> Forms { get; init; }
    public required IReadOnlyDictionary<string, string> Endings { get; init; }
}

public static partial class VerbCatalog
{
    public static readonly string[] QuizPronouns = ["ich", "du", "er", "wir", "ihr"];

    public static IReadOnlyList<VerbCard> All { get; } =
    [
        Weak("machen", "A1", "mach"),
        Weak("lernen", "A1", "lern"),
        Weak("wohnen", "A1", "wohn"),
        Weak("spielen", "A1", "spiel"),
        Weak("kaufen", "A1", "kauf"),
        Weak("arbeiten", "A1", "arbeit", linking: true),
        Weak("antworten", "A1", "antwort", linking: true),
        Weak("warten", "A1", "wart", linking: true),
        Strong("sprechen", "A1", "sprech", "sprich", "sprichst", "spricht", "sprecht"),
        Strong("fahren", "A1", "fahr", "fähr", "fährst", "fährt", "fahrt"),
        Strong("schlafen", "A1", "schlaf", "schläf", "schläfst", "schläft", "schlaft"),
        Strong("geben", "A1", "geb", "gib", "gibst", "gibt", "gebt"),
        Strong("sehen", "A1", "seh", "sieh", "siehst", "sieht", "seht"),
        Strong("lesen", "A1", "les", "lies", "liest", "liest", "lest"),
        Strong("essen", "A1", "ess", "iss", "isst", "isst", "esst"),
        Strong("helfen", "A1", "helf", "hilf", "hilfst", "hilft", "helft"),
        Strong("tragen", "A2", "trag", "träg", "trägst", "trägt", "tragt"),
        Strong("laufen", "A2", "lauf", "läuf", "läufst", "läuft", "lauft"),
        Strong("treffen", "A2", "treff", "triff", "triffst", "trifft", "trefft"),
        Irregular("sein", "A1", stem: null, "bin", "bist", "ist", "sind", "seid"),
        Irregular("haben", "A1", "hab", "habe", "hast", "hat", "haben", "habt"),
        Irregular("werden", "A1", "werd", "werde", "wirst", "wird", "werden", "werdet"),
        Irregular("können", "A1", "könn", "kann", "kannst", "kann", "können", "könnt"),
        Irregular("müssen", "A1", "müss", "muss", "musst", "muss", "müssen", "müsst")
    ];

    public static bool IsQuizPronoun(string text) =>
        QuizPronouns.Contains(text.Trim(), StringComparer.OrdinalIgnoreCase);

    public static VerbCard? Find(string text)
    {
        var token = Latin().Match(TextFold.Fold(text));
        if (!token.Success)
            return null;

        return All.FirstOrDefault(verb => TextFold.Fold(verb.Infinitive) == token.Value);
    }

    public static void Validate()
    {
        var infinitives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in All)
        {
            if (!infinitives.Add(card.Infinitive))
                throw new InvalidOperationException($"Дубль інфінітива: {card.Infinitive}");

            foreach (var pronoun in QuizPronouns)
            {
                if (!card.Forms.TryGetValue(pronoun, out var form) || string.IsNullOrWhiteSpace(form))
                    throw new InvalidOperationException($"{card.Infinitive}: немає форми для {pronoun}");
            }

            if (!card.Forms.TryGetValue("es", out var es) || es != card.Forms["er"])
                throw new InvalidOperationException($"{card.Infinitive}: es має збігатися з er");

            if (!card.Forms.TryGetValue("Sie", out var sieFormal) || sieFormal != card.Forms["wir"])
                throw new InvalidOperationException($"{card.Infinitive}: Sie має збігатися з wir");

            if (card.VowelChanges != (card.ChangedStem is not null))
                throw new InvalidOperationException($"{card.Infinitive}: зміна голосної не узгоджена з основою");

            if (card.Class == VerbClass.Weak)
                ValidateWeak(card);
            else if (card.Class == VerbClass.Strong)
                ValidateStrong(card);
            else
                ValidateIrregular(card);
        }

        Expect("machen", "du", "machst");
        Expect("arbeiten", "du", "arbeitest");
        Expect("arbeiten", "er", "arbeitet");
        Expect("antworten", "ihr", "antwortet");
        Expect("sprechen", "ich", "spreche");
        Expect("sprechen", "du", "sprichst");
        Expect("sprechen", "er", "spricht");
        Expect("sprechen", "ihr", "sprecht");
        Expect("geben", "du", "gibst");
        Expect("geben", "er", "gibt");
        Expect("sehen", "du", "siehst");
        Expect("sehen", "er", "sieht");
        Expect("sehen", "ihr", "seht");
        Expect("lesen", "du", "liest");
        Expect("lesen", "er", "liest");
        Expect("essen", "du", "isst");
        Expect("essen", "er", "isst");
        Expect("essen", "ihr", "esst");
        Expect("fahren", "du", "fährst");
        Expect("schlafen", "er", "schläft");
        Expect("laufen", "du", "läufst");
        Expect("tragen", "ihr", "tragt");
        Expect("treffen", "du", "triffst");
        Expect("treffen", "er", "trifft");
        Expect("helfen", "du", "hilfst");
        Expect("helfen", "er", "hilft");
        Expect("sein", "ich", "bin");
        Expect("sein", "du", "bist");
        Expect("sein", "er", "ist");
        Expect("sein", "wir", "sind");
        Expect("sein", "ihr", "seid");
        Expect("haben", "ich", "habe");
        Expect("haben", "du", "hast");
        Expect("haben", "er", "hat");
        Expect("haben", "ihr", "habt");
        Expect("werden", "du", "wirst");
        Expect("werden", "er", "wird");
        Expect("werden", "ihr", "werdet");
        Expect("können", "ich", "kann");
        Expect("können", "du", "kannst");
        Expect("können", "ihr", "könnt");
        Expect("müssen", "du", "musst");
        Expect("müssen", "ihr", "müsst");
    }

    private static void ValidateWeak(VerbCard card)
    {
        if (card.Stem is null || card.HasStandardEndings == false || card.VowelChanges)
            throw new InvalidOperationException($"{card.Infinitive}: слабке дієслово зібране неправильно");

        var stem = card.Stem;
        if (card.Infinitive != stem + "en")
            throw new InvalidOperationException($"{card.Infinitive}: інфінітив не дорівнює основі + en");

        Require(card, "ich", stem + "e");
        Require(card, "wir", card.Infinitive);
        if (card.NeedsLinkingE)
        {
            if (!stem.EndsWith('t') && !stem.EndsWith('d'))
                throw new InvalidOperationException($"{card.Infinitive}: linking-e лише після t/d");

            Require(card, "du", stem + "est");
            Require(card, "er", stem + "et");
            Require(card, "ihr", stem + "et");
        }
        else
        {
            if (stem.EndsWith('t') || stem.EndsWith('d'))
                throw new InvalidOperationException($"{card.Infinitive}: основа на t/d потребує linking-e");

            Require(card, "du", stem + "st");
            Require(card, "er", stem + "t");
            Require(card, "ihr", stem + "t");
        }
    }

    private static void ValidateStrong(VerbCard card)
    {
        if (card.Stem is null || card.ChangedStem is null || !card.VowelChanges || !card.HasStandardEndings)
            throw new InvalidOperationException($"{card.Infinitive}: сильне дієслово зібране неправильно");

        if (card.Infinitive != card.Stem + "en")
            throw new InvalidOperationException($"{card.Infinitive}: інфінітив не дорівнює основі + en");

        Require(card, "ich", card.Stem + "e");
        Require(card, "wir", card.Infinitive);

        var du = card.Forms["du"];
        var er = card.Forms["er"];
        if (du == card.Stem + "st" || er == card.Stem + "t")
            throw new InvalidOperationException($"{card.Infinitive}: у du/er немає зміни голосної");

        if (!du.StartsWith(card.ChangedStem, StringComparison.Ordinal) ||
            !er.StartsWith(card.ChangedStem, StringComparison.Ordinal))
            throw new InvalidOperationException($"{card.Infinitive}: du/er не починаються зі зміненої основи");

        if (card.Forms["ihr"].StartsWith(card.ChangedStem, StringComparison.Ordinal))
            throw new InvalidOperationException($"{card.Infinitive}: ihr не має змінювати голосну");
    }

    private static void ValidateIrregular(VerbCard card)
    {
        if (card.HasStandardEndings || card.Endings.Count != 0 || card.VowelChanges)
            throw new InvalidOperationException($"{card.Infinitive}: неправильне дієслово зібране неправильно");
    }

    private static void Require(VerbCard card, string pronoun, string expected)
    {
        if (card.Forms[pronoun] != expected)
            throw new InvalidOperationException($"{card.Infinitive}/{pronoun}: очікувалось {expected}, є {card.Forms[pronoun]}");
    }

    private static void Expect(string infinitive, string pronoun, string form)
    {
        var card = Find(infinitive) ?? throw new InvalidOperationException($"Немає {infinitive}");
        if (card.Forms[pronoun] != form)
            throw new InvalidOperationException($"{infinitive}/{pronoun}: у довіднику {card.Forms[pronoun]}, контроль {form}");
    }

    private static VerbCard Weak(string infinitive, string level, string stem, bool linking = false)
    {
        var endings = linking
            ? EndingsOf("e", "est", "et", "en", "et")
            : EndingsOf("e", "st", "t", "en", "t");

        return new VerbCard
        {
            Infinitive = infinitive,
            Level = level,
            Class = VerbClass.Weak,
            Stem = stem,
            NeedsLinkingE = linking,
            HasStandardEndings = true,
            Endings = endings,
            Forms = new Dictionary<string, string>
            {
                ["ich"] = stem + endings["ich"],
                ["du"] = stem + endings["du"],
                ["er"] = stem + endings["er"],
                ["es"] = stem + endings["er"],
                ["wir"] = infinitive,
                ["ihr"] = stem + endings["ihr"],
                ["Sie"] = infinitive
            }
        };
    }

    private static VerbCard Strong(
        string infinitive,
        string level,
        string stem,
        string changedStem,
        string du,
        string er,
        string ihr) =>
        new()
        {
            Infinitive = infinitive,
            Level = level,
            Class = VerbClass.Strong,
            Stem = stem,
            ChangedStem = changedStem,
            VowelChanges = true,
            HasStandardEndings = true,
            Endings = EndingsOf("e", "st", "t", "en", "t"),
            Forms = new Dictionary<string, string>
            {
                ["ich"] = stem + "e",
                ["du"] = du,
                ["er"] = er,
                ["es"] = er,
                ["wir"] = infinitive,
                ["ihr"] = ihr,
                ["Sie"] = infinitive
            }
        };

    private static VerbCard Irregular(
        string infinitive,
        string level,
        string? stem,
        string ich,
        string du,
        string er,
        string wir,
        string ihr) =>
        new()
        {
            Infinitive = infinitive,
            Level = level,
            Class = VerbClass.Irregular,
            Stem = stem,
            HasStandardEndings = false,
            Endings = new Dictionary<string, string>(),
            Forms = new Dictionary<string, string>
            {
                ["ich"] = ich,
                ["du"] = du,
                ["er"] = er,
                ["es"] = er,
                ["wir"] = wir,
                ["ihr"] = ihr,
                ["Sie"] = wir
            }
        };

    private static Dictionary<string, string> EndingsOf(string ich, string du, string er, string wir, string ihr) =>
        new()
        {
            ["ich"] = ich,
            ["du"] = du,
            ["er"] = er,
            ["wir"] = wir,
            ["ihr"] = ihr
        };

    [GeneratedRegex("[a-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Latin();
}
