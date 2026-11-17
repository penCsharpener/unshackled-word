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

    /// <summary>The schema of a run mode.</summary>
    public static string For(AgentMode mode) => mode == AgentMode.Lemma ? LemmaJson : Json;
}
