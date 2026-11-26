namespace UnshackledWord.Application.Features.Lxx;

/// <summary>One Greek word of Swete's text with its running position inside the verse (1..n). <c>Text</c> carries the punctuation.</summary>
public sealed record SweteWord(int Id, int Position, string Text);

/// <summary>
/// One verse of Swete's text in Swete's own numbering (parsed by the SeedDb <c>SweteParser</c>, roadmap 0028.1). <c>BookCode</c> is Swete's
/// three character code (<c>Gen</c>, <c>1Sa</c>, <c>Dat</c>). A verse may have no words: Swete keeps the number of a verse its Greek text
/// lacks (1Ki 14:1-20, Jer 46:4-13 ...).
/// </summary>
public sealed record SweteVerse(int FirstWordId, string BookCode, int Chapter, int Verse, IReadOnlyList<SweteWord> Words)
{
    public string Reference => $"{BookCode}.{Chapter}:{Verse}";
}
