using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping.Lxx;

/// <summary>Builds the packages of run mode <c>lxx-tag</c>; <see cref="AgentPackageBuilder"/> hands those requests here.</summary>
public interface ILxxAgentPackageBuilder
{
    /// <summary>Null when the range has no LXX unit (or every unit already has a proposal of the run).</summary>
    Task<GetAgentPackageResponse?> BuildAsync(AgentBuildRequest request, CancellationToken token = default);
}

/// <summary>
/// Packages of roadmap 0028.7 (05-tagging.md "Pass 2"): the LXX units of a Rahlfs range in Swete's reading order, never across a chapter and at most
/// <see cref="MaxVersesPerPackage"/> units (or the profile's smaller cap), without cutting the subverses of one verse apart. Per unit: the Swete words
/// with any stored tag as prefill ("check it"), TBESG candidates for the others (<see cref="LxxCandidateIndex"/>), and the STEP Hebrew words of its
/// <c>HebRefId</c> (none for additions). The model answers by Swete position and Hebrew number; <see cref="LxxVerseMap"/> maps them back.
/// Stats reuse <see cref="AgentTokenStats"/>: <c>GermanTokens</c> = LXX words, <c>Hint</c> = prefilled words, <c>Fixed</c> = confirmed ones, <c>Open</c> = the rest.
/// </summary>
public sealed class LxxAgentPackageBuilder : ILxxAgentPackageBuilder
{
    public const int MaxVersesPerPackage = 40;
    public const string PackageLanguage = "lxx";

    private readonly ILxxAgentDataRepository _data;
    private readonly IAgentStoreRepository _store;
    private readonly AgentMappingOptions _options;
    private readonly TimeProvider _time;
    private LxxCandidateIndex? _index;

    public LxxAgentPackageBuilder(ILxxAgentDataRepository data, IAgentStoreRepository store, IConfiguration configuration, TimeProvider time)
    {
        _data = data;
        _store = store;
        _options = AgentMappingOptions.From(configuration);
        _time = time;
    }

    public async Task<GetAgentPackageResponse?> BuildAsync(AgentBuildRequest request, CancellationToken token = default)
    {
        var range = request.Range;
        var data = await _data.GetRangeAsync(range.BibleBookId, range.FromChapter, range.FromVerse, range.ToChapter, range.ToVerse, token);
        if (data.Units.Count == 0)
        {
            return null;
        }

        var index = _index ??= new LxxCandidateIndex(LxxLexicon.Build(await _data.GetLexiconAsync(token)), await _data.GetNtFormsAsync(token));
        var skip = request.ExistingRunId is { } runId && !request.DryRun ? await _data.GetProposedUnitIdsAsync(runId, token) : new HashSet<int>();
        var drafts = BuildDrafts(range.BibleBookId, data, index)
            .Where(d => !skip.Contains(d.Unit.Id) && (request.IncludeConfirmed || !AllConfirmed(d)))
            .ToList();
        if (drafts.Count == 0)
        {
            return null;
        }

        var maxVerses = Math.Min(MaxVersesPerPackage, request.MaxVersesPerPackage ?? MaxVersesPerPackage);
        var runIdToUse = request.DryRun ? 0 : request.ExistingRunId ?? await _store.CreateRunAsync(new AgentRunInsert
        {
            CreatedByUserId = request.UserId,
            AiUserId = request.UserId,
            BibleBookId = range.BibleBookId,
            FromRefId = AgentRange.RefId(range.BibleBookId, range.FromChapter, range.FromVerse),
            ToRefId = AgentRange.RefId(range.BibleBookId, range.ToChapter, Math.Min(range.ToVerse, 999)),
            Mode = AgentModes.LxxTag,
            Profile = "manual",
            Model = "manual",
            StatusNote = "LXX package built by hand (debug endpoint); no worker is attached.",
        }, token);

        var response = new GetAgentPackageResponse { RunId = runIdToUse, Mode = AgentModes.LxxTag, TokenBudget = request.TokenBudget ?? _options.TokenBudget, Stats = Totals(drafts) };
        var expiresAt = _time.GetUtcNow().AddHours(_options.PackageExpiryHours);

        foreach (var group in Cut(drafts, Math.Max(1, maxVerses)))
        {
            var packageId = Guid.NewGuid();
            var (dto, map) = Materialize(packageId, range.BibleBookId, group);
            request.Maps?.Add(map);
            var fromRef = group.Min(d => AgentRange.RefId(range.BibleBookId, d.Unit.Chapter, d.Unit.Verse));
            var toRef = group.Max(d => AgentRange.RefId(range.BibleBookId, d.Unit.Chapter, d.Unit.Verse));

            if (!request.DryRun)
            {
                await _store.InsertPackageAsync(new AgentPackageInsert
                {
                    Id = packageId,
                    RunId = runIdToUse,
                    FromRefId = fromRef,
                    ToRefId = toRef,
                    WordIdsHash = AgentPackageBuilder.PackageHash(group.Select(d => d.Hash)),
                    LocalIdMapJson = map.ToJson(),
                    ExpiresAt = expiresAt,
                }, token);
            }

            response.Packages.Add(new AgentPackageEnvelope
            {
                Package = dto,
                FromRefId = fromRef,
                ToRefId = toRef,
                EstimatedTokens = EstimateTokens(JsonSerializer.Serialize(dto, AgentPackageJson.Options)),
                ExpiresAt = expiresAt,
                Stats = Totals(group),
            });
        }

        return response;
    }

    /// <summary>Hash of the LXX word ids and the Hebrew word ids of a unit: a re-import or a STEP re-import changes it.</summary>
    public static string VerseHash(IEnumerable<int> lxxWordIds, IEnumerable<int> hebrewWordIds)
    {
        var text = $"l:{string.Join(',', lxxWordIds)}|h:{string.Join(',', hebrewWordIds)}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>The package's label of a unit: <c>Rt 1:1</c>, <c>3Kö 2:35.1</c>, <c>Dan 3:24 (th)</c>.</summary>
    public static string Label(int bookId, int chapter, int verse, int subverse, string textForm) =>
        $"{GermanBookNames.GetAbbreviation(bookId)} {chapter}:{verse}{(subverse > 0 ? $".{subverse}" : string.Empty)}{(textForm.Length > 0 ? $" ({textForm})" : string.Empty)}";

    public static string HebLabel(int hebRefId) => $"{GermanBookNames.GetAbbreviation(hebRefId / 1_000_000)} {hebRefId / 1000 % 1000}:{hebRefId % 1000}";

    /// <summary>The words and Hebrew rows of every unit, in Swete order, with the same hashing the stale check uses.</summary>
    public static IReadOnlyList<LxxUnitDraft> Units(LxxAgentRangeData data)
    {
        var words = data.Words.GroupBy(w => w.LxxVerseId).ToDictionary(g => g.Key, g => g.OrderBy(w => w.Position).ToList());
        var hebrew = data.HebrewWords.GroupBy(h => h.RefId).ToDictionary(g => g.Key, g => g.OrderBy(h => h.Position).ThenBy(h => h.Id).ToList());
        return data.Units
            .OrderBy(u => u.SortOrder)
            .Select(u =>
            {
                var lxx = words.GetValueOrDefault(u.Id) ?? [];
                var heb = u.HebRefId is { } h ? hebrew.GetValueOrDefault(h) ?? [] : [];
                return new LxxUnitDraft(u, lxx, heb, VerseHash(lxx.Select(w => w.Id), heb.Select(w => w.Id)));
            })
            .ToList();
    }

    /// <summary>Every word of the unit has a confirmed tag: nothing is left for the model (05-tagging.md, "KI neu" skips such units unless "auch bestätigte").</summary>
    public static bool AllConfirmed(LxxUnitDraft draft) => draft.Words.Count > 0 && draft.Words.All(w => w.TagStatus == "confirmed");

    private int EstimateTokens(string text) => (int)Math.Ceiling(text.Sum(c => c < 128 ? 1 : 2) / _options.CharsPerToken);

    private static List<LxxUnitDraft> BuildDrafts(int bookId, LxxAgentRangeData data, LxxCandidateIndex index) =>
        Units(data).Where(d => d.Words.Count > 0).Select(d => d with { Index = index, BookId = bookId }).ToList();

    /// <summary>Consecutive units of one chapter, at most <paramref name="max"/>; the subverses of one verse (same text form) stay together.</summary>
    private static List<List<LxxUnitDraft>> Cut(List<LxxUnitDraft> drafts, int max)
    {
        var packages = new List<List<LxxUnitDraft>>();
        List<LxxUnitDraft>? current = null;
        foreach (var draft in drafts)
        {
            var last = current?[^1].Unit;
            var sameVerse = last is not null && last.Chapter == draft.Unit.Chapter && last.Verse == draft.Unit.Verse && last.TextForm == draft.Unit.TextForm;
            if (current is null || last!.Chapter != draft.Unit.Chapter || (current.Count >= max && !sameVerse))
            {
                current = [];
                packages.Add(current);
            }

            current.Add(draft);
        }

        return packages;
    }

    private static AgentTokenStats Totals(IReadOnlyList<LxxUnitDraft> drafts)
    {
        var words = drafts.SelectMany(d => d.Words).ToList();
        var confirmed = words.Count(w => w.TagStatus == "confirmed");
        var prefilled = words.Count(w => w.TagStrongs is not null && w.TagStatus != "confirmed");
        return new AgentTokenStats
        {
            GermanTokens = words.Count,
            Fixed = confirmed,
            Hint = prefilled,
            Open = words.Count - confirmed - prefilled,
            Verses = drafts.Count,
        };
    }

    private static (AgentPackageDto Dto, AgentPackageMap Map) Materialize(Guid packageId, int bookId, IReadOnlyList<LxxUnitDraft> units)
    {
        var dto = new AgentPackageDto { PackageId = packageId.ToString(), Lang = PackageLanguage };
        var map = new AgentPackageMap { Language = PackageLanguage, Mode = AgentModes.LxxTag, BookId = bookId, LxxVerses = [] };

        for (var i = 0; i < units.Count; i++)
        {
            var (verse, verseMap) = MaterializeUnit(units[i], $"v{i + 1}");
            dto.Verses.Add(verse);
            map.LxxVerses[verse.V] = verseMap;
        }

        return (dto, map);
    }

    private static (AgentVerseDto Dto, LxxVerseMap Map) MaterializeUnit(LxxUnitDraft draft, string v)
    {
        var unit = draft.Unit;
        var index = draft.Index!;
        var label = Label(draft.BookId, unit.Chapter, unit.Verse, unit.Subverse, unit.TextForm);
        var dto = new AgentVerseDto
        {
            V = v,
            Ref = label,
            Lxx = [],
            HebRef = unit.HebRefId is { } h && draft.Hebrew.Count > 0 ? HebLabel(h) : null,
            Map = unit.MapKind is "split" or "merged" ? unit.MapKind : null,
        };
        var map = new LxxVerseMap
        {
            Ref = label,
            LxxVerseId = unit.Id,
            Chapter = unit.Chapter,
            Verse = unit.Verse,
            Subverse = unit.Subverse,
            TextForm = unit.TextForm,
            HebRefId = unit.HebRefId,
            Hash = draft.Hash,
        };

        foreach (var word in draft.Words)
        {
            map.Words[word.Position] = word.Id;
            var row = new List<object?> { word.Position, word.Greek };
            if (word.TagStrongs is { } strongs && word.TagMorphology is { } morph)
            {
                // The lemma as TBESG writes it, so copying a correct prefill passes the validator (STEP writes some lemmas differently, ἐγώ for G3165 μέ).
                var lemma = index.Lexicon.MatchLemma(strongs, word.TagLemma) ?? index.Lexicon.First(strongs)?.Lemma ?? word.TagLemma ?? string.Empty;
                row.Add(word.TagStatus == "confirmed" ? new object[] { lemma, strongs, morph, "confirmed" } : new object[] { lemma, strongs, morph });
                map.Prefill[word.Position] = new LxxPrefillMap { Strongs = strongs, Morph = morph, Source = word.TagSource ?? string.Empty, Status = word.TagStatus ?? string.Empty };
            }
            else
            {
                var candidates = index.Candidates(word.Greek);
                row.Add(null);
                if (candidates.Count > 0)
                {
                    row.Add(candidates.Select(c => new object[] { c.Strongs, c.Lemma, c.Gloss }).ToArray());
                }
            }

            while (row.Count > 2 && row[^1] is null)
            {
                row.RemoveAt(row.Count - 1);
            }

            dto.Lxx.Add([.. row]);
        }

        if (dto.HebRef is not null)
        {
            dto.Heb = [];
            var n = 1;
            foreach (var word in draft.Hebrew)
            {
                map.Hebrew[n] = word.Id;
                var row = new List<object?> { n, word.Text, word.Strongs, word.Gloss, word.Morphology };
                var morphemes = HebrewMorphemes.Parse(word.ExpandedStrongTags);
                if (morphemes.Count > 1)
                {
                    row.Add(morphemes.Select(m => m.IsMain ? $"{{{m.Strongs}}}" : m.Strongs).ToArray());
                }

                dto.Heb.Add([.. row]);
                n++;
            }
        }

        return (dto, map);
    }
}

/// <summary>One unit with its words and Hebrew words in reading order; <see cref="Index"/> and <see cref="BookId"/> are set when it is packaged.</summary>
public sealed record LxxUnitDraft(LxxAgentUnitRow Unit, IReadOnlyList<LxxAgentWordRow> Words, IReadOnlyList<AgentSourceWordRow> Hebrew, string Hash)
{
    public LxxCandidateIndex? Index { get; init; }
    public int BookId { get; init; }
}
