using UnshackledWord.Application.Extensions;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.AiWorker.Models.Hebrew;
using GeminiClient = Google.GenAI.Client;

namespace UnshackledWord.Tooling.AiWorker;

public class HebrewGeminiFlashClient : GeminiFlashAbstractClient
{
    // private const string ModelName = "gemini-3-flash-preview";

    private const string HebrewSystemInstruction = """
                                                  You are a linguistic expert mapping the Elberfelder 1871 German NT to STEP Bible Hebrew data.
                                                  RULES:
                                                  1. OUTPUT: Return a JSON object matching the provided schema.
                                                  2. SPLIT VERBS: Map split German verb parts (e.g., 'aus' in 'geht...aus') to the same Hebrew 'StepWordId'.
                                                  3. ADDED WORDS: If a German word has no Hebrew source, set 'IsAddedWord': true and 'StepWordId': null.
                                                  4. PARENT MAPPING: For German words where IsAddedWord is true (e.g., articles like 'der' or particles), set ParentElbWordId to the ElbWordId of the semantic head of the phrase. For articles and adjectives, this is the Noun. For auxiliary verbs or split particles, this is the Main Verb. If 'der' refers to 'Tisch' in 'der kleine Tisch', map 'der' to the ID of 'Tisch', even if 'kleine' is in between.
                                                  5. VERSE INTEGRITY: Never map a German word ID to a Hebrew word from a different verse.
                                                  6. NO MARKDOWN: Return only raw JSON.
                                                  7. The Hebrew is normalized and the mapped StepIds will then link it back to the normal Hebrew word. Do not merge Hebrew parts.
                                                  """;

    public HebrewGeminiFlashClient(GeminiClient client, ILogger<GreekGeminiFlashClient> logger) : base(client, logger) { }

    public async Task<List<VerseDataList<ElbStepAiMapping>>> GetElbStepMappings(IEnumerable<VerseDataList<ElbVerseData>> elbWords,
        IEnumerable<VerseDataList<StepHebrewVerseData>> stepWords, CancellationToken token = default)
    {
        var germanVerseJson = elbWords.Select(x => new VerseDataList<ElbVerseDataWithoutOrder>()
        {
            BookId = x.BookId,
            Chapter = x.Chapter,
            Verse = x.Verse,
            Data = x.Data.Select(k => new ElbVerseDataWithoutOrder
            {
                Id = k.Id,
                German = k.German
            }).ToList()
        }).ToNonIndentedJson();
        var hebrewVerseJson = stepWords.Select(x => new VerseDataList<StepHebrewVerseDataWithOrder>()
        {
            BookId = x.BookId,
            Chapter = x.Chapter,
            Verse = x.Verse,
            Data = x.Data.Select(k => new StepHebrewVerseDataWithOrder
            {
                Id = k.Id,
                Hebrew = k.Hebrew
            }).ToList()
        }).ToNonIndentedJson();

        var prompt = $"""
                      German Words: {germanVerseJson}
                      Hebrew Words: {hebrewVerseJson}
                      """;

        return await SubmitAsync(prompt, HebrewSystemInstruction, token);
    }
}
