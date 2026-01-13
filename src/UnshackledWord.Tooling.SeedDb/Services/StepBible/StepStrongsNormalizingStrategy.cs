using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepStrongsNormalizingStrategy : IFileParserStrategy
{
    private readonly IStepGreekWordsRepository _greekRepo;
    private readonly IStepHebrewWordsRepository _hebRepo;
    private readonly IStepStrongsToVersesRepository _versesRepo;

    public StepStrongsNormalizingStrategy(IStepGreekWordsRepository greekRepo, IStepHebrewWordsRepository hebRepo, IStepStrongsToVersesRepository versesRepo)
    {
        _greekRepo = greekRepo;
        _hebRepo = hebRepo;
        _versesRepo = versesRepo;
    }

    public async Task SaveToDatabase(string _, CancellationToken token = default)
    {
        var filter = new StepStrongsFilter
        {
            IncludeExtendedStrongs = ["G0001"],
        };

        var count = await _versesRepo.CountByFilterAsync(filter, token);
        if (count > 0)
        {
            return;
        }

        var gFilter = new StepGreekWordFilter();
        gFilter.Columns = [ nameof(StepGreekWordDbo.BibleBookId), nameof(StepGreekWordDbo.Chapter), nameof(StepGreekWordDbo.Verse), nameof(StepGreekWordDbo.DisambiguatedStrongs) ];
        var greekEntries = await _greekRepo.GetByFilterAsync(gFilter, token);
        var hFilter = new StepHebrewWordFilter();
        hFilter.Columns = [ nameof(StepHebrewWordDbo.BibleBookId), nameof(StepHebrewWordDbo.Chapter), nameof(StepHebrewWordDbo.Verse), nameof(StepHebrewWordDbo.DisambiguatedStrongs), nameof(StepHebrewWordDbo.Grammar), nameof(StepHebrewWordDbo.ExpandedStrongTags) ];
        var hebrewEntries = await _hebRepo.GetByFilterAsync(hFilter, token);
        var index = 0;
        var normalisedEntries = new List<StepStrongsToVersesDbo>();

        foreach (var word in greekEntries)
        {
            index++;

            var normalisedStrongs = new StepStrongsToVersesDbo()
            {
                Id = index,
                StrongsNumber = word.DisambiguatedStrongs,
                BibleBookId = word.BibleBookId,
                Chapter = word.Chapter,
                Verse = word.Verse
            };

            normalisedEntries.Add(normalisedStrongs);
        }

        foreach (var word in hebrewEntries)
        {
            if (word.DisambiguatedStrongs.IsNullOrWhiteSpace())
            {
                continue;
            }

            var splitStrongs = word.DisambiguatedStrongs.Split(['\\', '/'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var splitExpanded = word.ExpandedStrongTags!.Split(['\\', '/'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var splitGrammar = word.Grammar.Split(['\\', '/'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i < splitStrongs.Length; i++)
            {
                var part = splitStrongs[i];
                index++;
                var grammar = GetStringAtIndex(splitGrammar, i);
                var expanded = GetStringAtIndex(splitExpanded, i);
                var expandedParts = expanded?.Split('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var hebrew = GetStringAtIndex(expandedParts, 1);
                var gloss = GetStringAtIndex(expandedParts, 2);
                gloss = gloss?.Replace("{", "").Replace("}", "");
                var parsedGloss = ParseGloss(gloss);

                var normalisedStrongs = new StepStrongsToVersesDbo
                {
                    Id = index,
                    StrongsNumber = part.Replace("{", "").Replace("}", ""),
                    BibleBookId = word.BibleBookId,
                    Chapter = word.Chapter,
                    Verse = word.Verse,
                    Grammar = grammar,
                    Hebrew = hebrew,
                    Gloss = parsedGloss?.Gloss,
                    FirstOccuranceBibleBookId = parsedGloss?.FirstBookId,
                    FirstOccuranceChapter = parsedGloss?.FirstChapter,
                    FirstOccuranceVerse = parsedGloss?.FirstVerse,
                    LastOccuranceBibleBookId = parsedGloss?.LastBookId,
                    LastOccuranceChapter = parsedGloss?.LastChapter,
                    LastOccuranceVerse = parsedGloss?.LastVerse,
                    IsRoot = part.Contains('{')
                };

                normalisedEntries.Add(normalisedStrongs);
            }
        }

        foreach (var chunk in normalisedEntries.Chunk(10000))
        {
            await _versesRepo.BulkInsertAsync(chunk, token);
        }
    }

    private string? GetStringAtIndex(string[]? array, int index)
    {
        if (array is null || index < 0 || index >= array.Length)
        {
            return null;
        }

        return array[index];
    }

    //Passover»Passover@Exo.12.11-Heb
    //LORD»LORD@Gen.1.1-Heb
    private (string Gloss, int? FirstBookId, int? FirstChapter, int? FirstVerse, int? LastBookId, int? LastChapter, int? LastVerse)? ParseGloss(string? gloss)
    {
        if (gloss.IsNullOrWhiteSpace())
        {
            return null;
        }

        if (!gloss.Contains('»'))
        {
            return (gloss, null, null, null, null, null, null);
        }

        var parts = gloss.Split('»', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var mainGloss = GetStringAtIndex(parts, 0);
        var referencePart = GetStringAtIndex(parts, 1)!.Replace("+", "");

        var referenceParts = referencePart?.Split(['@'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (referenceParts is null || referenceParts.Length == 1)
        {
            return (gloss, null, null, null, null, null, null);
        }

        var mainGloss2 = GetStringAtIndex(referenceParts, 0);
        mainGloss2 = mainGloss == mainGloss2 ? "" : $":{mainGloss2}";
        var reference = GetStringAtIndex(referenceParts, 1)!.Replace("-0-", "-");
        var referenceDetails = reference?.Split(['.', '-'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var firstBook = GetStringAtIndex(referenceDetails, 0);
        var chapter = int.Parse(GetStringAtIndex(referenceDetails, 1)!);
        var verse = int.Parse(GetStringAtIndex(referenceDetails, 2)!.Replace("a", "").Replace("b", "").Replace("+", ""));
        var firstBibleBook = BibleBook.FindByAbbreviation(firstBook!);
        var lastBook = GetStringAtIndex(referenceDetails, 3);
        var finalGloss = $"{mainGloss}{mainGloss2}";

        if (lastBook.IsNotNullOrWhiteSpace() && lastBook.Equals("+"))
        {
            return (finalGloss, firstBibleBook?.Id!, chapter, verse, null, null, null);
        }

        if (referenceDetails?.Length == 4 && int.TryParse(lastBook, out var lastBookInt))
        {
            return (finalGloss, firstBibleBook?.Id!, chapter, verse, firstBibleBook!.Value.Id, chapter, lastBookInt);
        }

        if (referenceDetails?.Length == 5)
        {
            var lastChapter = int.Parse(GetStringAtIndex(referenceDetails, 3)!);
            var lastVerse = int.Parse(GetStringAtIndex(referenceDetails, 4)!.Replace("a", "").Replace("b", "").Replace("+", ""));

            return ($"{mainGloss}:{mainGloss2}", firstBibleBook?.Id!, chapter, verse, firstBibleBook!.Value.Id, lastChapter, lastVerse);
        }

        if (lastBook is null && referenceDetails?.Length == 3)
        {
            return (finalGloss, firstBibleBook?.Id!, chapter, verse, null, null, null);
        }

        var lastBibleBook = BibleBook.FindByAbbreviation(lastBook!.Replace("+", ""));

        return (finalGloss, firstBibleBook?.Id!, chapter, verse, lastBibleBook!.Value.Id, null, null);
    }
}
