using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Models.DTOs;

/// <summary>
/// Read model of a store test result.
/// </summary>
public sealed class StoreTestResultDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public StoreTestResultDTO()
    {
    }

    /// <summary>
    /// Maps a stored test result.
    /// </summary>
    /// <param name="name">The store name.</param>
    /// <param name="entity">The stored result.</param>
    public StoreTestResultDTO(string name, StoreTestRecord entity)
    {
        Name = name;
        At = entity.At;
        Status = entity.Status;
        Query = entity.Query;
        Results = entity.Results;
        Relevant = entity.Relevant;
        Matches = entity.Matches;
        Sample = entity.Sample;
        DurationMs = entity.DurationMs;
        Message = entity.Message;
    }

    /// <summary>Gets or sets the store name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets when the test ran (UTC).</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Gets or sets the outcome.</summary>
    public StoreTestStatus Status { get; set; }

    /// <summary>Gets or sets the search text used.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Gets or sets how many products the store returned.</summary>
    public int Results { get; set; }

    /// <summary>Gets or sets how many of them a task recognises.</summary>
    public int Relevant { get; set; }

    /// <summary>Gets or sets how many of them would alert now.</summary>
    public int Matches { get; set; }

    /// <summary>Gets or sets one returned product title.</summary>
    public string Sample { get; set; } = string.Empty;

    /// <summary>Gets or sets how long the test took.</summary>
    public long DurationMs { get; set; }

    /// <summary>Gets or sets the explanation of a failure (empty on success).</summary>
    public string Message { get; set; } = string.Empty;
}
