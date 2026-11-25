using System.Globalization;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.VerseSimilarity;

namespace UnshackledWord.Infrastructure.VerseSimilarity;

/// <summary>
/// Dapper loader of the Strong's sequences per verse (roadmap 0027.1). One row per verse with the tokens joined by a space in SQL, so the
/// whole testament is about 23k (OT) or 8k (NT) rows. Strong's come as stored by the STEP import: <c>G0886</c>, <c>H0430G</c>
/// (four digits, optional disambiguation letter, no braces); they are used as they are.
/// </summary>
public sealed class VerseTokenRepository : IVerseTokenRepository
{
    private const int HebrewLanguageId = 0;
    private const int GreekLanguageId = 2;

    private readonly IDbReader _dbReader;

    public VerseTokenRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<VerseTokenSequence>> LoadAsync(Testament testament, CancellationToken token = default)
    {
        var sql = testament == Testament.New ? GreekSql : HebrewSql;
        var rows = await _dbReader.ReadAsListAsync<VerseTokenRow>(sql);

        return rows
            .Select(r => new VerseTokenSequence(r.LxxRefId, r.Tokens.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, StrongsLabel>> GetLabelsAsync(IReadOnlyCollection<string> strongs, CancellationToken token = default)
    {
        var wanted = strongs.Distinct(StringComparer.Ordinal).ToList();
        var numbers = wanted
            .Select(s => s.Length >= 5 && int.TryParse(s.AsSpan(1, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1)
            .Where(n => n >= 0)
            .Distinct()
            .ToArray();

        if (numbers.Length == 0)
        {
            return new Dictionary<string, StrongsLabel>();
        }

        const string sql = """
                           SELECT ssl."LanguageId"
                                , ssl."Number"
                                , COALESCE(ssl."Extra", '') "Extra"
                                , ssl."OriginalWord"
                                , ssl."Gloss"
                           FROM "unshackled-word"."StepStrongsLexicon" ssl
                           WHERE 1=1
                               AND ssl."LanguageId" IN (0, 2)
                               AND ssl."Number" = ANY(@Numbers)
                           """;

        var rows = await _dbReader.ReadAsListAsync<LexiconRow>(sql, new { Numbers = numbers });
        var byKey = new Dictionary<string, StrongsLabel>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var prefix = row.LanguageId switch
            {
                HebrewLanguageId => 'H',
                GreekLanguageId => 'G',
                _ => '?',
            };
            var key = string.Create(CultureInfo.InvariantCulture, $"{prefix}{row.Number:D4}{row.Extra.Trim()}");
            byKey.TryAdd(key, new StrongsLabel(key, row.OriginalWord, row.Gloss));
        }

        return wanted
            .Where(byKey.ContainsKey)
            .ToDictionary(s => s, s => byKey[s], StringComparer.Ordinal);
    }

    public async Task<int?> FindLxxRefIdAsync(int hebRefId, CancellationToken token = default)
    {
        const string sql = """
                           SELECT ev."LxxRefId"
                           FROM "unshackled-word"."Elb1871Verses" ev
                           WHERE 1=1
                               AND ev."HebRefId" = @HebRefId
                           """;

        return await _dbReader.ReadFirstOrDefaultAsync<int?>(sql, new { HebRefId = hebRefId });
    }

    /// <summary>Greek: words in at least one main edition (<c>GreekEditions."IsMain"</c>), in <c>PositionInVerse</c> order. Public for the database test.</summary>
    public const string GreekSql = """
                                   SELECT sgw."LxxRefId"
                                        , string_agg(sgw."DisambiguatedStrongs", ' ' ORDER BY sgw."PositionInVerse", sgw."Id") "Tokens"
                                   FROM "unshackled-word"."StepGreekWords" sgw
                                   WHERE 1=1
                                       AND EXISTS (SELECT 1
                                                   FROM "unshackled-word"."StepGreekWordEditions" swe
                                                       JOIN "unshackled-word"."GreekEditions" ge ON ge."Id" = swe."EditionId"
                                                   WHERE 1=1
                                                       AND swe."StepWordId" = sgw."Id"
                                                       AND ge."IsMain")
                                   GROUP BY sgw."LxxRefId"
                                   ORDER BY sgw."LxxRefId"
                                   """;

    /// <summary>Hebrew/Aramaic: root pieces (<c>IsRoot</c>) of the normalized words, by <c>PositionInVerse</c> then <c>PositionInWord</c>. Public for the database test.</summary>
    public const string HebrewSql = """
                                    SELECT shw."LxxRefId"
                                         , string_agg(shn."StrongsNumber", ' ' ORDER BY shw."PositionInVerse", shw."Id", map."PositionInWord") "Tokens"
                                    FROM "unshackled-word"."StepHebrewWords" shw
                                        JOIN "unshackled-word"."StepHebrewWordsNormalizedToHebrewWords" map ON map."StepHebrewWordsId" = shw."Id"
                                        JOIN "unshackled-word"."StepHebrewWordsNormalized" shn ON shn."Id" = map."StepHebrewWordsNormalizedId"
                                    WHERE 1=1
                                        AND shn."IsRoot"
                                    GROUP BY shw."LxxRefId"
                                    ORDER BY shw."LxxRefId"
                                    """;

    private sealed class VerseTokenRow
    {
        public int LxxRefId { get; init; }
        public string Tokens { get; init; } = string.Empty;
    }

    private sealed class LexiconRow
    {
        public int LanguageId { get; init; }
        public int Number { get; init; }
        public string Extra { get; init; } = string.Empty;
        public string OriginalWord { get; init; } = string.Empty;
        public string Gloss { get; init; } = string.Empty;
    }
}
