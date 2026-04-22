using UnshackledWord.Domain.Extensions;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.AiWorker.Models.Hebrew;

namespace UnshackledWord.Tooling.AiWorker;

public struct AiResponseStats<T> where T : IVerseDataWithoutOrder
{
    private readonly List<ElbStepAiMapping> _results;
    private readonly List<ElbVerseData> _elbWords;
    private readonly List<T> _stepWords;
    public List<VerseDataList<ElbStepAiMapping>> Results { get; }
    public List<VerseDataList<ElbVerseData>> ElbWords { get; }
    public List<VerseDataList<T>> StepWords { get; }
    public int[] Verses { get; set; } = default!;
    public string VerseRange { get; set; } = default!;
    public List<(int RefId, int ElbId)> WrongElbIds { get; set; } = [];
    public List<(int RefId, int StepId)> WrongStepIds { get; set; } = [];

    public AiResponseStats(List<VerseDataList<ElbStepAiMapping>> results, List<VerseDataList<ElbVerseData>> elbWords,  List<VerseDataList<T>> stepWords)
    {
        Results = results;
        _results = results.SelectMany(x => x.Data).ToList();
        ElbWords = elbWords;
        _elbWords = elbWords.SelectMany(x => x.Data).ToList();
        StepWords = stepWords;
        _stepWords = stepWords.SelectMany(x => x.Data).ToList();
        EvaluateResults();
    }

    /// <summary>
    /// Check that ids belong to one verse have not been assigned to another verse
    /// </summary>
    private void ValidateMappingWithinVerseBoundaries()
    {
        var dictResults = Results.ToDictionary(x => x.RefId, x => x.Data.ToList());
        var dictElbWords = ElbWords.ToDictionary(x => x.RefId, x => x.Data.Select(y => y.Id).ToList());
        var dictStepWords = StepWords.ToDictionary(x => x.RefId, x => x.Data.Select(b => b.Id).ToList());

        foreach (var (refId, data) in dictResults)
        {
            var elbWordsInVerse = dictElbWords[refId];
            var stepWordsInVerse = dictStepWords[refId];

            foreach (var mapping in data)
            {
                var elbId = mapping.ElbWordId;
                var stepId = mapping.StepWordId;
                var parentId = mapping.ParentElbWordId;

                if (!elbWordsInVerse.Contains(elbId))
                {
                    WrongElbIds.Add((refId, elbId));
                }

                if (stepId.HasValue && !stepWordsInVerse.Contains(stepId.Value))
                {
                    WrongStepIds.Add((refId, stepId.Value));
                }

                if (parentId.HasValue && !elbWordsInVerse.Contains(parentId.Value))
                {
                    WrongElbIds.Add((refId, parentId.Value));
                }
            }
        }
    }

    private void EvaluateResults()
    {
        TotalMappingEntries = _results.Count;
        TotalElbWords = _elbWords.Count;
        TotalStepWords = _stepWords.Count;
        Verses = ElbWords.Select(x => x.RefId).OrderBy(x => x).ToArray();
        VerseRange = $"{Verses.First()}-{Verses.Last()}";

        FixOverMapping();
        ValidateMappingWithinVerseBoundaries();

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
                    mappedEntry.GermanWordPart = null;
                    mappedEntry.PartOrder = null;
                    //WronglyAssignedGermanWordParts.Add(group.Key.ElbWordId);
                }
            }
        }
    }

    private void FixOverMapping()
    {
        foreach (var mapping in _results)
        {
            if (mapping is { IsAddedWord: false, ParentElbWordId: not null })
            {
                mapping.ParentElbWordId = null;
            }

            if (mapping is { IsAddedWord: true, ParentElbWordId: null })
            {
                mapping.IsAddedWord = false;
            }

            if (mapping is { StepWordId: not null, IsAddedWord: true, ParentElbWordId: not null })
            {
                mapping.ParentElbWordId = null;
            }

            if (mapping.InternalElbWord == mapping.GermanWordPart)
            {
                mapping.GermanWordPart = null;
            }

            if (mapping.PartOrder.HasValue && mapping.GermanWordPart.IsNullOrWhiteSpace())
            {
                mapping.PartOrder = null;
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
