using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Features.CrossReferences;

/// <summary>
/// Word links of TSK links (roadmap 0024.5), same rule as words notes (roadmap 0023, <see cref="NoteQuote"/>): positions are Elb1871 positions of the
/// source verse, the quote is rebuilt server-side from <c>Elb1871Words.WordInContext</c>; on read a link whose words no longer match the quote
/// (whitespace and punctuation ignored) or whose positions are gone is <c>drifted</c> and loses its positions.
/// </summary>
public static class CrossReferenceWordLink
{
    /// <summary>Null when the link is not attached to a word.</summary>
    public static TskWordResponse? Build(TskCrossReference row)
    {
        if (row.StartPosition is not { } from || row.EndPosition is not { } to || row.QuotedText is null)
        {
            return null;
        }

        return IsDrifted(from, to, row.QuotedText, row.CurrentWordCount, row.CurrentQuote)
            ? new TskWordResponse { QuotedText = row.QuotedText, State = TskWordLinkStates.Drifted }
            : new TskWordResponse { StartPosition = from, EndPosition = to, QuotedText = row.QuotedText, State = TskWordLinkStates.Ok };
    }

    /// <summary>A position is gone (fewer words than the range) or the words differ from the quote after <see cref="NoteQuote.Normalize"/>.</summary>
    public static bool IsDrifted(int from, int to, string quotedText, int currentWordCount, string? currentQuote) =>
        currentWordCount != to - from + 1 || !NoteQuote.Matches(quotedText, currentQuote);

    /// <summary>True when the link is attached (state <c>ok</c>) to the Elb1871 word at <paramref name="positionInVerse"/> of its source verse.</summary>
    public static bool IsOnWord(TskWordResponse? word, int positionInVerse) =>
        word is { State: TskWordLinkStates.Ok, StartPosition: { } from, EndPosition: { } to } && positionInVerse >= from && positionInVerse <= to;

    /// <summary>
    /// The variant words of the source verse (active at the requested level) that carry the marker: <c>BasePositionInVerse</c> inside the range.
    /// Null when the link has no ok word link or the verse has no variant at that level.
    /// </summary>
    public static List<TskMappedWordResponse>? MapToVariant(TskWordResponse? word, IReadOnlyList<NoteVariantWordRow>? variantWords)
    {
        if (word is not { State: TskWordLinkStates.Ok, StartPosition: { } from, EndPosition: { } to } || variantWords is not { Count: > 0 })
        {
            return null;
        }

        return variantWords
            .Where(w => w.BasePositionInVerse >= from && w.BasePositionInVerse <= to)
            .OrderBy(w => w.PositionInVerse)
            .Select(w => new TskMappedWordResponse { PositionInVerse = w.PositionInVerse, BasePositionInVerse = w.BasePositionInVerse!.Value })
            .ToList();
    }
}
