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
                                             2. If a German word has no Greek equivalent, mark it 'isAddedWord': true.
                                             3. Return a JSON array matching the 'elb_greek_mapping' schema.
                                             4. Return NO markdown or text, only the raw JSON array.
                                             """;

    public GeminiFlashClient(GeminiClient client, ILogger<GeminiFlashClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<List<ElbStepMapping>> GetElbStepMappings(IEnumerable<ElbVerseData> elbWords,
        IEnumerable<StepGreekVerseData> stepWords, CancellationToken token = default)
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

        return JsonSerializer.Deserialize<List<ElbStepMapping>>(text) ?? [];
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
                    ["elb_word_id"] = new() { Type = GeminiType.Integer, Description = "ID from the German table" },
                    ["step_greek_id"] = new()
                        { Type = GeminiType.Integer, Nullable = true, Description = "ID from STEP Bible Greek data" },
                    ["strongs_number"] = new() { Type = GeminiType.String, Nullable = true },
                    ["is_added_word"] = new()
                        { Type = GeminiType.Boolean, Description = "True if no direct Greek equivalent" },
                    ["parent_german_word_id"] = new()
                    {
                        Type = GeminiType.Integer, Nullable = true, Description = "Closest mapped word for added words"
                    }
                },
                Required = new List<string> { "elb_word_id", "is_added_word" }
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
