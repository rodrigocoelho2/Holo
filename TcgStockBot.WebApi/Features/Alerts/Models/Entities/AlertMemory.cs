using System.Text.Json;
using System.Text.Json.Serialization;

namespace TcgStockBot.WebApi.Features.Alerts.Models.Entities;

/// <summary>
/// What the bot remembers about a product it already alerted (one entry per alert key in alert-state.json):
/// when it first alerted and the last price it alerted at, so a lower price can alert again.
/// </summary>
[JsonConverter(typeof(AlertMemoryJsonConverter))]
public sealed class AlertMemory
{
    /// <summary>Gets or sets when the product was first alerted (UTC).</summary>
    public DateTimeOffset AlertedAt { get; set; }

    /// <summary>Gets or sets the price of the last alert; null when unknown (entries written before v3.1).</summary>
    public decimal? Price { get; set; }
}

/// <summary>
/// Reads both formats of alert-state.json: the v3.0 one (key → timestamp string) and the current one
/// (key → { alertedAt, price }). Always writes the current one.
/// </summary>
internal sealed class AlertMemoryJsonConverter : JsonConverter<AlertMemory>
{
    /// <inheritdoc />
    public override AlertMemory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new AlertMemory { AlertedAt = reader.GetDateTimeOffset() };
        }

        var memory = new AlertMemory();
        using var document = JsonDocument.ParseValue(ref reader);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.NameEquals("alertedAt") && property.Value.ValueKind == JsonValueKind.String)
            {
                memory.AlertedAt = property.Value.GetDateTimeOffset();
            }
            else if (property.NameEquals("price") && property.Value.ValueKind == JsonValueKind.Number)
            {
                memory.Price = property.Value.GetDecimal();
            }
        }

        return memory;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AlertMemory value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("alertedAt", value.AlertedAt);
        if (value.Price is { } price)
        {
            writer.WriteNumber("price", price);
        }
        else
        {
            writer.WriteNull("price");
        }

        writer.WriteEndObject();
    }
}
