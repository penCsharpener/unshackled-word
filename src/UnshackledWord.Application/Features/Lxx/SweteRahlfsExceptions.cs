using System.Globalization;
using System.Text.RegularExpressions;

namespace UnshackledWord.Application.Features.Lxx;

/// <summary>
/// The Swete side of an exception row: a verse or verse range of one chapter in Swete's numbering and book code (<c>Joe</c>, <c>Sol</c>,
/// <c>Dat</c> ...), optionally narrowed to marker segments (<c>[N]</c>, <c>[N-M]</c>: Swete's printed subverse numbers) or to a word range
/// (<c>#A-B</c>, <c>#A-</c> = to the end; 1-based word positions of the verse as in <c>01-Swete_word_with_punctuations.csv</c>).
/// </summary>
public sealed record SweteSelector(string Book, int Chapter, int FromVerse, int ToVerse, int? FromMarker, int? ToMarker, int? FromWord, int? ToWord)
{
    public bool IsWordRange => FromWord is not null;

    public bool IsMarkerRange => FromMarker is not null;

    public bool IsWholeVerse => !IsWordRange && !IsMarkerRange;

    public bool Covers(string book, int chapter, int verse) => Book == book && Chapter == chapter && verse >= FromVerse && verse <= ToVerse;

    public override string ToString()
    {
        var text = FromVerse == ToVerse ? $"{Book}.{Chapter}:{FromVerse}" : $"{Book}.{Chapter}:{FromVerse}-{ToVerse}";
        if (IsMarkerRange)
        {
            text += FromMarker == ToMarker ? $"[{FromMarker}]" : $"[{FromMarker}-{ToMarker}]";
        }

        if (IsWordRange)
        {
            text += $"#{FromWord}-{(ToWord is null ? "" : ToWord.Value.ToString(CultureInfo.InvariantCulture))}";
        }

        return text;
    }
}

/// <summary>
/// One row of <c>assets/seed/lxx/swete-to-rahlfs.csv</c> (roadmap 0028.2).
/// <list type="bullet">
/// <item><see cref="Swete"/> null: a gap row, the Rahlfs/TVTMS Greek references have no text in Swete.</item>
/// <item><see cref="Rahlfs"/> empty: Swete's text has no Rahlfs/TVTMS counterpart (MapKind <c>absent</c>).</item>
/// <item>Otherwise the selected Swete units take the Rahlfs references pairwise (same count), or all of them join into one Rahlfs verse
/// (one reference).</item>
/// </list>
/// </summary>
public sealed record SweteRahlfsException(int Line, SweteSelector? Swete, IReadOnlyList<TvtmsRef> Rahlfs, string RahlfsText, string TextForm, string Note);

/// <summary>
/// Reader of the exception list <c>assets/seed/lxx/swete-to-rahlfs.csv</c>: UTF-8, <c>;</c> separated, header
/// <c>SweteRef;RahlfsRef;TextForm;Note</c>, lines starting with <c>#</c> and empty lines are skipped. <c>RahlfsRef</c> uses the TVTMS syntax
/// (<c>Jol.3:1-5</c>, subverses <c>Jos.9:2.1-6</c>; TVTMS book codes) and never crosses a chapter. <c>TextForm</c> is empty, or <c>og</c>/<c>th</c>
/// for Daniel (it must agree with the Swete book: <c>Dan</c> = og, <c>Dat</c> = th). Every row needs a <c>Note</c>.
/// </summary>
public static partial class SweteRahlfsExceptionList
{
    public const string Header = "SweteRef;RahlfsRef;TextForm;Note";

    [GeneratedRegex(@"^(?<b>[1-3A-Za-z][A-Za-z0-9]{2})\.(?<c>[0-9]+):(?<v>[0-9]+)(?:-(?<v2>[0-9]+))?(?:\[(?<m>[0-9]+)(?:-(?<m2>[0-9]+))?\]|#(?<w>[0-9]+)-(?<w2>[0-9]+)?)?$")]
    private static partial Regex SwetePattern();

    public static IReadOnlyList<SweteRahlfsException> Parse(IEnumerable<string> lines, List<string> errors)
    {
        var rows = new List<SweteRahlfsException>();
        var lineNumber = 0;
        var headerSeen = false;
        foreach (var raw in lines)
        {
            lineNumber++;
            var line = raw.TrimEnd('\r').TrimStart('﻿');
            if (line.Trim().Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (!headerSeen)
            {
                headerSeen = true;
                if (line.Trim() != Header)
                {
                    errors.Add($"line {lineNumber}: expected header '{Header}'.");
                }

                continue;
            }

            var cells = line.Split(';', 4);
            if (cells.Length < 4)
            {
                errors.Add($"line {lineNumber}: expected 4 cells 'SweteRef;RahlfsRef;TextForm;Note'.");
                continue;
            }

            var row = ReadRow(lineNumber, cells.Select(c => c.Trim()).ToArray(), errors);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static SweteRahlfsException? ReadRow(int line, string[] cells, List<string> errors)
    {
        var (sweteText, rahlfsText, textForm, note) = (cells[0], cells[1], cells[2], cells[3]);
        if (note.Length == 0)
        {
            errors.Add($"line {line}: every row needs a Note.");
        }

        if (textForm is not ("" or "og" or "th"))
        {
            errors.Add($"line {line}: TextForm must be empty, 'og' or 'th'.");
            return null;
        }

        SweteSelector? swete = null;
        if (sweteText.Length > 0)
        {
            swete = ParseSwete(sweteText);
            if (swete is null)
            {
                errors.Add($"line {line}: SweteRef '{sweteText}' is not 'Book.C:V', 'Book.C:V-W', 'Book.C:V[N]', 'Book.C:V[N-M]' or 'Book.C:V#A-B'.");
                return null;
            }

            var derived = swete.Book == "Dat" ? "th" : swete.Book == "Dan" ? "og" : "";
            if (textForm.Length > 0 && textForm != derived)
            {
                errors.Add($"line {line}: TextForm '{textForm}' does not match Swete book {swete.Book}.");
            }
        }

        var rahlfs = new List<TvtmsRef>();
        if (rahlfsText.Length > 0)
        {
            var spans = TvtmsGreekParser.ParseCell(rahlfsText, out var absent, out var ok);
            if (!ok || absent || spans.Count == 0 || spans.Any(s => s.CrossesChapter))
            {
                errors.Add($"line {line}: RahlfsRef '{rahlfsText}' is not a TVTMS reference list inside one chapter.");
                return null;
            }

            rahlfs.AddRange(spans.SelectMany(s => s.Expand()));
            if (rahlfs.Any(r => LxxBookCodes.BookIdFromTvtms(r.Book) is null || !int.TryParse(r.Chapter, out _)))
            {
                errors.Add($"line {line}: RahlfsRef '{rahlfsText}' needs a protocanonical TVTMS book code and a numeric chapter.");
                return null;
            }
        }

        if (swete is null && rahlfs.Count == 0)
        {
            errors.Add($"line {line}: SweteRef and RahlfsRef are both empty.");
            return null;
        }

        return new SweteRahlfsException(line, swete, rahlfs, rahlfsText, textForm, note);
    }

    /// <summary>Parses the Swete side of a row, or null.</summary>
    public static SweteSelector? ParseSwete(string text)
    {
        var match = SwetePattern().Match(text.Trim());
        if (!match.Success)
        {
            return null;
        }

        int? Group(string name) => match.Groups[name].Success ? int.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture) : null;

        var from = Group("v")!.Value;
        var to = Group("v2") ?? from;
        var marker = Group("m");
        var word = Group("w");
        if (to < from || ((marker is not null || word is not null) && to != from))
        {
            return null;
        }

        var markerTo = marker is null ? null : Group("m2") ?? marker;
        if (markerTo < marker || (word is not null && Group("w2") is { } w2 && w2 < word))
        {
            return null;
        }

        return new SweteSelector(match.Groups["b"].Value, Group("c")!.Value, from, to, marker, markerTo, word, word is null ? null : Group("w2"));
    }
}
