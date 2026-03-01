using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Extensions;
using GeminiClient = Google.GenAI.Client;
using Type = Google.GenAI.Types.Type;

namespace UnshackledWord.Tooling.AiWorker;

public class ElberfelderMapping
{
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _serviceProvider;

    public ElberfelderMapping(ITestOutputHelper output)
    {
        _output = output;
        _serviceProvider = Program.SetupDependencies(null, null);
    }

    [Fact]
    public async Task Test1()
    {
        var repo = _serviceProvider.GetRequiredService<MappingRepository>();
        var structureData = await repo.GetGreekNtStructureAsync();

        var client = _serviceProvider.GetRequiredService<GeminiClient>();
        var modelName = "gemini-3-flash-preview";

        foreach (var bRef in structureData)
        {
            var elbWords = await repo.GetElbVerseDataAsync(bRef.BibleBookId, bRef.Chapter, bRef.Verse);
            var stepWords = await repo.GetStepGreekVerseDataAsync(bRef.BibleBookId, bRef.Chapter, bRef.Verse);

            var germanVerseJson = elbWords.ToNonIndentedJson();
            var greekVerseJson = stepWords.ToNonIndentedJson();

            string prompt = $"""
                             Map the German words in this JSON to the Greek words.
                             Rules:
                             1. Link split verbs (e.g. 'geht...aus') to the same Greek ID.
                             2. If a German word has no Greek equivalent, mark it 'isAddedWord': true.
                             3. Return a JSON array matching the 'elb_greek_mapping' schema.

                             German Words: {germanVerseJson}
                             Greek Words: {greekVerseJson}
                             """;

            var response = await client.Models.GenerateContentAsync(
                model: modelName,
                contents: prompt,
                config: GetResponseSchema(),
                cancellationToken: TestContext.Current.CancellationToken
            );

            if (response.Candidates is null || response.Candidates.Count == 0)
            {
                _output.WriteLine($@"No Candidates for verse ");
                continue;
            }

            if (response.Candidates[0].Content is null)
            {
                _output.WriteLine($@"No Content for verse ");
                continue;
            }

            if (response.Candidates[0].Content!.Parts is null || response.Candidates[0].Content!.Parts!.Count == 0)
            {
                _output.WriteLine($@"No parts for verse ");
                continue;
            }

            var text = response.Candidates[0].Content!.Parts![0].Text;
        }
    }

    private GenerateContentConfig GetResponseSchema()
    {
        // Define the schema as an object (OpenAPI 3.0 compatible)
        var responseSchema = new Schema
        {
            Type = Type.Array,
            Items = new Schema
            {
                Type = Type.Object,
                Properties = new Dictionary<string, Schema>
                {
                    ["elb_word_id"] = new Schema { Type = Type.Integer, Description = "ID from the German table" },
                    ["step_greek_id"] = new Schema { Type = Type.Integer, Nullable = true, Description = "ID from STEP Bible Greek data" },
                    ["strongs_number"] = new Schema { Type = Type.String, Nullable = true },
                    ["is_added_word"] = new Schema { Type = Type.Boolean, Description = "True if no direct Greek equivalent" },
                    ["parent_german_word_id"] = new Schema { Type = Type.Integer, Nullable = true, Description = "Closest mapped word for added words" }
                },
                Required = new List<string> { "elb_word_id", "is_added_word" }
            }
        };

        var config = new GenerateContentConfig
        {
            ResponseMimeType = "application/json",
            ResponseSchema = responseSchema // This is how the model "knows" the mapping
        };

        return config;
    }
}
