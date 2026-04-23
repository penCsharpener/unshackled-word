using Google.GenAI.Types;
using GeminiClient = Google.GenAI.Client;
using GeminiType = Google.GenAI.Types.Type;

namespace UnshackledWord.Tooling.AiWorker.Mapping;

public class GeminiMappingBaseClient : GeminiFlashAbstractClient
{
    public GeminiMappingBaseClient(GeminiClient client, ILogger<GeminiFlashAbstractClient> logger) : base(client, logger)
    {
    }

    protected override GenerateContentConfig GetResponseSchema(string systemInstructions)
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
                    ["RefId"] = new() { Type = GeminiType.Integer, Description = "Is combined Integer of (BookId * 1000000) + (Chapter * 1000) + Verse" },
                    ["Data"] = new()
                    {
                        Type = GeminiType.Array,
                        Items = new Schema
                        {
                            Type = GeminiType.String,
                            // Description = "A pipe-delimited string (no spaces) representing these 6 fields: " +
                            //               "ElbWordId|StepWordId|IsAddedWord|ParentElbWordId|PartOrder|GermanWordPart " +
                            //               "Rules: 1=true, 0=false, '-'=null. " +
                            //               "Examples: '123|456|0|-|-|-', '123|-|1|456|-|-', '123|456|0|-|1|Gersten', '123|876|0|-|2|ernte'"
                        }
                    }
                },
                Required = ["RefId", "Data"]
            }
        };

        var config = new GenerateContentConfig
        {
            ResponseMimeType = "application/json",
            ResponseSchema = responseSchema,
            // Temperature = 0.1f,
            // TopP = 0.1f,
            // TopK = 1,
            SystemInstruction = new Content
            {
                Parts = new List<Part>
                {
                    new()
                    {
                        Text = systemInstructions
                    }
                }
            },
        };

        return config;
    }
}
