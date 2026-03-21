namespace UnshackledWord.Domain.Models.BibleStructure;

public interface IBibleReference;

public record struct BibleReference(int BookId, int Chapter, int Verse) : IBibleReference, IComparer<BibleReference>, IComparable<BibleReference>
{
    public BibleReference FromRefId(int refId)
    {
        var verse = refId % 1000;
        var remaining = refId / 1000;
        var chapter = remaining % 1000;
        var bookId = remaining / 1000;

        return new(bookId, chapter, verse);
    }

    public int RefId => GetRefId();

    /// <summary>
    /// Generates a continuous, sortable number based on bookId, chapter and verse.
    /// </summary>
    /// <returns></returns>
    private int GetRefId()
    {
        var bookRefId = BookId * 1000000;
        var chapterRefId = Chapter * 1000;
        return bookRefId + chapterRefId + Verse;
    }

    public int Compare(BibleReference x, BibleReference y)
    {
        var bookIdComparison = x.BookId.CompareTo(y.BookId);
        if (bookIdComparison != 0)
        {
            return bookIdComparison;
        }

        var chapterComparison = x.Chapter.CompareTo(y.Chapter);
        if (chapterComparison != 0)
        {
            return chapterComparison;
        }

        return x.Verse.CompareTo(y.Verse);
    }

    public int CompareTo(BibleReference other)
    {
        var bookIdComparison = BookId.CompareTo(other.BookId);
        if (bookIdComparison != 0)
        {
            return bookIdComparison;
        }

        var chapterComparison = Chapter.CompareTo(other.Chapter);
        if (chapterComparison != 0)
        {
            return chapterComparison;
        }

        return Verse.CompareTo(other.Verse);
    }

    public bool Equals(BibleReference other)
    {
        return BookId == other.BookId &&
               Chapter == other.Chapter &&
               Verse == other.Verse;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(BookId, Chapter, Verse);
    }

    public override string ToString()
    {
        return $"{BibleBook.AllBooks[BookId].Abbreviations[0]} {Chapter}:{Verse}";
    }

    public string ToString(string bookSeparation)
    {
        return $"{BibleBook.AllBooks[BookId].Abbreviations[0]}{bookSeparation}{Chapter}:{Verse}";
    }

    public static bool operator <(BibleReference left, BibleReference right) => left.CompareTo(right) < 0;
    public static bool operator >(BibleReference left, BibleReference right) => left.CompareTo(right) > 0;
    public static bool operator <=(BibleReference left, BibleReference right) => left.CompareTo(right) <= 0;
    public static bool operator >=(BibleReference left, BibleReference right) => left.CompareTo(right) >= 0;

    public static BibleReference NtStart => new(40, 1, 1);
    public static BibleReference OtStart => new(1, 1, 1);
}
