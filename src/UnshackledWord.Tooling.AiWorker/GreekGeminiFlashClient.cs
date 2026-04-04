using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.AiWorker.Models.Greek;
using GeminiClient = Google.GenAI.Client;

namespace UnshackledWord.Tooling.AiWorker;

public class GreekGeminiFlashClient : GeminiFlashAbstractClient
{
    // Skip thinking and provide only the direct mapping in JSON format.

    private const string GreekSystemInstruction = """
                                             You are a linguistic mapping tool mapping the Elberfelder 1871 German NT to STEP Bible Greek data.
                                             RULES:
                                             1. OUTPUT: Return a JSON object matching the provided schema.
                                                INPUT: The Input format is [Ref BookId:Chapter:Verse[(wordId<Word>)(wordId<Word>)]] which is an array of Bible References containing another array with tuples of words in the verse.
                                             2. SPLIT VERBS: Map split German verb parts (e.g., 'aus' in 'geht...aus') to the same Greek 'StepWordId' and 'Strongs'.
                                             3. ADDED WORDS: If a German word has no Greek source, set 'IsAddedWord': true and 'StepWordId': null.
                                             4. PARENT MAPPING: For German words where IsAddedWord is true (e.g., articles like 'der' or particles), set ParentId to the German word id of the semantic head of the phrase. For articles and adjectives, this is the Noun. For auxiliary verbs or split particles, this is the Main Verb. If 'der' refers to 'Tisch' in 'der kleine Tisch', map 'der' to the ID of 'Tisch', even if 'kleine' is in between.
                                             5. COMPOUND WORDS: If a German compound word corresponds to two distinct Greek words, split the German word into its constituent parts (e.g., 'Gerstenernte' into 'Gersten' and 'ernte'). Assign each part a unique 'StepId' corresponding to its source word, but maintain the original 'ElbWordId' for both parts to ensure they can be reconstructed. Use the 'GermanWordPart' property to store the split fragments exactly as they appear in the compound and in the order they need to be joined back together.
                                             6. VERSE INTEGRITY: Never map a German word ID to a Greek word from a different verse.
                                             7. NO MARKDOWN: Return only raw JSON.
                                             8. COLUMN COUNT: Ensure that the Data property only has strings that consist of 6 pipe delimited columns.
                                             9. COMPLETE MAPPING: Ensure that all German and Greek Words id have been mapped. Duplicate German Ids must have a value for 'GermanWordPart'. No id can be returned as 0!
                                             """;

    private IntShrinkDictionary _elbIdMapping = new();
    private IntShrinkDictionary _stepIdMapping = new();

    public GreekGeminiFlashClient(GeminiClient client, ILogger<GreekGeminiFlashClient> logger) : base(client, logger) { }

    public async Task<List<VerseDataList<ElbStepAiMapping>>> GetElbStepMappings(List<VerseDataList<ElbVerseData>> elbWords,
        List<VerseDataList<StepGreekVerseData>> stepWords, CancellationToken token = default)
    {
        // replace original ids with smaller ints starting from 1
        // use two dictionaries to be able to map the ids back after the response comes back
        // this should also further reduce the costs as ids are much shorter

        var germanVerseJson = elbWords.ToWithoutOrder().ReduceIds(_elbIdMapping).ToDelimitedString();
        var greekVerseJson = stepWords.ToWithoutOrder().ReduceIds(_stepIdMapping).ToDelimitedString();

        var prompt = $"""
                      German Words: {germanVerseJson}
                      Greek Words: {greekVerseJson}
                      """;

        var response = await SubmitAsync(prompt, GreekSystemInstruction, GeminiModelType.Flash3_1LitePreview, token);
        var result = response.ToTypedResponse().RestoreIds(_elbIdMapping, (dictionary, mapping) =>
        {
            var originalId = dictionary.GetOriginalId(mapping.ElbWordId);
            mapping.ElbWordId = originalId;
            if (mapping.ParentElbWordId is null)
            {
                return;
            }

            var originalParentId = dictionary.GetOriginalId(mapping.ParentElbWordId.Value);
            mapping.ParentElbWordId = originalParentId;
        }).RestoreIds(_stepIdMapping, (dictionary, mapping) =>
        {
            if (mapping.StepWordId is null)
            {
                return;
            }

            var originalId = dictionary.GetOriginalId(mapping.StepWordId.Value);
            mapping.StepWordId = originalId;
        }).ToList();

        _elbIdMapping.Reset();
        _stepIdMapping.Reset();

        foreach (var x in result)
        {
            x.RefId = new BibleReference(x.BookId, x.Chapter, x.Verse).RefId;
        }

        return result;
    }
}
