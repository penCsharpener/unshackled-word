namespace UnshackledWord.Domain.Models.Grammar;

/// <summary>One row of the "SttsTags" table (source of truth for the STTS tags).</summary>
public sealed class SttsTag
{
    public string Tag { get; init; } = string.Empty;
    public string LabelDe { get; init; } = string.Empty;
    public string LabelEn { get; init; } = string.Empty;
    public string Upos { get; init; } = string.Empty;
    public string[] AllowedUpos { get; init; } = [];
}
