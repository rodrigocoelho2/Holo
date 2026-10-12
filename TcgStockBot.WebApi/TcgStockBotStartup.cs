using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TcgStockBot.WebApi.Extensions.Authentication;
using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Extensions.Http;
using TcgStockBot.WebApi.Extensions.Mvc;
using TcgStockBot.WebApi.Features;
using TcgStockBot.WebApi.Features.Alerts.Repositories;
using TcgStockBot.WebApi.Features.Alerts.Services;
using TcgStockBot.WebApi.Features.Compares.Repositories;
using TcgStockBot.WebApi.Features.Compares.Services;
using TcgStockBot.WebApi.Features.Monitors.Services;
using TcgStockBot.WebApi.Features.Notifications.Models;
using TcgStockBot.WebApi.Features.Notifications.Services;
using TcgStockBot.WebApi.Features.PriceHistories.Repositories;
using TcgStockBot.WebApi.Features.PriceHistories.Services;
using TcgStockBot.WebApi.Features.Settings.Repositories;
using TcgStockBot.WebApi.Features.Settings.Services;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;
using TcgStockBot.WebApi.Features.StockTasks.Services;
using TcgStockBot.WebApi.Features.Stores.Repositories;
using TcgStockBot.WebApi.Features.Stores.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters.Amazon;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters.Awin;
using TcgStockBot.WebApi.Features.Msrps.Repositories;
using TcgStockBot.WebApi.Features.Msrps.Services;
using TcgStockBot.WebApi.Features.Releases.Repositories;
using TcgStockBot.WebApi.Features.Users.Repositories;
using TcgStockBot.WebApi.Features.Users.Services;
using TcgStockBot.WebApi.Features.Releases.Services;
using TcgStockBot.WebApi.Features.Sets.Repositories;
using TcgStockBot.WebApi.Features.Sets.Services;
using TcgStockBot.WebApi.Features.Push.Models;
using TcgStockBot.WebApi.Features.Push.Repositories;
using TcgStockBot.WebApi.Features.Push.Services;

namespace TcgStockBot.WebApi;

/// <summary>
/// Service registration and request pipeline of the WebApi.
/// </summary>
internal static class TcgStockBotStartup
{
    /// <summary>
    /// Registers every service. All services and repositories are singletons; each repository is registered before its service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTcgStockBot(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddOptions<TcgStockBotOptions>()
            .Bind(configuration.GetSection(TcgStockBotOptions.SectionName))
            .PostConfigure(options => options.DataDirectory = Path.Combine(environment.ContentRootPath, options.DataDirectory))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ApiKey), "TcgStockBot:ApiKey is required (env TcgStockBot__ApiKey).")
            .ValidateOnStart();
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));
        services.Configure<WebPushOptions>(configuration.GetSection(WebPushOptions.SectionName));

        services.AddControllers()
            .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new InternalControllerFeatureProvider()))
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
        services.AddAuthorization();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options => options.EnableAnnotations());

        // Infrastructure
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<BotDataContext>();
        services.AddSingleton<IGlobalizationHelper, GlobalizationHelper>();
        // Exception to the AddSingleton rule: IHttpClientFactory rebuilds the handler chain every few minutes
        // and needs a fresh DelegatingHandler each time; a singleton fails with "The 'InnerHandler' property must be null".
        services.AddTransient<UserAgentHandler>();

        // Outbound HTTP for the price providers: per-domain throttling, Retry-After and request logging.
        services.Configure<OutboundHttpOptions>(configuration.GetSection(OutboundHttpOptions.SectionName));
        services.AddSingleton<DomainThrottle>();
        services.AddTransient<OutboundTrafficHandler>();

        var storeTimeout = configuration.GetSection(TcgStockBotOptions.SectionName).Get<TcgStockBotOptions>()?.StoreRequestTimeout ?? TimeSpan.FromSeconds(30);
        services.AddHttpClient(StoreHttpClient.Name, client =>
            {
                client.Timeout = storeTimeout;
                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en,pt-PT;q=0.8");
            })
            .ConfigurePrimaryHttpMessageHandler(CreateOutboundHandler)
            .AddHttpMessageHandler<OutboundTrafficHandler>()
            .AddHttpMessageHandler<UserAgentHandler>();

        // Amazon Creators API (official API for amazon.es; credentials in the "Amazon" section).
        services.Configure<AmazonCreatorsOptions>(configuration.GetSection(AmazonCreatorsOptions.SectionName));
        services.AddHttpClient(AmazonCreatorsStoreAdapter.HttpClientName, client => client.Timeout = storeTimeout)
            .ConfigurePrimaryHttpMessageHandler(CreateOutboundHandler)
            .AddHttpMessageHandler<OutboundTrafficHandler>()
            .AddHttpMessageHandler<UserAgentHandler>();

        // Awin affiliate product feeds (authorized source for FNAC / Worten / El Corte Inglés; URLs in "AwinFeeds:Feeds").
        // Feeds can be large, hence the longer timeout; the URL path carries the feed key, so the HttpClient loggers are removed.
        services.Configure<AwinFeedOptions>(configuration.GetSection(AwinFeedOptions.SectionName));
        services.AddHttpClient(AwinFeedCatalog.HttpClientName, static client => client.Timeout = TimeSpan.FromMinutes(5))
            .ConfigurePrimaryHttpMessageHandler(CreateOutboundHandler)
            .AddHttpMessageHandler<OutboundTrafficHandler>()
            .AddHttpMessageHandler<UserAgentHandler>()
            .RemoveAllLoggers();

        // The CallMeBot URL carries the phone and API key: never let the HttpClient loggers write it.
        services.AddHttpClient(WhatsAppNotifier.HttpClientName, static client => client.Timeout = TimeSpan.FromSeconds(30))
            .RemoveAllLoggers();

        // Same for Telegram: the bot token is part of the URL.
        services.AddHttpClient(TelegramNotifier.HttpClientName, static client => client.Timeout = TimeSpan.FromSeconds(30))
            .RemoveAllLoggers();

        // And for push: every endpoint URL carries a per-device token.
        services.AddHttpClient(PushService.HttpClientName, static client => client.Timeout = TimeSpan.FromSeconds(20))
            .RemoveAllLoggers();

        // Stores
        services.AddSingleton<IStoreRepository, StoreRepository>();
        services.AddSingleton<IStoreAdapter, ShopifyStoreAdapter>();
        services.AddSingleton<IStoreAdapter, WooCommerceStoreAdapter>();
        services.AddSingleton<IStoreAdapter, HtmlStoreAdapter>();
        services.AddSingleton<AmazonCreatorsTokenProvider>();
        services.AddSingleton<IStoreAdapter, AmazonCreatorsStoreAdapter>();
        services.AddSingleton<AwinFeedCatalog>();
        services.AddSingleton<IStoreAdapter, FnacStoreAdapter>();
        services.AddSingleton<IStoreAdapter, WortenStoreAdapter>();
        services.AddSingleton<IStoreAdapter, ElCorteInglesStoreAdapter>();
        services.AddSingleton<IStoreAdapter, GenericAwinStoreAdapter>();
        services.AddSingleton<IStoreAdapter, ManualStoreAdapter>();
        services.AddSingleton<IStoreAdapterResolver, StoreAdapterResolver>();

        // StockTasks
        services.AddSingleton<IStockTaskRepository, StockTaskRepository>();
        services.AddSingleton<IStockTaskService, StockTaskService>();
        services.AddSingleton<IStoreService, StoreService>();
        services.AddSingleton<IStoreDetectService, StoreDetectService>();
        services.AddSingleton<IStoreHealthService, StoreHealthService>();

        // Settings
        services.AddSingleton<ISettingRepository, SettingRepository>();
        services.AddSingleton<ISettingService, SettingService>();

        // Alerts
        services.AddSingleton<IAlertRepository, AlertRepository>();
        services.AddSingleton<IAlertService, AlertService>();

        // Msrps
        services.AddSingleton<IMsrpRepository, MsrpRepository>();
        services.AddSingleton<IMsrpService, MsrpService>();

        // PriceHistories
        services.AddSingleton<IPriceHistoryRepository, PriceHistoryRepository>();
        services.AddSingleton<IPriceHistoryService, PriceHistoryService>();

        // Users
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IUserService, UserService>();

        // Releases
        services.AddSingleton<IReleaseRepository, ReleaseRepository>();
        services.AddSingleton<IReleaseService, ReleaseService>();

        // Sets
        services.AddSingleton<ISetRepository, SetRepository>();
        services.AddSingleton<ISetService, SetService>();

        // Compares
        services.AddSingleton<ICompareOverrideRepository, CompareOverrideRepository>();
        services.AddSingleton<ICompareService, CompareService>();

        // Notifications
        services.AddSingleton<INotifier, LogNotifier>();
        services.AddSingleton<INotifier, EmailNotifier>();
        services.AddSingleton<INotifier, WhatsAppNotifier>();
        services.AddSingleton<INotifier, TelegramNotifier>();
        services.AddSingleton<IPushRepository, PushRepository>();
        services.AddSingleton<IPushService, PushService>();
        services.AddSingleton<INotifier, WebPushNotifier>();
        services.AddSingleton<INotificationService, NotificationService>();

        // Monitors
        services.AddSingleton<BotActivity>();
        services.AddSingleton<CycleScheduler>();
        services.AddSingleton<IStockCheckService, StockCheckService>();
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddHostedService<StockMonitorWorker>();
        services.AddHostedService<DailySummaryWorker>();

        return services;
    }

    /// <summary>
    /// Configures the request pipeline.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static WebApplication UseTcgStockBot(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }

    /// <summary>
    /// Primary handler of the outbound HttpClients, created by IHttpClientFactory on each handler rotation.
    /// </summary>
    /// <param name="provider">The service provider.</param>
    /// <returns>The handler.</returns>
    private static HttpMessageHandler CreateOutboundHandler(IServiceProvider provider) =>
        OutboundHttpHandlerFactory.Create(provider.GetRequiredService<IOptions<OutboundHttpOptions>>().Value);
}
