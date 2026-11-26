using UnshackledWord.Application.Features.ScriptureReferences;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.BibleStructure.References;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Application.Features.WordDetail;

/// <summary>Lexicon (TBESG/TBESH, <c>StepStrongsLexicon</c>) lookup and summary shared by the Elb1871 word detail and the LXX word detail (roadmap 0028.5).</summary>
public static class LexiconSummaries
{
    /// <summary>The entry of a (disambiguated) Strong's number: the exact <c>Extra</c> when there is one, else the entry without, else the first.</summary>
    public static async Task<LexiconEntry?> FindEntryAsync(IStrongsLexiconRepository lexicon, string strongs, CancellationToken token)
    {
        var parsed = StrongsNumber.Parse(strongs);
        if (parsed.Value is null)
        {
            return null;
        }

        var entries = await lexicon.GetEntriesAsync(parsed.Value.Value, token);
        if (entries.Count == 0)
        {
            return null;
        }

        var extra = parsed.Value.Value.Extra;
        if (extra is not null)
        {
            var exact = entries.FirstOrDefault(e => string.Equals(e.Extra, extra, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }
        }

        return entries.FirstOrDefault(e => string.IsNullOrEmpty(e.Extra)) ?? entries[0];
    }

    public static LexiconSummaryResponse? Build(string strongs, LexiconEntry? entry, IVerseCountingMap map)
    {
        if (entry is null)
        {
            return null;
        }

        var (text, truncated) = LexiconTextCleaner.Clean(entry.Lexicon);
        var (segments, _) = LexiconTextSegmenter.Segment(entry.Lexicon, 600, map);
        return new LexiconSummaryResponse
        {
            Strongs = strongs,
            Lemma = entry.OriginalWord,
            Transliteration = NullIfEmpty(entry.Transliteration),
            Gloss = NullIfEmpty(entry.Gloss),
            Definition = NullIfEmpty(text),
            DefinitionSegments = LexiconSegmentMapper.ToResponse(segments),
            DefinitionTruncated = truncated ? true : null,
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
