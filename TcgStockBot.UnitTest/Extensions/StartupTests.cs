using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TcgStockBot.WebApi;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.Extensions;

public class StartupTests
{
    /// <summary>
    /// IHttpClientFactory needs a new DelegatingHandler every time it rebuilds a client's handler chain
    /// (every ~2 minutes). A singleton handler makes every store request fail after the first rebuild.
    /// </summary>
    [Fact]
    public void UserAgentHandler_IsTransient()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["TcgStockBot:ApiKey"] = "test";

        builder.Services.AddTcgStockBot(builder.Configuration, builder.Environment);

        var descriptor = Assert.Single(builder.Services, static service => service.ServiceType == typeof(UserAgentHandler));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void StoreClient_CanBeBuiltRepeatedly()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["TcgStockBot:ApiKey"] = "test";
        builder.Configuration["TcgStockBot:DataDirectory"] = Path.Combine(Path.GetTempPath(), $"tcg-startup-{Guid.NewGuid():N}");
        builder.Services.AddTcgStockBot(builder.Configuration, builder.Environment);
        builder.Services.AddHttpClient(StoreHttpClient.Name).SetHandlerLifetime(TimeSpan.FromSeconds(1));

        using var provider = builder.Services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        // Each client created after the 1 s lifetime expires forces a new handler chain.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var client = factory.CreateClient(StoreHttpClient.Name);
            Thread.Sleep(TimeSpan.FromSeconds(1.3));
        }
    }
}
