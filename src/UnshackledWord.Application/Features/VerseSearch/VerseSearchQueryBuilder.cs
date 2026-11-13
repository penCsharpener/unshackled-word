using System.Text.RegularExpressions;

namespace UnshackledWord.Application.Features.VerseSearch;

/// <summary>A searched word with the lexemes and lemmas it is matched by.</summary>
public sealed record VerseQueryToken(string Text, IReadOnlyList<string> Lexemes, IReadOnlyList<string> Lemmas);

/// <summary>
/// The tsquery text (lemma-widened) and its literal twin (stems only, used to rank literal hits first), the searched words,
/// and the lexemes / lemmas that mark a word of a hit verse as matched.
/// </summary>
public sealed record BuiltVerseQuery(
    string TsQuery,
    string LiteralTsQuery,
    IReadOnlyList<VerseQueryToken> Tokens,
    IReadOnlySet<string> MatchStems,
    IReadOnlySet<string> MatchLemmas);

/// <summary>
/// Turns a parsed query into tsquery text. Every lexeme is single-quoted with quotes and backslashes escaped, so user text can never
/// become an operator. Lemma alternatives (<c>'lemma':B</c>, weight B = lemma part of the search vector) are added to single words only:
/// not inside phrases (the lemma positions do not line up with the text positions) and not to excluded words.
/// </summary>
public static partial class VerseSearchQueryBuilder
{
    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex AtomRegex();

    /// <summary>Letters only: what the 'simple' configuration of the lemma part keeps as one lexeme.</summary>
    public static bool IsPlainLemma(string lemma) => lemma.Length > 0 && AtomRegex().Match(lemma) is { Success: true } m && m.Length == lemma.Length;

    /// <summary>The letter runs of a word ("jahwe-gott" gives jahwe and gott); each is stemmed on its own.</summary>
    public static IReadOnlyList<string> Atoms(string word) => AtomRegex().Matches(word).Select(m => m.Value).ToList();

    /// <param name="stems">Stem lexeme per atom (ts_lexize); an atom without entry is searched as typed.</param>
    /// <param name="lemmas">Lemma candidates per single word (already limited); only used for the lemma-widened query.</param>
    public static BuiltVerseQuery Build(VerseSearchQuery query, IReadOnlyDictionary<string, string> stems, IReadOnlyDictionary<string, IReadOnlyList<string>> lemmas)
    {
        var tokens = new List<VerseQueryToken>();
        var matchStems = new HashSet<string>(StringComparer.Ordinal);
        var matchLemmas = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        string Stem(string atom) => stems.TryGetValue(atom, out var s) && !string.IsNullOrEmpty(s) ? s : atom;

        IReadOnlyList<string> WordLexemes(string word) => Atoms(word).Select(Stem).ToList();

        IReadOnlyList<string> WidenedLemmas(string word)
        {
            if (!lemmas.TryGetValue(word, out var candidates))
            {
                return [];
            }

            var own = WordLexemes(word);
            return candidates.Where(l => IsPlainLemma(l) && !own.Contains(l)).Distinct(StringComparer.Ordinal).ToList();
        }

        string TermExpression(SearchTerm term, bool widen)
        {
            if (term.IsPhrase)
            {
                return "(" + string.Join(" <-> ", term.Words.SelectMany(WordLexemes).Select(Quote)) + ")";
            }

            var word = term.Words[0];
            var stem = string.Join(" & ", WordLexemes(word).Select(Quote));
            if (term.Negated)
            {
                return "!(" + stem + ")";
            }

            IReadOnlyList<string> extra = widen ? WidenedLemmas(word) : [];
            return extra.Count == 0
                ? "(" + stem + ")"
                : "(" + stem + " | " + string.Join(" | ", extra.Select(l => Quote(l) + ":B")) + ")";
        }

        string Expression(bool widen) => string.Join(" | ", query.Groups.Select(g => "(" + string.Join(" & ", g.Select(t => TermExpression(t, widen))) + ")"));

        foreach (var term in query.Groups.SelectMany(g => g).Where(t => !t.Negated))
        {
            foreach (var word in term.Words)
            {
                var lexemes = WordLexemes(word);
                matchStems.UnionWith(lexemes);

                IReadOnlyList<string> widened = term.IsPhrase ? [] : WidenedLemmas(word);
                matchLemmas.UnionWith(widened);

                if (seen.Add(word))
                {
                    tokens.Add(new VerseQueryToken(word, lexemes, widened));
                }
            }
        }

        return new BuiltVerseQuery(Expression(true), Expression(false), tokens, matchStems, matchLemmas);
    }

    private static string Quote(string lexeme) => "'" + lexeme.Replace("\\", "\\\\").Replace("'", "''") + "'";
}
