using System.Text;
using System.Text.RegularExpressions;

namespace UnshackledWord.Domain.Models.BibleStructure.References;

/// <summary>A named alias set. The parser is called with one profile, the scanner with another.</summary>
public enum AliasProfile
{
    /// <summary>German names and abbreviations (search overlay, references/parse).</summary>
    German,

    /// <summary><see cref="German"/> first, then English names/abbreviations as fallback (old parser wrapper, MCP).</summary>
    Legacy,

    /// <summary>STEP/OSIS codes found in the lexicon text.</summary>
    Osis,
}

public enum BookMatchKind
{
    Exact,
    Prefix,
    NonCanonical,
}

/// <summary>Result of resolving a book token. <see cref="BookId"/> is null for non-canonical books.</summary>
public readonly record struct BookMatch(int? BookId, string? NonCanonical, BookMatchKind Kind);

/// <summary>The only place that turns a book token into a book id.</summary>
public static partial class BookAliasCatalog
{
    private const int MinPrefixLength = 3;

    private static readonly string[] OsisCodes =
    [
        "Gen", "Exo", "Lev", "Num", "Deu", "Jos", "Jdg", "Rut", "1Sa", "2Sa", "1Ki", "2Ki", "1Ch", "2Ch", "Ezr", "Neh", "Est",
        "Job", "Psa", "Pro", "Ecc", "Sng", "Isa", "Jer", "Lam", "Ezk", "Dan", "Hos", "Jol", "Amo", "Oba", "Jon", "Mic", "Nam",
        "Hab", "Zep", "Hag", "Zec", "Mal", "Mat", "Mrk", "Luk", "Jhn", "Act", "Rom", "1Co", "2Co", "Gal", "Eph", "Php", "Col",
        "1Th", "2Th", "1Ti", "2Ti", "Tit", "Phm", "Heb", "Jas", "1Pe", "2Pe", "1Jn", "2Jn", "3Jn", "Jud", "Rev",
    ];

    // Variants found in the dev DB: (alias, book id).
    private static readonly (string Alias, int BookId)[] OsisVariants =
    [
        ("Jude", 65), ("Luke", 42), ("Mark", 41), ("John", 43), ("Matt", 40), ("Acts", 44), ("Phil", 50),
        ("1Cor", 46), ("2Cor", 47), ("1Jo", 62), ("2Jo", 63), ("3Jo", 64), ("1Tim", 54), ("2Tim", 55),
        ("1Pet", 60), ("2Pet", 61), ("Phlm", 57),
    ];

    // Loccum forms, Elberfelder spellings and full names (the numbered full names come from GermanBookNames).
    private static readonly (string Alias, int BookId)[] GermanExtras =
    [
        ("Gen", 1), ("Ex", 2), ("Dtn", 5), ("Ri", 7), ("1 Kön", 11), ("2 Kön", 12), ("Ijob", 18), ("Koh", 21), ("Hld", 22),
        ("Hohelied", 22), ("Hohes Lied", 22), ("Klgl", 25), ("Klagelieder Jeremias", 25), ("Ez", 26), ("Obd", 31), ("Jona", 32),
        ("Zef", 36), ("Phlm", 57), ("1 Thess", 52), ("2 Thess", 53), ("Hebr", 58), ("Offb", 66),
        ("1 Mose", 1), ("2 Mose", 2), ("3 Mose", 3), ("4 Mose", 4), ("5 Mose", 5),
        ("Roemer", 45), ("Hebraeer", 58),
    ];

    private static readonly string[] NonCanonicalCodes =
    [
        "Sir", "Wis", "Tob", "Jdt", "1Ma", "2Ma", "3Ma", "4Ma", "1Mac", "2Mac", "3Mac", "Bar", "Sus", "Bel", "1Es", "2Es",
        "Man", "PSS", "Pss", "3Ki", "4Ki", "Weish", "1Makk", "2Makk",
    ];

    private static readonly Dictionary<AliasProfile, Dictionary<string, int>> Indexes;
    private static readonly HashSet<string> NonCanonicalKeys;

    static BookAliasCatalog()
    {
        NonCanonicalKeys = NonCanonicalCodes.Select(Normalize).ToHashSet();

        var german = BuildIndex(GermanEntries());

        var legacy = new Dictionary<string, int>(german);
        foreach (var book in BibleBook.AllBooks.Values)
        {
            foreach (var alias in book.Abbreviations.Append(book.Name))
            {
                legacy.TryAdd(Normalize(alias), book.Id); // German wins, then first English book wins
            }
        }

        var osisEntries = new List<(string, int)>();
        for (var i = 0; i < OsisCodes.Length; i++)
        {
            osisEntries.Add((OsisCodes[i], i + 1));
        }

        osisEntries.AddRange(OsisVariants);

        Indexes = new Dictionary<AliasProfile, Dictionary<string, int>>
        {
            [AliasProfile.German] = german,
            [AliasProfile.Legacy] = legacy,
            [AliasProfile.Osis] = BuildIndex(osisEntries),
        };
    }

    private static IEnumerable<(string Alias, int BookId)> GermanEntries()
    {
        for (var id = 1; id <= 66; id++)
        {
            yield return (GermanBookNames.GetName(id), id);
            foreach (var abbreviation in GermanBookNames.GetAbbreviations(id))
            {
                yield return (abbreviation, id);
            }
        }

        foreach (var extra in GermanExtras)
        {
            yield return extra;
        }
    }

    /// <summary>Builds a normalised alias index; the same alias for two different books throws.</summary>
    public static Dictionary<string, int> BuildIndex(IEnumerable<(string Alias, int BookId)> entries)
    {
        var index = new Dictionary<string, int>();
        foreach (var (alias, bookId) in entries)
        {
            var key = Normalize(alias);
            if (index.TryGetValue(key, out var existing) && existing != bookId)
            {
                throw new InvalidOperationException($"Alias '{alias}' (normalised '{key}') maps to books {existing} and {bookId}.");
            }

            index[key] = bookId;
        }

        return index;
    }

    /// <summary>All normalised aliases of a profile (alias -> book id).</summary>
    public static IReadOnlyDictionary<string, int> Aliases(AliasProfile profile) => Indexes[profile];

    [GeneratedRegex(@"^(?:(?<d>[1-3])\s*\.?\s*(?=\p{L})|(?<r>iii|ii|i)(?:\s*\.\s*|\s+)(?=\p{L}))")]
    private static partial Regex OrdinalRegex();

    /// <summary>NFC, trim, lower-case, leading ordinal to a digit, umlauts to ae/oe/ue/ss, spaces/dots/hyphens removed.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var value = text.Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
        var match = OrdinalRegex().Match(value);
        var prefix = string.Empty;
        if (match.Success)
        {
            prefix = match.Groups["d"].Success ? match.Groups["d"].Value : match.Groups["r"].Value.Length.ToString();
            value = value[match.Length..];
        }

        var sb = new StringBuilder(prefix);
        foreach (var c in value)
        {
            switch (c)
            {
                case 'ä': sb.Append("ae"); break;
                case 'ö': sb.Append("oe"); break;
                case 'ü': sb.Append("ue"); break;
                case 'ß': sb.Append("ss"); break;
                case ' ' or '.' or '-': break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }

    /// <summary>Resolves a book token; null when unknown. Prefix matching (search only) needs a unique book and >= 3 characters.</summary>
    public static BookMatch? Resolve(string token, AliasProfile profile, bool allowPrefix = false)
    {
        var key = Normalize(token);
        if (key.Length == 0)
        {
            return null;
        }

        var index = Indexes[profile];
        if (index.TryGetValue(key, out var bookId))
        {
            return new BookMatch(bookId, null, BookMatchKind.Exact);
        }

        if (NonCanonicalKeys.Contains(key))
        {
            return new BookMatch(null, token.Trim(), BookMatchKind.NonCanonical);
        }

        if (allowPrefix)
        {
            var candidates = PrefixCandidates(key, index);
            if (candidates.Count == 1)
            {
                return new BookMatch(candidates[0], null, BookMatchKind.Prefix);
            }
        }

        return null;
    }

    /// <summary>Book ids the token could mean: the exact book, else every book with a matching prefix (for "ambiguous" messages).</summary>
    public static IReadOnlyList<int> Candidates(string token, AliasProfile profile)
    {
        var key = Normalize(token);
        if (key.Length == 0)
        {
            return [];
        }

        var index = Indexes[profile];
        return index.TryGetValue(key, out var bookId) ? [bookId] : PrefixCandidates(key, index);
    }

    // The last typed character may be a typo, so the token shortened by one character also counts
    // ("Phili" is ambiguous between Philipper and Philemon although only Philipper starts with it).
    private static List<int> PrefixCandidates(string key, Dictionary<string, int> index)
    {
        if (key.Length < MinPrefixLength)
        {
            return [];
        }

        var shorter = key.Length > MinPrefixLength ? key[..^1] : key;
        return index.Where(e => e.Key.StartsWith(shorter, StringComparison.Ordinal)).Select(e => e.Value).Distinct().Order().ToList();
    }
}
