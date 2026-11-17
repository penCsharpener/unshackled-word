using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.VerseMapping;

public sealed class VerseMappingService : IVerseMappingService
{
    /// <summary>Configuration key of the write gate; one extra server-wide switch on top of the Admin/Maintainer role check of the save endpoint.</summary>
    public const string EditingEnabledKey = "MappingEditing:Enabled";

    private const int LastHebrewBookId = 39;

    private readonly IVerseMappingRepository _repository;
    private readonly IConfiguration _configuration;

    public VerseMappingService(IVerseMappingRepository repository, IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    public async Task<GetVerseMappingResponse?> GetAsync(int bibleBookId, int chapter, int verse, bool callerCanWrite, CancellationToken token = default)
    {
        var germanWords = await _repository.GetGermanWordsAsync(bibleBookId, chapter, verse, token);
        return germanWords.Count == 0 ? null : await BuildAsync(bibleBookId, chapter, verse, germanWords, callerCanWrite, token);
    }

    public async Task<SaveVerseMappingResult> SaveAsync(int bibleBookId, int chapter, int verse, IReadOnlyList<VerseMappingLink> links, int userId, bool clearParts = false, CancellationToken token = default)
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

        var roleAware = MappingRoles.IsRoleAware(links);
        if (!roleAware)
        {
            links = await KeepStoredRolesAsync(hebrew, bibleBookId, chapter, verse, links, germanWords, originalWords, token);
        }

        var input = BuildInput(links, germanWords, originalWords, hebrew, enforceSingleHead: roleAware);
        var validation = await new SaveVerseMappingValidator().ValidateAsync(input, token);

        if (!validation.IsValid)
        {
            return new SaveVerseMappingResult
            {
                Outcome = SaveVerseMappingOutcome.Invalid,
                Errors = validation.Errors.Select(e => e.ErrorMessage).ToList(),
            };
        }

        if (!clearParts)
        {
            var dropped = await FindDroppedPartsAsync(hebrew, bibleBookId, chapter, verse, germanWords, links, token);
            if (dropped.Count > 0)
            {
                return new SaveVerseMappingResult
                {
                    Outcome = SaveVerseMappingOutcome.Invalid,
                    Errors = dropped,
                };
            }
        }

        var multiHead = AcceptedMultiHeads(links, input);
        var rows = links
            .Select(l => new VerseMappingRow
            {
                ElbWordId = l.ElbWordId,
                StepWordId = l.IsAddedWord ? null : l.StepWordId,
                IsAddedWord = l.IsAddedWord,
                ParentElbWordId = l.IsAddedWord ? l.ParentElbWordId : null,
                GermanWordPart = HasPart(l) ? l.GermanWordPart!.Trim() : null,
                PartOrder = HasPart(l) ? l.PartOrder : null,
                Role = !l.IsAddedWord && MappingRoles.IsUnit(l) ? MappingRoles.Unit : MappingRoles.Head,
                // Hebrew only, in the spelling of the word's morpheme list; NULL = main morpheme. Greek takes the STEP word's Strong's on insert.
                StrongsNumber = hebrew && !l.IsAddedWord && !string.IsNullOrWhiteSpace(l.StrongsNumber) ? input.MorphemeKey(l).Morpheme : null,
                PartLemma = HasPart(l) && !string.IsNullOrWhiteSpace(l.PartLemma) ? l.PartLemma.Trim() : null,
                MultiHead = !l.IsAddedWord && !MappingRoles.IsUnit(l) && multiHead.Contains(input.MorphemeKey(l)),
                CreatedByUserId = userId,
            })
            .ToList();

        await _repository.ReplaceMappingAsync(hebrew, bibleBookId, chapter, verse, rows, token);

        return new SaveVerseMappingResult
        {
            Outcome = SaveVerseMappingOutcome.Saved,
            // The endpoint only lets write roles in, so the saved verse is editable for this caller.
            Mapping = await BuildAsync(bibleBookId, chapter, verse, germanWords, callerCanWrite: true, token),
        };
    }

    private static bool HasPart(VerseMappingLink link) => link is { PartOrder: not null } && !string.IsNullOrWhiteSpace(link.GermanWordPart);

    /// <summary>The validator input of a save; shared by the Elb1871 and the revision save path.</summary>
    public static VerseMappingSaveInput BuildInput(IReadOnlyList<VerseMappingLink> links, IReadOnlyList<VerseGermanWordRow> germanWords, IReadOnlyList<VerseOriginalWordRow> originalWords, bool hebrew, bool enforceSingleHead, bool partsSupported = true)
    {
        return new VerseMappingSaveInput
        {
            Links = links,
            GermanWordIds = germanWords.Select(w => w.Id).ToHashSet(),
            OriginalWordIds = originalWords.Select(w => w.Id).ToHashSet(),
            GermanWordTexts = partsSupported ? germanWords.ToDictionary(w => w.Id, w => w.Text) : null,
            HebrewMorphemes = hebrew ? originalWords.ToDictionary(w => w.Id, w => HebrewMorphemes.Parse(w.MorphemeTags)) : null,
            OriginalWordStrongs = hebrew ? null : originalWords.ToDictionary(w => w.Id, w => w.Strongs),
            GermanWordStts = germanWords.ToDictionary(w => w.Id, w => w.Stts),
            EnforceSingleHead = enforceSingleHead,
        };
    }

    /// <summary>Source morphemes with several heads of which one sets allowMultiHead; every head row of them is stored with MultiHead.</summary>
    public static HashSet<(int, string)> AcceptedMultiHeads(IReadOnlyList<VerseMappingLink> links, VerseMappingSaveInput input)
    {
        return links
            .Where(l => !l.IsAddedWord && l.StepWordId is not null && !MappingRoles.IsUnit(l))
            .GroupBy(input.MorphemeKey)
            .Where(g => g.Count() > 1 && g.Any(l => l.AllowMultiHead == true))
            .Select(g => g.Key)
            .ToHashSet();
    }

    /// <summary>
    /// Legacy save (no role fields, a client from before roadmap 0018, or a merged agent proposal): every link keeps the role, morpheme,
    /// multi-head flag and part lemma of the stored row with the same (elbWordId, stepWordId), so the save does not reset them to defaults.
    /// A kept unit whose morpheme lost its head link becomes head again.
    /// </summary>
    private async Task<IReadOnlyList<VerseMappingLink>> KeepStoredRolesAsync(bool hebrew, int bibleBookId, int chapter, int verse, IReadOnlyList<VerseMappingLink> links, IReadOnlyList<VerseGermanWordRow> germanWords, IReadOnlyList<VerseOriginalWordRow> originalWords, CancellationToken token)
    {
        var stored = await _repository.GetMappingRowsAsync(hebrew, bibleBookId, chapter, verse, token);
        return KeepStoredRoles(links, stored.Select(r => (r.ElbWordId, r.StepWordId, r.IsAddedWord, r.Role, r.StrongsNumber, r.MultiHead, r.GermanWordPart, r.PartLemma)), germanWords, originalWords, hebrew);
    }

    /// <summary>Pure part of <see cref="KeepStoredRolesAsync"/>; the revision save path uses it with its own rows.</summary>
    public static IReadOnlyList<VerseMappingLink> KeepStoredRoles(IReadOnlyList<VerseMappingLink> links, IEnumerable<(int ElbWordId, int? StepWordId, bool IsAddedWord, string Role, string? StrongsNumber, bool MultiHead, string? GermanWordPart, string? PartLemma)> stored, IReadOnlyList<VerseGermanWordRow> germanWords, IReadOnlyList<VerseOriginalWordRow> originalWords, bool hebrew)
    {
        var byKey = stored
            .Where(r => !r.IsAddedWord && r.StepWordId is not null)
            .GroupBy(r => (r.ElbWordId, r.StepWordId))
            .ToDictionary(g => g.Key, g => g.First());

        var merged = links
            .Select(l =>
            {
                if (l.IsAddedWord || l.StepWordId is null || !byKey.TryGetValue((l.ElbWordId, l.StepWordId), out var old))
                {
                    return l;
                }

                var samePart = HasPart(l) && !string.IsNullOrWhiteSpace(old.GermanWordPart)
                               && GermanWordParts.Normalize(old.GermanWordPart) == GermanWordParts.Normalize(l.GermanWordPart);
                return new VerseMappingLink
                {
                    ElbWordId = l.ElbWordId,
                    StepWordId = l.StepWordId,
                    IsAddedWord = false,
                    ParentElbWordId = l.ParentElbWordId,
                    PartOrder = l.PartOrder,
                    GermanWordPart = l.GermanWordPart,
                    Role = old.Role == MappingRoles.Unit ? MappingRoles.Unit : null,
                    StrongsNumber = hebrew ? old.StrongsNumber : null,
                    AllowMultiHead = old.MultiHead ? true : null,
                    PartLemma = samePart ? old.PartLemma : null,
                };
            })
            .ToList();

        // A unit only stays one while its morpheme still has a head (or it is the article part of a contraction).
        var input = BuildInput(merged, germanWords, originalWords, hebrew, enforceSingleHead: false);
        var withHead = merged.Where(l => !l.IsAddedWord && l.StepWordId is not null && !MappingRoles.IsUnit(l)).Select(input.MorphemeKey).ToHashSet();
        foreach (var link in merged.Where(l => MappingRoles.IsUnit(l) && !l.IsAddedWord && l.StepWordId is not null))
        {
            var key = input.MorphemeKey(link);
            if (!withHead.Contains(key) && !input.IsContractionArticle(link, key))
            {
                link.Role = null;
            }
        }

        return merged;
    }

    /// <summary>
    /// One message per German word that has compound parts in the database but none in the new links. A client that does not know parts
    /// (an old browser tab) would silently delete them, so the caller has to say so with <c>clearParts</c>.
    /// </summary>
    private async Task<List<string>> FindDroppedPartsAsync(bool hebrew, int bibleBookId, int chapter, int verse, IReadOnlyList<VerseGermanWordRow> germanWords, IReadOnlyList<VerseMappingLink> links, CancellationToken token)
    {
        var stored = await _repository.GetMappingRowsAsync(hebrew, bibleBookId, chapter, verse, token);
        var wordsWithParts = links.Where(HasPart).Select(l => l.ElbWordId).ToHashSet();
        var texts = germanWords.ToDictionary(w => w.Id, w => w.Text);

        return stored
            .Where(r => !string.IsNullOrWhiteSpace(r.GermanWordPart) && texts.ContainsKey(r.ElbWordId) && !wordsWithParts.Contains(r.ElbWordId))
            .GroupBy(r => r.ElbWordId)
            .OrderBy(g => g.Key)
            .Select(g => $"Word {g.Key} ({texts[g.Key]}) has compound parts ({string.Join(" + ", g.Select(r => r.GermanWordPart).Distinct())}) that this save would remove. Send the parts again or set clearParts to true.")
            .ToList();
    }

    private async Task<GetVerseMappingResponse> BuildAsync(int bibleBookId, int chapter, int verse, IReadOnlyList<VerseGermanWordRow> germanWords, bool callerCanWrite, CancellationToken token)
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
            EditingEnabled = callerCanWrite && IsEditingEnabled(),
            GermanWords = germanWords
                .Select(w => new VerseMappingGermanWord { Id = w.Id, PositionInVerse = w.PositionInVerse, Text = w.Text })
                .ToList(),
            OriginalWords = originalWords.Select(w => w.ToResponse(withMorphemes: hebrew)).ToList(),
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

                link = new VerseMappingLink
                {
                    ElbWordId = row.ElbWordId,
                    StepWordId = stepId,
                    StrongsNumber = string.IsNullOrWhiteSpace(row.StrongsNumber) ? null : row.StrongsNumber,
                    AllowMultiHead = row.MultiHead ? true : null,
                };
                if (!string.IsNullOrWhiteSpace(row.GermanWordPart))
                {
                    link.GermanWordPart = row.GermanWordPart.Trim();
                    link.PartOrder = row.PartOrder is >= 1 ? row.PartOrder : 0; // 0 = derive below
                    link.PartLemma = string.IsNullOrWhiteSpace(row.PartLemma) ? null : row.PartLemma;
                }
            }

            link.Role = row.Role == MappingRoles.Unit && !row.IsAddedWord ? MappingRoles.Unit : MappingRoles.Head;

            if (seen.Add((link.ElbWordId, link.StepWordId)))
            {
                links.Add(link);
            }
        }

        DeriveMissingPartOrders(links, germanWords);

        return links
            .OrderBy(l => germanOrder[l.ElbWordId])
            .ThenBy(l => l.StepWordId is { } id ? originalOrder[id] : -1)
            .ToList();
    }

    /// <summary>Parts stored without an order (older imports) get the order of their position inside the word, so a client can send them back.</summary>
    private static void DeriveMissingPartOrders(List<VerseMappingLink> links, IReadOnlyList<VerseGermanWordRow> germanWords)
    {
        var texts = germanWords.ToDictionary(w => w.Id, w => w.Text);

        foreach (var word in links.Where(l => l.GermanWordPart is not null).GroupBy(l => l.ElbWordId).Where(g => g.Any(l => l.PartOrder == 0)))
        {
            var members = word.ToList();
            var orders = GermanWordParts.DeriveOrders(texts[word.Key], members.Select(l => l.GermanWordPart!).ToList());

            for (var i = 0; i < members.Count; i++)
            {
                members[i].PartOrder = orders[i];
            }
        }
    }

    private bool IsEditingEnabled()
    {
        return bool.TryParse(_configuration[EditingEnabledKey], out var enabled) && enabled;
    }

    private static bool IsHebrew(int bibleBookId)
    {
        return bibleBookId <= LastHebrewBookId;
    }
}
