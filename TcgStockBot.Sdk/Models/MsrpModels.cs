using TcgStockBot.Library.Enums;

namespace TcgStockBot.Sdk.Models;

/// <summary>The product kinds and the MSRP table (mirror of the WebApi MsrpListDTO).</summary>
public sealed class MsrpListDTO
{
    public List<ProductKindDTO> Kinds { get; set; } = [];
    public List<MsrpDTO> Entries { get; set; } = [];
}

/// <summary>A product kind.</summary>
public sealed class ProductKindDTO
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>The MSRP of one product kind of one game.</summary>
public sealed class MsrpDTO
{
    public string Game { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public MsrpBasis Basis { get; set; }
    public string Source { get; set; } = string.Empty;
}
