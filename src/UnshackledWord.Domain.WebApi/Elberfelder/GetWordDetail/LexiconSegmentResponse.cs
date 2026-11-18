namespace UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

/// <summary>Piece of a lexicon definition (roadmap 0019). Concatenating the texts of all segments gives the plain <c>definition</c>.</summary>
public sealed class LexiconSegmentResponse
{
    public string Text { get; set; } = string.Empty;

    /// <summary>Set for scripture references; <see cref="Text"/> is then the link label.</summary>
    public LexiconReferenceResponse? Ref { get; set; }
}

/// <summary>A reference in the reader's (Hebrew) verse counting.</summary>
public sealed class LexiconReferenceResponse
{
    public int BookId { get; set; }

    public List<LexiconSpanResponse> Spans { get; set; } = [];

    /// <summary>Reader deep link, e.g. "/bible/40/5?v=29#v29".</summary>
    public string Path { get; set; } = string.Empty;
}

/// <summary>Inclusive span; a whole chapter is verse 0 to 999.</summary>
public sealed class LexiconSpanResponse
{
    public int StartChapter { get; set; }

    public int StartVerse { get; set; }

    public int EndChapter { get; set; }

    public int EndVerse { get; set; }
}
