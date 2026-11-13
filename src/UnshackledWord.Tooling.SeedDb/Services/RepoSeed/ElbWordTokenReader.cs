using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed;

/// <summary>Reads the German words of <c>Elb1871Words</c> as anchoring tokens (read-only).</summary>
public sealed class ElbWordTokenReader
{
    private readonly IDbReader _dbReader;

    public ElbWordTokenReader(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    private const string SelectBookWords = """
        select
             w."HebRefId"        as "Ref"
            ,w."PositionInVerse" as "Pos"
            ,w."PlainWord"       as "Form"
        from "unshackled-word"."Elb1871Words" w
        where w."BibleBookId" = @bookId
        order by w."HebRefId", w."PositionInVerse"
        """;

    private const string SelectBookIds = """
        select distinct
             w."BibleBookId" as "BookId"
        from "unshackled-word"."Elb1871Words" w
        order by 1
        """;

    public async Task<List<AnchorToken>> ReadBookAsync(int bookId)
    {
        var rows = await _dbReader.ReadAsListAsync<WordRow>(SelectBookWords, new { bookId });

        return rows.Select(r => AnchorToken.ForWord(r.Ref, r.Pos, r.Form)).ToList();
    }

    public async Task<List<int>> ReadBookIdsAsync()
    {
        return (await _dbReader.ReadAsListAsync<int>(SelectBookIds)).ToList();
    }

    private sealed class WordRow
    {
        public int Ref { get; init; }
        public int Pos { get; init; }
        public string? Form { get; init; }
    }
}
