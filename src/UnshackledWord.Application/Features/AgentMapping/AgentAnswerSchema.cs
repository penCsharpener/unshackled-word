namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>
/// JSON Schema of the answer of 04-work-package.md ("Answer format"). One string for everything: it goes into the static prompt prefix
/// (placeholder <c>{{ANSWER_SCHEMA}}</c> of the prompt files) and into the structured-output parameter of the adapters. Rows are arrays
/// (tuples) to keep the answer short; the exact element rules are checked by <see cref="AgentAnswerParser"/> and the validator, not here.
/// </summary>
public static class AgentAnswerSchema
{
    public const string Placeholder = "{{ANSWER_SCHEMA}}";

    public const string Json = """
        {"type":"object","properties":{"packageId":{"type":"string"},"verses":{"type":"array","items":{"type":"object","properties":{"v":{"type":"string"},"links":{"type":"array","items":{"type":"array","items":{"type":"string"}}},"added":{"type":"array","items":{"type":"array","items":{"type":"string"}}},"lemmas":{"type":"array","items":{"type":"array","items":{"type":"string"}}},"parts":{"type":"array","items":{"type":"array","items":{"anyOf":[{"type":"string"},{"type":"integer"}]}}},"unmapped":{"type":"array","items":{"type":"array","items":{"type":"string"}}},"note":{"type":"string"}},"required":["v","links","added","lemmas","parts","unmapped"]}}},"required":["packageId","verses"]}
        """;

    /// <summary>
    /// Run mode <c>lemma</c> (roadmap 0018, 06-ai-runs.md): a diff against the package, silence keeps. <c>lemmas</c> as in the mapping
    /// answer, <c>roles</c> <c>[german, source, role, morpheme?]</c>, <c>added</c> <c>[german, parentGerman]</c> (a linked subject pronoun
    /// becomes an added word), <c>partLemmas</c> <c>[german, partOrder, partLemma]</c>. No <c>links</c>: links are fixed.
    /// </summary>
    public const string LemmaJson = """
        {"type":"object","properties":{"packageId":{"type":"string"},"verses":{"type":"array","items":{"type":"object","properties":{"v":{"type":"string"},"lemmas":{"type":"array","items":{"type":"array","items":{"type":"string"}}},"roles":{"type":"array","items":{"type":"array","items":{"type":"string"}}},"added":{"type":"array","items":{"type":"array","items":{"type":"string"}}},"partLemmas":{"type":"array","items":{"type":"array","items":{"anyOf":[{"type":"string"},{"type":"integer"}]}}},"note":{"type":"string"}},"required":["v"]}}},"required":["packageId","verses"]}
        """;

    /// <summary>
    /// Run mode <c>vocab-form</c> (roadmap 0022.13): <c>forms</c> rows <c>[entryId, form]</c>; an entry the model cannot form is left out.
    /// </summary>
    public const string VocabFormJson = """
        {"type":"object","properties":{"forms":{"type":"array","items":{"type":"array","items":{"type":"string"}}}},"required":["forms"]}
        """;

    /// <summary>
    /// Run mode <c>vocab-root</c> (roadmap 0030.4): per answered entry <c>id</c>, <c>simple</c>, <c>prefixes</c> (Strong's keys of the stripped prepositions),
    /// <c>rootKey</c> (Greek), <c>rootLetters</c> (Hebrew), <c>confidence</c> and <c>note</c>; an entry the model cannot decide is left out.
    /// </summary>
    public const string VocabRootJson = """
        {"type":"object","properties":{"roots":{"type":"array","items":{"type":"object","properties":{"id":{"type":"string"},"simple":{"type":"boolean"},"prefixes":{"type":"array","items":{"type":"string"}},"rootKey":{"type":"string"},"rootLetters":{"type":"string"},"confidence":{"type":"string","enum":["high","low"]},"note":{"type":"string"}},"required":["id"]}}},"required":["roots"]}
        """;

    /// <summary>
    /// Run mode <c>lxx-tag</c> (roadmap 0028.7, prompts <c>lxx-tag-v1</c>/<c>v2</c>; <c>note</c> before <c>words</c> since v2): per verse <c>words</c> rows
    /// <c>[position, lemma, strongs, morph, heb, confidence]</c> (<c>heb</c> an array of Hebrew positions, <c>confidence</c> <c>high</c>/<c>low</c>),
    /// every LXX word exactly once, and an optional <c>note</c>. The row rules are checked by <see cref="Lxx.LxxAgentAnswerParser"/> and the validator.
    /// </summary>
    public const string LxxJson = """
        {"type":"object","properties":{"packageId":{"type":"string"},"verses":{"type":"array","items":{"type":"object","properties":{"v":{"type":"string"},"note":{"type":"string"},"words":{"type":"array","items":{"type":"array","items":{"anyOf":[{"type":"string"},{"type":"integer"},{"type":"array","items":{"type":"integer"}}]}}}},"required":["v","words"]}}},"required":["packageId","verses"]}
        """;

    /// <summary>The schema of a run mode.</summary>
    public static string For(AgentMode mode) => mode switch
    {
        AgentMode.Lemma => LemmaJson,
        AgentMode.LxxTag => LxxJson,
        _ => Json,
    };

    /// <summary>The schema of a prompt language (<c>greek</c>, <c>lemma-hebrew</c>, <c>lxx-tag</c>, <c>vocab-form-greek</c> …).</summary>
    public static string ForLanguage(string language) =>
        language.StartsWith(AgentPromptProvider.VocabFormPrefix, StringComparison.Ordinal) ? VocabFormJson
        : language.StartsWith(AgentPromptProvider.VocabRootPrefix, StringComparison.Ordinal) ? VocabRootJson
        : language.StartsWith(AgentPromptProvider.LemmaPrefix, StringComparison.Ordinal) ? LemmaJson
        : language == AgentPromptProvider.LxxTagLanguage ? LxxJson
        : Json;
}
