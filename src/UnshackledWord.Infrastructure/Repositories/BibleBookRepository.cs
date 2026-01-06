using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class BibleBookRepository : IBibleBookRepository
{
    private readonly IDbReader _dbReader;

    public BibleBookRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IEnumerable<BibleBookDbo>> GetBibleBooksAsync(int languageId, CancellationToken token = default)
    {
        var sql = $"""
                   select *
                   from {BibleBookDbo.DboName}
                   where "{nameof(BibleBookDbo.LanguageId)}" = @languageId
                   """;

        var param = new { languageId };
        Console.WriteLine(sql);

        return await _dbReader.ReadAsListAsync<BibleBookDbo>(sql, param);
    }
}
