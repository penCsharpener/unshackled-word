using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.Lxx.Review;

/// <summary>
/// The LXX review page (roadmap 0028.7f, 05-tagging.md "Review and confirm"). Two proposal sources: <c>proposed</c> rows of <c>LxxWordTags</c>
/// (pass 1, <c>nt-form</c>) and open AI proposals (<c>AgentVerseProposals</c>, language <c>lxx</c>, one per unit). Confirming writes
/// <c>LxxWordTags</c> (<c>confirmed</c>), replaces the words' <c>LxxHebrewLinks</c> (<c>confirmed</c>), appends one <c>LxxTagAudit</c> row per unit and
/// closes the AI proposal, all in one transaction (<see cref="ILxxReviewRepository.ApplyAsync{T}"/>). Validation uses the rules of the AI validator:
/// the Strong's is in TBESG and the lemma is one of its TBESG lemmas (the stored lemma is TBESG's), the code passes <see cref="GreekMorphologyCode"/>,
/// and every Hebrew word belongs to the unit's Hebrew verse.
/// <para>
/// Source rule of a confirmed tag: <c>ai</c> when it equals the open AI proposal's answer for the word; else the current tag's source when it equals the
/// current tag (<c>nt-form</c> for an accepted prefill); else <c>human</c>. "Equal" = same Strong's, same code and the same TBESG lemma.
/// </para>
/// </summary>
public sealed partial class LxxReviewService
{
    public const string Confirmed = "confirmed";
    public const string Proposed = "proposed";
    public const string SourceAi = "ai";
    public const string SourceHuman = "human";
    public const string High = "high";
    public const string ActionConfirm = "confirm";
    public const string ActionEdit = "edit";
    public const string ActionReject = "reject";
    public const string Merged = "merged";
    public const string EditedMerged = "edited-merged";
    public const string Discarded = "discarded";
    public const int DefaultSearchLimit = 20;
    public const int MaxSearchLimit = 50;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly ILxxReviewRepository _repository;
    private readonly ILxxLexiconSource _lexicon;

    public LxxReviewService(ILxxReviewRepository repository, ILxxLexiconSource lexicon)
    {
        _repository = repository;
        _lexicon = lexicon;
    }

    // ---- reads -------------------------------------------------------------------------------------------------------------------------

    public async Task<LxxReviewBooksResponse> GetBooksAsync(CancellationToken token = default)
    {
        var rows = await _repository.GetChapterCountsAsync(token);
        return BuildBooks(rows);
    }

    public static LxxReviewBooksResponse BuildBooks(IEnumerable<LxxReviewChapterCountRow> rows) => new()
    {
        Books = rows
            .GroupBy(r => r.BibleBookId)
            .OrderBy(g => g.Key)
            .Select(g => new LxxReviewBookResponse
            {
                BibleBookId = g.Key,
                OpenUnits = g.Sum(r => r.OpenUnits),
                AiUnits = g.Sum(r => r.AiUnits),
                LowWords = g.Sum(r => r.LowWords),
                Chapters = g.OrderBy(r => r.Chapter)
                    .Select(r => new LxxReviewChapterCountResponse { Chapter = r.Chapter, OpenUnits = r.OpenUnits, AiUnits = r.AiUnits, LowWords = r.LowWords })
                    .ToList(),
            })
            .ToList(),
    };

    public async Task<LxxReviewChapterResponse> GetChapterAsync(int bibleBookId, int chapter, CancellationToken token = default)
    {
        var state = await _repository.GetChapterAsync(bibleBookId, chapter, token);
        var lexicon = await _lexicon.GetAsync(token);
        return BuildChapter(bibleBookId, chapter, state, lexicon);
    }

    /// <summary>Pure (public for tests): the page's view of the units.</summary>
    public static LxxReviewChapterResponse BuildChapter(int bibleBookId, int chapter, LxxReviewState state, LxxLexicon lexicon)
    {
        var response = new LxxReviewChapterResponse { BibleBookId = bibleBookId, Chapter = chapter };
        foreach (var view in Views(state).Values.OrderBy(v => v.Unit.SortOrder))
        {
            var unit = view.Unit;
            var proposal = view.Proposal;
            var aiWords = proposal?.Words.ToDictionary(w => w.LxxWordId) ?? [];
            var dto = new LxxReviewUnitResponse
            {
                LxxVerseId = unit.Id,
                Chapter = unit.Chapter,
                Verse = unit.Verse,
                Subverse = unit.Subverse,
                Ref = LxxChapterReadService.FormatRef(unit.BibleBookId, unit.Chapter, unit.Verse, unit.Subverse),
                TextForm = unit.TextForm,
                MapKind = unit.MapKind,
                IsAddition = unit.MapKind == "addition",
                HebRefId = unit.HebRefId,
                Open = proposal is not null || view.Words.Any(w => w.TagStatus == Proposed),
                HasConfirmed = view.Words.Any(w => w.TagStatus == Confirmed),
                Hebrew = view.Hebrew.Select((h, i) => new LxxReviewHebrewWordResponse
                {
                    Id = h.Id,
                    Number = i + 1,
                    Text = h.Text,
                    Strongs = h.Strongs,
                    Gloss = h.Gloss,
                    Morphology = h.Morphology,
                    MorphologyDetail = MorphologyResponses.Decode(true, h.Morphology),
                    German = state.HebrewGerman.GetValueOrDefault(h.Id),
                    GermanWordIds = state.HebrewGermanIds.TryGetValue(h.Id, out var ids) && ids.Count > 0 ? [.. ids] : null,
                    Note = state.HebrewNotes.GetValueOrDefault(h.Id),
                }).ToList(),
                German = unit.HebRefId is { } refId
                    ? [.. state.GermanWords.Where(g => g.HebRefId == refId).Select(g => new LxxReviewGermanWordResponse { Id = g.Id, Text = g.Text })]
                    : [],
            };

            foreach (var word in view.Words)
            {
                var links = view.LinksOf(word.Id);
                var current = TagOf(word);
                dto.Words.Add(new LxxReviewWordResponse
                {
                    Id = word.Id,
                    Position = word.Position,
                    Greek = word.Greek,
                    Tag = current is null
                        ? null
                        : new LxxReviewTagResponse
                        {
                            Lemma = TbesgLemma(lexicon, current),
                            Strongs = current.Strongs,
                            Morphology = current.Morphology,
                            Status = word.TagStatus ?? string.Empty,
                            Source = word.TagSource ?? string.Empty,
                        },
                    HebrewWordIds = links.Select(l => l.StepHebrewWordId).Order().ToList(),
                    LinksConfirmed = links.Count > 0 && links.All(l => l.Status == Confirmed),
                    Ai = aiWords.TryGetValue(word.Id, out var ai)
                        ? new LxxReviewAiWordResponse
                        {
                            Lemma = ai.Lemma,
                            Strongs = ai.Strongs,
                            Morphology = ai.Morph,
                            Confidence = ai.Confidence,
                            Prefilled = ai.Prefilled,
                            HebrewWordIds = proposal!.LinksOf(word.Id),
                        }
                        : null,
                });
            }

            if (proposal is not null)
            {
                dto.Proposal = new LxxReviewProposalResponse
                {
                    Id = proposal.Row.Id,
                    RunId = proposal.Row.RunId,
                    CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(proposal.Row.CreatedAt, DateTimeKind.Utc)),
                    Note = proposal.Row.Note,
                    Flags = proposal.Flags.Select(f => new LxxReviewFlagResponse { Code = f.Code, Message = f.Message, Position = f.Position }).ToList(),
                };
            }

            response.Units.Add(dto);
        }

        return response;
    }

    public async Task<SearchLxxLexiconResponse> SearchLexiconAsync(string? q, int? limit, CancellationToken token = default)
    {
        var lexicon = await _lexicon.GetAsync(token);
        return new SearchLxxLexiconResponse
        {
            Entries = SearchLexicon(lexicon, q, Math.Clamp(limit ?? DefaultSearchLimit, 1, MaxSearchLimit))
                .Select(e => new LxxLexiconEntryResponse { Strongs = e.Strongs, Lemma = e.Lemma, Gloss = e.Gloss })
                .ToList(),
        };
    }

    /// <summary>
    /// A Strong's query (<c>G746</c>, <c>746</c>, <c>G1487M</c>) gives that entry first and then its disambiguated siblings (<c>G1487</c> → <c>G1487G</c>,
    /// <c>G1487M</c>); anything else is a lemma prefix compared without accents and breathings, exact lemmas first, then shorter ones.
    /// </summary>
    public static IReadOnlyList<LxxLexiconEntry> SearchLexicon(LxxLexicon lexicon, string? q, int limit)
    {
        var text = q?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return [];
        }

        if (StrongsQueryRegex().IsMatch(text))
        {
            var normalized = LxxLexicon.NormalizeStrongs(char.IsDigit(text[0]) ? "G" + text : text);
            if (normalized is null)
            {
                return [];
            }

            var bare = (char.IsLetter(normalized[^1]) ? normalized[..^1] : normalized);
            bare = bare.Length == 0 ? normalized : bare;
            return lexicon.All
                .Where(e => e.Strongs == normalized || (e.Strongs.StartsWith(bare, StringComparison.Ordinal) && e.Strongs.Length <= bare.Length + 1))
                .OrderBy(e => e.Strongs == normalized ? 0 : 1)
                .ThenBy(e => e.Strongs, StringComparer.Ordinal)
                .Take(limit)
                .ToList();
        }

        var key = LxxLexicon.Bare(text);
        if (key.Length == 0)
        {
            return [];
        }

        return lexicon.All
            .Select(e => (Entry: e, Bare: LxxLexicon.Bare(e.Lemma)))
            .Where(x => x.Bare.StartsWith(key, StringComparison.Ordinal))
            .OrderBy(x => x.Bare == key ? 0 : 1)
            .ThenBy(x => x.Bare.Length)
            .ThenBy(x => x.Entry.Strongs, StringComparer.Ordinal)
            .Select(x => x.Entry)
            .Take(limit)
            .ToList();
    }

    /// <summary>The live check of the parse field: what confirm would store and why a code is refused.</summary>
    public static CheckLxxMorphologyResponse CheckMorphology(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (GreekMorphologyCode.TryParse(normalized, out var parse, out var error))
        {
            return new CheckLxxMorphologyResponse { Code = normalized, Valid = true, German = parse.German, English = parse.English };
        }

        return new CheckLxxMorphologyResponse { Code = normalized, Valid = false, Error = error };
    }

    // ---- writes ------------------------------------------------------------------------------------------------------------------------

    public async Task<AgentReviewResult<ConfirmLxxUnitResponse>> ConfirmAsync(ConfirmLxxUnitRequest request, int userId, CancellationToken token = default)
    {
        var lexicon = await _lexicon.GetAsync(token);
        var applied = await _repository.ApplyAsync([request.LxxVerseId], state => PlanConfirm(request, state, lexicon), userId, token);
        return applied.Conflict ? AgentReviewResult<ConfirmLxxUnitResponse>.Fail(AgentReviewOutcome.Stale, StaleMessage) : applied.Result;
    }

    public async Task<AgentReviewResult<DiscardLxxUnitResponse>> DiscardAsync(DiscardLxxUnitRequest request, int userId, CancellationToken token = default)
    {
        var applied = await _repository.ApplyAsync([request.LxxVerseId], state => PlanDiscard(request, state), userId, token);
        return applied.Conflict ? AgentReviewResult<DiscardLxxUnitResponse>.Fail(AgentReviewOutcome.NotOpen, StaleMessage) : applied.Result;
    }

    public async Task<AgentReviewResult<ConfirmLxxSafeResponse>> ConfirmSafeAsync(ConfirmLxxSafeRequest request, int userId, CancellationToken token = default)
    {
        var lexicon = await _lexicon.GetAsync(token);
        var ids = request.Units.Select(u => u.LxxVerseId).Distinct().ToList();
        var applied = await _repository.ApplyAsync(ids, state => PlanSafe(request, state, lexicon), userId, token);
        return applied.Conflict ? AgentReviewResult<ConfirmLxxSafeResponse>.Fail(AgentReviewOutcome.Stale, StaleMessage) : applied.Result;
    }

    private const string StaleMessage = "The proposals of this verse changed meanwhile; reload the chapter.";

    /// <summary>Pure (public for tests): validation, source rule, links, audit row and proposal status of a unit confirm.</summary>
    public static LxxReviewPlan<AgentReviewResult<ConfirmLxxUnitResponse>> PlanConfirm(ConfirmLxxUnitRequest request, LxxReviewState state, LxxLexicon lexicon)
    {
        static LxxReviewPlan<AgentReviewResult<ConfirmLxxUnitResponse>> Fail(AgentReviewOutcome outcome, params string[] errors) =>
            new(AgentReviewResult<ConfirmLxxUnitResponse>.Fail(outcome, errors), null);

        if (!Views(state).TryGetValue(request.LxxVerseId, out var view))
        {
            return Fail(AgentReviewOutcome.NotFound, $"LXX verse {request.LxxVerseId} does not exist.");
        }

        var proposal = view.Proposal;
        if (proposal?.Row.Id != request.ProposalId)
        {
            return Fail(AgentReviewOutcome.Stale, proposal is null
                ? "The AI proposal of this verse is no longer open; reload the chapter."
                : "Another AI proposal is open for this verse; reload the chapter.");
        }

        var words = view.Words.ToDictionary(w => w.Id);
        var hebrewIds = view.Hebrew.Select(h => h.Id).ToHashSet();
        var errors = new List<string>();
        var finals = new List<(LxxAgentWordRow Word, Tag? Tag, List<int> Hebrew)>();
        foreach (var input in request.Words)
        {
            if (!words.TryGetValue(input.LxxWordId, out var word))
            {
                errors.Add($"Word {input.LxxWordId} is not a word of this verse.");
                continue;
            }

            var at = $"#{word.Position} {word.Greek}";
            var before = errors.Count;
            var tag = ValidateTag(input.Lemma, input.Strongs, input.Morphology, lexicon, at, errors);
            var hebrew = input.HebrewWordIds.Distinct().Order().ToList();
            var unknown = hebrew.Where(h => !hebrewIds.Contains(h)).ToList();
            if (unknown.Count > 0)
            {
                errors.Add(view.Hebrew.Count == 0
                    ? $"{at}: the verse has no Hebrew words to link."
                    : $"{at}: Hebrew word {string.Join(", ", unknown)} is not in the verse's Hebrew text.");
            }

            if (errors.Count == before)
            {
                finals.Add((word, tag, hebrew));
            }
        }

        if (errors.Count > 0)
        {
            return Fail(AgentReviewOutcome.Invalid, [.. errors]);
        }

        var write = new LxxReviewWrite();
        var edited = false;
        var aiWords = proposal?.Words.ToDictionary(w => w.LxxWordId) ?? [];
        foreach (var (word, tag, hebrew) in finals)
        {
            var current = TagOf(word);
            if (tag is not null)
            {
                aiWords.TryGetValue(word.Id, out var ai);
                var source = SourceOf(tag, ai is null ? null : TagOf(ai), current, word.TagSource, lexicon);
                var unchanged = word.TagStatus == Confirmed && current == tag && word.TagSource == source;
                if (!unchanged)
                {
                    write.Tags.Add(new LxxTagWrite(word.Id, tag.Lemma, tag.Strongs, tag.Morphology, source));
                    // Rewriting a confirmed tag in TBESG spelling or with another source is no edit; another Strong's, code or lemma is.
                    edited |= word.TagStatus == Confirmed && current is not null && !Same(current, tag, lexicon);
                }
            }

            var links = view.LinksOf(word.Id);
            var stored = links.Select(l => l.StepHebrewWordId).Order().ToList();
            var linksConfirmed = links.All(l => l.Status == Confirmed);
            if (!stored.SequenceEqual(hebrew) || !linksConfirmed)
            {
                write.LinkWordIds.Add(word.Id);
                write.Links.AddRange(hebrew.Select(h => (word.Id, h)));
                edited |= stored.Count > 0 && linksConfirmed && !stored.SequenceEqual(hebrew);
            }
        }

        string? proposalStatus = null;
        if (proposal is not null)
        {
            proposalStatus = EqualsProposal(finals, proposal, lexicon) ? Merged : EditedMerged;
            write.ProposalCloses.Add(new LxxProposalClose(proposal.Row.Id, proposalStatus));
        }

        var response = new ConfirmLxxUnitResponse
        {
            LxxVerseId = request.LxxVerseId,
            ConfirmedWords = finals.Count(f => f.Tag is not null),
            ProposalStatus = proposalStatus,
        };
        if (write.IsEmpty)
        {
            return new(AgentReviewResult<ConfirmLxxUnitResponse>.Ok(response), null);
        }

        response.Action = edited ? ActionEdit : ActionConfirm;
        write.Audits.Add(new LxxAuditWrite(view.Unit.Id, response.Action, Snapshot(view, null), Snapshot(view, write, proposal?.Row.Id, proposalStatus)));
        return new(AgentReviewResult<ConfirmLxxUnitResponse>.Ok(response), write);
    }

    /// <summary>Pure (public for tests): the AI proposal becomes <c>discarded</c>; the nt-form proposals stay (pass 1 would write them again anyway).</summary>
    public static LxxReviewPlan<AgentReviewResult<DiscardLxxUnitResponse>> PlanDiscard(DiscardLxxUnitRequest request, LxxReviewState state)
    {
        if (!Views(state).TryGetValue(request.LxxVerseId, out var view))
        {
            return new(AgentReviewResult<DiscardLxxUnitResponse>.Fail(AgentReviewOutcome.NotFound, $"LXX verse {request.LxxVerseId} does not exist."), null);
        }

        var proposal = view.Proposal;
        if (proposal is null || proposal.Row.Id != request.ProposalId)
        {
            return new(AgentReviewResult<DiscardLxxUnitResponse>.Fail(AgentReviewOutcome.NotOpen, "This AI proposal is not open (any more); reload the chapter."), null);
        }

        var write = new LxxReviewWrite();
        write.ProposalCloses.Add(new LxxProposalClose(proposal.Row.Id, Discarded));
        var positions = view.Words.ToDictionary(w => w.Id, w => w.Position);
        var discarded = proposal.Words
            .OrderBy(w => w.Position)
            .Select(w => new AuditWord(positions.GetValueOrDefault(w.LxxWordId, w.Position), w.Lemma, w.Strongs, w.Morph, Proposed, SourceAi, proposal.LinksOf(w.LxxWordId)))
            .ToList();
        var after = JsonSerializer.Serialize(new AuditState(SnapshotWords(view, null), proposal.Row.Id, Discarded, discarded), Json);
        write.Audits.Add(new LxxAuditWrite(view.Unit.Id, ActionReject, Snapshot(view, null), after));

        return new(AgentReviewResult<DiscardLxxUnitResponse>.Ok(new DiscardLxxUnitResponse { LxxVerseId = view.Unit.Id, ProposalId = proposal.Row.Id }), write);
    }

    /// <summary>
    /// Pure (public for tests): "Kapitel bestätigen (sichere)". Confirms exactly the listed words that the unit's open AI proposal (the one the page
    /// showed) answers with confidence <c>high</c> and that are not confirmed yet, with the AI's tag and links (source <c>ai</c>). <c>low</c> words, words
    /// without an AI answer (nt-form prefills alone are not "safe") and units of another chapter or with another proposal are skipped. A proposal is
    /// closed as <c>merged</c> only when every word of it is now confirmed as proposed; otherwise it stays open for the rest.
    /// </summary>
    public static LxxReviewPlan<AgentReviewResult<ConfirmLxxSafeResponse>> PlanSafe(ConfirmLxxSafeRequest request, LxxReviewState state, LxxLexicon lexicon)
    {
        var views = Views(state);
        var response = new ConfirmLxxSafeResponse();
        var write = new LxxReviewWrite();
        foreach (var input in request.Units.DistinctBy(u => u.LxxVerseId))
        {
            if (!views.TryGetValue(input.LxxVerseId, out var view) || view.Unit.BibleBookId != request.BibleBookId || view.Unit.Chapter != request.Chapter
                || view.Proposal is not { } proposal || proposal.Row.Id != input.ProposalId)
            {
                response.StaleUnits.Add(input.LxxVerseId);
                continue;
            }

            var words = view.Words.ToDictionary(w => w.Id);
            var hebrewIds = view.Hebrew.Select(h => h.Id).ToHashSet();
            var aiWords = proposal.Words.ToDictionary(w => w.LxxWordId);
            var unit = new LxxReviewWrite();
            foreach (var wordId in input.LxxWordIds.Distinct())
            {
                if (!words.TryGetValue(wordId, out var word) || !aiWords.TryGetValue(wordId, out var ai))
                {
                    response.SkippedOther++;
                    continue;
                }

                if (ai.Confidence != High)
                {
                    response.SkippedLow++;
                    continue;
                }

                var tag = ValidateTag(ai.Lemma, ai.Strongs, ai.Morph, lexicon, $"#{word.Position}", []);
                var hebrew = proposal.LinksOf(wordId);
                if (word.TagStatus == Confirmed || tag is null || hebrew.Any(h => !hebrewIds.Contains(h)))
                {
                    response.SkippedOther++;
                    continue;
                }

                unit.Tags.Add(new LxxTagWrite(wordId, tag.Lemma, tag.Strongs, tag.Morphology, SourceAi));
                unit.LinkWordIds.Add(wordId);
                unit.Links.AddRange(hebrew.Select(h => (wordId, h)));
            }

            if (unit.Tags.Count == 0)
            {
                continue;
            }

            var confirmedNow = unit.Tags.Select(t => t.LxxWordId).ToHashSet();
            var complete = proposal.Words.All(ai => confirmedNow.Contains(ai.LxxWordId)
                || (words.TryGetValue(ai.LxxWordId, out var w) && w.TagStatus == Confirmed && TagOf(w) is { } current && Same(current, TagOf(ai), lexicon)));
            string? status = null;
            if (complete)
            {
                status = Merged;
                unit.ProposalCloses.Add(new LxxProposalClose(proposal.Row.Id, Merged));
                response.MergedProposals++;
            }

            unit.Audits.Add(new LxxAuditWrite(view.Unit.Id, ActionConfirm, Snapshot(view, null), Snapshot(view, unit, proposal.Row.Id, status)));
            write.Tags.AddRange(unit.Tags);
            write.LinkWordIds.AddRange(unit.LinkWordIds);
            write.Links.AddRange(unit.Links);
            write.Audits.AddRange(unit.Audits);
            write.ProposalCloses.AddRange(unit.ProposalCloses);
            response.Units++;
            response.ConfirmedWords += unit.Tags.Count;
        }

        return new(AgentReviewResult<ConfirmLxxSafeResponse>.Ok(response), write.IsEmpty ? null : write);
    }

    // ---- rules -------------------------------------------------------------------------------------------------------------------------

    /// <summary>A tag as stored: TBESG lemma, normalized Strong's, upper-case code.</summary>
    public sealed record Tag(string Lemma, string Strongs, string Morphology);

    /// <summary>Null when all three fields are empty (no tag) or when a rule fails (the reason is appended to <paramref name="errors"/>).</summary>
    public static Tag? ValidateTag(string? lemma, string? strongs, string? morphology, LxxLexicon lexicon, string at, List<string> errors)
    {
        var blank = new[] { lemma, strongs, morphology }.Count(string.IsNullOrWhiteSpace);
        if (blank == 3)
        {
            return null;
        }

        if (blank > 0)
        {
            errors.Add($"{at}: lemma, Strong's and parse are needed together.");
            return null;
        }

        if (LxxLexicon.NormalizeStrongs(strongs) is not { } normalized || !lexicon.Contains(normalized))
        {
            errors.Add($"{at}: Strong's '{strongs}' is not in TBESG.");
            return null;
        }

        if (lexicon.MatchLemma(normalized, lemma) is not { } tbesg)
        {
            var expected = string.Join(" / ", lexicon.Entries(normalized).Select(e => e.Lemma).Distinct());
            errors.Add($"{at}: lemma '{lemma}' is not the TBESG lemma of {normalized} ({expected}).");
            return null;
        }

        var code = morphology!.Trim().ToUpperInvariant();
        if (!GreekMorphologyCode.TryParse(code, out _, out var error))
        {
            errors.Add($"{at}: parse '{morphology}' is not a Robinson code: {error}");
            return null;
        }

        return new Tag(tbesg, normalized, code);
    }

    /// <summary>The source rule (see the class summary).</summary>
    public static string SourceOf(Tag final, Tag? ai, Tag? current, string? currentSource, LxxLexicon lexicon)
    {
        if (ai is not null && Same(final, ai, lexicon))
        {
            return SourceAi;
        }

        if (current is not null && !string.IsNullOrEmpty(currentSource) && Same(final, current, lexicon))
        {
            return currentSource;
        }

        return SourceHuman;
    }

    /// <summary>Same Strong's, same code, and the same lemma once both are written as TBESG does.</summary>
    public static bool Same(Tag a, Tag b, LxxLexicon lexicon) =>
        a.Strongs == b.Strongs && a.Morphology == b.Morphology && LxxLexicon.Key(TbesgLemma(lexicon, a)) == LxxLexicon.Key(TbesgLemma(lexicon, b));

    /// <summary>The lemma in TBESG spelling (STEP writes some differently, e.g. ἐγώ for G3165 μέ), as the AI package rewrites prefills.</summary>
    public static string TbesgLemma(LxxLexicon lexicon, Tag tag) =>
        lexicon.MatchLemma(tag.Strongs, tag.Lemma) ?? lexicon.First(tag.Strongs)?.Lemma ?? tag.Lemma;

    private static bool EqualsProposal(List<(LxxAgentWordRow Word, Tag? Tag, List<int> Hebrew)> finals, ParsedProposal proposal, LxxLexicon lexicon)
    {
        var byWord = finals.ToDictionary(f => f.Word.Id);
        foreach (var ai in proposal.Words)
        {
            if (!byWord.TryGetValue(ai.LxxWordId, out var final) || final.Tag is null || !Same(final.Tag, TagOf(ai), lexicon)
                || !final.Hebrew.SequenceEqual(proposal.LinksOf(ai.LxxWordId)))
            {
                return false;
            }
        }

        var aiIds = proposal.Words.Select(w => w.LxxWordId).ToHashSet();
        return finals.Where(f => !aiIds.Contains(f.Word.Id)).All(f => f.Tag is null && f.Hebrew.Count == 0);
    }

    private static Tag? TagOf(LxxAgentWordRow word) =>
        word.TagStrongs is { } strongs && word.TagMorphology is { } morph ? new Tag(word.TagLemma ?? string.Empty, strongs, morph) : null;

    private static Tag TagOf(LxxWordProposal ai) => new(ai.Lemma, ai.Strongs, ai.Morph);

    // ---- audit snapshots ---------------------------------------------------------------------------------------------------------------

    /// <summary>One word of an audit snapshot (04-model.md: <c>{words: [{position, lemma, strongs, morphology, status, source, hebrewWordIds}]}</c>).</summary>
    public sealed record AuditWord(int Position, string? Lemma, string? Strongs, string? Morphology, string? Status, string? Source, List<int> HebrewWordIds);

    public sealed record AuditState(List<AuditWord> Words, long? ProposalId = null, string? ProposalStatus = null, List<AuditWord>? Discarded = null);

    /// <summary>The unit's tagged or linked words, before (<paramref name="write"/> null) or after the write.</summary>
    private static string Snapshot(UnitView view, LxxReviewWrite? write, long? proposalId = null, string? proposalStatus = null) =>
        JsonSerializer.Serialize(new AuditState(SnapshotWords(view, write), proposalId, proposalStatus), Json);

    private static List<AuditWord> SnapshotWords(UnitView view, LxxReviewWrite? write)
    {
        var tags = write?.Tags.ToDictionary(t => t.LxxWordId) ?? [];
        var relinked = write?.LinkWordIds.ToHashSet() ?? [];
        var result = new List<AuditWord>();
        foreach (var word in view.Words)
        {
            var links = relinked.Contains(word.Id)
                ? write!.Links.Where(l => l.LxxWordId == word.Id).Select(l => l.StepHebrewWordId).Order().ToList()
                : view.LinksOf(word.Id).Select(l => l.StepHebrewWordId).Order().ToList();
            var entry = tags.TryGetValue(word.Id, out var t)
                ? new AuditWord(word.Position, t.Lemma, t.Strongs, t.Morphology, Confirmed, t.Source, links)
                : new AuditWord(word.Position, word.TagLemma, word.TagStrongs, word.TagMorphology, word.TagStatus, word.TagSource, links);
            if (entry.Strongs is not null || entry.HebrewWordIds.Count > 0)
            {
                result.Add(entry);
            }
        }

        return result;
    }

    // ---- state per unit ----------------------------------------------------------------------------------------------------------------

    private sealed class ParsedProposal
    {
        public required LxxReviewProposalRow Row { get; init; }
        public List<LxxWordProposal> Words { get; init; } = [];
        public List<LxxLinkProposal> Links { get; init; } = [];
        public List<LxxWordFlag> Flags { get; init; } = [];

        public List<int> LinksOf(int lxxWordId) => Links.Where(l => l.LxxWordId == lxxWordId).Select(l => l.StepHebrewWordId).Distinct().Order().ToList();
    }

    private sealed class UnitView
    {
        public required LxxAgentUnitRow Unit { get; init; }
        public required List<LxxAgentWordRow> Words { get; init; }
        public required List<AgentSourceWordRow> Hebrew { get; init; }
        public required ILookup<int, LxxReviewLinkRow> Links { get; init; }
        public ParsedProposal? Proposal { get; init; }

        public List<LxxReviewLinkRow> LinksOf(int lxxWordId) => Links[lxxWordId].ToList();
    }

    private static Dictionary<int, UnitView> Views(LxxReviewState state)
    {
        var words = state.Words.GroupBy(w => w.LxxVerseId).ToDictionary(g => g.Key, g => g.OrderBy(w => w.Position).ToList());
        var hebrew = state.HebrewWords.GroupBy(h => h.RefId).ToDictionary(g => g.Key, g => g.OrderBy(h => h.Position).ThenBy(h => h.Id).ToList());
        var links = state.Links.ToLookup(l => l.LxxWordId);
        var proposals = state.Proposals.GroupBy(p => p.LxxVerseId).ToDictionary(g => g.Key, g => Parse(g.OrderByDescending(p => p.Id).First()));
        return state.Units.ToDictionary(u => u.Id, u => new UnitView
        {
            Unit = u,
            Words = words.GetValueOrDefault(u.Id) ?? [],
            Hebrew = u.HebRefId is { } h ? hebrew.GetValueOrDefault(h) ?? [] : [],
            Links = links,
            Proposal = proposals.GetValueOrDefault(u.Id),
        });
    }

    private static ParsedProposal Parse(LxxReviewProposalRow row)
    {
        var flags = !string.IsNullOrWhiteSpace(row.FlagsJson) && row.FlagsJson.TrimStart().StartsWith('{')
            ? JsonSerializer.Deserialize<LxxProposalFlags>(row.FlagsJson, Json)?.Flags ?? []
            : [];
        return new ParsedProposal
        {
            Row = row,
            Words = string.IsNullOrWhiteSpace(row.LemmasJson) ? [] : JsonSerializer.Deserialize<List<LxxWordProposal>>(row.LemmasJson, Json) ?? [],
            Links = string.IsNullOrWhiteSpace(row.LinksJson) ? [] : JsonSerializer.Deserialize<List<LxxLinkProposal>>(row.LinksJson, Json) ?? [],
            Flags = flags,
        };
    }

    [GeneratedRegex("^[Gg]?[0-9]{1,6}[A-Za-z]?$")]
    private static partial Regex StrongsQueryRegex();
}
