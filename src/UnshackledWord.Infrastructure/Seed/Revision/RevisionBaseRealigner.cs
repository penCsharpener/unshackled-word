using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Revision;

namespace UnshackledWord.Infrastructure.Seed.Revision;

/// <summary>
/// After <c>rebase</c> rebuilt <c>Elb1871Words</c> (roadmap 0011.13): recomputes <c>BasePositionInVerse</c> and <c>Change</c> of every
/// ACTIVE variant against the new base words, with the same carry-over the editor uses. Text, mapping, versions and history stay as
/// they are; <c>BaseTextHash</c> is deliberately not touched, so a variant whose base verse changed shows up in the "Basistext
/// geändert" list of the revision overview. Variants of verses that no longer exist are left alone and counted.
/// </summary>
public sealed class RevisionBaseRealigner
{
    private const string SelectActive = """
        SELECT rv."Id"       AS "Id"
             , rv."HebRefId" AS "HebRefId"
        FROM "unshackled-word"."RevisionVerses" rv
        WHERE rv."IsActive"
        ORDER BY rv."HebRefId"
               , rv."RevisionId"
        """;

    private const string SelectWords = """
        SELECT rw."Id"              AS "Id"
             , rw."PositionInVerse" AS "Position"
             , rw."PlainWord"       AS "PlainWord"
        FROM "unshackled-word"."RevisionWords" rw
        WHERE rw."RevisionVerseId" = @Id
        ORDER BY rw."PositionInVerse"
        """;

    private const string UpdateWord = """
        UPDATE "unshackled-word"."RevisionWords"
        SET "BasePositionInVerse" = @BasePosition
          , "Change"              = @Change
        WHERE "Id" = @Id
        """;

    private readonly IDbReader _dbReader;
    private readonly IDbConnectionFactory _factory;
    private readonly IRevisionRepository _revisions;

    public RevisionBaseRealigner(IDbReader dbReader, IDbConnectionFactory factory, IRevisionRepository revisions)
    {
        _dbReader = dbReader;
        _factory = factory;
        _revisions = revisions;
    }

    /// <summary>Returns (variants realigned, variants whose verse is missing in the new base text).</summary>
    public async Task<(int Realigned, int Missing)> RunAsync(CancellationToken token = default)
    {
        var variants = (await _dbReader.ReadAsListAsync<ActiveRow>(SelectActive)).ToList();
        var realigned = 0;
        var missing = 0;

        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        foreach (var variant in variants)
        {
            var baseWords = await _revisions.GetBaseWordsAsync(variant.HebRefId, token);

            if (baseWords.Count == 0)
            {
                missing++;
                continue;
            }

            var words = (await connection.QueryAsync<WordRow>(SelectWords, new { variant.Id }, transaction)).ToList();
            var carry = RevisionCarry.Carry(
                baseWords.Select(w => new RevisionCarryParentWord(w.PositionInVerse, w.PlainWord, w.PositionInVerse, RevisionCarry.Same)).ToList(),
                [],
                words.Select(w => new RevisionCarryToken(w.Position, w.PlainWord)).ToList());
            var byPosition = carry.Words.ToDictionary(w => w.Position);

            foreach (var word in words)
            {
                var found = byPosition.GetValueOrDefault(word.Position);
                await connection.ExecuteAsync(UpdateWord, new { word.Id, BasePosition = found?.BasePositionInVerse, Change = found?.Change ?? RevisionCarry.New }, transaction);
            }

            realigned++;
        }

        transaction.Commit();

        return (realigned, missing);
    }

    private sealed class ActiveRow
    {
        public int Id { get; init; }
        public int HebRefId { get; init; }
    }

    private sealed class WordRow
    {
        public int Id { get; init; }
        public int Position { get; init; }
        public string? PlainWord { get; init; }
    }
}
