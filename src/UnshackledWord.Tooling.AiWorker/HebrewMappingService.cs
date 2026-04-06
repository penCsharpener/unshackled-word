using FastEndpoints;
using FluentValidation;
using Polly;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.AiWorker.Models.Hebrew;

namespace UnshackledWord.Tooling.AiWorker;

public class HebrewMappingService
{
    private readonly HebrewMappingRepository _repo;
    private readonly HebrewGeminiFlashClient _client;
    private readonly ILogger<HebrewMappingService> _logger;

    public HebrewMappingService(HebrewMappingRepository repo, HebrewGeminiFlashClient client, ILogger<HebrewMappingService> logger)
    {
        _repo = repo;
        _client = client;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken token = default)
    {
        var policy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(5, retryAttempt) + (10 * retryAttempt)), onRetry:
                (ex, timeSpan, retryCount, context) =>
                {
                    _logger.LogError(ex, "Retry {retryCount} after {delay} delay.", retryCount, timeSpan.ToString(@"mm\:ss"));
                });

        var bookNames = await _repo.GetBookNamesAsync();
        const int versesPerTask = 10;
        const int parallelTasks = 5;

        while (!token.IsCancellationRequested)
        {
            try
            {
                var missingVerses = await _repo.GetMissingVersesAsync();

                if (missingVerses.IsNullOrEmpty())
                {
                    _logger.LogWarning("no more verses left to map.");
                    break;
                }

                foreach (var requestBatches in missingVerses.Take(versesPerTask * parallelTasks).Chunk(versesPerTask).Chunk(parallelTasks))
                {
                    var tasks = requestBatches.Select(async verses =>
                    {
                        try
                        {
                            var elbWords = await _repo.GetElbVerseDataAsync(verses.Select(x => x.HebRefId).ToArray());
                            var stepWords = await _repo.GetStepHebrewVerseDataAsync(verses.Select(x => x.LxxRefId).ToArray());
                            var first = verses.GetFirstReference();
                            var last = verses.GetLastReference();

                            _logger.LogInformation("Submitting {firstRef}-{lastRef} of a total of {totalVerses} verses with {totalWords} words", first, last, verses.Length, elbWords.Select(x => x.Data.Count()).Sum());

                            var response = await _client.GetElbStepMappings(elbWords, stepWords, token);
                            var validator = new AiResponseStatsValidation();
                            var stats = new AiResponseStats(response, elbWords, stepWords);
                            var validationResult = await validator.ValidateAsync(stats, token);

                            if (!validationResult.IsValid)
                            {
                                var errorSummary = validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}");
                                _logger.LogWarning("Validation failed: {Errors}", errorSummary.JoinStrings("||"));
                                return null;
                            }

                            _logger.LogInformation("\"HebRefId\" IN ({ids})", elbWords.Select(x => x.RefId).JoinStrings(","));

                            return new CombinedMappingResult<StepHebrewVerseData>
                            {
                                Response = response.Where(x => x.Data.All(k => k.ElbWordId > 0)).ToList(),
                                ElbWords = elbWords.SelectMany(x => x.Data).ToList(),
                                StepWords = stepWords.SelectMany(x => x.Data).ToList()
                            };
                        }
                        catch (TaskCanceledException)
                        {
                            return null;
                        }
                        catch (KeyNotFoundException keyEx)
                        {
                            _logger.LogError("Failed to map verses {idRefs}: {msg}", verses.Select(x => x.HebRefId).JoinStrings(","), keyEx.Message);
                            return null;
                        }
                        catch (Exception ex)
                        {
                            // 5. Catch exception internally per request so the other 4 aren't aborted
                            _logger.LogError(ex, "Failed to map verses {idRefs}", verses.Select(x => x.HebRefId).JoinStrings(","));

                            // Return null to signify this specific request failed
                            return null;
                        }
                    });

                    var batchResults = await Task.WhenAll(tasks);

                    // 7. Filter out the failed requests (the ones that returned null)
                    var successfulResults = batchResults.Where(x => x is not null).ToList();

                    if (successfulResults.Count == 0)
                    {
                        continue;
                    }

                    // 8. Combine the lists from the successful requests.
                    var combinedResponses = successfulResults.SelectMany(x => x!.Response).ToList();
                    var combinedElbWords = successfulResults.SelectMany(x => x!.ElbWords).ToList();
                    var combinedStepWords = successfulResults.SelectMany(x => x!.StepWords).ToList();

                    // 9. Insert all gathered results from the up to 5 requests in a single database call
                    await _repo.InsertMappingsAsync(combinedResponses, combinedElbWords, combinedStepWords);
                }
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
            catch (KeyNotFoundException keyEx)
            {
                _logger.LogError(keyEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during processing Greek mapping");
            }
        }
    }
}

public sealed class AiResponseStatsValidation : Validator<AiResponseStats>
{
    public AiResponseStatsValidation()
    {
        RuleFor(x => x.FaultyStepIds).Must(x => x.Length == 0).WithMessage((model, ints) => $"AI assigned StepIds which don't exist in the source data: {ints.JoinStrings(",")}");
        RuleFor(x => x.FaultyElbWordIds).Must(x => x.Length == 0).WithMessage((model, ints) => $"AI assigned ElbWordIds which don't exist in the source data: {ints.JoinStrings(",")}");
        RuleFor(x => x.FaultyHebRefIds).Must(x => x.Length == 0).WithMessage((model, ints) => $"AI produced wrong HebRefIds: {ints.JoinStrings(",")}");
        RuleFor(x => x.OverassignedRows).Must(x => x.Length < 20).WithMessage((model, ints) => $"AI produced overassigned rows for ElbWordIds: {ints.JoinStrings(",")}.");
        RuleFor(x => x.WronglyAssignedGermanWordParts).Must(x => x.Count == 0).WithMessage((model, ints) => $"AI wrongly split German words: {ints.JoinStrings(",")}.");
    }
}

public struct AiResponseStats
{
    private readonly List<ElbStepAiMapping> _results;
    private readonly List<ElbVerseData> _elbWords;
    private readonly List<StepHebrewVerseData> _stepWords;
    public List<VerseDataList<ElbStepAiMapping>> Results { get; }
    public List<VerseDataList<ElbVerseData>> ElbWords { get; }
    public List<VerseDataList<StepHebrewVerseData>> StepWords { get; }

    public AiResponseStats(List<VerseDataList<ElbStepAiMapping>> results, List<VerseDataList<ElbVerseData>> elbWords,  List<VerseDataList<StepHebrewVerseData>> stepWords)
    {
        Results = results;
        _results = results.SelectMany(x => x.Data).ToList();
        ElbWords = elbWords;
        _elbWords = elbWords.SelectMany(x => x.Data).ToList();
        StepWords = stepWords;
        _stepWords = stepWords.SelectMany(x => x.Data).ToList();
        EvaluateResults();
    }

    private void EvaluateResults()
    {
        TotalMappingEntries = _results.Count;
        TotalElbWords = _elbWords.Count;
        TotalStepWords = _stepWords.Count;
        var totalHebRefIdFromResponse = Results.Select(x => x.RefId).Distinct().ToList();
        var totalHebRefIdFromRequest = ElbWords.Select(x => x.RefId).Distinct().ToList();
        FaultyHebRefIds = totalHebRefIdFromResponse.Except(totalHebRefIdFromRequest).ToArray();

        var totalElbWordIds = _elbWords.Select(y => y.Id).ToList();
        var totalMappedElbWordIds = _results.Select(x => x.ElbWordId).Concat(_results.Where(x => x.ParentElbWordId.HasValue)
            .Select(x => x.ParentElbWordId!.Value)).Distinct().ToList();
        FaultyElbWordIds = totalMappedElbWordIds.Except(totalElbWordIds).ToArray();
        var totalStepWordIds = _stepWords.Select(y => y.Id).ToList();
        var totalMappedStepWordIds =
            _results.Where(x => x.StepWordId.HasValue).Select(x => x.StepWordId!.Value).ToList();
        FaultyStepIds = totalMappedStepWordIds.Except(totalStepWordIds).ToArray();
        foreach (var item in _results.Where(x => x is { StepWordId: not null, IsAddedWord: true, ParentElbWordId: not null }))
        {
            item.ParentElbWordId = null;
        }

        foreach (var item in _results.Where(x => x.InternalElbWord == x.GermanWordPart))
        {
            item.GermanWordPart = null;
        }
        OverassignedRows = _results.Where(x => x is { StepWordId: not null, IsAddedWord: true, ParentElbWordId: not null }).Select(x => x.ElbWordId).ToArray();
        CorrectlyMappedRows = _results.Where(x => x is { StepWordId: not null, IsAddedWord: false, ParentElbWordId: null } || (!x.StepWordId.HasValue && x is { IsAddedWord: true, ParentElbWordId: not null })).Select(x => x.ElbWordId).ToArray();

        foreach (var group in _results.Where(x => x.GermanWordPart.IsNotNullOrWhiteSpace())
                     .GroupBy(x => new { x.ElbWordId })
                     .Where(x => x.Count() > 1))
        {
            var elbWord = _elbWords.FirstOrDefault(x => x.Id == group.Key.ElbWordId);

            if (elbWord is null)
            {
                continue;
            }

            var mappedEntries = group.Select(x => x).ToList();
            foreach (var mappedEntry in mappedEntries)
            {
                if (!elbWord.German.Contains(mappedEntry.GermanWordPart!))
                {
                    WronglyAssignedGermanWordParts.Add(group.Key.ElbWordId);
                }
            }
        }
    }

    public int TotalMappingEntries { get; set; }
    public int TotalElbWords { get; set; }
    public int TotalStepWords { get; set; }
    // AI returned StepIds that were not present in StepWords
    public int[] FaultyStepIds { get; set; } = default!;
    // AI returned StepIds that were not present in ElbWords
    public int[] FaultyElbWordIds { get; set; } = default!;
    // AI returned made up Bible References
    public int[] FaultyHebRefIds { get; set; } = default!;
    // AI ignored mapping rules and assigned ElbWordId, StepWordId, IsAddedWord, ParentGermanWordId and GermanWordPart
    public int[] OverassignedRows { get; set; } = default!;
    // AI adhered to mapping rules
    // ElbWordId must have StepWordId
    // When StepWordId is null, IsAddedWord == true and ParentGermanWordId points to ElbWordId of parent word
    public int[] CorrectlyMappedRows { get; set; } = default!;
    // When one ElbWordId has 2+ StepWordIds assigned, GermanWordPart must contain German word parts
    public List<int> WronglyAssignedGermanWordParts { get; set; } = [];
}
