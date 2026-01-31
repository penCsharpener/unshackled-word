namespace UnshackledWord.Domain.Models.BibleStructure;

public interface IBibleReference;

public record struct BibleReference(int BookId, int Chapter, int Verse) : IBibleReference, IComparer<BibleReference>, IComparable<BibleReference>
{
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

    public static bool operator <(BibleReference left, BibleReference right) => left.CompareTo(right) < 0;
    public static bool operator >(BibleReference left, BibleReference right) => left.CompareTo(right) > 0;
    public static bool operator <=(BibleReference left, BibleReference right) => left.CompareTo(right) <= 0;
    public static bool operator >=(BibleReference left, BibleReference right) => left.CompareTo(right) >= 0;

    public static BibleReference NtStart => new(40, 1, 1);
    public static BibleReference OtStart => new(1, 1, 1);
}
