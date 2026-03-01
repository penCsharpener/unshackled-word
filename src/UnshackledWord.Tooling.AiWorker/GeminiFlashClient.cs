using System.Text.Json;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Extensions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Tooling.AiWorker.Models;
using GeminiClient = Google.GenAI.Client;
using GeminiType = Google.GenAI.Types.Type;

namespace UnshackledWord.Tooling.AiWorker;

public class GeminiFlashClient
{
    private readonly GeminiClient _client;
    private readonly ILogger _logger;
    private CachedContent? _cacheContent;
    private const string ModelName = "gemini-3-flash-preview";

    private const string SystemInstruction = """
                                             I'm tagging the Elberfelder 1871 NT with the Greek words and Strongs numbers of the Step Bible Data.
                                             Map the German words in this JSON to the Greek words.
                                             Rules:
                                             1. Link split verbs (e.g. 'geht...aus') to the same Greek ID.
                                             2. If a German word has no Greek equivalent, mark it 'IsAddedWord': true.
                                             3. Return a JSON array matching the given json schema.
                                             4. Make sure that no id of one Greek verse is mapped to a word in different verse in German.
                                             5. For every word marked 'IsAddedWord': true, identify the nearest German word that HAS a Greek mapping and set 'ParentGermanWordId' to that word's ID. This should typically be the noun an article modifies or the main verb of a phrase.
                                             6. Return NO markdown or text, only the raw JSON array.
                                             """;

    public GeminiFlashClient(GeminiClient client, ILogger<GeminiFlashClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<List<VerseDataList<ElbStepMapping>>> GetElbStepMappings(IEnumerable<VerseDataList<ElbVerseData>> elbWords,
        IEnumerable<VerseDataList<StepGreekVerseData>> stepWords, CancellationToken token = default)
    {
        if (_cacheContent is null && SystemInstruction.Length > 4500)
        {
            _cacheContent = await GetCachedContentAsync(token);
        }

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

        var result = JsonSerializer.Deserialize<List<VerseDataList<ElbStepMapping>>>(text) ?? [];


    }

    private async Task<CachedContent?> GetCachedContentAsync(CancellationToken token = default)
    {
        return await _client.Caches.CreateAsync(ModelName, new CreateCachedContentConfig
        {
            SystemInstruction = new Content
            {
                Parts = new List<Part>
                {
                    new()
                    {
                        Text = SystemInstruction
                    }
                }
            },
            // Ttl = $"{30 * 24 * 60 * 60}s"
            Ttl = $"{60 * 60}s"
        }, token);
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
                        Text = SystemInstruction
                    }
                }
            },
            CachedContent = cache?.Name
        };

        return config;
    }
}
