using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871Lemmatizer;

public sealed class LemmatizerRepository
{
    private readonly IDbReader _dbReader;
    private readonly IDbWriter _dbWriter;
    private readonly ILogger<LemmatizerRepository> _logger;

    public LemmatizerRepository(IDbReader dbReader, IDbWriter dbWriter, ILogger<LemmatizerRepository> logger)
    {
        _dbReader = dbReader;
        _dbWriter = dbWriter;
        _logger = logger;
    }

    public async Task<List<string>> GetElberfelderWordsAsync(CancellationToken token = default)
    {
        var sql = """
                  select elb."PlainWord" --, count(elb."PlainWord") CountPlainWord
                  from "unshackled-word"."Elb1871Words" elb
                  where elb."BibleBookId" >= 40
                  group by elb."PlainWord"
                  order by count(elb."PlainWord") desc;
                  """;

        var collection = await _dbReader.ReadAsListAsync<StringWrapper>(sql);
        return collection.Select(x => x.PlainWord).ToList();
    }

    public Task UpdateElberfelderLemmaAsync(CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    public async Task<int> GetCountAsync(CancellationToken token = default)
    {
        var sql = """
                  select COUNT(*)
                  from "unshackled-word"."Elb1871Words"
                  """;

        return await _dbReader.ExecuteScalarAsync<int>(sql);
    }

    private class StringWrapper
    {
        public string PlainWord { get; set; } = default!;
    }
}
