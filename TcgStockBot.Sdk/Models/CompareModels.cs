using TcgStockBot.Library.Enums;

namespace TcgStockBot.Sdk.Models;

/// <summary>One product across stores (mirror of the WebApi CompareGroupDTO).</summary>
public sealed class CompareGroupDTO
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SetName { get; set; } = string.Empty;
    public string KindLabel { get; set; } = string.Empty;
    public List<CompareListingDTO> Listings { get; set; } = [];
    public decimal? Msrp { get; set; }
    public MsrpBasis? MsrpBasis { get; set; }
    public string MsrpLabel { get; set; } = string.Empty;
}

/// <summary>One store's listing of a compared product.</summary>
public sealed class CompareListingDTO
{
    public string HistoryKey { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public decimal? CurrentPrice { get; set; }
    public bool CurrentInStock { get; set; }
    public decimal? LowestPrice { get; set; }
    public DateTimeOffset? LowestPriceAt { get; set; }
    public decimal? LastDropFrom { get; set; }
    public decimal? LastDropTo { get; set; }
    public DateTimeOffset? LastDropAt { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public bool IsManual { get; set; }
    public List<PricePointDTO> Points { get; set; } = [];
}

/// <summary>Input of CompareAssign.</summary>
public sealed class CompareAssignRequest
{
    public string HistoryKey { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
}
