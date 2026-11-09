namespace UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

/// <summary>Mirrors <c>GetWordDetailResponse</c> in <c>src/UnshackledWord.Web/src/lib/bible/types.ts</c>. Null properties are omitted by the serializer.</summary>
public sealed class GetWordDetailResponse
{
    public int WordId { get; set; }
    public string WordInContext { get; set; } = default!;
    public int PositionInVerse { get; set; }
    public string? GermanLemma { get; set; }
    /// <summary>true or null (never false), so the property is omitted for normal words.</summary>
    public bool? IsAddedWord { get; set; }
    public List<OriginalReadingResponse> Readings { get; set; } = [];
    public MappingProvenanceResponse? Mapping { get; set; }
    public List<CoMappedWordResponse>? CoMappedWords { get; set; }
    public List<string>? Hints { get; set; }
}

public sealed class OriginalReadingResponse
{
    public int StepWordId { get; set; }
    public string Language { get; set; } = default!;
    public int PositionInVerse { get; set; }
    public string? GermanWordPart { get; set; }
    public string Original { get; set; } = default!;
    public string? Transliteration { get; set; }
    public string? Lemma { get; set; }
    public string Strongs { get; set; } = default!;
    public string? Gloss { get; set; }
    public string? ContextGloss { get; set; }
    public MorphologyResponse? Morphology { get; set; }
    public List<MorphemeResponse>? Morphemes { get; set; }
    public LexiconSummaryResponse? Lexicon { get; set; }
    /// <summary>Uses of this root Strong's number in its testament (NT for Greek, OT for Hebrew/Aramaic).</summary>
    public int? OccurrenceCount { get; set; }
    /// <summary>Top Elberfelder renderings of this Strong's number, most used first.</summary>
    public List<RenderingResponse>? Renderings { get; set; }
    public List<int>? SharedWithWordIds { get; set; }
}

public sealed class RenderingResponse
{
    public string Word { get; set; } = default!;
    public int Count { get; set; }
}

public sealed class MorphologyResponse
{
    public string Code { get; set; } = default!;
    public string? PartOfSpeech { get; set; }
    public string? Tense { get; set; }
    public string? Voice { get; set; }
    public string? Mood { get; set; }
    public string? Person { get; set; }
    public string? Case { get; set; }
    public string? Number { get; set; }
    public string? Gender { get; set; }
    public string? Degree { get; set; }
    public string? Stem { get; set; }
    public string? HebrewForm { get; set; }
    public string? State { get; set; }
    /// <summary>English STEP description, only when the decoder could not map the code.</summary>
    public string? Description { get; set; }
}

public sealed class MorphemeResponse
{
    /// <summary>prefix, root or suffix.</summary>
    public string Role { get; set; } = default!;
    public string Original { get; set; } = default!;
    public string? Strongs { get; set; }
    public string? Gloss { get; set; }
    public MorphologyResponse? Morphology { get; set; }
}

public sealed class LexiconSummaryResponse
{
    public string Strongs { get; set; } = default!;
    public string Lemma { get; set; } = default!;
    public string? Transliteration { get; set; }
    public string? Gloss { get; set; }
    public string? Definition { get; set; }
    public bool? DefinitionTruncated { get; set; }
}

public sealed class MappingProvenanceResponse
{
    /// <summary>ai or human.</summary>
    public string Source { get; set; } = default!;
    /// <summary>ISO 8601.</summary>
    public string CreatedAt { get; set; } = default!;
    public string? Model { get; set; }
    public string? ConfirmedAt { get; set; }
}

public sealed class CoMappedWordResponse
{
    public int WordId { get; set; }
    public string WordInContext { get; set; } = default!;
    public int PositionInVerse { get; set; }
    /// <summary>sameOriginal, addedWord or parent.</summary>
    public string Relation { get; set; } = default!;
}
