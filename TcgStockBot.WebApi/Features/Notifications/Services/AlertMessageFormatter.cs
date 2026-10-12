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
    private const string LanguageWarning = "⚠️ The title does not say the language: check on the page that it is the English version.";
    private const string PreOrderNote = "🕒 Pre-order: check the shipping date on the page.";

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-IE");

    /// <summary>
    /// The email subject.
    /// </summary>
    /// <param name="alerts">The alerts.</param>
    /// <returns>The subject.</returns>
    public static string Subject(IReadOnlyList<StockAlert> alerts)
    {
        if (alerts.Count == 1)
        {
            var alert = alerts[0];
            var kind = alert.IsPriceDrop ? "Price drop" : alert.Listing.PreOrder ? "Pre-order" : "In stock";
            return $"[Holo] {kind}: {alert.Listing.Title} ({alert.Listing.StoreName})";
        }

        var drops = alerts.Count(static alert => alert.IsPriceDrop);
        return drops == alerts.Count ? $"[Holo] {alerts.Count} price drops"
            : drops > 0 ? $"[Holo] {alerts.Count} alerts ({drops} price drop(s))"
            : $"[Holo] {alerts.Count} products in stock";
    }

    /// <summary>
    /// The WhatsApp message (WhatsApp formatting: *bold*).
    /// </summary>
    /// <param name="alerts">The alerts.</param>
    /// <param name="maxAlerts">How many alerts to list before summarising the rest.</param>
    /// <returns>The message text.</returns>
    public static string WhatsApp(IReadOnlyList<StockAlert> alerts, int maxAlerts)
    {
        var drops = alerts.Count(static alert => alert.IsPriceDrop);
        var header = alerts.Count == 1
            ? drops == 1 ? "🔻 *Holo: price drop*" : "🟢 *Holo: in stock*"
            : drops == alerts.Count ? $"🔻 *Holo: {alerts.Count} price drops*"
            : drops > 0 ? $"🟢 *Holo: {alerts.Count} alerts ({drops} price drop(s))*"
            : $"🟢 *Holo: {alerts.Count} products in stock*";
        var text = new StringBuilder(header);

        foreach (var alert in alerts.Take(Math.Max(1, maxAlerts)))
        {
            text.Append("\n\n*").Append(alert.Listing.Title).Append('*')
                .Append('\n').Append(alert.Listing.StoreName).Append(" · ").Append(PriceLine(alert))
                .Append('\n').Append(alert.Listing.Url.AbsoluteUri);

            if (alert.Listing.PreOrder)
            {
                text.Append('\n').Append(PreOrderNote);
            }

            if (alert.LanguageToConfirm)
            {
                text.Append('\n').Append(LanguageWarning);
            }
        }

        if (alerts.Count > maxAlerts)
        {
            text.Append("\n\n+").Append(alerts.Count - maxAlerts).Append(" more in Holo.");
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
            $"{alert.Listing.Title}{Environment.NewLine}{alert.Listing.StoreName} — {PriceLine(alert)}{Environment.NewLine}{alert.Listing.Url}"
            + (alert.Listing.PreOrder ? Environment.NewLine + PreOrderNote : string.Empty)
            + (alert.LanguageToConfirm ? Environment.NewLine + LanguageWarning : string.Empty)));

    /// <summary>
    /// The HTML email body. Every store-provided value is encoded.
    /// </summary>
    /// <param name="alerts">The alerts.</param>
    /// <returns>The HTML.</returns>
    public static string Html(IReadOnlyList<StockAlert> alerts)
    {
        var html = new StringBuilder("<h2>In stock, in the right language and at your price</h2><ul>");
        foreach (var alert in alerts)
        {
            var listing = alert.Listing;
            html.Append("<li><p><b>").Append(WebUtility.HtmlEncode(listing.Title)).Append("</b><br/>")
                .Append(WebUtility.HtmlEncode(listing.StoreName)).Append(" — ").Append(WebUtility.HtmlEncode(PriceLine(alert))).Append("<br/>")
                .Append("Watching: ").Append(WebUtility.HtmlEncode(alert.StockTaskName)).Append("<br/>");

            if (listing.PreOrder)
            {
                html.Append("<span style=\"color:#6d28d9\">").Append(WebUtility.HtmlEncode(PreOrderNote)).Append("</span><br/>");
            }

            if (alert.LanguageToConfirm)
            {
                html.Append("<span style=\"color:#b45309\">").Append(WebUtility.HtmlEncode(LanguageWarning)).Append("</span><br/>");
            }

            html.Append("<a href=\"").Append(WebUtility.HtmlEncode(listing.Url.AbsoluteUri)).Append("\">Open product →</a></p></li>");
        }

        return html.Append("</ul>").ToString();
    }

    /// <summary>
    /// "€115.00", or "🔻 €115.00 (was €130.00)" for a price drop; followed by " · MSRP 59,99 € (−8%)" when the
    /// reference price is known.
    /// </summary>
    /// <summary>
    /// The Telegram message (HTML parse mode: only &lt;b&gt;, &lt;i&gt;, &lt;a&gt; and escaped text).
    /// </summary>
    /// <param name="alerts">The alerts.</param>
    /// <param name="maxAlerts">How many products to list.</param>
    /// <returns>The message.</returns>
    public static string Telegram(IReadOnlyList<StockAlert> alerts, int maxAlerts)
    {
        var drops = alerts.Count(static alert => alert.IsPriceDrop);
        var header = alerts.Count == 1
            ? drops == 1 ? "🔻 <b>Price drop</b>" : alerts[0].Listing.PreOrder ? "🕒 <b>Pre-order</b>" : "🟢 <b>In stock</b>"
            : drops == alerts.Count ? $"🔻 <b>{alerts.Count} price drops</b>"
            : $"🟢 <b>{alerts.Count} alerts</b>" + (drops > 0 ? $" ({drops} price drop(s))" : string.Empty);
        var text = new StringBuilder(header);

        foreach (var alert in alerts.Take(Math.Max(1, maxAlerts)))
        {
            var listing = alert.Listing;
            text.Append("\n\n<b>").Append(WebUtility.HtmlEncode(listing.Title)).Append("</b>")
                .Append("\n").Append(WebUtility.HtmlEncode(listing.StoreName)).Append(" · ").Append(WebUtility.HtmlEncode(PriceLine(alert)))
                .Append("\n<a href=\"").Append(WebUtility.HtmlEncode(listing.Url.AbsoluteUri)).Append("\">Open in the store →</a>");

            if (listing.PreOrder)
            {
                text.Append("\n<i>").Append(WebUtility.HtmlEncode(PreOrderNote)).Append("</i>");
            }

            if (alert.LanguageToConfirm)
            {
                text.Append("\n<i>").Append(WebUtility.HtmlEncode(LanguageWarning)).Append("</i>");
            }
        }

        if (alerts.Count > maxAlerts)
        {
            text.Append("\n\n+").Append(alerts.Count - maxAlerts).Append(" more in Holo.");
        }

        return text.ToString();
    }

    /// <summary>A notice (store turned off, daily summary) for Telegram.</summary>
    /// <param name="subject">The subject.</param>
    /// <param name="text">The plain text.</param>
    /// <returns>The HTML message.</returns>
    public static string TelegramNotice(string subject, string text) =>
        $"ℹ️ <b>{WebUtility.HtmlEncode(subject)}</b>\n\n{WebUtility.HtmlEncode(text)}";

    private static string PriceLine(StockAlert alert) => (alert.IsPriceDrop
        ? $"🔻 {Price(alert.Listing.Price)} (was {Price(alert.PreviousPrice)})"
        : Price(alert.Listing.Price)) + ShippingPart(alert) + MsrpPart(alert);

    /// <summary>" + €4.99 shipping", " · free shipping" or empty when the store's shipping is not known.</summary>
    internal static string ShippingPart(StockAlert alert) => alert.Shipping switch
    {
        null => string.Empty,
        0 => " · free shipping",
        var shipping => $" + {Price(shipping)} shipping",
    };

    /// <summary>" · MSRP 59,99 € (−8%)", " · MSRP 59,99 € (=)" or empty when the MSRP or the price is unknown.</summary>
    internal static string MsrpPart(StockAlert alert)
    {
        if (alert.Msrp is not > 0 || alert.Listing.Price is not > 0)
        {
            return string.Empty;
        }

        var percent = (int)Math.Round((alert.Listing.Price.Value - alert.Msrp.Value) / alert.Msrp.Value * 100m, MidpointRounding.AwayFromZero);
        var difference = percent switch
        {
            0 => "=",
            < 0 => $"−{-percent}%",
            _ => $"+{percent}%",
        };
        return $" · MSRP {Price(alert.Msrp)} ({difference})";
    }

    private static string Price(decimal? price) => price?.ToString("C", English) ?? "unknown price";
}
