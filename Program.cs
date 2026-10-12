using GermanTutorBot.Data;
using GermanTutorBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

if (args.Contains("--self-check"))
{
    SelfCheck.Run();
    return;
}

AppConfig settings;
try
{
    settings = AppConfig.Load();
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
    return;
}

using var cts = new CancellationTokenSource();
var bot = new TelegramBotClient(settings.TelegramBotToken);
var tutor = new TutorAiService(settings.HuggingFaceToken, settings.HuggingFaceModel);
var engine = new ExerciseEngine();

bot.StartReceiving(
    updateHandler: HandleUpdateAsync,
    errorHandler: HandleErrorAsync,
    receiverOptions: new ReceiverOptions
    {
        AllowedUpdates = [UpdateType.Message],
        DropPendingUpdates = true
    },
    cancellationToken: cts.Token);

var me = await bot.GetMe(cts.Token);
Console.WriteLine($"Бот @{me.Username} слухає повідомлення. Зупинка: Ctrl+C.");
Console.WriteLine($"Модель: {settings.HuggingFaceModel} через Hugging Face. Довідник: {VerbCatalog.All.Count} дієслів A1–A2.");

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Зупинено.");
}

async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken cancellationToken)
{
    var message = update.Message;
    if (message?.Text is not { Length: > 0 } text)
    {
        if (message is not null)
        {
            await client.SendMessage(
                message.Chat.Id,
                "Надішли, будь ласка, текстову відповідь.",
                cancellationToken: cancellationToken);
        }

        return;
    }

    var chatId = message.Chat.Id;
    try
    {
        var (command, argument) = ParseCommand(text);
        switch (command)
        {
            case "/start":
                await client.SendMessage(chatId, engine.StartText, cancellationToken: cancellationToken);
                return;
            case "/help":
                await client.SendMessage(chatId, engine.HelpText, cancellationToken: cancellationToken);
                return;
            case "/types":
                await client.SendMessage(chatId, engine.TypesText, cancellationToken: cancellationToken);
                return;
            case "/status":
                await client.SendMessage(chatId, engine.Describe(chatId), cancellationToken: cancellationToken);
                return;
            case "/reset":
                engine.Reset(chatId);
                tutor.Reset(chatId);
                await client.SendMessage(
                    chatId,
                    "Діалог скинуто. Надішли /practice, щоб почати нову вправу.",
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: cancellationToken);
                return;
            case "/practice":
                tutor.Reset(chatId);
                SplitPracticeArgument(argument, out var infinitive, out var pronoun);
                await DeliverAsync(client, chatId, "Хочу нову вправу.", engine.StartPractice(chatId, infinitive, pronoun), cancellationToken);
                return;
            default:
                await DeliverAsync(client, chatId, text, engine.Handle(chatId, text), cancellationToken);
                return;
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"[повідомлення] {exception.GetType().Name}: {exception.Message}");
        try
        {
            await client.SendMessage(
                chatId,
                "Щось пішло не так на моєму боці. Спробуй ще раз або надішли /practice.",
                cancellationToken: cancellationToken);
        }
        catch (Exception sendError)
        {
            Console.Error.WriteLine($"[telegram] {sendError.GetType().Name}: {sendError.Message}");
        }
    }
}

async Task DeliverAsync(
    ITelegramBotClient client,
    long chatId,
    string userText,
    TurnResult turn,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] чат {chatId}: {turn.LogSummary}");
    var reply = turn.FallbackReply;
    if (turn.UseModel)
    {
        using var typingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var typing = KeepTypingAsync(client, chatId, typingCts.Token);
        try
        {
            reply = await tutor.GetTutorResponseAsync(userText, chatId, turn.TutorNote, turn.FallbackReply, cancellationToken);
        }
        finally
        {
            typingCts.Cancel();
            try
            {
                await typing;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    reply = AnswerGuard.Sanitize(reply, turn.FallbackReply);
    if (reply.Length > 280)
        reply = turn.FallbackReply;
    if (reply.Length > 4000)
        reply = reply[..4000] + "…";

    await client.SendMessage(chatId, reply, replyMarkup: Markup(turn.Keyboard), cancellationToken: cancellationToken);
}

async Task KeepTypingAsync(ITelegramBotClient client, long chatId, CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            await client.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[typing] {exception.GetType().Name}: {exception.Message}");
            break;
        }
    }
}

Task HandleErrorAsync(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
{
    Console.Error.WriteLine($"[помилка:{source}] {exception.GetType().Name}: {exception.Message}");
    return Task.CompletedTask;
}

static ReplyMarkup Markup(BotKeyboard keyboard) => keyboard switch
{
    BotKeyboard.Types => new ReplyKeyboardMarkup([
        ["Слабке", "Сильне", "Неправильне"]
    ])
    {
        ResizeKeyboard = true,
        IsPersistent = true
    },
    BotKeyboard.Endings => new ReplyKeyboardMarkup([
        ["-e", "-st", "-t"],
        ["-en", "-est", "-et"]
    ])
    {
        ResizeKeyboard = true,
        IsPersistent = true
    },
    _ => new ReplyKeyboardRemove()
};

static (string Command, string? Argument) ParseCommand(string text)
{
    var trimmed = text.Trim();
    if (!trimmed.StartsWith('/'))
        return ("", null);

    var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
    var command = parts[0];
    var at = command.IndexOf('@');
    if (at >= 0)
        command = command[..at];

    var argument = parts.Length > 1 ? parts[1].Trim() : null;
    return (command.ToLowerInvariant(), argument);
}

static void SplitPracticeArgument(string? argument, out string? infinitive, out string? pronoun)
{
    infinitive = null;
    pronoun = null;
    if (string.IsNullOrWhiteSpace(argument))
        return;

    foreach (var bit in argument.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2))
    {
        if (VerbCatalog.IsQuizPronoun(bit))
            pronoun = bit;
        else
            infinitive = bit;
    }
}
