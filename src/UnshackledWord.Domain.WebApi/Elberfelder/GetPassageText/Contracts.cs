using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseCrossReferenceTexts;

namespace UnshackledWord.Domain.WebApi.Elberfelder.GetPassageText;

public sealed class GetPassageTextRequest
{
    /// <summary>Ref-id spans, comma separated, e.g. "66002001-66002005,66002007" (a single id is one verse). At most 10 spans and 40 verses are returned.</summary>
    public string Spans { get; set; } = string.Empty;

    /// <summary>Literalness level 1-1000; 200 (default) = base level, 0 = Elb1871 only.</summary>
    public int Level { get; set; } = 200;
}

public sealed class GetPassageTextResponse
{
    /// <summary>The verses of the spans in request order, one group per chapter (a span crossing chapters gives one group per chapter).</summary>
    public List<CrossReferenceTextResponse> Passages { get; set; } = [];

    /// <summary>True when spans or verses beyond the limits were left out.</summary>
    public bool Truncated { get; set; }
}
