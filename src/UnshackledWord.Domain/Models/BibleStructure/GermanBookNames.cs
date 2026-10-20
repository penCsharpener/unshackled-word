namespace UnshackledWord.Domain.Models.BibleStructure;

/// <summary>
/// German book names and abbreviations (Elberfelder / Luther style). First abbreviation is the display abbreviation.
/// </summary>
public static class GermanBookNames
{
    private static readonly Dictionary<int, (string Name, string[] Abbreviations)> Books = new()
    {
        [1] = ("1. Mose", ["1Mo", "Gen"]),
        [2] = ("2. Mose", ["2Mo", "Ex"]),
        [3] = ("3. Mose", ["3Mo", "Lev"]),
        [4] = ("4. Mose", ["4Mo", "Num"]),
        [5] = ("5. Mose", ["5Mo", "Dtn"]),
        [6] = ("Josua", ["Jos"]),
        [7] = ("Richter", ["Ri"]),
        [8] = ("Rut", ["Rt", "Rut"]),
        [9] = ("1. Samuel", ["1Sam"]),
        [10] = ("2. Samuel", ["2Sam"]),
        [11] = ("1. Könige", ["1Kön", "1Kö", "1Koen"]),
        [12] = ("2. Könige", ["2Kön", "2Kö", "2Koen"]),
        [13] = ("1. Chronika", ["1Chr"]),
        [14] = ("2. Chronika", ["2Chr"]),
        [15] = ("Esra", ["Esr"]),
        [16] = ("Nehemia", ["Neh"]),
        [17] = ("Esther", ["Est"]),
        [18] = ("Hiob", ["Hi", "Hiob"]),
        [19] = ("Psalmen", ["Ps"]),
        [20] = ("Sprüche", ["Spr"]),
        [21] = ("Prediger", ["Pred", "Koh"]),
        [22] = ("Hoheslied", ["Hl"]),
        [23] = ("Jesaja", ["Jes"]),
        [24] = ("Jeremia", ["Jer"]),
        [25] = ("Klagelieder", ["Kla", "Klgl"]),
        [26] = ("Hesekiel", ["Hes", "Ez"]),
        [27] = ("Daniel", ["Dan"]),
        [28] = ("Hosea", ["Hos"]),
        [29] = ("Joel", ["Joe", "Joel"]),
        [30] = ("Amos", ["Am"]),
        [31] = ("Obadja", ["Ob", "Obd"]),
        [32] = ("Jona", ["Jon"]),
        [33] = ("Micha", ["Mi"]),
        [34] = ("Nahum", ["Nah"]),
        [35] = ("Habakuk", ["Hab"]),
        [36] = ("Zephanja", ["Zeph", "Zef"]),
        [37] = ("Haggai", ["Hag"]),
        [38] = ("Sacharja", ["Sach"]),
        [39] = ("Maleachi", ["Mal"]),
        [40] = ("Matthäus", ["Mt"]),
        [41] = ("Markus", ["Mk"]),
        [42] = ("Lukas", ["Lk"]),
        [43] = ("Johannes", ["Joh"]),
        [44] = ("Apostelgeschichte", ["Apg"]),
        [45] = ("Römer", ["Röm", "Roem"]),
        [46] = ("1. Korinther", ["1Kor"]),
        [47] = ("2. Korinther", ["2Kor"]),
        [48] = ("Galater", ["Gal"]),
        [49] = ("Epheser", ["Eph"]),
        [50] = ("Philipper", ["Phil"]),
        [51] = ("Kolosser", ["Kol"]),
        [52] = ("1. Thessalonicher", ["1Thes", "1Thess"]),
        [53] = ("2. Thessalonicher", ["2Thes", "2Thess"]),
        [54] = ("1. Timotheus", ["1Tim"]),
        [55] = ("2. Timotheus", ["2Tim"]),
        [56] = ("Titus", ["Tit"]),
        [57] = ("Philemon", ["Phlm", "Phim"]),
        [58] = ("Hebräer", ["Hebr", "Heb"]),
        [59] = ("Jakobus", ["Jak"]),
        [60] = ("1. Petrus", ["1Petr", "1Pet"]),
        [61] = ("2. Petrus", ["2Petr", "2Pet"]),
        [62] = ("1. Johannes", ["1Joh"]),
        [63] = ("2. Johannes", ["2Joh"]),
        [64] = ("3. Johannes", ["3Joh"]),
        [65] = ("Judas", ["Jud"]),
        [66] = ("Offenbarung", ["Offb"]),
    };

    public static string GetName(int bookId) => Books[bookId].Name;

    public static string GetAbbreviation(int bookId) => Books[bookId].Abbreviations[0];

    /// <summary>
    /// Finds a book by German abbreviation or name (case-insensitive, ignores spaces, dots and umlaut spelling).
    /// </summary>
    public static int? FindBookId(string text)
    {
        var key = Normalize(text);
        foreach (var (id, book) in Books)
        {
            if (Normalize(book.Name) == key || book.Abbreviations.Any(a => Normalize(a) == key))
            {
                return id;
            }
        }

        return null;
    }

    public static string Normalize(string text)
    {
        return text.Replace(" ", "").Replace(".", "")
            .Replace("ä", "ae", StringComparison.OrdinalIgnoreCase)
            .Replace("ö", "oe", StringComparison.OrdinalIgnoreCase)
            .Replace("ü", "ue", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
    }
}
