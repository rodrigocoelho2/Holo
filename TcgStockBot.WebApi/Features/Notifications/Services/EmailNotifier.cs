using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Notifications.Models;

namespace TcgStockBot.WebApi.Features.Notifications.Services;

/// <summary>
/// Sends one email per cycle listing every new alert, each with a direct link to the product page.
/// </summary>
/// <param name="options">SMTP settings.</param>
/// <param name="logger">Logger.</param>
internal sealed class EmailNotifier(IOptions<EmailOptions> options, ILogger<EmailNotifier> logger) : INotifier
{
    /// <inheritdoc />
    public NotificationChannel? Channel => NotificationChannel.Email;

    /// <inheritdoc />
    public bool IsEnabled => options.Value.Enabled;

    /// <inheritdoc />
    public async Task SendAsync(IReadOnlyList<StockAlert> alerts, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var message = new MimeMessage
        {
            Subject = AlertMessageFormatter.Subject(alerts),
            Priority = MessagePriority.Urgent,
            Body = new BodyBuilder { HtmlBody = AlertMessageFormatter.Html(alerts), TextBody = AlertMessageFormatter.Text(alerts) }.ToMessageBody(),
        };
        message.From.Add(MailboxAddress.Parse(settings.From));
        foreach (var recipient in settings.To)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort,
            settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, cancellationToken);

        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Alert email sent to {Recipients} ({Count} product(s))", string.Join(", ", settings.To), alerts.Count);
    }
}
