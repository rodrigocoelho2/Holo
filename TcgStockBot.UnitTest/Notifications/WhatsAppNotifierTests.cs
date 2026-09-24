using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Notifications.Models;
using TcgStockBot.WebApi.Features.Notifications.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.Notifications;

public class WhatsAppNotifierTests
{
    private static readonly WhatsAppOptions Settings = new()
    {
        Enabled = true,
        Phone = "+351 912 345 678",
        ApiKey = "123456",
        MaxAlertsPerMessage = 2,
    };

    private static StockAlert Alert(string title, bool languageToConfirm = false) =>
        new("etb", "ETB", new StoreListing("Versus Gamecenter", title, new Uri("https://versus.example/products/etb?x=1&y=2"), 79.90m, true), languageToConfirm);

    [Fact]
    public async Task Send_CallsCallMeBotWithEncodedPhoneTextAndKey()
    {
        var handler = new FakeHttpMessageHandler("Message queued");
        var notifier = new WhatsAppNotifier(Options.Create(Settings), new FakeHttpClientFactory(handler), NullLogger<WhatsAppNotifier>.Instance);

        await notifier.SendAsync([Alert("Perfect Order Elite Trainer Box")], CancellationToken.None);

        var query = System.Web.HttpUtility.ParseQueryString(handler.LastRequestUri!.Query);
        Assert.Equal("https://api.callmebot.com/whatsapp.php", handler.LastRequestUri.GetLeftPart(UriPartial.Path));
        Assert.Equal("+351912345678", query["phone"]);
        Assert.Equal("123456", query["apikey"]);
        Assert.Contains("Perfect Order Elite Trainer Box", query["text"]);
        Assert.Contains("https://versus.example/products/etb?x=1&y=2", query["text"]);
    }

    [Fact]
    public void Message_FlagsLanguageToConfirmAndSummarisesTheRest()
    {
        var text = AlertMessageFormatter.WhatsApp([Alert("A", languageToConfirm: true), Alert("B"), Alert("C")], Settings.MaxAlertsPerMessage);

        Assert.Contains("3 produtos em stock", text);
        Assert.Contains("confirma na página", text);
        Assert.Contains("+1 no dashboard", text);
        Assert.DoesNotContain("*C*", text);
    }

    [Fact]
    public void EmailHtml_EncodesStoreText()
    {
        var html = AlertMessageFormatter.Html([Alert("<script>alert(1)</script> ETB")]);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
