using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class StrongsLexiconRepository : IStrongsLexiconRepository
{
    private readonly IDbReader _dbReader;

    public StrongsLexiconRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<LexiconEntry>> GetEntriesAsync(StrongsNumber strongs, CancellationToken token = default)
    {
        // Aramaic words also use H numbers.
        int[] languageIds = strongs.Language == StrongsLanguage.Greek
            ? [(int)StrongsLanguage.Greek]
            : [(int)StrongsLanguage.Hebrew, (int)StrongsLanguage.Aramaic];

        const string sql = """
                           SELECT ssl."LanguageId"
                                , ssl."Number"
                                , ssl."Extra"
                                , ssl."DisambiguatedExtra"
                                , ssl."OriginalWord"
                                , ssl."Transliteration"
                                , ssl."Morphology"
                                , ssl."Gloss"
                                , ssl."Lexicon"
                           FROM "unshackled-word"."StepStrongsLexicon" ssl
                           WHERE 1=1
                               AND ssl."LanguageId" = ANY(@LanguageIds)
                               AND ssl."Number" = @Number
                               AND (@Extra::varchar IS NULL OR ssl."Extra" = @Extra)
                           ORDER BY ssl."Extra"
                           """;

        var rows = await _dbReader.ReadAsListAsync<LexiconEntry>(sql, new { LanguageIds = languageIds, strongs.Number, strongs.Extra });
        return rows.ToList();
    }
}
