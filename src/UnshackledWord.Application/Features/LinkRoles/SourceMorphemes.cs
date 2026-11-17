namespace UnshackledWord.Application.Features.LinkRoles;

/// <summary>
/// One morpheme of a Hebrew source word. <see cref="Strongs"/> as in ExpandedStrongTags without braces, instance (<c>#6</c>) and <c>+</c>
/// (<c>H9001</c>, <c>H0559</c>, <c>H1121A</c>); <see cref="Grammar"/> is the matching segment of StepHebrewWords.Grammar (<c>c</c>,
/// <c>Vqw3ms</c>, <c>Sp3fs</c>) or null when the segment count does not match. <see cref="Original"/> and <see cref="Gloss"/> are the second
/// and third <c>=</c> fields of ExpandedStrongTags (null for DisambiguatedStrongs).
/// </summary>
public sealed record SourceMorpheme(string Strongs, bool IsMain, string? Grammar, string? Original = null, string? Gloss = null)
{
    /// <summary>The number of an H Strong's (<c>H9024</c> → 9024), else null.</summary>
    public int? Number => SourceMorphemes.HebrewNumber(Strongs);
}

/// <summary>Person, gender and number of a pronoun (German) or pronoun suffix (Hebrew). Gender null = any; Hebrew common gender is null.</summary>
public readonly record struct Pgn(int Person, char Number, char? Gender);

/// <summary>Parsing of Hebrew ExpandedStrongTags and Grammar, Greek Morphology, and the Hebrew pronoun-suffix inventory (H9020–H9049).</summary>
public static class SourceMorphemes
{
    /// <summary>
    /// Morphemes of <c>H9001=ו=&amp;/{H0559=אָמַר=to say}</c> or <c>{H1121A=בֵּן=son}/H9023=Ps3m=his</c>: pieces split at <c>/</c> and
    /// <c>\</c>, the braced piece is the main morpheme. Punctuation (H9014–H9019) and blank pieces are dropped; a Strong's that occurs
    /// twice in a word is one morpheme (a link can only name the Strong's). Compound words (<c>{H0935G}//{H1409}</c>) have two main morphemes.
    /// A word without a braced piece (rare) gets its first non-prefix/suffix morpheme (else the first) as main. The one parser for Hebrew
    /// morphemes: <see cref="VerseMapping.HebrewMorphemes"/> (GET/save of the mapping editor) delegates here, so a morpheme the backfill
    /// writes is always one the save validator accepts.
    /// </summary>
    public static IReadOnlyList<SourceMorpheme> ParseHebrew(string? expandedStrongTags, string? grammar = null)
    {
        if (string.IsNullOrWhiteSpace(expandedStrongTags))
        {
            return [];
        }

        var pieces = new List<(string Strongs, bool IsMain, string? Original, string? Gloss)>();
        foreach (var raw in expandedStrongTags.Split(['/', '\\'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = raw.Replace("{", string.Empty).Replace("}", string.Empty).Trim().TrimEnd('+').Trim().Split('=');
            var strongs = NormalizeStrongs(fields[0]);
            if (strongs.Length == 0 || IsPunctuation(strongs))
            {
                continue;
            }

            var original = fields.Length > 1 && fields[1].Trim().Length > 0 ? fields[1].Trim() : null;
            var gloss = fields.Length > 2 && fields[2].Trim().Length > 0 ? string.Join("=", fields[2..]).Trim() : null;
            pieces.Add((strongs, raw.Contains('{'), original, gloss));
        }

        var segments = GrammarSegments(grammar);
        var aligned = segments.Count == pieces.Count;
        var result = new List<SourceMorpheme>();
        for (var i = 0; i < pieces.Count; i++)
        {
            var (strongs, isMain, original, gloss) = pieces[i];
            var existing = result.FindIndex(m => string.Equals(m.Strongs, strongs, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                result[existing] = result[existing] with { IsMain = result[existing].IsMain || isMain };
                continue;
            }

            result.Add(new SourceMorpheme(strongs, isMain, aligned ? segments[i] : null, original, gloss));
        }

        if (result.Count > 0 && !result.Any(m => m.IsMain))
        {
            var main = result.FindIndex(m => HebrewNumber(m.Strongs) is not >= 9000);
            main = main < 0 ? 0 : main;
            result[main] = result[main] with { IsMain = true };
        }

        return result;
    }

    /// <summary><c>{H0430G</c> → <c>H0430G</c>, <c>H9033#6</c> → <c>H9033</c>, <c>H6301}+</c> → <c>H6301</c>.</summary>
    public static string NormalizeStrongs(string piece)
    {
        var text = piece.Replace("{", string.Empty).Replace("}", string.Empty).Trim().TrimEnd('+').Trim();
        var hash = text.IndexOf('#');
        return hash >= 0 ? text[..hash] : text;
    }

    /// <summary>H9014–H9018 (maqqef, sof pasuq, paragraph/section marks) and H9019 (inverted nun, a section mark).</summary>
    public static bool IsPunctuation(string strongs) => HebrewNumber(strongs) is >= 9014 and <= 9019;

    /// <summary>H9020–H9049: pronoun suffixes (possessive H9020–29, object H9030–39, subject H9040–49).</summary>
    public static bool IsPronounSuffix(string strongs) => HebrewNumber(strongs) is >= 9020 and <= 9049;

    /// <summary>Person, number, gender of a pronoun suffix; each decade runs 1cs, 2ms, 2fs, 3ms, 3fs, 1cp, 2mp, 2fp, 3mp, 3fp.</summary>
    public static Pgn? SuffixPgn(string strongs)
    {
        if (!IsPronounSuffix(strongs))
        {
            return null;
        }

        return ((HebrewNumber(strongs)!.Value - 9020) % 10) switch
        {
            0 => new Pgn(1, 's', null),
            1 => new Pgn(2, 's', 'm'),
            2 => new Pgn(2, 's', 'f'),
            3 => new Pgn(3, 's', 'm'),
            4 => new Pgn(3, 's', 'f'),
            5 => new Pgn(1, 'p', null),
            6 => new Pgn(2, 'p', 'm'),
            7 => new Pgn(2, 'p', 'f'),
            8 => new Pgn(3, 'p', 'm'),
            _ => new Pgn(3, 'p', 'f'),
        };
    }

    /// <summary>True for possessive suffixes H9020–H9029 (<c>Ps3m</c> "his").</summary>
    public static bool IsPossessiveSuffix(string strongs) => HebrewNumber(strongs) is >= 9020 and <= 9029;

    public static int? HebrewNumber(string strongs)
    {
        if (strongs.Length < 2 || strongs[0] is not ('H' or 'h'))
        {
            return null;
        }

        var end = 1;
        while (end < strongs.Length && char.IsAsciiDigit(strongs[end]))
        {
            end++;
        }

        return int.TryParse(strongs.AsSpan(1, end - 1), out var n) ? n : null;
    }

    /// <summary><c>HC/R/Ncmsc/Sp3fs</c> → <c>C</c>, <c>R</c>, <c>Ncmsc</c>, <c>Sp3fs</c> (leading language letter H/A dropped).</summary>
    public static IReadOnlyList<string> GrammarSegments(string? grammar)
    {
        if (string.IsNullOrWhiteSpace(grammar))
        {
            return [];
        }

        var text = grammar.Trim();
        if (text[0] is 'H' or 'A')
        {
            text = text[1..];
        }

        return text.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Verb facts of a Hebrew grammar segment: <c>Vqw3ms</c> (finite: perfect p, sequential perfect q, imperfect i, wayyiqtol w,
    /// cohortative h, jussive j, imperative v), <c>Vqrmsa</c> (participle r/s), <c>Vqcc</c> (infinitive). Null when not a verb.
    /// </summary>
    public static SourceVerb? HebrewVerb(string? segment)
    {
        if (segment is null || segment.Length < 3 || segment[0] != 'V')
        {
            return null;
        }

        var conjugation = segment[2];
        if ("pqiwhjv".Contains(conjugation))
        {
            int? person = segment.Length > 3 && char.IsAsciiDigit(segment[3]) ? segment[3] - '0' : null;
            char? number = segment.Length > 5 ? Lower(segment[5]) : null;
            return new SourceVerb(true, false, person, number is 's' or 'p' or 'd' ? Plural(number.Value) : null);
        }

        if (conjugation is 'r' or 's')
        {
            char? number = segment.Length > 4 ? Lower(segment[4]) : null;
            return new SourceVerb(false, true, null, number is 's' or 'p' or 'd' ? Plural(number.Value) : null);
        }

        return new SourceVerb(false, false, null, null);
    }

    /// <summary>
    /// Verb facts of a Greek morphology code: <c>V-AAI-1S</c> (finite moods I, S, O, M), <c>V-2AAP-NPM</c> (participle), <c>V-PAN</c>
    /// (infinitive). Null when not a verb.
    /// </summary>
    public static SourceVerb? GreekVerb(string? morphology)
    {
        if (morphology is null || !morphology.StartsWith("V-", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = morphology.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || parts[1].Length == 0)
        {
            return new SourceVerb(false, false, null, null);
        }

        var mood = parts[1][^1];
        var detail = parts.Length > 2 ? parts[2] : string.Empty;
        if (mood is 'I' or 'S' or 'O' or 'M')
        {
            int? person = detail.Length > 0 && char.IsAsciiDigit(detail[0]) ? detail[0] - '0' : null;
            char? number = detail.Length > 1 ? Lower(detail[1]) : null;
            return new SourceVerb(true, false, person, number is 's' or 'p' ? number : null);
        }

        if (mood == 'P')
        {
            char? number = detail.Length > 1 ? Lower(detail[1]) : null;
            return new SourceVerb(false, true, null, number is 's' or 'p' ? number : null);
        }

        return new SourceVerb(false, false, null, null);
    }

    private static char Lower(char c) => char.ToLowerInvariant(c);

    /// <summary>Dual counts as plural for German pronouns.</summary>
    private static char Plural(char number) => number == 'd' ? 'p' : number;
}

/// <summary>Finite verb with person/number, participle (number only) or other verb form (infinitive).</summary>
public sealed record SourceVerb(bool IsFinite, bool IsParticiple, int? Person, char? Number);
