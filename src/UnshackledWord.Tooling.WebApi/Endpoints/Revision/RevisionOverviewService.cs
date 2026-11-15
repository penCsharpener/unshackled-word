using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Domain.Models.Dbo.Revision;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision;

/// <summary>
/// Builds <c>GET api/revision/overview</c>: tiles per level, the sizes of the three work lists and one page of a list.
/// The stale-hash list is decided here, not in SQL, because the hash is the C# chapter fingerprint over the normalised Elb1871 tokens.
/// </summary>
public sealed class RevisionOverviewService
{
    public const int MaxPageSize = 250;

    private readonly IRevisionOverviewRepository _repository;
    private readonly IRevisionLevelRepository _levels;

    public RevisionOverviewService(IRevisionOverviewRepository repository, IRevisionLevelRepository levels)
    {
        _repository = repository;
        _levels = levels;
    }

    public async Task<RevisionOverviewResponse> GetAsync(RevisionOverviewRequest request, string locale)
    {
        var levels = await _levels.GetAllAsync();
        var variants = await _repository.GetActiveVariantsAsync();
        var total = await _repository.GetTotalVersesAsync();
        var stale = await FindStaleAsync(variants);

        var response = new RevisionOverviewResponse
        {
            Levels = BuildLevels(levels, variants, total, locale),
            Counts = new RevisionWorkCounts
            {
                Unreviewed = variants.Count(IsUnreviewed),
                Incomplete = variants.Count(IsIncomplete),
                Stale = stale.Count,
            },
        };

        if (request.List is { } name)
        {
            response.List = BuildList(name, request, variants, stale);
        }

        return response;
    }

    private static bool IsUnreviewed(RevisionOverviewVariantRow row) => row.MappingReviewedAt is null;

    private static bool IsIncomplete(RevisionOverviewVariantRow row) => row.UnlinkedGermanWords > 0 || row.UnlinkedOriginalWords > 0;

    private static List<RevisionLevelOverview> BuildLevels(IReadOnlyList<RevisionLevelDbo> levels, IReadOnlyList<RevisionOverviewVariantRow> variants, int total, string locale) =>
        levels
            .Where(l => !l.IsBase)
            .OrderBy(l => l.Level)
            .Select(l =>
            {
                var own = variants.Where(v => v.Level == l.Level).ToList();
                return new RevisionLevelOverview
                {
                    Level = l.Level,
                    Code = l.Code,
                    Name = locale == "de" ? l.NameDe : l.NameEn,
                    RevisedVerses = own.Count,
                    TotalVerses = total,
                    Books = own
                        .GroupBy(v => BookOf(v.HebRefId))
                        .OrderBy(g => g.Key)
                        .Select(g => new RevisionBookCount { BibleBookId = g.Key, RevisedVerses = g.Count() })
                        .ToList(),
                };
            })
            .ToList();

    private async Task<HashSet<int>> FindStaleAsync(IReadOnlyList<RevisionOverviewVariantRow> variants)
    {
        var stale = new HashSet<int>();
        if (variants.Count == 0)
        {
            return stale;
        }

        var words = await _repository.GetBaseWordsAsync(variants.Select(v => v.HebRefId).Distinct().ToArray());
        var hashes = words
            .GroupBy(w => w.HebRefId)
            .ToDictionary(g => g.Key, g => ChapterFingerprint.Compute(g.Select(w => AnchorToken.ForWord(g.Key, w.PositionInVerse, w.PlainWord))));

        foreach (var variant in variants)
        {
            // A verse without Elb1871 words no longer exists: the variant's base text is gone, which is stale too.
            if (!hashes.TryGetValue(variant.HebRefId, out var current) || current != variant.BaseTextHash)
            {
                stale.Add(variant.RevisionVerseId);
            }
        }

        return stale;
    }

    private static RevisionWorkList BuildList(string name, RevisionOverviewRequest request, IReadOnlyList<RevisionOverviewVariantRow> variants, HashSet<int> stale)
    {
        Func<RevisionOverviewVariantRow, bool> filter = name switch
        {
            RevisionWorkListNames.Unreviewed => IsUnreviewed,
            RevisionWorkListNames.Incomplete => IsIncomplete,
            RevisionWorkListNames.Stale => v => stale.Contains(v.RevisionVerseId),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown work list."),
        };

        var rows = variants
            .Where(filter)
            .Where(v => request.Level is null || v.Level == request.Level)
            .OrderBy(v => v.Level)
            .ThenBy(v => v.HebRefId)
            .ToList();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        return new RevisionWorkList
        {
            Name = name,
            Total = rows.Count,
            Page = page,
            PageSize = pageSize,
            Items = rows.Skip((page - 1) * pageSize).Take(pageSize).Select(ToItem).ToList(),
        };
    }

    private static RevisionWorkItem ToItem(RevisionOverviewVariantRow row) => new()
    {
        RevisionVerseId = row.RevisionVerseId,
        Level = row.Level,
        Version = row.Version,
        BibleBookId = BookOf(row.HebRefId),
        Chapter = row.HebRefId / 1000 % 1000,
        Verse = row.HebRefId % 1000,
        CreatedAt = row.CreatedAt,
        UnlinkedGermanWords = row.UnlinkedGermanWords,
        UnlinkedOriginalWords = row.UnlinkedOriginalWords,
    };

    private static int BookOf(int hebRefId) => hebRefId / 1_000_000;
}
