using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Alerts.Models.Entities;
using TcgStockBot.WebApi.Features.Monitors.Models.DTOs;
using TcgStockBot.WebApi.Features.Notifications.Models;
using TcgStockBot.WebApi.Features.Notifications.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.Notifications;

/// <summary>
/// Telegram (URL, HTML escaping, header) and the daily summary text.
/// </summary>
public class TelegramAndSummaryTests
{
    private static StockAlert Alert(string title, decimal? previous = null) =>
        new("etb", "ETB", new StoreListing("Loja <A>", title, new Uri("https://loja.example/p?x=1&y=2"), 59.90m, true), false, previous);

    [Fact]
    public async Task Send_PostsToTheBotSendMessageUrl()
    {
        var handler = new FakeHttpMessageHandler("{\"ok\":true}");
        var settings = new TelegramOptions { Enabled = true, BotToken = "123:abc", ChatId = "42" };
        var notifier = new TelegramNotifier(Options.Create(settings), new FakeHttpClientFactory(handler), NullLogger<TelegramNotifier>.Instance);

        await notifier.SendAsync([Alert("Pitch Black ETB")], CancellationToken.None);

        Assert.Equal("https://api.telegram.org/bot123:abc/sendMessage", handler.LastRequestUri!.AbsoluteUri);
        Assert.Equal(NotificationChannel.Telegram, notifier.Channel);
    }

    [Fact]
    public void Message_EscapesStoreText_AndCountsDrops()
    {
        var text = AlertMessageFormatter.Telegram([Alert("<b>ETB</b>", previous: 69.90m), Alert("Bundle")], 1);

        Assert.Contains("2 alertas", text);
        Assert.Contains("1 descida(s)", text);
        Assert.Contains("&lt;b&gt;ETB&lt;/b&gt;", text);
        Assert.Contains("Loja &lt;A&gt;", text);
        Assert.Contains("x=1&amp;y=2", text);
        Assert.Contains("+1 no dashboard", text);
    }

    [Fact]
    public void DailySummary_ListsLastDayAlertsOffersAndFailingStores()
    {
        var now = new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);
        List<AlertHistory> history =
        [
            new() { At = now.AddHours(-2), Title = "Pitch Black ETB", StoreName = "Loja A", Price = 59.90m },
            new() { At = now.AddDays(-3), Title = "Antigo", StoreName = "Loja A", Price = 10m },
        ];
        var monitor = new MonitorDTO
        {
            Searches =
            [
                new SearchReportDTO
                {
                    StoreName = "Loja B", Succeeded = true,
                    Listings =
                    [
                        new ListingReportDTO { Title = "Chaos Rising Bundle", Url = "https://b.example/1", Price = 29.90m, Outcome = MatchOutcome.Match },
                        new ListingReportDTO { Title = "Caro", Url = "https://b.example/2", Price = 99m, Outcome = MatchOutcome.AboveMaxPrice },
                    ],
                },
                new SearchReportDTO { StoreName = "Loja C", Succeeded = false },
            ],
        };

        var (subject, text) = DailySummaryWorker.Build(history, monitor, now);

        Assert.Contains("1 alerta(s), 1 em stock", subject);
        Assert.Contains("Pitch Black ETB", text);
        Assert.DoesNotContain("Antigo", text);
        Assert.Contains("Chaos Rising Bundle", text);
        Assert.DoesNotContain("Caro", text);
        Assert.Contains("Loja C", text);
    }
}
