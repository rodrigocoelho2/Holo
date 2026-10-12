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

/// <summary>When one store restocks (mirror of the WebApi StoreRestockDTO).</summary>
public sealed class StoreRestockDTO
{
    public string StoreName { get; set; } = string.Empty;
    public int Restocks { get; set; }
    public int Products { get; set; }
    public int Days { get; set; }
    public DateTimeOffset? LastRestockAt { get; set; }
    public List<int> ByWeekday { get; set; } = [];
    public List<int> ByHour { get; set; } = [];
    public List<List<int>> ByWeekdayHour { get; set; } = [];
    public List<int> UsualWeekdays { get; set; } = [];
    public int? UsualHour { get; set; }
    public bool Confident { get; set; }
    public string TimeZone { get; set; } = string.Empty;
}
