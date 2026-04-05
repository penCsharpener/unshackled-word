using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.AiWorker.Models.Hebrew;
using GeminiClient = Google.GenAI.Client;

namespace UnshackledWord.Tooling.AiWorker;

public class HebrewGeminiFlashClient : GeminiFlashAbstractClient
{
    // private const string ModelName = "gemini-3-flash-preview";
    // a) if there are multiple German words possible candidates for a parent then prefer less frequent words as parent, ie. 'er gemacht hatte' the parent is 'gemacht', not 'hatte'
    // ElbWordId|StepWordId|IsAddedWord|ParentElbWordId|PartOrder|GermanWordPart
    private const string HebrewSystemInstruction = """
                                                  You are a linguistic expert mapping the Elberfelder 1871 German NT to STEP Bible Hebrew data.
                                                  RULES:
                                                  1. OUTPUT: Return a JSON object matching the provided schema.
                                                     INPUT: The Input format is an array with an array [Ref BookId:Chapter:Verse[(Id,Word)...]] of Bible References and tuples of ids and words in the verse.
                                                  2. SPLIT VERBS: Map split German verb parts (geht...aus') to the same StepWordId. For example a) 'עָשָׂה' to 'er', 'gemacht', 'hatte' or b) 'כָּבַשׁ' to 'machet', 'sie', 'euch', 'untertan'
                                                  3. ADDED WORDS: If a German word has no Hebrew source, set 'IsAddedWord' true and StepWordId: null.
                                                  4. PARENT MAPPING: For German words with IsAddedWord = true (e.g., articles, particles), set ParentElbWordId to the ElbWordId of the semantic head of the phrase. For articles and adjectives, this is the Noun. For auxiliary verbs or split particles, this is the Main Verb. If 'der' refers to 'Tisch' in 'der kleine Tisch', map 'der' to the ID of 'Tisch'.
                                                  5. COMPOUND WORDS: If a German compound word corresponds to two distinct Hebrew words, split the German word into its constituent parts (e.g., 'Gerstenernte' into 'Gersten' for 'שְׂעֹרִֽים' and 'ernte' for 'קְצִ֥יר'.). Store them in 'GermanWordPart'. Assign each part its unique StepWordId corresponding to its source word, but maintain the original ElbWordId for both parts to ensure they can be reconstructed.
                                                  6. VERSE INTEGRITY: Never map a ElbWordId to a Hebrew word from a different verse.
                                                  7. NO MARKDOWN: Return only raw JSON.
                                                  8. COLUMN COUNT: Ensure that the Data property only has strings that consist of 6 pipe delimited columns.
                                                  9. COMPLETE MAPPING: Ensure that ALL ElbWordIds and StepWordIds have been mapped! Duplicate ElbWordIds must have a value for 'GermanWordPart'.
                                                  """;

    private IntShrinkDictionary _elbIdMapping = new();
    private IntShrinkDictionary _stepIdMapping = new();

    public HebrewGeminiFlashClient(GeminiClient client, ILogger<GreekGeminiFlashClient> logger) : base(client, logger) { }

    public async Task<List<VerseDataList<ElbStepAiMapping>>> GetElbStepMappings(IEnumerable<VerseDataList<ElbVerseData>> elbWords,
        IEnumerable<VerseDataList<StepHebrewVerseData>> stepWords, CancellationToken token = default)
    {
        // replace original ids with smaller ints starting from 1
        // use two dictionaries to be able to map the ids back after the response comes back
        // this should also further reduce the costs as ids are much shorter

        var germanVerseJson = elbWords.ToWithoutOrder().ReduceIds(_elbIdMapping).ToDelimitedString();
        var hebrewVerseJson = stepWords.ToWithoutOrder().ReduceIds(_stepIdMapping).ToDelimitedString();

        var prompt = $"""
                      German Words: {germanVerseJson}
                      Hebrew Words: {hebrewVerseJson}
                      """;

        var response = await SubmitAsync(prompt, HebrewSystemInstruction, GeminiModelType.Flash3_1LitePreview, token);
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
