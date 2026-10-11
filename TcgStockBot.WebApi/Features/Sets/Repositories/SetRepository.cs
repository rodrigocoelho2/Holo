using System.Text.Json;
using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Features.Sets.Models.Entities;

namespace TcgStockBot.WebApi.Features.Sets.Repositories;

/// <summary>
/// The sets embedded in the assembly (Features/Sets/Data/set-defaults.json). New sets come from the releases, so
/// nothing here is written.
/// </summary>
internal sealed class SetRepository : ISetRepository
{
    private const string DefaultsResourceName = "TcgStockBot.WebApi.Features.Sets.Data.set-defaults.json";

    private static readonly Lazy<string> DefaultsJson = new(static () =>
    {
        using var stream = typeof(SetRepository).Assembly.GetManifestResourceStream(DefaultsResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {DefaultsResourceName} not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <inheritdoc />
    public Task<List<TcgSet>> ListAsync() =>
        Task.FromResult(JsonSerializer.Deserialize<List<TcgSet>>(DefaultsJson.Value, JsonFile.SerializerOptions) ?? []);
}
