namespace UnshackledWord.Domain.WebApi.Vocab;

/// <summary>The learner's vocab settings (roadmap 0022.9): <c>GET api/vocab/settings</c> and the answer of <c>PUT</c>.</summary>
public sealed class VocabSettingsResponse
{
    /// <summary>Quiz types the learner turned off. Empty by default.</summary>
    public List<string> DisabledQuizTypes { get; set; } = [];

    /// <summary>The quiz types that can be switched on or off at the moment (the engine builds them). The UI offers a switch per entry.</summary>
    public List<string> AvailableQuizTypes { get; set; } = [];
}

/// <summary>Body of <c>PUT api/vocab/settings</c>. Replaces the list of disabled quiz types.</summary>
public sealed class UpdateVocabSettingsRequest
{
    /// <summary>Only <c>multiple_choice</c>, <c>reverse_multiple_choice</c>, <c>listening</c>, <c>verse_context</c>; at least one of the two multiple-choice types stays on.</summary>
    public List<string> DisabledQuizTypes { get; set; } = [];
}
