using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.WebApi.Lxx;

namespace UnshackledWord.Application.Features.Lxx;

public interface ILxxChapterReadService
{
    /// <param name="includeProposed">Admin/Maintainer: proposed tags are returned too (flagged by their status).</param>
    Task<GetLxxChapterResponse> GetChapterAsync(int bibleBookId, int chapter, bool includeProposed, CancellationToken token = default);
}

/// <summary>
/// The Septuagint text per Elb1871 verse (roadmap 0028.4). Per verse the units are the LXX verses whose <c>HebRefId</c> equals it plus the additions whose
/// <c>AfterHebRefId</c> equals it, all in <c>SortOrder</c> (Swete's reading order). So an addition that Swete prints before its anchor verse (Esther
/// addition A before 1:1, <c>SortOrder</c> lower than the main unit) comes first, and one printed after it comes last. The order is Swete's, not a
/// rule of "additions after"; additions of one anchor keep their relative order. Word tags (0028.5) follow <see cref="LxxTagVisibility"/>.
/// </summary>
public sealed class LxxChapterReadService : ILxxChapterReadService
{
    private readonly ILxxReadRepository _repository;

    public LxxChapterReadService(ILxxReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetLxxChapterResponse> GetChapterAsync(int bibleBookId, int chapter, bool includeProposed, CancellationToken token = default)
    {
        var (from, to) = RevisionSql.ChapterRange(bibleBookId, chapter);
        var rows = await _repository.GetByHebRangeAsync(from, to, token);
        return Build(bibleBookId, chapter, rows, includeProposed);
    }

    /// <summary>Pure grouping (public for tests).</summary>
    public static GetLxxChapterResponse Build(int bibleBookId, int chapter, LxxChapterRows rows, bool includeProposed = false)
    {
        var wordsByUnit = rows.Words
            .GroupBy(w => w.LxxVerseId)
            .ToDictionary(g => g.Key, g => g.OrderBy(w => w.Position)
                .Select(w => new LxxWordResponse { Id = w.Id, Position = w.Position, Greek = w.Greek, Tag = ToTag(w, includeProposed) })
                .ToList());

        // Key = the Elb verse a unit belongs to (HebRefId for mapped units, AfterHebRefId for additions).
        var unitsByVerse = rows.Units
            .Where(u => (u.HebRefId ?? u.AfterHebRefId) is not null)
            .OrderBy(u => u.SortOrder)
            .ToLookup(u => (u.HebRefId ?? u.AfterHebRefId)!.Value);

        var response = new GetLxxChapterResponse { BibleBookId = bibleBookId, Chapter = chapter };
        foreach (var hebRefId in rows.ElbHebRefIds.Order())
        {
            var verse = hebRefId % 1_000;
            response.Verses.Add(new LxxElbVerseResponse
            {
                HebRefId = hebRefId,
                Verse = verse,
                Units = unitsByVerse[hebRefId].Select(u => ToResponse(u, chapter, verse, wordsByUnit)).ToList(),
            });
        }

        return response;
    }

    /// <summary>The word's tag when the caller may see it, else null.</summary>
    public static LxxTagResponse? ToTag(LxxWordRow word, bool includeProposed)
    {
        if (!LxxTagVisibility.IsVisible(word.Status, includeProposed) || word.Lemma is null || word.Strongs is null || word.Morphology is null)
        {
            return null;
        }

        return new LxxTagResponse
        {
            Lemma = word.Lemma,
            Strongs = word.Strongs,
            Gloss = string.IsNullOrWhiteSpace(word.Gloss) ? null : word.Gloss,
            Morphology = MorphologyResponses.Decode(false, word.Morphology)!,
            Status = word.Status!,
        };
    }

    /// <summary><c>Jer.32:1</c>, <c>1Ki.2:35.1</c>: TVTMS book code, Rahlfs chapter and verse, subverse when not 0.</summary>
    public static string FormatRef(int bibleBookId, int chapter, int verse, int subverse)
    {
        var code = bibleBookId is >= 1 and <= 39 ? LxxBookCodes.Tvtms[bibleBookId - 1] : bibleBookId.ToString();
        return $"{code}.{chapter}:{verse}" + (subverse > 0 ? $".{subverse}" : "");
    }

    private static LxxUnitResponse ToResponse(LxxUnitRow unit, int elbChapter, int elbVerse, Dictionary<int, List<LxxWordResponse>> wordsByUnit)
    {
        var isAddition = unit.MapKind == "addition";
        return new LxxUnitResponse
        {
            LxxVerseId = unit.Id,
            BibleBookId = unit.BibleBookId,
            Chapter = unit.Chapter,
            Verse = unit.Verse,
            Subverse = unit.Subverse,
            Ref = FormatRef(unit.BibleBookId, unit.Chapter, unit.Verse, unit.Subverse),
            TextForm = unit.TextForm,
            MapKind = unit.MapKind,
            IsAddition = isAddition,
            DiffersFromHebrew = isAddition || unit.Subverse > 0 || unit.Chapter != elbChapter || unit.Verse != elbVerse,
            Words = wordsByUnit.GetValueOrDefault(unit.Id) ?? [],
        };
    }
}
