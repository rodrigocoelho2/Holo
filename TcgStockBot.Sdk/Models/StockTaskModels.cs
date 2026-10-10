using TcgStockBot.Library.Enums;

namespace TcgStockBot.Sdk.Models;

/// <summary>Read model of a stock task (mirror of the WebApi StockTaskDTO).</summary>
public sealed class StockTaskDTO
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public List<string> StoreNames { get; set; } = [];
    public string SearchQuery { get; set; } = string.Empty;
    public List<string> RequiredKeywords { get; set; } = [];
    public List<string> ExcludedKeywords { get; set; } = [];
    public decimal MaxPrice { get; set; }

    /// <summary>How a price is judged.</summary>
    public PriceRule PriceRule { get; set; }

    /// <summary>How far above the MSRP still alerts, in percent.</summary>
    public int MsrpTolerancePercent { get; set; } = 10;

    /// <summary>The product kinds wanted; empty means every kind.</summary>
    public List<string> Kinds { get; set; } = [];
    public TaskLanguage Language { get; set; }
    public TcgGame Game { get; set; }
}

/// <summary>Input of StockTaskSave.</summary>
public sealed class StockTaskSaveRequest
{
    public bool IsNew { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<string> StoreNames { get; set; } = [];
    public string SearchQuery { get; set; } = string.Empty;
    public List<string> RequiredKeywords { get; set; } = [];
    public List<string> ExcludedKeywords { get; set; } = [];
    public decimal MaxPrice { get; set; }

    /// <summary>How a price is judged.</summary>
    public PriceRule PriceRule { get; set; }

    /// <summary>How far above the MSRP still alerts, in percent.</summary>
    public int MsrpTolerancePercent { get; set; } = 10;

    /// <summary>The product kinds wanted; empty means every kind.</summary>
    public List<string> Kinds { get; set; } = [];
    public TaskLanguage Language { get; set; } = TaskLanguage.English;
    public TcgGame Game { get; set; } = TcgGame.Pokemon;
}

/// <summary>Output of StockTaskSave.</summary>
public sealed class StockTaskSaveResponse
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>Input of StockTaskEnable (null Id = every task).</summary>
public sealed class StockTaskEnableRequest
{
    public string? Id { get; set; }
    public bool Enabled { get; set; }
}
