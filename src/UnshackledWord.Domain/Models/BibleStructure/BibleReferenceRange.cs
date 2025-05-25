namespace UnshackledWord.Domain.Models.BibleStructure;

public record struct BibleReferenceRange : IBibleReference
{
    public BibleReferenceRange(BibleReference start, BibleReference end)
    {
        if (Start > End)
        {
            End = start;
            Start = end;
            return;
        }

        Start = start;
        End = end;
    }

    public BibleReference Start { get; set; }
    public BibleReference End { get; set; }

    public bool IsMultipleBooks => Start.BookId < End.BookId;
    public bool IsMultpleChapters => IsMultipleBooks || (Start.Chapter < End.Chapter);
    public bool IsMultpleVerses => IsMultipleBooks || IsMultipleBooks || Start.Verse < End.Verse;

    public override string ToString()
    {
        if (IsMultipleBooks)
        {
            return $"{Start} - {End}";
        }

        if (IsMultipleBooks is false && IsMultpleChapters)
        {
            return $"{Start}-{End.Chapter}:{End.Verse}";
        }

        if (IsMultpleChapters is false && IsMultpleVerses)
        {
            return $"{Start}-{End.Verse}";
        }

        return Start.ToString();
    }
}
