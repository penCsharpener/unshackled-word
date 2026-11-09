using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.VerseMapping;

public sealed class VerseMappingService : IVerseMappingService
{
    /// <summary>Configuration key of the write gate; replaced by real authorization once there is an auth system.</summary>
    public const string EditingEnabledKey = "MappingEditing:Enabled";

    private const int LastHebrewBookId = 39;

    private readonly IVerseMappingRepository _repository;
    private readonly IConfiguration _configuration;

    public VerseMappingService(IVerseMappingRepository repository, IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    public async Task<GetVerseMappingResponse?> GetAsync(int bibleBookId, int chapter, int verse, CancellationToken token = default)
    {
        var germanWords = await _repository.GetGermanWordsAsync(bibleBookId, chapter, verse, token);
        return germanWords.Count == 0 ? null : await BuildAsync(bibleBookId, chapter, verse, germanWords, token);
    }

    public async Task<SaveVerseMappingResult> SaveAsync(int bibleBookId, int chapter, int verse, IReadOnlyList<VerseMappingLink> links, CancellationToken token = default)
    {
        if (!IsEditingEnabled())
        {
            return new SaveVerseMappingResult
            {
                Outcome = SaveVerseMappingOutcome.Forbidden,
                Errors = ["Mapping editing is disabled on this server (MappingEditing:Enabled is false)."],
            };
        }

        var germanWords = await _repository.GetGermanWordsAsync(bibleBookId, chapter, verse, token);
        if (germanWords.Count == 0)
        {
            return new SaveVerseMappingResult { Outcome = SaveVerseMappingOutcome.NotFound };
        }

        var hebrew = IsHebrew(bibleBookId);
        var originalWords = await _repository.GetOriginalWordsAsync(hebrew, bibleBookId, chapter, verse, token);

        var validation = await new SaveVerseMappingValidator().ValidateAsync(new VerseMappingSaveInput
        {
            Links = links,
            GermanWordIds = germanWords.Select(w => w.Id).ToHashSet(),
            OriginalWordIds = originalWords.Select(w => w.Id).ToHashSet(),
        }, token);

        if (!validation.IsValid)
        {
            return new SaveVerseMappingResult
            {
                Outcome = SaveVerseMappingOutcome.Invalid,
                Errors = validation.Errors.Select(e => e.ErrorMessage).ToList(),
            };
        }

        var rows = links
            .Select(l => new VerseMappingRow
            {
                ElbWordId = l.ElbWordId,
                StepWordId = l.IsAddedWord ? null : l.StepWordId,
                IsAddedWord = l.IsAddedWord,
                ParentElbWordId = l.IsAddedWord ? l.ParentElbWordId : null,
            })
            .ToList();

        await _repository.ReplaceMappingAsync(hebrew, bibleBookId, chapter, verse, rows, token);

        return new SaveVerseMappingResult
        {
            Outcome = SaveVerseMappingOutcome.Saved,
            Mapping = await BuildAsync(bibleBookId, chapter, verse, germanWords, token),
        };
    }

    private async Task<GetVerseMappingResponse> BuildAsync(int bibleBookId, int chapter, int verse, IReadOnlyList<VerseGermanWordRow> germanWords, CancellationToken token)
    {
        var hebrew = IsHebrew(bibleBookId);
        var originalWords = await _repository.GetOriginalWordsAsync(hebrew, bibleBookId, chapter, verse, token);
        var rows = await _repository.GetMappingRowsAsync(hebrew, bibleBookId, chapter, verse, token);

        return new GetVerseMappingResponse
        {
            BibleBookId = bibleBookId,
            Chapter = chapter,
            Verse = verse,
            Language = hebrew ? "hebrew" : "greek",
            EditingEnabled = IsEditingEnabled(),
            GermanWords = germanWords
                .Select(w => new VerseMappingGermanWord { Id = w.Id, PositionInVerse = w.PositionInVerse, Text = w.Text })
                .ToList(),
            OriginalWords = originalWords
                .Select(w => new VerseMappingOriginalWord
                {
                    Id = w.Id,
                    Position = w.Position,
                    Original = w.Original,
                    Transliteration = NullIfEmpty(w.Transliteration),
                    Strongs = NullIfEmpty(w.Strongs),
                    Gloss = NullIfEmpty(w.Gloss),
                    MorphologyCode = NullIfEmpty(w.MorphologyCode),
                })
                .ToList(),
            Links = BuildLinks(rows, germanWords, originalWords),
        };
    }

    /// <summary>
    /// Defensive reading of stored rows: the seed over-assigned some of them. A parent from another verse is ignored, an original
    /// word from another verse invalidates the link, and added-word rows never link to an original.
    /// </summary>
    private static List<VerseMappingLink> BuildLinks(IReadOnlyList<VerseMappingRow> rows, IReadOnlyList<VerseGermanWordRow> germanWords, IReadOnlyList<VerseOriginalWordRow> originalWords)
    {
        var germanIds = germanWords.Select(w => w.Id).ToHashSet();
        var originalIds = originalWords.Select(w => w.Id).ToHashSet();
        var germanOrder = germanWords.Select((w, i) => (w.Id, Index: i)).ToDictionary(x => x.Id, x => x.Index);
        var originalOrder = originalWords.Select((w, i) => (w.Id, Index: i)).ToDictionary(x => x.Id, x => x.Index);
        var seen = new HashSet<(int, int?)>();
        var links = new List<VerseMappingLink>();

        foreach (var row in rows)
        {
            if (!germanIds.Contains(row.ElbWordId))
            {
                continue;
            }

            VerseMappingLink link;
            if (row.IsAddedWord)
            {
                if (row.ParentElbWordId is not { } parent || parent == row.ElbWordId || !germanIds.Contains(parent))
                {
                    continue;
                }

                link = new VerseMappingLink { ElbWordId = row.ElbWordId, IsAddedWord = true, ParentElbWordId = parent };
            }
            else
            {
                if (row.StepWordId is not { } stepId || !originalIds.Contains(stepId))
                {
                    continue;
                }

                link = new VerseMappingLink { ElbWordId = row.ElbWordId, StepWordId = stepId };
            }

            if (seen.Add((link.ElbWordId, link.StepWordId)))
            {
                links.Add(link);
            }
        }

        return links
            .OrderBy(l => germanOrder[l.ElbWordId])
            .ThenBy(l => l.StepWordId is { } id ? originalOrder[id] : -1)
            .ToList();
    }

    private bool IsEditingEnabled()
    {
        return bool.TryParse(_configuration[EditingEnabledKey], out var enabled) && enabled;
    }

    private static bool IsHebrew(int bibleBookId)
    {
        return bibleBookId <= LastHebrewBookId;
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
