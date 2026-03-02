using System.Text.Json;
using Google.GenAI.Types;
using UnshackledWord.Application.Extensions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.AiWorker.Models.Greek;
using GeminiClient = Google.GenAI.Client;
using GeminiType = Google.GenAI.Types.Type;

namespace UnshackledWord.Tooling.AiWorker;

public class GreekGeminiFlashClient
{
    private readonly GeminiClient _client;
    private readonly ILogger _logger;
    private CachedContent? _cacheContent;
    private const string ModelName = "gemini-3-flash-preview";

    private const string GreekSystemInstruction = """
                                             You are a linguistic expert mapping the Elberfelder 1871 German NT to STEP Bible Greek data.
                                             RULES:
                                             1. OUTPUT: Return a JSON object matching the provided schema.
                                             2. SPLIT VERBS: Map split German verb parts (e.g., 'aus' in 'geht...aus') to the same Greek 'StepWordId' and 'Strongs'.
                                             3. ADDED WORDS: If a German word has no Greek source, set 'IsAddedWord': true and 'StepWordId': null.
                                             4. PARENT MAPPING: For German words where IsAddedWord is true (e.g., articles like 'der' or particles), set ParentElbWordId to the ElbWordId of the semantic head of the phrase. For articles and adjectives, this is the Noun. For auxiliary verbs or split particles, this is the Main Verb. If 'der' refers to 'Tisch' in 'der kleine Tisch', map 'der' to the ID of 'Tisch', even if 'kleine' is in between.
                                             5. VERSE INTEGRITY: Never map a German word ID to a Greek word from a different verse.
                                             6. NO MARKDOWN: Return only raw JSON.
                                             """;

    public GreekGeminiFlashClient(GeminiClient client, ILogger<GreekGeminiFlashClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<List<VerseDataList<ElbStepAiMapping>>> GetElbStepMappings(List<VerseDataList<ElbVerseData>> elbWords,
        List<VerseDataList<StepGreekVerseData>> stepWords, CancellationToken token = default)
    {
        var germanVerseJson = elbWords.ToNonIndentedJson();
        var greekVerseJson = stepWords.ToNonIndentedJson();

        var prompt = $"""
                      German Words: {germanVerseJson}
                      Greek Words: {greekVerseJson}
                      """;

        var response = await _client.Models.GenerateContentAsync(
            model: ModelName,
            contents: prompt,
            config: GetResponseSchema(_cacheContent!),
            cancellationToken: token
        );

        if (response.Candidates is null || response.Candidates.Count == 0)
        {
            _logger.LogDebug($@"No Candidates for verse ");
            return [];
        }

        if (response.Candidates[0].Content is null)
        {
            _logger.LogDebug($@"No Content for verse ");
            return [];
        }

        if (response.Candidates[0].Content!.Parts is null || response.Candidates[0].Content!.Parts!.Count == 0)
        {
            _logger.LogDebug($@"No parts for verse ");
            return [];
        }

        var text = response.Candidates[0].Content!.Parts![0].Text;

        if (text.IsNullOrWhiteSpace())
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<VerseDataList<ElbStepAiMapping>>>(text) ?? [];
    }

    private GenerateContentConfig GetResponseSchema(CachedContent? cache)
    {
        // Define the schema as an object (OpenAPI 3.0 compatible)
        var responseSchema = new Schema
        {
            Type = GeminiType.Array,
            Items = new Schema
            {
                Type = GeminiType.Object,
                Properties = new Dictionary<string, Schema>
                {
                    ["BookId"] = new() { Type = GeminiType.Integer },
                    ["Chapter"] = new() { Type = GeminiType.Integer },
                    ["Verse"] = new() { Type = GeminiType.Integer },
                    ["Data"] = new()
                    {
                        Type = GeminiType.Array,
                        Items = new Schema
                        {
                            Type = GeminiType.Object,
                            Properties = new Dictionary<string, Schema>
                            {
                                ["ElbWordId"] = new() { Type = GeminiType.Integer },
                                ["StepWordId"] = new() { Type = GeminiType.Integer, Nullable = true },
                                ["Strongs"] = new() { Type = GeminiType.String, Nullable = true },
                                ["IsAddedWord"] = new() { Type = GeminiType.Boolean },
                                ["ParentElbWordId"] = new() { Type = GeminiType.Integer, Nullable = true }
                            },
                            Required = ["ElbWordId", "IsAddedWord"]
                        }
                    }
                },
                Required = ["BookId", "Chapter", "Verse", "Data"]

            }
        };

        var config = new GenerateContentConfig
        {
            ResponseMimeType = "application/json",
            ResponseSchema = responseSchema,
            SystemInstruction = new Content
            {
                Parts = new List<Part>
                {
                    new()
                    {
                        Text = GreekSystemInstruction
                    }
                }
            },
            CachedContent = cache?.Name
        };

        return config;
    }
}
