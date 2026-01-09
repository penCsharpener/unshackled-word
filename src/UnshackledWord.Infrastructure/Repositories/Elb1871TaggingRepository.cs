using System.Text;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class Elb1871TaggingRepository : IElb1871TaggingRepository
{
    private readonly IDbReader _dbReader;
    private readonly IDbWriter _dbWriter;
    private readonly IElb1871WordRepository _elbRepo;
    private readonly ILogger<Elb1871TaggingRepository> _logger;

    public Elb1871TaggingRepository(IDbReader dbReader, IDbWriter dbWriter, IElb1871WordRepository elbRepo, ILogger<Elb1871TaggingRepository> logger)
    {
        _dbReader = dbReader;
        _dbWriter = dbWriter;
        _elbRepo = elbRepo;
        _logger = logger;
    }

    public async Task<int> BulkInsertAsync(List<Elb1871SrGntTaggingDbo> taggings, CancellationToken token = default)
    {
        if (taggings.Count == 0)
        {
            return 0;
        }

        var insertedRows = 0;

        foreach (var tagging in taggings)
        {
            try
            {
                var sql = $"""
                           INSERT INTO "unshackled-word"."Elb1871SrGntTagging"
                           ("Elb1871WordsId", "SrGntWordsId", "PositionInVerse")
                           VALUES(@{nameof(tagging.Elb1871WordsId)}, @{nameof(tagging.SrGntWordsId)}, @{nameof(tagging.PositionInVerse)})
                           ON CONFLICT ("{nameof(tagging.Elb1871WordsId)}", "{nameof(tagging.SrGntWordsId)}") DO NOTHING;
                           """;

                await _dbWriter.WriteAsync(sql, tagging);
                insertedRows++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not insert tagging entry for ElbId {elbId} and SrId {srId}", tagging.Elb1871WordsId, tagging.SrGntWordsId);
            }
        }

        _logger.LogInformation("Inserted {count} elb taggings", insertedRows);

        return insertedRows;
    }

    public async Task<CreateMappingResult> CreateSingleMappingAsync(Elb1871WordDbo elbWord, SrGntWordDbo srWord,
        CancellationToken token = default)
    {
        var matches = await GetTagMatchesAsync(elbWord, srWord, onlySingleVerse: true);
        var book = (BibleBook)elbWord.BibleBookId;

        if (matches.ElbWords.Count == 0)
        {
            _logger.LogWarning("No words in verse {book} {bookId} {chapter}:{verse}! Impossible case.", elbWord.BibleBookId, book.Abbreviations[0], elbWord.Chapter, elbWord.Verse);
            return new(0, []);
        }

        if (matches.ElbWords.Count != matches.SrWords.Count)
        {
            _logger.LogWarning("Word count in verse {book} {bookId} {chapter}:{verse} doesn't match.", elbWord.BibleBookId, book.Abbreviations[0], elbWord.Chapter, elbWord.Verse);
            return new(0, []);
        }

        if (matches.SrWords.Any(x => x.Strongs != matches.SrWords[0].Strongs))
        {
            _logger.LogWarning("Not all SR words have the same strongs number in verse {book} {bookId} {chapter}:{verse}", elbWord.BibleBookId, book.Abbreviations[0], elbWord.Chapter, elbWord.Verse);
            return new(0, []);
        }

        var updatedWordsInVerse = new List<Elb1871WordDbo>();
        var updatedTagsinVerse = new List<Elb1871SrGntTaggingDbo>();

        for (int i = 0; i < matches.ElbWords.Count; i++)
        {
            var e =  matches.ElbWords[i];
            var s =  matches.SrWords[i];

            updatedWordsInVerse.Add(new()
            {
                Id = e.Id,
                Strongs = e.Strongs
            });

            updatedTagsinVerse.Add(new ()
            {
                Elb1871WordsId = e.Id,
                SrGntWordsId = s.Id,
                PositionInVerse = i + 1
            });
        }

        var ids = await _elbRepo.BulkUpdateStrongsAsync(updatedWordsInVerse, token);
        var count = await BulkInsertAsync(updatedTagsinVerse, token);
        return new(count, ids);
    }

    public async Task<CreateMappingResult> CreateMappingsAsync(Elb1871WordDbo elbWord, SrGntWordDbo srWord, CancellationToken token = default)
    {
        await CreateSingleMappingAsync(elbWord, srWord, token);

        var matches = await GetTagMatchesAsync(elbWord, srWord, onlySingleVerse: false);
        var elbUpdates = new List<Elb1871WordDbo>();
        var taggings = new List<Elb1871SrGntTaggingDbo>();

        foreach (var elbVerse in matches.ElbWords.GroupBy(x => new { x.BibleBookId, x.Chapter, x.Verse }))
        {
            var wordsInVerse = elbVerse.Select(x => x).ToList();
            var bbRef = elbVerse.Key;
            var book = (BibleBook)elbWord.BibleBookId;

            var srInVerse = matches.SrWords.Where(x =>
                x.BibleBookId == bbRef.BibleBookId &&
                x.Chapter == bbRef.Chapter &&
                x.Verse == bbRef.Verse).ToList();

            if (wordsInVerse.Count == 0)
            {
                _logger.LogWarning("No words in verse {book} {bookId} {chapter}:{verse}! Impossible case.", bbRef.BibleBookId, book.Abbreviations[0], bbRef.Chapter, bbRef.Verse);
                continue;
            }

            if (wordsInVerse.Count != srInVerse.Count)
            {
                _logger.LogWarning("Word count in verse {book} {bookId} {chapter}:{verse} doesn't match.", bbRef.BibleBookId, book.Abbreviations[0], bbRef.Chapter, bbRef.Verse);
                continue;
            }

            if (srInVerse.Any(x => x.Strongs != srInVerse[0].Strongs))
            {
                _logger.LogWarning("Not all SR words have the same strongs number in verse {book} {bookId} {chapter}:{verse}", bbRef.BibleBookId, book.Abbreviations[0], bbRef.Chapter, bbRef.Verse);
                continue;
            }

            for (var index = 0; index < wordsInVerse.Count; index++)
            {
                var elbItem = wordsInVerse[index];
                var srItem = srInVerse[index];

                var elbUpdateItem = new Elb1871WordDbo
                {
                    Id = elbItem.Id,
                    BibleBookId = elbItem.BibleBookId,
                    Chapter = elbItem.Chapter,
                    Verse = elbItem.Verse,
                    Strongs = srItem.Strongs
                };

                elbUpdates.Add(elbUpdateItem);

                var tag = new Elb1871SrGntTaggingDbo
                {
                    PositionInVerse = index + 1,
                    Elb1871WordsId = elbItem.Id,
                    SrGntWordsId = srItem.Id
                };

                taggings.Add(tag);
            }
        }

        var ids = await _elbRepo.BulkUpdateStrongsAsync(elbUpdates, token);
        var count = await BulkInsertAsync(taggings, token);
        return new(count, ids);
    }

    private async Task<TagMatches> GetTagMatchesAsync(Elb1871WordDbo elbWord, SrGntWordDbo srWord, bool onlySingleVerse)
    {
        var lemmaClause = elbWord.Lemma.IsNullOrWhiteSpace()
            ? ""
            : $"or e.\"{nameof(Elb1871WordDbo.Lemma)}\" = @{nameof(Elb1871WordDbo.Lemma)}";
        var elbSql = $"""
                      select
                          e."{nameof(Elb1871WordDbo.Id)}",
                          e."{nameof(Elb1871WordDbo.BibleBookId)}",
                          e."{nameof(Elb1871WordDbo.Chapter)}",
                          e."{nameof(Elb1871WordDbo.Verse)}",
                          e."{nameof(Elb1871WordDbo.PlainWord)}",
                          e."{nameof(Elb1871WordDbo.PositionInVerse)}",
                          e."{nameof(Elb1871WordDbo.Strongs)}"
                      from {Elb1871WordDbo.DboName} e
                      where (e."{nameof(Elb1871WordDbo.PlainWord)}" = @{nameof(Elb1871WordDbo.PlainWord)} {lemmaClause})
                        {(onlySingleVerse ? $"  and e.\"{nameof(Elb1871WordDbo.BibleBookId)}\" = {elbWord.BibleBookId}" : "")}
                        {(onlySingleVerse ? $"  and e.\"{nameof(Elb1871WordDbo.Chapter)}\" = {elbWord.Chapter}" : "")}
                        {(onlySingleVerse ? $"  and e.\"{nameof(Elb1871WordDbo.Verse)}\" = {elbWord.Verse}" : "")}
                        and e."{nameof(Elb1871WordDbo.Strongs)}" is null
                        and e."{nameof(Elb1871WordDbo.BibleBookId)}" >= 40
                        and e."{nameof(Elb1871WordDbo.BibleBookId)}" <= 66
                      """;

        var elbWords = await _dbReader.ReadAsListAsync<Elb1871WordDbo>(elbSql, new { elbWord.PlainWord, elbWord.Lemma });

        var srSql = $"""
                     select
                         s."{nameof(SrGntWordDbo.Id)}",
                         s."{nameof(SrGntWordDbo.BibleBookId)}",
                         s."{nameof(SrGntWordDbo.Chapter)}",
                         s."{nameof(SrGntWordDbo.Verse)}",
                         s."{nameof(SrGntWordDbo.Lemma)}",
                         s."{nameof(SrGntWordDbo.PositionInVerse)}",
                         s."{nameof(SrGntWordDbo.Strongs)}"
                     from {SrGntWordDbo.DboName} s
                     where s."{nameof(SrGntWordDbo.Lemma)}" = @{nameof(SrGntWordDbo.Lemma)}
                       {(onlySingleVerse ? $"  and s.\"{nameof(SrGntWordDbo.BibleBookId)}\" = {srWord.BibleBookId}" : "")}
                       {(onlySingleVerse ? $"  and s.\"{nameof(SrGntWordDbo.Chapter)}\" = {srWord.Chapter}" : "")}
                       {(onlySingleVerse ? $"  and s.\"{nameof(SrGntWordDbo.Verse)}\" = {srWord.Verse}" : "")}
                       and s."{nameof(SrGntWordDbo.BibleBookId)}" >= 40
                       and s."{nameof(SrGntWordDbo.BibleBookId)}" <= 66
                     """;

        var srWords = await _dbReader.ReadAsListAsync<SrGntWordDbo>(srSql, new { srWord.Lemma });

        return new TagMatches(elbWords.ToList(), srWords.ToList());
    }

    private record TagMatches(List<Elb1871WordDbo> ElbWords, List<SrGntWordDbo> SrWords);
}
