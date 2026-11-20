using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>The verses of one package request: one book, consecutive verses (chapter, verse) from..to inclusive.</summary>
public sealed class AgentRange
{
    public int BibleBookId { get; init; }
    public int FromChapter { get; init; }
    public int FromVerse { get; init; }
    public int ToChapter { get; init; }
    public int ToVerse { get; init; }

    public static int RefId(int book, int chapter, int verse) => book * 1_000_000 + chapter * 1_000 + verse;
}

/// <summary>Everything the agent worker needs beyond the debug endpoint: an existing run, a package size cap, verses to leave out, a dry run for estimates.</summary>
public sealed class AgentBuildRequest
{
    public required AgentRange Range { get; init; }
    public AgentMode Mode { get; init; } = AgentMode.Redo;
    public int? TokenBudget { get; init; }

    /// <summary>At most this many verses per package (profile <c>MaxVersesPerPackage</c>, shrunk after a schema failure).</summary>
    public int? MaxVersesPerPackage { get; init; }

    public int UserId { get; init; }

    /// <summary>Packages are stored for this run; when null a new <c>manual</c> run is created (debug endpoint).</summary>
    public int? ExistingRunId { get; init; }

    /// <summary>Build only: nothing is stored (no run, no package rows); the package ids are empty GUIDs. For estimates.</summary>
    public bool DryRun { get; init; }

    /// <summary>Verses (BBCCCVVV) that already have a proposal in the run; they are not packaged again (resume).</summary>
    public IReadOnlySet<int>? SkipRefIds { get; init; }

    /// <summary>MCP: the token that asks; the new run is bound to it (daily proposal cap) and named <c>mcp</c> instead of <c>manual</c>.</summary>
    public int? ApiTokenId { get; init; }

    /// <summary>When set, the id map of every built package is appended in package order (the example exporter needs the real ids of a dry run).</summary>
    public List<AgentPackageMap>? Maps { get; init; }

    /// <summary>
    /// The prompt version the packages are for (<c>greek-v2</c>). Mapping modes: from version 2 on (<see cref="AgentPromptProvider.CarriesRoles"/>)
    /// links carry role and morpheme and Hebrew source words their morphemes (roadmap 0018.9); null or v1 keeps the v1 format.
    /// </summary>
    public string? PromptVersion { get; init; }
}

public interface IAgentPackageBuilder
{
    Task<GetAgentPackageResponse?> BuildAsync(AgentBuildRequest request, CancellationToken token = default);

    /// <summary>Rough token count of a text (the measure packages are cut by).</summary>
    int EstimateTokens(string text);

    /// <summary>
    /// Builds the packages of a range, saves a run (<c>manual</c> profile, owned by <paramref name="userId"/>) and the packages
    /// (<c>AgentPackages</c>, so a submission can be bound to them). Null when the range has no German words.
    /// </summary>
    Task<GetAgentPackageResponse?> BuildAsync(AgentRange range, AgentMode mode, int? tokenBudget, int userId, CancellationToken token = default);
}

public sealed class AgentPackageBuilder : IAgentPackageBuilder
{
    private static readonly JsonSerializerOptions SizeJson = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly IAgentDataRepository _data;
    private readonly IAgentStoreRepository _store;
    private readonly AgentMappingOptions _options;
    private readonly TimeProvider _time;

    public AgentPackageBuilder(IAgentDataRepository data, IAgentStoreRepository store, IConfiguration configuration, TimeProvider time)
    {
        _data = data;
        _store = store;
        _options = AgentMappingOptions.From(configuration);
        _time = time;
    }

    public Task<GetAgentPackageResponse?> BuildAsync(AgentRange range, AgentMode mode, int? tokenBudget, int userId, CancellationToken token = default) =>
        BuildAsync(new AgentBuildRequest { Range = range, Mode = mode, TokenBudget = tokenBudget, UserId = userId }, token);

    public async Task<GetAgentPackageResponse?> BuildAsync(AgentBuildRequest request, CancellationToken token = default)
    {
        var range = request.Range;
        var mode = request.Mode;
        var tokenBudget = request.TokenBudget;
        var userId = request.UserId;
        var data = await _data.GetRangeAsync(range.BibleBookId, range.FromChapter, range.FromVerse, range.ToChapter, range.ToVerse, token);
        if (data.GermanWords.Count == 0)
        {
            return null;
        }

        var hebrew = range.BibleBookId <= 39;
        var forms = data.GermanWords.Where(w => !AgentDeterministicPass.IsPunctuationOnly(w)).Select(w => w.Plain.ToLowerInvariant()).Distinct().ToList();
        var lexicon = LemmaLexicon.Build(await _data.GetFormLemmasAsync(forms, token));
        var lemmas = data.GermanWords
            .Select(w => lexicon.Resolve(w.Plain, w.Lemma, w.LemmaHumanEdited).Lemma ?? w.Lemma)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l!.ToLowerInvariant())
            .Distinct()
            .ToList();
        var stats = new ConfirmedStats(await _data.GetConfirmedPairsAsync(hebrew, lemmas, token));

        var roles = mode == AgentMode.Lemma || AgentPromptProvider.CarriesRoles(request.PromptVersion);
        var drafts = BuildDrafts(range.BibleBookId, data, lexicon, stats, mode, hebrew, roles);
        var budget = tokenBudget ?? _options.TokenBudget;
        var skip = request.SkipRefIds;
        var cut = Cut(drafts.Where(d => !d.FullyFixed && (skip is null || !skip.Contains(AgentRange.RefId(range.BibleBookId, d.Chapter, d.Verse)))).ToList(), budget, range.BibleBookId, hebrew, mode, roles, request.MaxVersesPerPackage);

        var runId = request.DryRun ? 0 : request.ExistingRunId ?? await _store.CreateRunAsync(new AgentRunInsert
        {
            CreatedByUserId = userId,
            AiUserId = userId,
            BibleBookId = range.BibleBookId,
            FromRefId = AgentRange.RefId(range.BibleBookId, range.FromChapter, range.FromVerse),
            ToRefId = AgentRange.RefId(range.BibleBookId, range.ToChapter, range.ToVerse),
            Mode = mode.ToDb(),
            Profile = request.ApiTokenId is null ? "manual" : "mcp",
            Model = request.ApiTokenId is null ? "manual" : "mcp-agent",
            ApiTokenId = request.ApiTokenId,
            StatusNote = request.ApiTokenId is null ? "Package built by hand (debug endpoint); no worker is attached." : "Packages built through the MCP server; the agent calls the model itself.",
        }, token);

        var response = new GetAgentPackageResponse { RunId = runId, Mode = mode.ToDb(), TokenBudget = budget, Stats = Totals(drafts) };
        var expiresAt = _time.GetUtcNow().AddHours(_options.PackageExpiryHours);

        foreach (var group in cut)
        {
            var packageId = Guid.NewGuid();
            var (dto, map) = Materialize(packageId, range.BibleBookId, hebrew, mode, roles, group);
            request.Maps?.Add(map);
            var first = group[0];
            var last = group[^1];

            var fromRef = AgentRange.RefId(range.BibleBookId, first.Chapter, first.Verse);
            var toRef = AgentRange.RefId(range.BibleBookId, last.Chapter, last.Verse);

            if (!request.DryRun)
            {
                await _store.InsertPackageAsync(new AgentPackageInsert
                {
                    Id = packageId,
                    RunId = runId,
                    FromRefId = fromRef,
                    ToRefId = toRef,
                    WordIdsHash = PackageHash(map),
                    LocalIdMapJson = map.ToJson(),
                    ExpiresAt = expiresAt,
                }, token);
            }

            response.Packages.Add(new AgentPackageEnvelope
            {
                Package = dto,
                FromRefId = fromRef,
                ToRefId = toRef,
                EstimatedTokens = EstimateTokens(JsonSerializer.Serialize(dto, SizeJson)),
                ExpiresAt = expiresAt,
                Stats = Totals(group),
            });
        }

        return response;
    }

    /// <summary>Hash of the German and original word ids of a verse; a changed text changes the ids (revisions and re-imports create new rows).</summary>
    public static string VerseHash(IEnumerable<AgentGermanWordRow> german, IEnumerable<AgentSourceWordRow> source)
    {
        var text = $"g:{string.Join(',', german.OrderBy(w => w.PositionInVerse).ThenBy(w => w.Id).Select(w => w.Id))}|s:{string.Join(',', source.OrderBy(w => w.Position).ThenBy(w => w.Id).Select(w => w.Id))}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>The value of <c>AgentPackages.WordIdsHash</c>: the hashes of the package verses in order.</summary>
    public static string PackageHash(AgentPackageMap map) => PackageHash(map.Verses.OrderBy(v => v.Key.Length).ThenBy(v => v.Key, StringComparer.Ordinal).Select(v => v.Value.Hash));

    public static string PackageHash(IEnumerable<string> verseHashes) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', verseHashes))));

    /// <summary>Rough token estimate of text: ASCII at <c>CharsPerToken</c> characters per token, everything else (Greek, Hebrew, umlauts) at half that.</summary>
    public int EstimateTokens(string text)
    {
        var weighted = text.Sum(c => c < 128 ? 1 : 2);
        return (int)Math.Ceiling(weighted / _options.CharsPerToken);
    }

    private List<VerseDraft> BuildDrafts(int bookId, AgentRangeData data, LemmaLexicon lexicon, ConfirmedStats stats, AgentMode mode, bool hebrew, bool roles)
    {
        var drafts = new List<VerseDraft>();
        foreach (var verse in data.GermanWords.GroupBy(w => (w.Chapter, w.Verse)).OrderBy(g => g.Key.Chapter).ThenBy(g => g.Key.Verse))
        {
            var german = verse.ToList();
            var refIds = german.Select(w => w.HebRefId).ToHashSet();
            var source = data.SourceWords.Where(s => refIds.Contains(s.RefId)).ToList();
            var ids = german.Select(w => w.Id).ToHashSet();
            var mappings = data.Mappings.Where(m => ids.Contains(m.ElbWordId)).ToList();
            var verseRef = $"{GermanBookNames.GetAbbreviation(bookId)} {verse.Key.Chapter}:{verse.Key.Verse}";

            var draft = mode == AgentMode.Lemma
                ? AgentLemmaPass.Run(verse.Key.Chapter, verse.Key.Verse, verseRef, VerseHash(german, source), german, source, mappings, lexicon, hebrew)
                : AgentDeterministicPass.Run(verse.Key.Chapter, verse.Key.Verse, verseRef, VerseHash(german, source), german, source, mappings, lexicon, stats, _options, mode, hebrew);
            drafts.Add(mode != AgentMode.Lemma && roles ? AgentLemmaPass.WithRoles(draft, mappings, hebrew) : draft);
        }

        return drafts;
    }

    /// <summary>
    /// Consecutive verses up to the budget, never splitting a verse; a single verse above the budget gets a package of its own. The size is measured on
    /// the real package JSON (ids, wrapper and all), so the estimate of a finished package never exceeds the budget unless it holds one verse.
    /// </summary>
    private List<List<VerseDraft>> Cut(List<VerseDraft> drafts, int budget, int bookId, bool hebrew, AgentMode mode, bool roles, int? maxVerses = null)
    {
        var packages = new List<List<VerseDraft>>();
        List<VerseDraft>? current = null;

        foreach (var draft in drafts)
        {
            if (current is not null && (maxVerses is null || current.Count < maxVerses))
            {
                var trial = Materialize(Guid.Empty, bookId, hebrew, mode, roles, [.. current, draft]).Dto;
                if (EstimateTokens(JsonSerializer.Serialize(trial, SizeJson)) <= budget)
                {
                    current.Add(draft);
                    continue;
                }
            }

            current = [draft];
            packages.Add(current);
        }

        return packages;
    }

    private static AgentTokenStats Totals(IReadOnlyList<VerseDraft> drafts) => new()
    {
        GermanTokens = drafts.Sum(d => d.German.Count),
        RemovedPunctuation = drafts.Sum(d => d.RemovedPunctuation),
        Fixed = drafts.Sum(d => d.German.Count(g => g.Status == AgentTokenStatus.Fixed)),
        Hint = drafts.Sum(d => d.German.Count(g => g.Status == AgentTokenStatus.Hint)),
        Open = drafts.Sum(d => d.German.Count(g => g.Status == AgentTokenStatus.Open)),
        LemmaFixed = drafts.Sum(d => d.German.Count(g => g.LemmaState == AgentLemmaState.Fixed)),
        LemmaHint = drafts.Sum(d => d.German.Count(g => g.LemmaState == AgentLemmaState.Hint)),
        LemmaOpen = drafts.Sum(d => d.German.Count(g => g.LemmaState is null)),
        Verses = drafts.Count,
        SkippedVerses = drafts.Count(d => d.FullyFixed),
    };

    private static (AgentPackageDto Dto, AgentPackageMap Map) Materialize(Guid packageId, int bookId, bool hebrew, AgentMode mode, bool roles, IReadOnlyList<VerseDraft> verses)
    {
        var dto = new AgentPackageDto { PackageId = packageId.ToString(), Lang = hebrew ? "hebrew" : "greek" };
        var map = new AgentPackageMap { Language = dto.Lang, Mode = mode.ToDb(), BookId = bookId, Roles = roles && mode != AgentMode.Lemma };
        int g = 1, s = 1;

        for (var i = 0; i < verses.Count; i++)
        {
            var (verseDto, verseMap, nextG, nextS) = MaterializeVerse(verses[i], $"v{i + 1}", g, s, mode == AgentMode.Lemma, roles);
            dto.Verses.Add(verseDto);
            map.Verses[verseDto.V] = verseMap;
            g = nextG;
            s = nextS;
        }

        return (dto, map);
    }

    private static (AgentVerseDto Dto, AgentVerseMap Map, int NextG, int NextS) MaterializeVerse(VerseDraft draft, string v, int g, int s, bool lemmaMode, bool roles)
    {
        var gIds = new Dictionary<int, string>();
        var sIds = new Dictionary<int, string>();
        var verse = new AgentVerseDto { V = v, Ref = draft.Ref };
        var map = new AgentVerseMap { Ref = draft.Ref, Chapter = draft.Chapter, Verse = draft.Verse, Hash = draft.Hash };

        foreach (var token in draft.German)
        {
            var id = $"g{g++}";
            gIds[token.Row.Id] = id;
            verse.De.Add(DeRow(id, token));
            map.German[id] = new AgentGermanMap
            {
                Id = token.Row.Id,
                Text = token.Row.Plain,
                Lemma = token.Lemma ?? token.Row.Lemma,
                Pos = token.Row.PartOfSpeech,
                Stts = token.Row.Stts,
                Status = token.Status,
                LemmaState = token.LemmaState,
            };
        }

        foreach (var word in draft.Source)
        {
            var id = $"s{s++}";
            sIds[word.Row.Id] = id;
            if (roles && word.Morphemes is { Count: > 0 } morphemes)
            {
                // Hebrew lemma mode: the morphemes a link can render, the main one in braces (H9001, {H0559}).
                verse.Src.Add([id, word.Row.Text, word.Row.Lemma, word.Row.Strongs, word.Row.Gloss, word.Row.Morphology, morphemes.Select(m => m.IsMain ? $"{{{m.Strongs}}}" : m.Strongs).ToArray()]);
            }
            else
            {
                verse.Src.Add([id, word.Row.Text, word.Row.Lemma, word.Row.Strongs, word.Row.Gloss, word.Row.Morphology]);
            }

            map.Source[id] = new AgentSourceMap { Id = word.Row.Id, Strongs = word.Row.Strongs, Exempt = word.Exempt, Morphemes = roles ? word.Morphemes?.ToList() : null };
        }

        if (lemmaMode)
        {
            MaterializeLemmaLinks(draft, verse, map, gIds, sIds);
            return (verse, map, g, s);
        }

        foreach (var link in draft.Links)
        {
            var linkMap = new AgentLinkMap
            {
                G = gIds[link.GermanId],
                S = link.SourceId is { } sid ? sIds[sid] : null,
                Parent = link.ParentId is { } pid ? gIds[pid] : null,
                PartOrder = link.PartOrder,
                Part = link.Part,
            };
            (link.Fixed ? map.Fixed : map.Hints).Add(linkMap);

            if (!roles || linkMap.S is null)
            {
                (link.Fixed ? verse.Fixed : verse.Hints).Add([linkMap.G, linkMap.S ?? linkMap.Parent!]);
                continue;
            }

            // Prompt v2 (0018.9): [german, source, role, morpheme?] — the morpheme only for Hebrew links that name one.
            linkMap.Role = link.Role ?? MappingRoles.Head;
            linkMap.Strongs = link.Strongs;
            linkMap.PartLemma = link.PartLemma;
            linkMap.MultiHead = link.MultiHead;
            (link.Fixed ? verse.Fixed : verse.Hints).Add(link.Strongs is null ? [linkMap.G, linkMap.S!, linkMap.Role] : [linkMap.G, linkMap.S!, linkMap.Role, link.Strongs]);
        }

        return (verse, map, g, s);
    }

    /// <summary>
    /// Lemma mode: every stored link goes to <see cref="AgentVerseMap.Fixed"/> with role and morpheme; the model sees real links in
    /// <see cref="AgentVerseDto.Links"/> and added words in <see cref="AgentVerseDto.Fixed"/>.
    /// </summary>
    private static void MaterializeLemmaLinks(VerseDraft draft, AgentVerseDto verse, AgentVerseMap map, Dictionary<int, string> gIds, Dictionary<int, string> sIds)
    {
        verse.Links = [];
        verse.Work = [.. draft.Work ?? []];

        foreach (var link in draft.Links)
        {
            var linkMap = new AgentLinkMap
            {
                G = gIds[link.GermanId],
                S = link.SourceId is { } sid ? sIds[sid] : null,
                Parent = link.ParentId is { } pid ? gIds[pid] : null,
                PartOrder = link.PartOrder,
                Part = link.Part,
                Role = link.SourceId is null ? null : link.Role,
                Strongs = link.Strongs,
                PartLemma = link.PartLemma,
                MultiHead = link.MultiHead,
                Decided = link.Decided,
                StoredRole = link.SourceId is null ? null : link.StoredRole,
                StoredStrongs = link.StoredStrongs,
                Open = link.Open,
            };
            map.Fixed.Add(linkMap);

            if (linkMap.S is null)
            {
                verse.Fixed.Add([linkMap.G, linkMap.Parent!]);
                continue;
            }

            var row = new List<object?> { linkMap.G, linkMap.S, linkMap.Role ?? "head", linkMap.Strongs, linkMap.Open ? "open" : null, linkMap.PartOrder, linkMap.Part, linkMap.PartLemma };
            while (row.Count > 3 && row[^1] is null)
            {
                row.RemoveAt(row.Count - 1);
            }

            verse.Links.Add([.. row]);
        }
    }

    /// <summary><c>[id, text, lemma, stts, lemmaState?, candidates?]</c> with trailing empty columns left out.</summary>
    private static object?[] DeRow(string id, DraftGerman token)
    {
        var state = token.LemmaState switch
        {
            AgentLemmaState.Fixed => "fixed-lemma",
            AgentLemmaState.Hint => "hint-lemma",
            _ => null,
        };

        var row = new List<object?> { id, token.Row.Plain, token.Lemma ?? token.Row.Lemma, token.Row.Stts };
        if (token.Candidates.Count > 0)
        {
            row.Add(state);
            row.Add(token.Candidates.ToArray());
        }
        else if (state is not null)
        {
            row.Add(state);
        }

        return [.. row];
    }
}
