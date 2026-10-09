using TcgStockBot.WebApi.Features.Msrps.Repositories;

namespace TcgStockBot.WebApi.Features.Msrps.Services;

/// <summary>
/// Default <see cref="IMsrpService"/>.
/// </summary>
/// <param name="msrpRepository">Reference price data access.</param>
internal sealed class MsrpService(IMsrpRepository msrpRepository) : IMsrpService
{
    private readonly IMsrpRepository _msrpRepository = msrpRepository;

    /// <inheritdoc />
    public async Task<MsrpLookup> LookupAsync() => new(await _msrpRepository.ListAsync());
}
