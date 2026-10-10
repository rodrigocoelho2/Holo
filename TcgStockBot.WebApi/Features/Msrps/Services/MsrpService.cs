using TcgStockBot.Library.Services;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Features.Msrps.Models.DTOs;
using TcgStockBot.WebApi.Features.Msrps.Repositories;

namespace TcgStockBot.WebApi.Features.Msrps.Services;

/// <summary>
/// Default <see cref="IMsrpService"/>.
/// </summary>
/// <param name="msrpRepository">The MSRP table.</param>
internal sealed class MsrpService(IMsrpRepository msrpRepository) : IMsrpService
{
    private readonly IMsrpRepository _msrpRepository = msrpRepository;

    /// <inheritdoc />
    public async Task<MsrpLookup> LookupAsync() => new(await _msrpRepository.ListAsync());

    /// <inheritdoc />
    public async Task<ServiceApiResponse<MsrpListDTO>> ListAsync()
    {
        var entries = await _msrpRepository.ListAsync();
        return ServiceApiResponse<MsrpListDTO>.Success(new MsrpListDTO
        {
            Kinds = ProductKindClassifier.Kinds.Select(static kind => new ProductKindDTO { Code = kind.Code, Label = kind.Label }).ToList(),
            Entries = entries.Where(static entry => entry.Price > 0)
                .Select(static entry => new MsrpDTO { Game = entry.Game, Kind = entry.Kind, Price = entry.Price, Basis = entry.Basis, Source = entry.Source })
                .ToList(),
        });
    }
}
