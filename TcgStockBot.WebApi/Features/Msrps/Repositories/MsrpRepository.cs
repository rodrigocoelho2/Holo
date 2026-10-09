using System.Text.Json;
using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Features.Msrps.Models.Entities;

namespace TcgStockBot.WebApi.Features.Msrps.Repositories;

/// <summary>
/// The reference prices embedded in the assembly (Features/Msrps/Data/msrp-defaults.json). A data/msrp.json in the
/// same format replaces them, for changing a value without a new build.
/// </summary>
/// <param name="dataContext">The data context (file paths).</param>
internal sealed class MsrpRepository(BotDataContext dataContext) : RepositoryBase(dataContext), IMsrpRepository
{
    private const string DefaultsResourceName = "TcgStockBot.WebApi.Features.Msrps.Data.msrp-defaults.json";

    private static readonly Lazy<string> DefaultsJson = new(static () =>
    {
        using var stream = typeof(MsrpRepository).Assembly.GetManifestResourceStream(DefaultsResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {DefaultsResourceName} not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <inheritdoc />
    public async Task<List<MsrpEntry>> ListAsync() =>
        await JsonFile.ReadAsync<List<MsrpEntry>>(DataContext.MsrpPath, CancellationToken.None)
        ?? JsonSerializer.Deserialize<List<MsrpEntry>>(DefaultsJson.Value, JsonFile.SerializerOptions)
        ?? [];
}
