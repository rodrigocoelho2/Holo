using System.Globalization;
using System.Net;
using System.Text;
using TcgStockBot.WebApi.Features.Alerts.Models;

namespace TcgStockBot.WebApi.Features.Notifications.Services;

/// <summary>
/// Builds the alert texts, so every channel says the same thing.
/// </summary>
internal static class AlertMessageFormatter
{
    private const string LanguageWarning = "⚠️ Título sem idioma: confirma na página que é a versão inglesa.";

    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-PT");

    /// <summary>
    /// The email subject.
    /// </summary>
    /// <param name="alerts">The alerts.</param>
    /// <returns>The subject.</returns>
    public static string Subject(IReadOnlyList<StockAlert> alerts) => alerts.Count == 1
        ? $"[TCG] Em stock: {alerts[0].Listing.Title} ({alerts[0].Listing.StoreName})"
        : $"[TCG] {alerts.Count} produtos em stock";

    /// <summary>
    /// The WhatsApp message (WhatsApp formatting: *bold*).
    /// </summary>
    /// <param name="alerts">The alerts.</param>
    /// <param name="maxAlerts">How many alerts to list before summarising the rest.</param>
    /// <returns>The message text.</returns>
    public static string WhatsApp(IReadOnlyList<StockAlert> alerts, int maxAlerts)
    {
        var text = new StringBuilder(alerts.Count == 1 ? "🟢 *TCG em stock*" : $"🟢 *TCG: {alerts.Count} produtos em stock*");

        foreach (var alert in alerts.Take(Math.Max(1, maxAlerts)))
        {
            text.Append("\n\n*").Append(alert.Listing.Title).Append('*')
                .Append('\n').Append(alert.Listing.StoreName).Append(" · ").Append(Price(alert.Listing.Price))
                .Append('\n').Append(alert.Listing.Url.AbsoluteUri);

            if (alert.LanguageToConfirm)
            {
                text.Append('\n').Append(LanguageWarning);
            }
        }

        if (alerts.Count > maxAlerts)
        {
            text.Append("\n\n+").Append(alerts.Count - maxAlerts).Append(" no dashboard.");
        }

        return text.ToString();
    }

    /// <summary>
    /// The plain-text email body.
    /// </summary>
    /// <param name="alerts">The alerts.</param>
    /// <returns>The text.</returns>
    public static string Text(IReadOnlyList<StockAlert> alerts) =>
        string.Join(Environment.NewLine + Environment.NewLine, alerts.Select(alert =>
            $"{alert.Listing.Title}{Environment.NewLine}{alert.Listing.StoreName} — {Price(alert.Listing.Price)}{Environment.NewLine}{alert.Listing.Url}"
            + (alert.LanguageToConfirm ? Environment.NewLine + LanguageWarning : string.Empty)));

    /// <summary>
    /// The HTML email body. Every store-provided value is encoded.
    /// </summary>
    /// <param name="alerts">The alerts.</param>
    /// <returns>The HTML.</returns>
    public static string Html(IReadOnlyList<StockAlert> alerts)
    {
        var html = new StringBuilder("<h2>Produtos em stock (versão inglesa, até ao preço máximo)</h2><ul>");
        foreach (var alert in alerts)
        {
            var listing = alert.Listing;
            html.Append("<li><p><b>").Append(WebUtility.HtmlEncode(listing.Title)).Append("</b><br/>")
                .Append(WebUtility.HtmlEncode(listing.StoreName)).Append(" — ").Append(Price(listing.Price)).Append("<br/>")
                .Append("Tarefa: ").Append(WebUtility.HtmlEncode(alert.StockTaskName)).Append("<br/>");

            if (alert.LanguageToConfirm)
            {
                html.Append("<span style=\"color:#b45309\">").Append(WebUtility.HtmlEncode(LanguageWarning)).Append("</span><br/>");
            }

            html.Append("<a href=\"").Append(WebUtility.HtmlEncode(listing.Url.AbsoluteUri)).Append("\">Abrir produto →</a></p></li>");
        }

        return html.Append("</ul>").ToString();
    }

    private static string Price(decimal? price) => price?.ToString("C", Portuguese) ?? "preço desconhecido";
}
