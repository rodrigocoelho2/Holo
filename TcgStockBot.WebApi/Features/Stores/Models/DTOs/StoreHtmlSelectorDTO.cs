using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Models.DTOs;

/// <summary>
/// CSS selectors of an HTML store.
/// </summary>
public sealed class StoreHtmlSelectorDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public StoreHtmlSelectorDTO()
    {
    }

    /// <summary>
    /// Maps an entity to the DTO.
    /// </summary>
    /// <param name="entity">The stored selectors.</param>
    public StoreHtmlSelectorDTO(StoreHtmlSelector entity)
    {
        SearchUrlTemplate = entity.SearchUrlTemplate;
        ProductSelector = entity.ProductSelector;
        TitleSelector = entity.TitleSelector;
        PriceSelector = entity.PriceSelector;
        LinkSelector = entity.LinkSelector;
        OutOfStockSelector = entity.OutOfStockSelector;
        InStockSelector = entity.InStockSelector;
    }

    /// <summary>Gets or sets the search URL ({query} placeholder) or category URL.</summary>
    public string SearchUrlTemplate { get; set; } = string.Empty;

    /// <summary>Gets or sets the product card selector.</summary>
    public string ProductSelector { get; set; } = string.Empty;

    /// <summary>Gets or sets the title selector.</summary>
    public string TitleSelector { get; set; } = string.Empty;

    /// <summary>Gets or sets the price selector.</summary>
    public string PriceSelector { get; set; } = string.Empty;

    /// <summary>Gets or sets the link selector.</summary>
    public string LinkSelector { get; set; } = string.Empty;

    /// <summary>Gets or sets the out-of-stock marker selector.</summary>
    public string OutOfStockSelector { get; set; } = string.Empty;

    /// <summary>Gets or sets the in-stock marker selector.</summary>
    public string InStockSelector { get; set; } = string.Empty;

    /// <summary>
    /// Maps the DTO to an entity.
    /// </summary>
    /// <returns>The entity.</returns>
    public StoreHtmlSelector StoreHtmlSelectorNew() => new()
    {
        SearchUrlTemplate = (SearchUrlTemplate ?? string.Empty).Trim(),
        ProductSelector = (ProductSelector ?? string.Empty).Trim(),
        TitleSelector = (TitleSelector ?? string.Empty).Trim(),
        PriceSelector = (PriceSelector ?? string.Empty).Trim(),
        LinkSelector = (LinkSelector ?? string.Empty).Trim(),
        OutOfStockSelector = (OutOfStockSelector ?? string.Empty).Trim(),
        InStockSelector = (InStockSelector ?? string.Empty).Trim(),
    };
}
