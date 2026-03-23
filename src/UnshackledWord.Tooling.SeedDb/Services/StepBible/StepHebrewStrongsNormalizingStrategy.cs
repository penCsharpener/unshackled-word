using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepHebrewStrongsNormalizingStrategy : IFileParserStrategy
{
    private readonly IStepHebrewWordsRepository _hebRepo;
    private readonly IStepHebrewWordsNormalizedRepository _versesRepo;
    private readonly ILogger<StepHebrewStrongsNormalizingStrategy> _logger;
    private static string[] _noneSuffixStrings = ["־", "׃", "׀", "׆", "a"];

    /*
    to reset and redo the seeding
    TRUNCATE TABLE "unshackled-word"."StepHebrewWordsNormalized" RESTART IDENTITY;
    TRUNCATE TABLE "unshackled-word"."StepHebrewWordsNormalizedToHebrewWords" RESTART IDENTITY;
    TRUNCATE TABLE "unshackled-word"."Elb1871HebrewMapping" RESTART IDENTITY;
    */
    public StepHebrewStrongsNormalizingStrategy(IStepHebrewWordsRepository hebRepo,
        IStepHebrewWordsNormalizedRepository versesRepo,
        ILogger<StepHebrewStrongsNormalizingStrategy> logger)
    {
        _hebRepo = hebRepo;
        _versesRepo = versesRepo;
        _logger = logger;
    }

    public async Task SaveToDatabase(string _, CancellationToken token = default)
    {
        var filter = new StepNormalizedHebrewWordsFilter();

        var count = await _versesRepo.CountByFilterAsync(filter, token);
        if (count > 0)
        {
            _logger.LogInformation("Normalized Hebrew words already imported...");
            return;
        }

        var hFilter = new StepHebrewWordFilter();
        hFilter.Columns =
        [
            nameof(StepHebrewWordDbo.Id), nameof(StepHebrewWordDbo.BibleBookId), nameof(StepHebrewWordDbo.Chapter),
            nameof(StepHebrewWordDbo.Verse), nameof(StepHebrewWordDbo.LxxRefId), nameof(StepHebrewWordDbo.DisambiguatedStrongs),
            nameof(StepHebrewWordDbo.Hebrew), nameof(StepHebrewWordDbo.Grammar), nameof(StepHebrewWordDbo.ExpandedStrongTags)
        ];
        var hebrewEntries = await _hebRepo.GetByFilterAsync(hFilter, token);
        var index = 0;
        // var normalisedEntries = new List<StepStrongsToVersesDbo>();
        var normalisedHebrewWordsDict = new Dictionary<StepHebrewWordsNormalizedDbo, List<int>>();
        var bridgeList = new List<StepHebrewWordsNormalizedToHebrewWordDbo>();
        var normalizedIndex = 1;

        foreach (var word in hebrewEntries)
        {
            if (word.DisambiguatedStrongs.IsNullOrWhiteSpace())
            {
                continue;
            }

            var splitStrongs = word.DisambiguatedStrongs.Split(['\\', '/'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var splitExpanded = word.ExpandedStrongTags!.Split(['\\', '/'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var splitGrammar = word.Grammar.Split(['\\', '/'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var positionInWord = 1;

            for (var i = 0; i < splitStrongs.Length; i++)
            {
                var part = splitStrongs[i];
                index++;
                var grammar = GetStringAtIndex(splitGrammar, i);
                var expanded = GetStringAtIndex(splitExpanded, i);
                var expandedParts = expanded?.Split('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var hebrew = GetStringAtIndex(expandedParts, 1)!;
                var gloss = GetStringAtIndex(expandedParts, 2);
                string? suffixType = null;
                gloss = gloss?.Replace("{", "").Replace("}", "");
                var parsedGloss = ParseGloss(gloss);

                // check if it actually contains Hebrew consonants
                if (!hebrew.Any(c => c is >= '\u05D0' and <= '\u05EA'))
                {
                    if (_noneSuffixStrings.All(x => x != hebrew))
                    {
                        // if not it's the grammar code for pronomial suffixes
                        suffixType = hebrew;
                        hebrew = string.Empty;
                    }
                }

                // var normalisedStrongs = new StepStrongsToVersesDbo
                // {
                //     Id = index,
                //     StrongsNumber = part.Replace("{", "").Replace("}", ""),
                //     BibleBookId = word.BibleBookId,
                //     Chapter = word.Chapter,
                //     Verse = word.Verse,
                //     Grammar = grammar,
                //     Hebrew = hebrew,
                //     Gloss = parsedGloss?.Gloss!,
                //     FirstOccuranceBibleBookId = parsedGloss?.FirstBookId,
                //     FirstOccuranceChapter = parsedGloss?.FirstChapter,
                //     FirstOccuranceVerse = parsedGloss?.FirstVerse,
                //     LastOccuranceBibleBookId = parsedGloss?.LastBookId,
                //     LastOccuranceChapter = parsedGloss?.LastChapter,
                //     LastOccuranceVerse = parsedGloss?.LastVerse,
                //     IsRoot = part.Contains('{')
                // };

                var normalisedHebrew = new StepHebrewWordsNormalizedDbo
                {
                    Hebrew = hebrew,
                    Grammar = grammar,
                    SuffixCode = suffixType,
                    IsRoot = part.Contains('{'),
                    StrongsNumber = part.Replace("{", "").Replace("}", ""),
                };

                var hebNormToWordRelation = new StepHebrewWordsNormalizedToHebrewWordDbo
                {
                    StepHebrewWordsId = word.Id,
                    StepHebrewWordsNormalizedId = normalizedIndex,
                    PositionInWord = positionInWord,
                    TestHebrewNormalisedWord = normalisedHebrew.Hebrew,
                    TestHebrewWord = word.Hebrew
                };

                // normalisedEntries.Add(normalisedStrongs);
                if (normalisedHebrewWordsDict.ContainsKey(normalisedHebrew))
                {
                    hebNormToWordRelation.StepHebrewWordsNormalizedId = normalisedHebrewWordsDict.Keys.First(x => x.Equals(normalisedHebrew)).Id;
                    normalisedHebrewWordsDict[normalisedHebrew].Add(word.Id);
                }
                else
                {
                    normalisedHebrew.Id = normalizedIndex;
                    hebNormToWordRelation.StepHebrewWordsNormalizedId = normalisedHebrew.Id;
                    normalisedHebrewWordsDict[normalisedHebrew] = [word.Id];
                    normalizedIndex++;
                }

                bridgeList.Add(hebNormToWordRelation);
                positionInWord++;
            }
        }

        foreach (var chunk in normalisedHebrewWordsDict.Keys.Chunk(10000))
        {
            await _versesRepo.BulkInsertAsync(chunk, token);
        }

        foreach (var chunk in bridgeList.Chunk(10000))
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
