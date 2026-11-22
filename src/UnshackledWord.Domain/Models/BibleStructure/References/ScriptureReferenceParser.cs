using System.Globalization;

namespace UnshackledWord.Domain.Models.BibleStructure.References;

/// <summary>Which verse numbering a parsed reference uses. Validation uses the verse table of the matching counting.</summary>
public enum VerseCounting
{
    Hebrew,
    English,
}

/// <param name="Profile">Alias set and notation: German/Legacy use German citation style, Osis the STEP notation.</param>
/// <param name="AllowPrefix">Resolve typed book prefixes ("Offen") when they are unique (search box).</param>
/// <param name="WholeInput">The whole input must be references; otherwise parsing stops quietly at the first text that is not one.</param>
/// <param name="Counting">Verse counting of the numbers in the input.</param>
public sealed record ScriptureParseOptions(AliasProfile Profile, bool AllowPrefix, bool WholeInput, VerseCounting Counting);

/// <summary>An inclusive span inside one book. A whole chapter is verse 0 to 999.</summary>
public readonly record struct VerseSpan(int StartChapter, int StartVerse, int EndChapter, int EndVerse)
{
    public const int WholeChapterEndVerse = 999;

    /// <summary>Start and end ref id (<c>book * 1_000_000 + chapter * 1_000 + verse</c>), compatible with <see cref="PassageRange"/>.</summary>
    public (int StartRefId, int EndRefId) ToRefIds(int bookId) =>
        (bookId * 1_000_000 + StartChapter * 1_000 + StartVerse, bookId * 1_000_000 + EndChapter * 1_000 + EndVerse);
}

/// <summary>One book with its verse spans. <see cref="BookLength"/> is the length of the typed book name at <see cref="Start"/>. <see cref="BookId"/> is null for non-canonical books (<see cref="NonCanonical"/> holds the text).</summary>
public sealed record ScriptureReference(
    int? BookId,
    string? NonCanonical,
    IReadOnlyList<VerseSpan> Spans,
    int Start,
    int Length,
    bool BookOnly,
    int BookLength = 0);

public sealed record ScriptureParseError(string Code, int Position, IReadOnlyDictionary<string, string> Args);

public sealed record ScriptureParseResult(
    IReadOnlyList<ScriptureReference> References,
    ScriptureParseError? Error,
    IReadOnlyList<int> BookCandidates)
{
    public bool IsSuccess => Error is null && References.Count > 0;
}

public static class ScriptureParseErrorCodes
{
    public const string Empty = "empty";
    public const string UnknownBook = "unknownBook";
    public const string AmbiguousBook = "ambiguousBook";
    public const string MissingChapter = "missingChapter";
    public const string ChapterOutOfRange = "chapterOutOfRange";
    public const string VerseOutOfRange = "verseOutOfRange";
    public const string EndBeforeStart = "endBeforeStart";
    public const string UnexpectedToken = "unexpectedToken";
}

/// <summary>
/// Reads scripture references with a regex tokenizer, a book resolver and a small interpretation state machine.
/// German citation style (profiles German and Legacy): "," chapter/verse, "." (and "u."/"und") verses, ";" new chapter or book.
/// STEP/OSIS style (profile Osis): "." chapter/verse, "," verses, ";" new chapter. Never throws.
/// </summary>
public static class ScriptureReferenceParser
{
    public static ScriptureParseResult Parse(string? input, ScriptureParseOptions options)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ScriptureParseResult([], MakeError(ScriptureParseErrorCodes.Empty, 0), []);
        }

        var walker = new Walker(input, ScriptureTokenizer.Tokenize(input), 0, options);
        var references = new List<ScriptureReference>();
        do
        {
            walker.ContinueWithBook = false;
            var reference = walker.ReadReference();
            if (reference is null)
            {
                return new ScriptureParseResult(references, walker.Error, walker.Candidates);
            }

            references.Add(reference);
            if (walker.Error is not null)
            {
                return new ScriptureParseResult(references, walker.Error, walker.Candidates);
            }
        }
        while (walker.ContinueWithBook);

        if (options.WholeInput)
        {
            var rest = walker.NextNonWs(walker.Position);
            if (rest < walker.TokenCount)
            {
                return new ScriptureParseResult(references, MakeError(ScriptureParseErrorCodes.UnexpectedToken, walker.TokenStart(rest)), []);
            }
        }

        return new ScriptureParseResult(references, null, []);
    }

    /// <summary>Scanner entry point: reads one reference beginning at or after <paramref name="start"/> and consumes what fits. Null when none.</summary>
    public static ScriptureReference? ParseAt(string text, int start, ScriptureParseOptions options)
    {
        if (string.IsNullOrEmpty(text) || start < 0 || start >= text.Length)
        {
            return null;
        }

        var tokens = ScriptureTokenizer.Tokenize(text);
        var first = 0;
        while (first < tokens.Count && tokens[first].Start < start)
        {
            first++;
        }

        var walker = new Walker(text, tokens, first, options with { WholeInput = false });
        var reference = walker.ReadReference();
        return walker.Error is null ? reference : null;
    }

    private static ScriptureParseError MakeError(string code, int position, params (string Key, string Value)[] args) =>
        new(code, position, args.ToDictionary(a => a.Key, a => a.Value));

    private sealed class Walker
    {
        private readonly string _input;
        private readonly IReadOnlyList<ScriptureToken> _tokens;
        private readonly ScriptureParseOptions _options;
        private readonly bool _osis;
        private int _p;
        private int _lastEnd;

        // interpretation state of the current book
        private int[]? _chapters;
        private bool _singleChapter;
        private bool _verseLevel;
        private int _chapter;

        public Walker(string input, IReadOnlyList<ScriptureToken> tokens, int position, ScriptureParseOptions options)
        {
            _input = input;
            _tokens = tokens;
            _p = position;
            _options = options;
            _osis = options.Profile == AliasProfile.Osis;
        }

        public ScriptureParseError? Error { get; private set; }
        public List<int> Candidates { get; } = [];
        public bool ContinueWithBook { get; set; }
        public int Position => _p;
        public int TokenCount => _tokens.Count;

        public int TokenStart(int index) => index < _tokens.Count ? _tokens[index].Start : _input.Length;

        public int NextNonWs(int index)
        {
            while (index < _tokens.Count && _tokens[index].Kind == ScriptureTokenKind.Whitespace)
            {
                index++;
            }

            return index;
        }

        private ScriptureTokenKind? KindAt(int index) => index >= 0 && index < _tokens.Count ? _tokens[index].Kind : null;

        private bool Fail(string code, int position, params (string Key, string Value)[] args)
        {
            Error = MakeError(code, position, args);
            return false;
        }

        // ---- reference ----

        public ScriptureReference? ReadReference()
        {
            _p = NextNonWs(_p);
            if (_p >= _tokens.Count)
            {
                if (Error is null)
                {
                    Fail(ScriptureParseErrorCodes.Empty, _input.Length);
                }

                return null;
            }

            var firstToken = _p;
            if (!TryResolveBook(_p, _options.AllowPrefix, out var match, out var bookEnd))
            {
                SetBookError(_p);
                return null;
            }

            _p = bookEnd;
            _lastEnd = EndOf(bookEnd - 1);
            _chapters = match.BookId is { } id && _options.Counting == VerseCounting.Hebrew
                ? VerseTables.HebrewCounts(id)
                : null;
            _singleChapter = match.BookId is { } bid && VerseTables.EnglishCounts(bid) is { Length: 1 };
            _verseLevel = false;
            _chapter = 0;

            var spans = new List<VerseSpan>();
            var next = NextNonWs(_p);
            var bookOnly = KindAt(next) != ScriptureTokenKind.Number;
            if (bookOnly)
            {
                if (KindAt(next) is ScriptureTokenKind.Comma or ScriptureTokenKind.Colon or ScriptureTokenKind.Dash)
                {
                    Fail(ScriptureParseErrorCodes.MissingChapter, TokenStart(next));
                    return null;
                }

                spans.Add(new VerseSpan(1, 0, 1, VerseSpan.WholeChapterEndVerse));
                HandleSeparator(allowGroup: false);
            }
            else
            {
                while (true)
                {
                    _p = next;
                    if (!ReadGroup(spans))
                    {
                        return null;
                    }

                    var after = HandleSeparator(allowGroup: true);
                    if (after == Separator.Error)
                    {
                        return null;
                    }

                    if (after != Separator.NextGroup)
                    {
                        break;
                    }

                    next = NextNonWs(_p);
                }
            }

            var start = _tokens[firstToken].Start;
            return new ScriptureReference(match.BookId, match.NonCanonical, spans, start, _lastEnd - start, bookOnly, EndOf(bookEnd - 1) - start);
        }

        private int EndOf(int tokenIndex) => _tokens[tokenIndex].Start + _tokens[tokenIndex].Length;

        private enum Separator
        {
            None,
            NextGroup,
            End,
            Error,
        }

        /// <summary>Handles a ";" or a sentence dot after a group. A following book sets <see cref="ContinueWithBook"/>.</summary>
        private Separator HandleSeparator(bool allowGroup)
        {
            var j = NextNonWs(_p);
            var kind = KindAt(j);
            if (kind == ScriptureTokenKind.Dot && NextNonWs(j + 1) >= _tokens.Count)
            {
                _p = _tokens.Count;
                return Separator.End;
            }

            if (kind != ScriptureTokenKind.Semicolon)
            {
                return Separator.None;
            }

            var k = NextNonWs(j + 1);
            if (k >= _tokens.Count)
            {
                _p = _tokens.Count;
                return Separator.End;
            }

            if (TryResolveBook(k, _options.AllowPrefix, out _, out _))
            {
                _p = k;
                ContinueWithBook = true;
                return Separator.End;
            }

            if (allowGroup && KindAt(k) == ScriptureTokenKind.Number)
            {
                _p = k;
                return Separator.NextGroup;
            }

            if (KindAt(k) == ScriptureTokenKind.Word && _options.WholeInput)
            {
                SetBookError(k);
                return Separator.Error;
            }

            return Separator.None;
        }

        // ---- book ----

        private static bool IsOrdinalNumber(ScriptureToken t) => t.Kind == ScriptureTokenKind.Number && t.Text is "1" or "2" or "3";

        private static bool IsRoman(ScriptureToken t) =>
            t.Kind == ScriptureTokenKind.Word && (t.Text.Equals("I", StringComparison.OrdinalIgnoreCase)
                                                  || t.Text.Equals("II", StringComparison.OrdinalIgnoreCase)
                                                  || t.Text.Equals("III", StringComparison.OrdinalIgnoreCase));

        /// <summary>Exclusive end token indexes of possible book names starting at <paramref name="index"/>, longest first.</summary>
        private List<int> BookEnds(int index)
        {
            var ends = new List<int>();
            if (index >= _tokens.Count)
            {
                return ends;
            }

            AddWordEnds(index, ends);
            var first = _tokens[index];
            if (IsOrdinalNumber(first) || IsRoman(first))
            {
                var w = index + 1;
                if (KindAt(w) == ScriptureTokenKind.Dot)
                {
                    w++;
                }

                if (KindAt(w) == ScriptureTokenKind.Whitespace)
                {
                    w++;
                }

                AddWordEnds(w, ends);
            }

            return ends.Distinct().OrderByDescending(e => e).ToList();
        }

        private void AddWordEnds(int w, List<int> ends)
        {
            if (KindAt(w) != ScriptureTokenKind.Word)
            {
                return;
            }

            var e1 = w + 1;
            ends.Add(e1);
            if (KindAt(e1) == ScriptureTokenKind.Dot)
            {
                ends.Add(e1 + 1);
            }

            var j = e1;
            if (KindAt(j) == ScriptureTokenKind.Whitespace)
            {
                j++;
            }

            if (KindAt(j) == ScriptureTokenKind.Word)
            {
                var e2 = j + 1;
                ends.Add(e2);
                if (KindAt(e2) == ScriptureTokenKind.Dot)
                {
                    ends.Add(e2 + 1);
                }
            }
        }

        private string TextOf(int from, int toExclusive) =>
            _input[_tokens[from].Start..EndOf(toExclusive - 1)];

        private bool TryResolveBook(int index, bool allowPrefix, out BookMatch match, out int end)
        {
            var ends = BookEnds(index);
            foreach (var e in ends)
            {
                if (BookAliasCatalog.Resolve(TextOf(index, e), _options.Profile) is { } exact)
                {
                    match = exact;
                    end = e;
                    return true;
                }
            }

            if (allowPrefix)
            {
                foreach (var e in ends)
                {
                    if (BookAliasCatalog.Resolve(TextOf(index, e), _options.Profile, allowPrefix: true) is { } prefix)
                    {
                        match = prefix;
                        end = e;
                        return true;
                    }
                }
            }

            match = default;
            end = index;
            return false;
        }

        private void SetBookError(int index)
        {
            var ends = BookEnds(index);
            if (ends.Count == 0)
            {
                Fail(ScriptureParseErrorCodes.UnexpectedToken, TokenStart(index));
                return;
            }

            if (_options.AllowPrefix)
            {
                foreach (var e in ends)
                {
                    var candidates = BookAliasCatalog.Candidates(TextOf(index, e), _options.Profile);
                    if (candidates.Count > 1)
                    {
                        Candidates.AddRange(candidates);
                        Fail(ScriptureParseErrorCodes.AmbiguousBook, TokenStart(index), ("text", TextOf(index, e)));
                        return;
                    }
                }
            }

            Fail(ScriptureParseErrorCodes.UnknownBook, TokenStart(index), ("text", TextOf(index, ends[^1])));
        }

        // ---- groups and items ----

        private readonly record struct Point(int First, int FirstPos, int? Second, int SecondPos);

        private bool ReadGroup(List<VerseSpan> spans)
        {
            var groupStart = true;
            while (true)
            {
                if (!ReadItem(spans, groupStart))
                {
                    return false;
                }

                groupStart = false;
                var j = NextNonWs(_p);
                var kind = KindAt(j);
                if (!_osis && kind is ScriptureTokenKind.Comma or ScriptureTokenKind.Colon)
                {
                    return Fail(ScriptureParseErrorCodes.UnexpectedToken, TokenStart(j));
                }

                if (!TryReadVerseSeparator(j))
                {
                    return true;
                }
            }
        }

        private int _verseSepPos;

        /// <summary>Consumes "." / "u." / "und" (German) or "," (Osis) when a number follows. Leaves the position before the number.</summary>
        private bool TryReadVerseSeparator(int j)
        {
            var kind = KindAt(j);
            int numberAt;
            if (_osis)
            {
                if (kind != ScriptureTokenKind.Comma)
                {
                    return false;
                }

                numberAt = NextNonWs(j + 1);
            }
            else if (kind == ScriptureTokenKind.Dot)
            {
                numberAt = NextNonWs(j + 1);
            }
            else if (kind == ScriptureTokenKind.Word && _tokens[j].Text.Equals("u", StringComparison.OrdinalIgnoreCase) && KindAt(j + 1) == ScriptureTokenKind.Dot)
            {
                numberAt = NextNonWs(j + 2);
            }
            else if (kind == ScriptureTokenKind.Word && _tokens[j].Text.Equals("und", StringComparison.OrdinalIgnoreCase))
            {
                numberAt = NextNonWs(j + 1);
            }
            else
            {
                return false;
            }

            if (KindAt(numberAt) != ScriptureTokenKind.Number)
            {
                return false;
            }

            _verseSepPos = TokenStart(j);
            _p = numberAt;
            return true;
        }

        private bool ReadItem(List<VerseSpan> spans, bool groupStart)
        {
            var wasVerseLevel = _verseLevel;
            if (!ReadPoint(groupStart, out var a))
            {
                return false;
            }

            int sc, sv;
            bool verseLevel;
            if (a.Second is { } second)
            {
                if (!CheckChapter(a.First, a.FirstPos) || !CheckVerse(a.First, second, a.SecondPos))
                {
                    return false;
                }

                sc = a.First;
                sv = second;
                verseLevel = true;
            }
            else if (groupStart)
            {
                if (_singleChapter)
                {
                    if (!CheckVerse(1, a.First, a.FirstPos))
                    {
                        return false;
                    }

                    sc = 1;
                    sv = a.First;
                    verseLevel = true;
                }
                else
                {
                    if (!CheckChapter(a.First, a.FirstPos))
                    {
                        return false;
                    }

                    sc = a.First;
                    sv = 0;
                    verseLevel = false;
                }
            }
            else
            {
                if (!wasVerseLevel)
                {
                    return Fail(ScriptureParseErrorCodes.UnexpectedToken, _verseSepPos);
                }

                sc = _chapter;
                sv = a.First;
                verseLevel = true;
                if (!CheckVerse(sc, sv, a.FirstPos))
                {
                    return false;
                }
            }

            var ec = sc;
            var ev = verseLevel ? sv : VerseSpan.WholeChapterEndVerse;
            var ranged = false;

            var dash = NextNonWs(_p);
            if (KindAt(dash) == ScriptureTokenKind.Dash)
            {
                _p = dash + 1;
                if (!ReadPoint(false, out var b))
                {
                    return false;
                }

                ranged = true;
                if (verseLevel)
                {
                    if (b.Second is { } endVerse)
                    {
                        if (!CheckChapter(b.First, b.FirstPos) || !CheckVerse(b.First, endVerse, b.SecondPos))
                        {
                            return false;
                        }

                        ec = b.First;
                        ev = endVerse;
                    }
                    else
                    {
                        if (!CheckVerse(sc, b.First, b.FirstPos))
                        {
                            return false;
                        }

                        ev = b.First;
                    }
                }
                else
                {
                    if (b.Second is not null)
                    {
                        return Fail(ScriptureParseErrorCodes.UnexpectedToken, b.SecondPos);
                    }

                    if (!CheckChapter(b.First, b.FirstPos))
                    {
                        return false;
                    }

                    ec = b.First;
                }

                if (ec < sc || (ec == sc && ev < sv) || (!verseLevel && ec < sc))
                {
                    return Fail(ScriptureParseErrorCodes.EndBeforeStart, b.FirstPos);
                }
            }

            if (!ReadSuffix(sc, sv, ref ec, ref ev, verseLevel, ranged))
            {
                return false;
            }

            spans.Add(new VerseSpan(sc, sv, ec, ev));
            _chapter = ec;
            _verseLevel = verseLevel;
            return true;
        }

        /// <summary>Optional "f", "ff" (end verse) and "a"/"b" (half verse, dropped), with an optional dot.</summary>
        private bool ReadSuffix(int sc, int sv, ref int ec, ref int ev, bool verseLevel, bool ranged)
        {
            var j = NextNonWs(_p);
            if (KindAt(j) != ScriptureTokenKind.Word)
            {
                return true;
            }

            var word = _tokens[j].Text.ToLowerInvariant();
            if (word is not ("f" or "ff" or "a" or "b"))
            {
                return true;
            }

            if (!verseLevel || (ranged && word is "f" or "ff"))
            {
                return Fail(ScriptureParseErrorCodes.UnexpectedToken, TokenStart(j));
            }

            var consumed = j + 1;
            if (KindAt(consumed) == ScriptureTokenKind.Dot && KindAt(NextNonWs(consumed + 1)) != ScriptureTokenKind.Number)
            {
                consumed++;
            }

            if (word == "f")
            {
                ev = sv + 1;
                if (!CheckVerse(sc, ev, TokenStart(j)))
                {
                    return false;
                }
            }
            else if (word == "ff")
            {
                ev = _chapters is not null && sc >= 1 && sc <= _chapters.Length 
                    ? _chapters[sc - 1]
                    : VerseSpan.WholeChapterEndVerse;
            }

            _p = consumed;
            _lastEnd = EndOf(j);
            return true;
        }

        /// <summary>Reads <c>NUM [cvSep NUM]</c> greedily. German: "," or ":" (blank only at group start); Osis: "." or ":".</summary>
        private bool ReadPoint(bool groupStart, out Point point)
        {
            point = default;
            _p = NextNonWs(_p);
            if (KindAt(_p) != ScriptureTokenKind.Number)
            {
                return Fail(ScriptureParseErrorCodes.UnexpectedToken, TokenStart(_p));
            }

            if (!TryNumber(_p, out var first))
            {
                return false;
            }

            var firstPos = _tokens[_p].Start;
            _lastEnd = EndOf(_p);
            _p++;

            var j = NextNonWs(_p);
            var kind = KindAt(j);
            var isSep = _osis
                ? kind == ScriptureTokenKind.Colon || (kind == ScriptureTokenKind.Dot && KindAt(NextNonWs(j + 1)) == ScriptureTokenKind.Number)
                : kind is ScriptureTokenKind.Comma or ScriptureTokenKind.Colon;
            if (isSep)
            {
                var n = NextNonWs(j + 1);
                if (KindAt(n) != ScriptureTokenKind.Number)
                {
                    return Fail(ScriptureParseErrorCodes.UnexpectedToken, n < _tokens.Count ? _tokens[n].Start : _tokens[j].Start);
                }

                if (!TryNumber(n, out var second))
                {
                    return false;
                }

                point = new Point(first, firstPos, second, _tokens[n].Start);
                _lastEnd = EndOf(n);
                _p = n + 1;
                return true;
            }

            if (groupStart && !_osis && KindAt(_p) == ScriptureTokenKind.Whitespace && KindAt(j) == ScriptureTokenKind.Number)
            {
                if (!TryNumber(j, out var second))
                {
                    return false;
                }

                point = new Point(first, firstPos, second, _tokens[j].Start);
                _lastEnd = EndOf(j);
                _p = j + 1;
                return true;
            }

            point = new Point(first, firstPos, null, 0);
            return true;
        }

        private bool TryNumber(int index, out int value)
        {
            // Number tokens may hold non-ASCII digits; int.TryParse with invariant culture only accepts ASCII.
            if (!int.TryParse(_tokens[index].Text, NumberStyles.None, CultureInfo.InvariantCulture, out value))
            {
                value = 0;
                return Fail(ScriptureParseErrorCodes.UnexpectedToken, _tokens[index].Start);
            }

            return true;
        }

        private bool CheckChapter(int chapter, int position)
        {
            var max = _chapters is not null ? _chapters.Length : 999;
            if (chapter < 1 || chapter > max)
            {
                return Fail(ScriptureParseErrorCodes.ChapterOutOfRange, position,
                    ("value", chapter.ToString(CultureInfo.InvariantCulture)), ("max", max.ToString(CultureInfo.InvariantCulture)));
            }

            return true;
        }

        private bool CheckVerse(int chapter, int verse, int position)
        {
            if (_chapters is not null)
            {
                var max = chapter >= 1 && chapter <= _chapters.Length ? _chapters[chapter - 1] : 0;
                if (verse < 1 || verse > max)
                {
                    return Fail(ScriptureParseErrorCodes.VerseOutOfRange, position,
                        ("value", verse.ToString(CultureInfo.InvariantCulture)), ("max", max.ToString(CultureInfo.InvariantCulture)));
                }
            }
            else if (verse > VerseSpan.WholeChapterEndVerse - 1)
            {
                return Fail(ScriptureParseErrorCodes.VerseOutOfRange, position,
                    ("value", verse.ToString(CultureInfo.InvariantCulture)), ("max", "998"));
            }

            return true;
        }
    }
}
