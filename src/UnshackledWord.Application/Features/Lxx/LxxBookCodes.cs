namespace UnshackledWord.Application.Features.Lxx;

/// <summary>
/// Book codes of the 39 protocanonical books (roadmap 0028): TVTMS/UBS codes (<c>Sng</c>, <c>Ezk</c>, <c>Jol</c>, <c>Nam</c>) in book id order
/// (1 = Gen ... 39 = Mal), and the differing codes of Swete's files (<c>Sol</c>, <c>Eze</c>, <c>Joe</c>, <c>Nah</c>; <c>Dat</c> is Daniel in
/// Theodotion's text). Deuterocanonical Swete codes (<c>Wis</c>, <c>Sir</c>, <c>1Es</c>, ...) have no book id here.
/// </summary>
public static class LxxBookCodes
{
    public static readonly IReadOnlyList<string> Tvtms =
    [
        "Gen", "Exo", "Lev", "Num", "Deu", "Jos", "Jdg", "Rut", "1Sa", "2Sa", "1Ki", "2Ki", "1Ch", "2Ch", "Ezr", "Neh", "Est",
        "Job", "Psa", "Pro", "Ecc", "Sng", "Isa", "Jer", "Lam", "Ezk", "Dan", "Hos", "Jol", "Amo", "Oba", "Jon", "Mic", "Nam",
        "Hab", "Zep", "Hag", "Zec", "Mal",
    ];

    private static readonly Dictionary<string, string> SweteToTvtms = new(StringComparer.Ordinal)
    {
        ["Sol"] = "Sng",
        ["Eze"] = "Ezk",
        ["Joe"] = "Jol",
        ["Nah"] = "Nam",
        ["Dat"] = "Dan",
    };

    /// <summary>The book id (1..39) of a TVTMS code, or null.</summary>
    public static int? BookIdFromTvtms(string code)
    {
        var index = Tvtms.ToList().IndexOf(code);

        return index < 0 ? null : index + 1;
    }

    /// <summary>The TVTMS code of a Swete book code, or null when Swete's book is not protocanonical (<c>Wis</c>, <c>1Ma</c>, ...).</summary>
    public static string? TvtmsFromSwete(string code)
    {
        var tvtms = SweteToTvtms.GetValueOrDefault(code, code);

        return Tvtms.Contains(tvtms) ? tvtms : null;
    }

    /// <summary>True for Swete's Theodotion text of Daniel (<c>Dat</c>); <c>Dan</c> is the Old Greek.</summary>
    public static bool IsTheodotion(string sweteCode) => sweteCode == "Dat";
}
