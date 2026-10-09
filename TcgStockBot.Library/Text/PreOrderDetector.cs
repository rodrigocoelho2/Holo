namespace TcgStockBot.Library.Text;

/// <summary>
/// Recognises a pre-order from the product title ("[Pré-venda]", "Pre-Order", "Reserva", "Preventa"…), accents,
/// case and hyphens ignored. Providers that know it from their data (Amazon PREORDER, Awin stock_status) say so too.
/// </summary>
public static class PreOrderDetector
{
    private static readonly string[] Phrases =
    [
        "pre venda", "prevenda", "pre order", "preorder", "pre reserva", "prereserva", "reserva",
        "pre venta", "preventa", "pre sale", "presale", "precommande", "vorbestellung",
    ];

    /// <summary>
    /// Whether the title marks the product as a pre-order.
    /// </summary>
    /// <param name="title">The title as the store shows it.</param>
    /// <returns>True for a pre-order.</returns>
    public static bool IsPreOrder(string title)
    {
        var normalized = $" {string.Join(' ', TextNormalizer.Words(title))} ";
        return Phrases.Any(phrase => normalized.Contains($" {phrase} ", StringComparison.Ordinal));
    }
}
