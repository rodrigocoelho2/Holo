namespace TcgStockBot.Sdk.Models;

/// <summary>The price history of one product on one store (mirror of the WebApi PriceHistoryDTO).</summary>
public sealed class PriceHistoryDTO
{
    public PriceHistorySummaryDTO Summary { get; set; } = new();
    public List<PricePointDTO> Points { get; set; } = [];
}

/// <summary>One tracked product with its current, lowest and highest price.</summary>
public sealed class PriceHistorySummaryDTO
{
    public string Key { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public DateTimeOffset? LastChange { get; set; }
    public decimal? CurrentPrice { get; set; }
    public bool CurrentInStock { get; set; }
    public decimal? LowestPrice { get; set; }
    public DateTimeOffset? LowestPriceAt { get; set; }
    public decimal? HighestPrice { get; set; }
    public int PointCount { get; set; }
}

/// <summary>One change of price or stock.</summary>
public sealed class PricePointDTO
{
    public DateTimeOffset At { get; set; }
    public decimal Price { get; set; }
    public bool InStock { get; set; }
}
