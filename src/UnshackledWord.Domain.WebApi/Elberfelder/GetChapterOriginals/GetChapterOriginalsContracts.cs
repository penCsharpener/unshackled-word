using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Domain.WebApi.Elberfelder.GetChapterOriginals;

public sealed class GetChapterOriginalsRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
}

/// <summary>The Greek/Hebrew words of a whole chapter with the Elberfelder 1871 links, for the verse side panel (read only).</summary>
public sealed class GetChapterOriginalsResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }

    /// <summary>"greek" (New Testament) or "hebrew" (Old Testament).</summary>
    public string Language { get; set; } = string.Empty;

    public List<VerseOriginalsResponse> Verses { get; set; } = [];

    /// <summary>English gloss and shortened lexicon text per Strong's number of the chapter (key as in the words' <c>Strongs</c>), for the hover info; null (omitted) when none.</summary>
    public Dictionary<string, ChapterLexiconResponse>? Lexicon { get; set; }
}

public sealed class ChapterLexiconResponse
{
    public string? Gloss { get; set; }
    public string? Definition { get; set; }
    public List<GetWordDetail.LexiconSegmentResponse>? DefinitionSegments { get; set; }
}

public sealed class VerseOriginalsResponse
{
    public int Verse { get; set; }

    /// <summary>Reading order. Gloss and transliteration stay empty (not shown in the panel).</summary>
    public List<VerseMappingOriginalWord> Words { get; set; } = [];

    /// <summary>Elberfelder 1871 word id (<c>GermanId</c>) linked to a word of this verse (<c>OriginalId</c>).</summary>
    public List<ChapterOriginalLinkResponse> Links { get; set; } = [];
}

public sealed class ChapterOriginalLinkResponse
{
    public int GermanId { get; set; }
    public int OriginalId { get; set; }
}
