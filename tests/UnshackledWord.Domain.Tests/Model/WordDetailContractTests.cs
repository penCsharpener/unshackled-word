using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Domain.Tests.Model;

/// <summary>
/// Reads the hand-written fixture shared with the React tests (<c>wordDetail.sample.json</c>) and proves the API DTOs
/// round-trip it unchanged, so the TypeScript contract and the C# contract cannot drift apart silently.
/// </summary>
public class WordDetailContractTests
{
    // Same options as ResponseSerializer in Tooling.WebApi/Program.cs.
    private static readonly JsonSerializerOptions ApiOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string FixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UnshackledWord.slnx")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull();
        return Path.Combine(dir.FullName, "src", "UnshackledWord.Web", "src", "test", "fixtures", "wordDetail.sample.json");
    }

    [Fact]
    public void Fixture_RoundTripsThroughApiDto()
    {
        var json = File.ReadAllText(FixturePath());

        var responses = JsonSerializer.Deserialize<List<GetWordDetailResponse>>(json, ApiOptions);

        responses.ShouldNotBeNull();
        responses.Count.ShouldBe(3);
        var roundTripped = JsonNode.Parse(JsonSerializer.Serialize(responses, ApiOptions));
        var original = JsonNode.Parse(json);
        JsonNode.DeepEquals(roundTripped, original).ShouldBeTrue();
    }
}
