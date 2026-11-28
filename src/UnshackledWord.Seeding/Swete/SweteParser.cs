using System.Globalization;
using UnshackledWord.Application.Features.Lxx;

namespace UnshackledWord.Seeding.Swete;

/// <summary>
/// Parser of eliranwong/LXX-Swete-1930 (roadmap 0028.1). Both files are UTF-8, tab separated, have no header and use LF line ends.
/// <list type="bullet">
/// <item><c>00-Swete_versification.csv</c>: <c>id TAB Book.Chapter:Verse</c>, e.g. <c>1  Gen.1:1</c>, <c>11  Gen.1:2</c>. The id is the id of the
/// verse's first word in the word file (confirmed: every id exists there, ids ascend).</item>
/// <item><c>01-Swete_word_with_punctuations.csv</c>: <c>id TAB word</c>, ids ascend and are consecutive from 1 (one word per line, the Greek
/// carries its trailing punctuation, e.g. <c>γῆν.</c>). Titles and headings are words like any other; they belong to the verse that
/// starts before them (the first words of Gen 1:1 are the heading <c>ΕΝ ΑΡΧΗ</c>).</item>
/// </list>
/// A verse owns the words from its id up to (excluding) the next verse's id; the last verse runs to the end of the word file. Words before
/// the first verse id are dropped. Book codes: Swete's own (see <c>LxxBookCodes</c>); Daniel appears twice (<c>Dan</c> = Old Greek,
/// <c>Dat</c> = Theodotion), likewise <c>Sus/Sut</c>, <c>Bel/Bet</c>, <c>Tob/Tbs</c>.
/// </summary>
public static class SweteParser
{
    public static IReadOnlyList<SweteVerse> Parse(IEnumerable<string> versificationLines, IEnumerable<string> wordLines)
    {
        var words = ParseWords(wordLines);
        var starts = ParseVerseStarts(versificationLines);

        var verses = new List<SweteVerse>(starts.Count);
        var wordIndex = 0;
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            var endId = i + 1 < starts.Count ? starts[i + 1].Id : int.MaxValue;
            while (wordIndex < words.Count && words[wordIndex].Id < start.Id)
            {
                wordIndex++;
            }

            var list = new List<SweteWord>();
            while (wordIndex < words.Count && words[wordIndex].Id < endId)
            {
                list.Add(new SweteWord(words[wordIndex].Id, list.Count + 1, words[wordIndex].Text));
                wordIndex++;
            }

            verses.Add(new SweteVerse(start.Id, start.Book, start.Chapter, start.Verse, list));
        }

        return verses;
    }

    private static List<(int Id, string Text)> ParseWords(IEnumerable<string> lines)
    {
        var words = new List<(int, string)>();
        var lineNumber = 0;
        foreach (var raw in lines)
        {
            lineNumber++;
            var line = raw.TrimEnd('\r').TrimStart('﻿');
            if (line.Length == 0)
            {
                continue;
            }

            var tab = line.IndexOf('\t');
            if (tab <= 0 || !int.TryParse(line.AsSpan(0, tab), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                throw new FormatException($"Swete word file, line {lineNumber}: expected 'id<TAB>word'.");
            }

            if (words.Count > 0 && id <= words[^1].Item1)
            {
                throw new FormatException($"Swete word file, line {lineNumber}: ids must ascend ({id} after {words[^1].Item1}).");
            }

            words.Add((id, line[(tab + 1)..]));
        }

        return words;
    }

    private static List<(int Id, string Book, int Chapter, int Verse)> ParseVerseStarts(IEnumerable<string> lines)
    {
        var starts = new List<(int, string, int, int)>();
        var lineNumber = 0;
        foreach (var raw in lines)
        {
            lineNumber++;
            var line = raw.TrimEnd('\r').TrimStart('﻿');
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('\t');
            var reference = parts.Length == 2 ? parts[1] : "";
            var dot = reference.IndexOf('.');
            var colon = reference.IndexOf(':');
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                || dot <= 0 || colon <= dot
                || !int.TryParse(reference.AsSpan(dot + 1, colon - dot - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var chapter)
                || !int.TryParse(reference.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var verse))
            {
                throw new FormatException($"Swete versification file, line {lineNumber}: expected 'id<TAB>Book.Chapter:Verse'.");
            }

            if (starts.Count > 0 && id <= starts[^1].Item1)
            {
                throw new FormatException($"Swete versification file, line {lineNumber}: ids must ascend ({id} after {starts[^1].Item1}).");
            }

            starts.Add((id, reference[..dot], chapter, verse));
        }

        return starts;
    }
}
