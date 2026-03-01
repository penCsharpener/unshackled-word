// using System.Text.Json.Serialization;

namespace UnshackledWord.Tooling.AiWorker;

public sealed class ElbStepMapping
{
    // [JsonPropertyName("elb_word_id")]
    public int ElbWordId { get; set; }

    // [JsonPropertyName("step_greek_id")]
    public int? StepWordId { get; set; }

    // [JsonPropertyName("strongs_number")]
    public string? Strongs { get; set; }

    // [JsonPropertyName("is_added_word")]
    public bool IsAddedWord { get; set; }

    // [JsonPropertyName("parent_german_word_id")]
    public int? ParentElbWordId { get; set; }
}
