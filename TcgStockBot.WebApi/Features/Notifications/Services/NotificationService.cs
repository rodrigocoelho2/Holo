using Microsoft.Extensions.Options;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Notifications.Models;
using TcgStockBot.WebApi.Features.Notifications.Models.DTOs;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.WebApi.Features.Notifications.Services;

/// <summary>
/// Fans alerts out to the enabled channels and reports failures instead of throwing.
/// </summary>
/// <param name="notifiers">Every channel (log, email, WhatsApp).</param>
/// <param name="emailOptions">Email settings (for the state view).</param>
/// <param name="whatsAppOptions">WhatsApp settings (for the state view).</param>
/// <param name="globalizationHelper">Translations.</param>
/// <param name="logger">Logger.</param>
internal sealed class NotificationService(
    IEnumerable<INotifier> notifiers,
    IOptions<EmailOptions> emailOptions,
    IOptions<WhatsAppOptions> whatsAppOptions,
    IOptions<TelegramOptions> telegramOptions,
    IGlobalizationHelper globalizationHelper,
    ILogger<NotificationService> logger) : INotificationService
{
    private const string FieldChannel = "Channel";

    private readonly IReadOnlyList<INotifier> _notifiers = [.. notifiers];
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly ILogger<NotificationService> _logger = logger;

    /// <inheritdoc />
    public Task<ServiceApiResponse<NotificationDTO>> GetAsync()
    {
        var email = emailOptions.Value;
        var whatsApp = whatsAppOptions.Value;
        var telegram = telegramOptions.Value;

        return Task.FromResult(ServiceApiResponse<NotificationDTO>.Success(new NotificationDTO
        {
            Email = new EmailNotificationDTO
            {
                Enabled = email.Enabled,
                SmtpHost = email.SmtpHost,
                From = email.From,
                To = [.. email.To],
                HasPassword = !string.IsNullOrEmpty(email.Password),
            },
            WhatsApp = new WhatsAppNotificationDTO
            {
                Enabled = whatsApp.Enabled,
                MaskedPhone = Mask(whatsApp.Phone),
                HasApiKey = !string.IsNullOrEmpty(whatsApp.ApiKey),
            },
            Telegram = new TelegramNotificationDTO
            {
                Enabled = telegram.Enabled,
                MaskedChatId = Mask(telegram.ChatId),
                HasBotToken = !string.IsNullOrEmpty(telegram.BotToken),
            },
            Push = new PushNotificationDTO
            {
                Enabled = _notifiers.Any(static notifier => notifier.Channel == NotificationChannel.Push && notifier.IsEnabled),
            },
        }));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> TestAsync(NotificationTestRequest item)
    {
        var notifier = _notifiers.FirstOrDefault(candidate => candidate.Channel == item.Channel);
        if (notifier is null || !notifier.IsEnabled)
        {
            var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NotificationChannelDisabled), item.Channel);
            return ServiceApiResponse<string>.ValidationProblems(message).AddError(FieldChannel, message);
        }

        var sample = new StockAlert("test", "Test", new StoreListing(
            "Test store", "Pokémon TCG Elite Trainer Box (EN) — test message", new Uri("https://example.com/"), 54.99m, InStock: true),
            LanguageToConfirm: false);

        try
        {
            await notifier.SendAsync([sample], CancellationToken.None);
            return ServiceApiResponse<string>.Success();
        }
        catch (Exception exception)
        {
            var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NotificationFailed), item.Channel, exception.Message);
            return ServiceApiResponse<string>.ValidationProblems(message).AddError(FieldChannel, message);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> NotifyAsync(IReadOnlyList<StockAlert> alerts, CancellationToken cancellationToken) =>
        SendToEveryChannelAsync(notifier => notifier.SendAsync(alerts, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> NotifyNoticeAsync(string subject, string text, CancellationToken cancellationToken) =>
        SendToEveryChannelAsync(notifier => notifier.SendNoticeAsync(subject, text, cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<string>> SendToEveryChannelAsync(Func<INotifier, Task> send, CancellationToken cancellationToken)
    {
        var failures = new List<string>();

        foreach (var notifier in _notifiers.Where(static notifier => notifier.IsEnabled))
        {
            try
            {
                await send(notifier);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Notifier {Channel} failed", notifier.Channel);
                failures.Add(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NotificationFailed), notifier.Channel?.ToString() ?? "log", exception.Message));
            }
        }

        return failures;
    }

    private static string Mask(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length <= 3 ? digits : $"{new string('•', digits.Length - 3)}{digits[^3..]}";
    }
}
