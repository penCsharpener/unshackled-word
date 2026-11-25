using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.GreekEditions.GetGreekEditions;

namespace UnshackledWord.Application.Features.GreekEditions;

public interface IGreekEditionService
{
    Task<GetGreekEditionsResponse> GetCatalogAsync(CancellationToken token = default);
}

public sealed class GreekEditionService : IGreekEditionService
{
    private readonly IGreekEditionRepository _repository;

    public GreekEditionService(IGreekEditionRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetGreekEditionsResponse> GetCatalogAsync(CancellationToken token = default)
    {
        var rows = await _repository.GetCatalogAsync(token);
        return new GetGreekEditionsResponse
        {
            Editions = rows.Select(r => new GreekEditionResponse { Code = r.Code, Name = r.Name, Year = r.Year, Kind = r.Kind, IsMain = r.IsMain }).ToList(),
        };
    }
}
