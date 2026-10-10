using TcgStockBot.Library.Enums;

namespace TcgStockBot.Sdk.Models;

/// <summary>Read model of a store (mirror of the WebApi StoreDTO).</summary>
public sealed class StoreDTO
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public StorePlatform Platform { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public UnmarkedLanguagePolicy UnmarkedLanguage { get; set; }
    public string WooCommerceProductsPath { get; set; } = string.Empty;
    public string ShopifyCatalogPaths { get; set; } = string.Empty;
    public StoreHtmlSelectorDTO? Html { get; set; }
    public string AwinFeedKey { get; set; } = string.Empty;
    public StoreCountry Country { get; set; }
    public StoreTier Tier { get; set; }
    public StoreType Type { get; set; }
    public List<string> Tags { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
    public StoreTestResultDTO? LastTest { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTimeOffset? AutoDisabledAt { get; set; }
}

/// <summary>CSS selectors of an HTML store.</summary>
public sealed class StoreHtmlSelectorDTO
{
    public string SearchUrlTemplate { get; set; } = string.Empty;
    public string ProductSelector { get; set; } = string.Empty;
    public string TitleSelector { get; set; } = string.Empty;
    public string PriceSelector { get; set; } = string.Empty;
    public string LinkSelector { get; set; } = string.Empty;
    public string OutOfStockSelector { get; set; } = string.Empty;
    public string InStockSelector { get; set; } = string.Empty;
}

/// <summary>Input of StoreSave (null OriginalName = new store).</summary>
public sealed class StoreSaveRequest
{
    public string? OriginalName { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public StorePlatform Platform { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public UnmarkedLanguagePolicy UnmarkedLanguage { get; set; }
    public string WooCommerceProductsPath { get; set; } = "/wp-json/wc/store/v1/products";
    public string ShopifyCatalogPaths { get; set; } = string.Empty;
    public StoreHtmlSelectorDTO? Html { get; set; }
    public string AwinFeedKey { get; set; } = string.Empty;
    public StoreCountry Country { get; set; } = StoreCountry.Portugal;
    public StoreTier Tier { get; set; } = StoreTier.None;
    public StoreType Type { get; set; } = StoreType.Unknown;
    public List<string>? Tags { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
}

/// <summary>Output of StoreSave.</summary>
public sealed class StoreSaveResponse
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>Input of StoreEnable.</summary>
public sealed class StoreEnableRequest
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

/// <summary>Input of StoreProbe.</summary>
public sealed class StoreProbeRequest
{
    public StoreSaveRequest Store { get; set; } = new();
    public string Query { get; set; } = string.Empty;
}

/// <summary>One listing returned by StoreProbe.</summary>
public sealed class StoreProbeResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public bool InStock { get; set; }
    public ProductLanguage Language { get; set; }
    public Dictionary<string, MatchOutcome> OutcomeByTask { get; set; } = [];
    public string? ProductId { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? Seller { get; set; }
    public string? ImageUrl { get; set; }
    public bool PreOrder { get; set; }
    public string Kind { get; set; } = "other";
    public string KindLabel { get; set; } = string.Empty;
    public decimal? Msrp { get; set; }
}

/// <summary>Input of StoreTest.</summary>
public sealed class StoreTestRequest
{
    public string Name { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
}

/// <summary>Result of a store test.</summary>
public sealed class StoreTestResultDTO
{
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset At { get; set; }
    public StoreTestStatus Status { get; set; }
    public string Query { get; set; } = string.Empty;
    public int Results { get; set; }
    public int Relevant { get; set; }
    public int Matches { get; set; }
    public string Sample { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>Input of StoreDetect.</summary>
public sealed class StoreDetectRequest
{
    public string Url { get; set; } = string.Empty;
}

/// <summary>Output of StoreDetect.</summary>
public sealed class StoreDetectResult
{
    public StoreDetectStatus Status { get; set; }
    public StorePlatform Platform { get; set; } = StorePlatform.Manual;
    public string BaseUrl { get; set; } = string.Empty;
    public string SuggestedName { get; set; } = string.Empty;
    public string ShopifyCatalogPaths { get; set; } = string.Empty;
    public string WooCommerceProductsPath { get; set; } = "/wp-json/wc/store/v1/products";
    public StoreHtmlSelectorDTO? Html { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Evidence { get; set; } = [];
}
