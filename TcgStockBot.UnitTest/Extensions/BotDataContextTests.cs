using TcgStockBot.Library.Enums;

namespace TcgStockBot.UnitTest.Extensions;

public sealed class BotDataContextTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"tcg-context-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void FirstStart_SeedsFromTheBotSectionAndWritesTheFile()
    {
        var context = TestDataContext.Create(_directory, new Dictionary<string, string?>
        {
            ["Bot:PollInterval"] = "00:05:00",
            ["Bot:Wishlist:0:Id"] = "etb",
            ["Bot:Wishlist:0:SearchQuery"] = "elite trainer box",
            ["Bot:Wishlist:0:MaxPrice"] = "70",
            ["Bot:Stores:0:Name"] = "Shop",
            ["Bot:Stores:0:Platform"] = "Shopify",
            ["Bot:Stores:0:BaseUrl"] = "https://shop.example",
            ["Bot:Stores:0:UnmarkedLanguage"] = "Confirm",
        });

        var configuration = context.Snapshot();
        Assert.Equal(TimeSpan.FromMinutes(5), configuration.PollInterval);
        Assert.Equal("etb", Assert.Single(configuration.Wishlist).Id);
        Assert.Equal(UnmarkedLanguagePolicy.Confirm, Assert.Single(configuration.Stores).UnmarkedLanguage);
        Assert.True(File.Exists(context.ConfigurationPath));
    }

    /// <summary>A redeploy must keep the configuration written by the previous version (v2) of the bot.</summary>
    [Fact]
    public void ExistingV2File_IsReadAsIs()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "bot-config.json"), """
            {
              "paused": true,
              "pollInterval": "00:07:00",
              "pollJitter": "00:01:00",
              "delayBetweenRequests": "00:00:02",
              "userAgent": "TcgStockBot/1.0 (personal stock alert)",
              "wishlist": [
                { "id": "etb", "name": "Elite Trainer Box (EN)", "enabled": true, "storeNames": [], "searchQuery": "pokemon elite trainer box",
                  "requiredKeywords": [ "elite trainer box" ], "excludedKeywords": [ "break" ], "maxPrice": 70.00 }
              ],
              "stores": [
                { "name": "Panda Collecting", "enabled": true, "platform": "WooCommerce", "baseUrl": "https://pandacollecting.com",
                  "unmarkedLanguage": "English", "wooCommerceProductsPath": "/wp-json/wc/store/products", "html": null }
              ]
            }
            """);

        var configuration = TestDataContext.Create(_directory, new Dictionary<string, string?> { ["Bot:PollInterval"] = "00:10:00" }).Snapshot();

        Assert.True(configuration.Paused);
        Assert.Equal(TimeSpan.FromMinutes(7), configuration.PollInterval);
        Assert.Equal(70m, Assert.Single(configuration.Wishlist).MaxPrice);
        Assert.Equal("/wp-json/wc/store/products", Assert.Single(configuration.Stores).WooCommerceProductsPath);
    }

    [Fact]
    public async Task Write_PersistsAndSnapshotsAreIndependent()
    {
        var context = TestDataContext.Create(_directory);
        var snapshot = context.Snapshot();
        snapshot.UserAgent = "changed only locally";

        await context.WriteAsync(configuration => configuration.Paused = true, CancellationToken.None);

        var reloaded = TestDataContext.Create(_directory).Snapshot();
        Assert.True(reloaded.Paused);
        Assert.NotEqual("changed only locally", reloaded.UserAgent);
    }
}
