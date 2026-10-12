using System.Text.Json;

namespace GermanTutorBot.Services;

public sealed record AppConfig(string TelegramBotToken, string HuggingFaceToken, string HuggingFaceModel)
{
    public static AppConfig Load()
    {
        foreach (var path in new[]
                 {
                     Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                     Path.Combine(AppContext.BaseDirectory, ".env")
                 })
        {
            LoadDotEnv(path);
        }

        var telegram = FirstNonEmpty("TelegramBotToken", "TELEGRAM_BOT_TOKEN")
            ?? throw new InvalidOperationException(
                "Не знайдено TelegramBotToken. Візьми його в @BotFather командою /newbot " +
                "і впиши в appsettings.json або у змінну середовища.");

        var huggingFace = FirstNonEmpty("HfApiToken", "HF_TOKEN", "HUGGINGFACE_TOKEN")
            ?? throw new InvalidOperationException(
                "Не знайдено HfApiToken. Безкоштовний токен створюється на https://huggingface.co/settings/tokens " +
                "після реєстрації. Перед цим відкрий https://huggingface.co/google/gemma-4-31B-it і прийми ліцензію.");

        var model = FirstNonEmpty("HuggingFaceModel", "HF_MODEL") ?? TutorAiService.DefaultModel;
        return new AppConfig(telegram, huggingFace, model);
    }

    private static string? FirstNonEmpty(params string[] keys)
    {
        foreach (var key in keys)
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(key);
            if (!IsPlaceholder(fromEnvironment))
                return fromEnvironment!.Trim();
        }

        foreach (var path in JsonPaths())
        {
            if (!File.Exists(path))
                continue;

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var key in keys)
            {
                if (document.RootElement.TryGetProperty(key, out var property) &&
                    property.ValueKind == JsonValueKind.String &&
                    !IsPlaceholder(property.GetString()))
                {
                    return property.GetString()!.Trim();
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> JsonPaths()
    {
        yield return Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        yield return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    private static void LoadDotEnv(string path)
    {
        if (!File.Exists(path))
            return;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"');
            if (IsPlaceholder(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
    }
}
