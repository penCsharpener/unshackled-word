using UnshackledWord.Application.Features.VerseCounting;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Application.Features.WordDetail;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;
using UnshackledWord.Domain.WebApi.Lxx;

namespace UnshackledWord.Application.Features.Lxx;

public interface ILxxWordReadService
{
    /// <summary>Null when no LXX word has this id.</summary>
    /// <param name="includeProposed">Admin/Maintainer: a proposed tag is returned too (status <c>proposed</c>, with its source).</param>
    Task<GetLxxWordResponse?> GetAsync(int lxxWordId, bool includeProposed, CancellationToken token = default);
}

/// <summary>
/// The word box of one LXX word (roadmap 0028.5): form and Rahlfs reference, and for a visible tag (<see cref="LxxTagVisibility"/>) lemma, Strong's,
/// the decoded code with its German/English description (<see cref="GreekMorphologyCode"/>) and the TBESG entry. Two queries for a tagged word
/// (word, lexicon), one otherwise.
/// </summary>
public sealed class LxxWordReadService : ILxxWordReadService
{
    private readonly ILxxReadRepository _repository;
    private readonly IStrongsLexiconRepository _lexicon;
    private readonly IVerseCountingMapProvider _countingMap;

    public LxxWordReadService(ILxxReadRepository repository, IStrongsLexiconRepository lexicon, IVerseCountingMapProvider countingMap)
    {
        _repository = repository;
        _lexicon = lexicon;
        _countingMap = countingMap;
    }

    public async Task<GetLxxWordResponse?> GetAsync(int lxxWordId, bool includeProposed, CancellationToken token = default)
    {
        var row = await _repository.GetWordAsync(lxxWordId, token);
        if (row is null)
        {
            return null;
        }

        LexiconSummaryResponse? lexicon = null;
        if (LxxTagVisibility.IsVisible(row.Status, includeProposed) && row.Strongs is not null)
        {
            var entry = await LexiconSummaries.FindEntryAsync(_lexicon, row.Strongs, token);
            if (entry is not null)
            {
                lexicon = LexiconSummaries.Build(row.Strongs, entry, await _countingMap.GetAsync(token));
            }
        }

        return Build(row, includeProposed, lexicon);
    }

    /// <summary>Pure mapping (public for tests): the tag only when visible to the caller; the source only for Admin/Maintainer.</summary>
    public static GetLxxWordResponse Build(LxxWordDetailRow row, bool includeProposed, LexiconSummaryResponse? lexicon)
    {
        var response = new GetLxxWordResponse
        {
            Id = row.Id,
            LxxVerseId = row.LxxVerseId,
            Position = row.Position,
            Greek = row.Greek,
            Ref = LxxChapterReadService.FormatRef(row.BibleBookId, row.Chapter, row.Verse, row.Subverse),
            BibleBookId = row.BibleBookId,
            Chapter = row.Chapter,
            Verse = row.Verse,
            Subverse = row.Subverse,
            TextForm = row.TextForm,
            IsAddition = row.MapKind == "addition",
            HebRefId = row.HebRefId ?? row.AfterHebRefId,
        };

        if (!LxxTagVisibility.IsVisible(row.Status, includeProposed) || row.Lemma is null || row.Strongs is null || row.Morphology is null)
        {
            return response;
        }

        var parse = GreekMorphologyCode.Parse(row.Morphology);
        response.Tag = new LxxWordTagDetailResponse
        {
            Lemma = row.Lemma,
            Strongs = row.Strongs,
            Gloss = lexicon?.Gloss ?? (string.IsNullOrWhiteSpace(row.Gloss) ? null : row.Gloss),
            Morphology = MorphologyResponses.Decode(false, row.Morphology)!,
            ParseGerman = parse?.German,
            ParseEnglish = parse?.English,
            Status = row.Status!,
            Source = includeProposed ? row.Source : null,
            Lexicon = lexicon,
        };
        return response;
    }
}
