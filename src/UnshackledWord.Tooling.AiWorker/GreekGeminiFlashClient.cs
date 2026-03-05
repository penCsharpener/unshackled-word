using UnshackledWord.Application.Extensions;
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
                                             2. SPLIT VERBS: Map split German verb parts (e.g., 'aus' in 'geht...aus') to the same Greek 'StepWordId' and 'Strongs'.
                                             3. ADDED WORDS: If a German word has no Greek source, set 'IsAddedWord': true and 'StepWordId': null.
                                             4. PARENT MAPPING: For German words where IsAddedWord is true (e.g., articles like 'der' or particles), set ParentElbWordId to the ElbWordId of the semantic head of the phrase. For articles and adjectives, this is the Noun. For auxiliary verbs or split particles, this is the Main Verb. If 'der' refers to 'Tisch' in 'der kleine Tisch', map 'der' to the ID of 'Tisch', even if 'kleine' is in between.
                                             5. VERSE INTEGRITY: Never map a German word ID to a Greek word from a different verse.
                                             6. NO MARKDOWN: Return only raw JSON.
                                             """;

    public GreekGeminiFlashClient(GeminiClient client, ILogger<GreekGeminiFlashClient> logger) : base(client, logger) { }

    public async Task<List<VerseDataList<ElbStepAiMapping>>> GetElbStepMappings(List<VerseDataList<ElbVerseData>> elbWords,
        List<VerseDataList<StepGreekVerseData>> stepWords, CancellationToken token = default)
    {
        var germanVerseJson = elbWords.ToNonIndentedJson();
        var greekVerseJson = stepWords.ToNonIndentedJson();

        var prompt = $"""
                      German Words: {germanVerseJson}
                      Greek Words: {greekVerseJson}
                      """;

        return await SubmitAsync(prompt, GreekSystemInstruction, GeminiModelType.Flash2_5, token);
    }
}
