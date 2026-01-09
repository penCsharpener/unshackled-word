using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.Models.Dto;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class Elb1871WordRepository : IElb1871WordRepository
{
    private readonly IDbReader _dbReader;
    private readonly ILogger<Elb1871WordRepository> _logger;
    private readonly IDbWriter _dbWriter;

    public Elb1871WordRepository(IDbWriter dbWriter, IDbReader dbReader, ILogger<Elb1871WordRepository> logger)
    {
        _dbWriter = dbWriter;
        _dbReader = dbReader;
        _logger = logger;
    }

    public async Task<List<Elb1871WordDbo>> GetWordForVerseAsync(int bookId, int chapterId, int verseId, CancellationToken token = default)
    {
        var sql = $"""
                   select *
                   from {Elb1871WordDbo.DboName}
                   where     "{nameof(Elb1871WordDbo.BibleBookId)}" = {bookId}
                         and "{nameof(Elb1871WordDbo.Chapter)}"     = {chapterId}
                         and "{nameof(Elb1871WordDbo.Verse)}"       = {verseId}
                   order by "{nameof(Elb1871WordDbo.BibleBookId)}",
                            "{nameof(Elb1871WordDbo.Chapter)}"    ,
                            "{nameof(Elb1871WordDbo.Verse)}"      ,
                            "{nameof(Elb1871WordDbo.PositionInVerse)}";
                   """;

        var result = await _dbReader.ReadAsListAsync<Elb1871WordDbo>(sql);

        return result.ToList();
    }

    public async Task<List<Elb1871WordDbo>> GetWordForChapterAsync(int bookId, int chapterId, CancellationToken token = default)
    {
        var sql = $"""
                   select *
                   from {Elb1871WordDbo.DboName}
                   where     "{nameof(Elb1871WordDbo.BibleBookId)}" = {bookId}
                         and "{nameof(Elb1871WordDbo.Chapter)}"     = {chapterId}
                   order by "{nameof(Elb1871WordDbo.BibleBookId)}",
                            "{nameof(Elb1871WordDbo.Chapter)}"    ,
                            "{nameof(Elb1871WordDbo.Verse)}"      ,
                            "{nameof(Elb1871WordDbo.PositionInVerse)}";
                   """;

        var result = await _dbReader.ReadAsListAsync<Elb1871WordDbo>(sql);

        return result.ToList();
    }

    public async Task<List<int>> BulkUpdateStrongsAsync(List<Elb1871WordDbo> modifiedWords, CancellationToken token = default)
    {
        if (modifiedWords.Count == 0)
        {
            return [];
        }

        var modifiedRows = new List<int>();

        foreach (var elbWord in modifiedWords)
        {
            modifiedRows.Add(elbWord.Id);
        }

        var sql = $"UPDATE {Elb1871WordDbo.DboName} SET \"{nameof(Elb1871WordDbo.Strongs)}\"=@Strongs WHERE \"{nameof(Elb1871WordDbo.Id)}\" IN ({modifiedRows.JoinStrings(",")});";

        var param = new { modifiedWords[0].Strongs };

        await _dbWriter.WriteAsync(sql, param);
        _logger.LogInformation("Updated rows with strongs {strongs}: {ids}", param.Strongs, modifiedRows.JoinStrings(","));

        return modifiedRows;
    }

    public async Task<List<Elb1871GrammarUpdateResult>> BulkUpdateGrammarAsync(List<Elb1871WordGrammarDto> modifiedWords, CancellationToken token = default)
    {
        var updatableWords = new List<Elb1871WordDbo>();
        var dictResults = new Dictionary<string, Elb1871GrammarUpdateResult>();

        foreach (var word in modifiedWords)
        {
            var optionalWords = word.OptionalForms.IsNotNullOrWhiteSpace() ? word.OptionalForms.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList() : [];
            optionalWords.Add(word.PlainWord!);
            var allWordForms = optionalWords.Distinct().ToList();
            var paramNames = Enumerable.Range(1, allWordForms.Count).Select(x => $"@PlainWord{x}").ToArray();

            var sqlExactMatch = $"""
                                 select *
                                 from {Elb1871WordDbo.DboName}
                                 where 1=1
                                    and "{nameof(Elb1871WordDbo.PlainWord)}" IN ({paramNames.JoinStrings(", ")})
                                    and "{nameof(Elb1871WordDbo.Lemma)}" is null
                                 order by "{nameof(Elb1871WordDbo.BibleBookId)}", "{nameof(Elb1871WordDbo.Chapter)}", "{nameof(Elb1871WordDbo.Verse)}", "{nameof(Elb1871WordDbo.PositionInVerse)}";
                                 """;

            var param = new DynamicParameters();

            for (int i = 0; i < optionalWords.Count; i++)
            {
                param.Add(paramNames[i], optionalWords[i]);
            }

            var exactMatches = await _dbReader.ReadAsListAsync<Elb1871WordDbo>(sqlExactMatch, param);

            foreach (var wordForm in allWordForms)
            {
                if (!dictResults.ContainsKey(wordForm))
                {
                    dictResults[wordForm] = new Elb1871GrammarUpdateResult
                    {
                        UpdatedLemma = word.Lemma!,
                        UpdatedPlainWord = wordForm,
                        UpdatedPartOfSpeech = word.PartOfSpeech
                    };
                }
            }

            foreach (var match in exactMatches)
            {
                var updateItem = new Elb1871WordDbo
                {
                    Id = match.Id,
                    BibleBookId = match.BibleBookId,
                    Chapter = match.Chapter,
                    Verse = match.Verse,
                    PositionInVerse = match.PositionInVerse,
                    Strongs = match.Strongs,
                    PlainWord = match.PlainWord,
                    Lemma = word.Lemma,
                    PartOfSpeech = word.PartOfSpeech
                };

                updatableWords.Add(updateItem);
            }
        }

        var updateParam = new DynamicParameters();
        var sqlSb = new StringBuilder();

        for (var index = 0; index < updatableWords.Count; index++)
        {
            var word = updatableWords[index];
            var sql = $"""
                       update {Elb1871WordDbo.DboName}
                            set "{nameof(Elb1871WordDbo.Lemma)}" = @Lemma0{index}, "{nameof(Elb1871WordDbo.PartOfSpeech)}" = @PartOfSpeech0{index}
                       where "{nameof(Elb1871WordDbo.Id)}" = {word.Id};
                       """;
            sqlSb.AppendLine(sql);

            updateParam.Add($"@Lemma0{index}", word.Lemma);
            updateParam.Add($"@PartOfSpeech0{index}", word.PartOfSpeech);
            dictResults[word.PlainWord!].UpdatedIds.Add(word.Id);
        }

        await _dbWriter.WriteAsync(sqlSb.ToString(), updateParam);

        return dictResults.Select(x => x.Value).ToList();
    }
}
