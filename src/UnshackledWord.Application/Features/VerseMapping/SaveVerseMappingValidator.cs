using FluentValidation;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.VerseMapping;

/// <summary>A save request together with the ids that belong to the verse; checked by <see cref="SaveVerseMappingValidator"/>.</summary>
public sealed class VerseMappingSaveInput
{
    public required IReadOnlyList<VerseMappingLink> Links { get; init; }
    public required IReadOnlySet<int> GermanWordIds { get; init; }
    public required IReadOnlySet<int> OriginalWordIds { get; init; }

    /// <summary>
    /// Text of each German word of the verse by id, needed to check compound parts. Null where parts are not supported (revision variants):
    /// a link with a part is then rejected.
    /// </summary>
    public IReadOnlyDictionary<int, string>? GermanWordTexts { get; init; }

    /// <summary>
    /// Hebrew verses: the morphemes of each original word by id (<see cref="HebrewMorphemes.Parse"/>). Non-null marks the verse as Hebrew, and a
    /// link's <c>strongsNumber</c> must be one of its word's morphemes. Null for Greek.
    /// </summary>
    public IReadOnlyDictionary<int, IReadOnlyList<VerseMappingMorpheme>>? HebrewMorphemes { get; init; }

    /// <summary>Greek verses: Strong's of each original word by id (G3588 marks the article). Null where unknown.</summary>
    public IReadOnlyDictionary<int, string?>? OriginalWordStrongs { get; init; }

    /// <summary>STTS tag of each German word by id (APPRART allows the contraction exception). Null where unknown.</summary>
    public IReadOnlyDictionary<int, string?>? GermanWordStts { get; init; }

    /// <summary>
    /// Checks invariant 1 (one head per source morpheme unless <c>allowMultiHead</c>). Off for legacy saves (<see cref="MappingRoles.IsRoleAware"/>
    /// false), where all stored links are heads until the backfill (roadmap 0018.4) sets the units.
    /// </summary>
    public bool EnforceSingleHead { get; init; }

    /// <summary>
    /// The source morpheme a real link renders: (stepWordId, morpheme). Greek words have one Strong's, so the morpheme is empty; Hebrew
    /// uses the link's <c>strongsNumber</c> in the spelling of the word's morpheme list, else the main morpheme.
    /// </summary>
    public (int StepWordId, string Morpheme) MorphemeKey(VerseMappingLink link)
    {
        var stepId = link.StepWordId ?? 0;
        if (HebrewMorphemes is null || !HebrewMorphemes.TryGetValue(stepId, out var morphemes))
        {
            return (stepId, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(link.StrongsNumber))
        {
            return (stepId, VerseMapping.HebrewMorphemes.Main(morphemes) ?? string.Empty);
        }

        var match = morphemes.FirstOrDefault(m => string.Equals(m.Strongs, link.StrongsNumber.Trim(), StringComparison.OrdinalIgnoreCase));
        return (stepId, match?.Strongs ?? link.StrongsNumber.Trim());
    }

    /// <summary>
    /// Invariant 2 exception: a contraction (<c>zum</c>, STTS APPRART) is a <c>unit</c> of the article (Greek G3588, Hebrew H9009) while it is the
    /// head of the preposition; that article needs no head of its own.
    /// </summary>
    public bool IsContractionArticle(VerseMappingLink unit, (int StepWordId, string Morpheme) key)
    {
        if (GermanWordStts is null || !GermanWordStts.TryGetValue(unit.ElbWordId, out var stts) || stts != MappingRoles.ContractionStts)
        {
            return false;
        }

        var isArticle = HebrewMorphemes is not null
            ? VerseMapping.HebrewMorphemes.IsArticle(key.Morpheme)
            : OriginalWordStrongs is not null && OriginalWordStrongs.TryGetValue(key.StepWordId, out var strongs)
              && strongs is not null && strongs.StartsWith(MappingRoles.GreekArticle, StringComparison.OrdinalIgnoreCase);
        if (!isArticle)
        {
            return false;
        }

        return Links.Any(l => l.ElbWordId == unit.ElbWordId && !l.IsAddedWord && l.StepWordId is not null && !MappingRoles.IsUnit(l) && MorphemeKey(l) != key);
    }
}

public sealed class SaveVerseMappingValidator : AbstractValidator<VerseMappingSaveInput>
{
    public SaveVerseMappingValidator()
    {
        RuleFor(x => x).Custom((input, context) =>
        {
            var seen = new HashSet<(int, int?)>();
            for (var i = 0; i < input.Links.Count; i++)
            {
                var link = input.Links[i];
                var label = $"Link {i + 1} (word {link.ElbWordId})";

                if (!input.GermanWordIds.Contains(link.ElbWordId))
                {
                    context.AddFailure($"{label}: elbWordId does not belong to this verse.");
                }

                if (link.StepWordId is { } stepId && !input.OriginalWordIds.Contains(stepId))
                {
                    context.AddFailure($"{label}: stepWordId {stepId} is not an original word of this verse.");
                }

                if (link.ParentElbWordId is { } parentId)
                {
                    if (!input.GermanWordIds.Contains(parentId))
                    {
                        context.AddFailure($"{label}: parentElbWordId {parentId} does not belong to this verse.");
                    }
                    else if (parentId == link.ElbWordId)
                    {
                        context.AddFailure($"{label}: a word cannot be its own parent.");
                    }
                }

                if (link.IsAddedWord)
                {
                    if (link.ParentElbWordId is null)
                    {
                        context.AddFailure($"{label}: an added word needs a parentElbWordId.");
                    }

                    if (link.StepWordId is not null)
                    {
                        context.AddFailure($"{label}: an added word must not have a stepWordId.");
                    }
                }
                else if (link.StepWordId is null)
                {
                    context.AddFailure($"{label}: a link needs a stepWordId unless it is an added word.");
                }
                else if (link.ParentElbWordId is not null)
                {
                    context.AddFailure($"{label}: only added words have a parentElbWordId.");
                }

                if (!seen.Add((link.ElbWordId, link.StepWordId)))
                {
                    context.AddFailure($"{label}: duplicate (elbWordId, stepWordId) pair.");
                }

                CheckPartFields(link, label, input, context);
                CheckRoleFields(link, label, input, context);
            }

            CheckParts(input, context);
            CheckHeads(input, context);
        });
    }

    private static bool HasPart(VerseMappingLink link) => link.PartOrder is not null || !string.IsNullOrWhiteSpace(link.GermanWordPart);

    /// <summary>Per link: part text and order go together, order is positive, the text fits the column, an added word has no part.</summary>
    private static void CheckPartFields(VerseMappingLink link, string label, VerseMappingSaveInput input, ValidationContext<VerseMappingSaveInput> context)
    {
        if (!HasPart(link))
        {
            return;
        }

        if (input.GermanWordTexts is null)
        {
            context.AddFailure($"{label}: compound parts are not supported here.");
            return;
        }

        if (link.IsAddedWord)
        {
            context.AddFailure($"{label}: an added word has no parts.");
        }

        if (string.IsNullOrWhiteSpace(link.GermanWordPart) || link.PartOrder is null)
        {
            context.AddFailure($"{label}: germanWordPart and partOrder must both be set or both be empty.");
            return;
        }

        if (link.PartOrder < 1)
        {
            context.AddFailure($"{label}: partOrder must be 1 or higher.");
        }

        if (link.GermanWordPart.Length > GermanWordParts.MaxPartLength)
        {
            context.AddFailure($"{label}: germanWordPart is longer than {GermanWordParts.MaxPartLength} characters.");
        }
    }

    /// <summary>Per link: known role, role/morpheme/multi-head only on real links, morpheme of the linked word, part lemma only with a part.</summary>
    private static void CheckRoleFields(VerseMappingLink link, string label, VerseMappingSaveInput input, ValidationContext<VerseMappingSaveInput> context)
    {
        if (!MappingRoles.IsKnown(link.Role))
        {
            context.AddFailure($"{label}: role must be '{MappingRoles.Head}' or '{MappingRoles.Unit}', got '{link.Role}'.");
        }

        if (!string.IsNullOrWhiteSpace(link.PartLemma))
        {
            if (string.IsNullOrWhiteSpace(link.GermanWordPart))
            {
                context.AddFailure($"{label}: partLemma needs a germanWordPart.");
            }
            else if (link.PartLemma.Trim().Length > MappingRoles.MaxPartLemmaLength)
            {
                context.AddFailure($"{label}: partLemma is longer than {MappingRoles.MaxPartLemmaLength} characters.");
            }
        }

        if (link.IsAddedWord || link.StepWordId is not { } stepId)
        {
            // An added word is not a link to a source morpheme, so it has no role of its own (it stays head and is ignored).
            if (MappingRoles.IsUnit(link))
            {
                context.AddFailure($"{label}: an added word has no role; only links to an original word can be '{MappingRoles.Unit}'.");
            }

            if (!string.IsNullOrWhiteSpace(link.StrongsNumber))
            {
                context.AddFailure($"{label}: an added word has no strongsNumber.");
            }

            if (link.AllowMultiHead == true)
            {
                context.AddFailure($"{label}: allowMultiHead is only for head links.");
            }

            return;
        }

        if (link.AllowMultiHead == true && MappingRoles.IsUnit(link))
        {
            context.AddFailure($"{label}: allowMultiHead is only for head links.");
        }

        if (string.IsNullOrWhiteSpace(link.StrongsNumber))
        {
            return;
        }

        var strongs = link.StrongsNumber.Trim();
        if (input.HebrewMorphemes is not null)
        {
            if (HebrewMorphemes.IsPunctuation(strongs))
            {
                context.AddFailure($"{label}: strongsNumber {strongs} is punctuation (H9014–H9018), not a morpheme.");
            }
            else if (!input.HebrewMorphemes.TryGetValue(stepId, out var morphemes)
                     || !morphemes.Any(m => string.Equals(m.Strongs, strongs, StringComparison.OrdinalIgnoreCase)))
            {
                var known = input.HebrewMorphemes.TryGetValue(stepId, out var list) ? string.Join(", ", list.Select(m => m.Strongs)) : string.Empty;
                context.AddFailure($"{label}: strongsNumber {strongs} is not a morpheme of original word {stepId} ({known}).");
            }

            return;
        }

        // Greek: the Strong's comes from the STEP word; a client may echo it but not change it.
        if (input.OriginalWordStrongs is null || !input.OriginalWordStrongs.TryGetValue(stepId, out var own)
                                              || !string.Equals(own, strongs, StringComparison.OrdinalIgnoreCase))
        {
            context.AddFailure($"{label}: strongsNumber is only for Hebrew morphemes; a Greek link takes the Strong's of its word.");
        }
    }

    /// <summary>
    /// Per source morpheme: a <c>unit</c> needs a head on the same morpheme (except the APPRART article, invariant 2); with
    /// <see cref="VerseMappingSaveInput.EnforceSingleHead"/> at most one head unless one of them sets allowMultiHead (invariant 1).
    /// Invariant 3 (a German word heads at most one morpheme per source word) holds by the duplicate (elbWordId, stepWordId) check.
    /// </summary>
    private static void CheckHeads(VerseMappingSaveInput input, ValidationContext<VerseMappingSaveInput> context)
    {
        var real = input.Links
            .Where(l => !l.IsAddedWord && l.StepWordId is not null && MappingRoles.IsKnown(l.Role))
            .Where(l => input.OriginalWordIds.Contains(l.StepWordId!.Value))
            .ToList();

        foreach (var morpheme in real.GroupBy(input.MorphemeKey))
        {
            var label = morpheme.Key.Morpheme.Length == 0
                ? $"Original word {morpheme.Key.StepWordId}"
                : $"Original word {morpheme.Key.StepWordId} morpheme {morpheme.Key.Morpheme}";
            var heads = morpheme.Where(l => !MappingRoles.IsUnit(l)).ToList();

            if (heads.Count == 0)
            {
                foreach (var unit in morpheme.Where(u => !input.IsContractionArticle(u, morpheme.Key)))
                {
                    context.AddFailure($"{label}: word {unit.ElbWordId} is a '{MappingRoles.Unit}' but the morpheme has no head link.");
                }
            }

            if (input.EnforceSingleHead && heads.Count > 1 && !heads.Any(h => h.AllowMultiHead == true))
            {
                context.AddFailure($"{label}: {heads.Count} head links (words {string.Join(", ", heads.Select(h => h.ElbWordId))}); make all but one '{MappingRoles.Unit}' or set allowMultiHead.");
            }
        }
    }

    /// <summary>Per German word with parts: no plain link beside them, orders 1..n without gaps, one text per order, the parts spell the word.</summary>
    private static void CheckParts(VerseMappingSaveInput input, ValidationContext<VerseMappingSaveInput> context)
    {
        if (input.GermanWordTexts is null)
        {
            return;
        }

        foreach (var word in input.Links.Where(HasPart).GroupBy(l => l.ElbWordId))
        {
            if (!input.GermanWordTexts.TryGetValue(word.Key, out var text))
            {
                continue; // reported per link: the word does not belong to the verse
            }

            var label = $"Word {word.Key} ({text})";
            var links = input.Links.Where(l => l.ElbWordId == word.Key).ToList();
            var parts = links.Where(l => l is { PartOrder: >= 1 } && !string.IsNullOrWhiteSpace(l.GermanWordPart)).ToList();

            if (links.Any(l => !l.IsAddedWord && !HasPart(l)))
            {
                context.AddFailure($"{label}: a word with parts has no whole-word link.");
            }

            var orders = parts.Select(l => l.PartOrder!.Value).Distinct().Order().ToList();
            if (orders.Count == 0)
            {
                continue; // reported per link
            }

            if (!orders.SequenceEqual(Enumerable.Range(1, orders.Count)))
            {
                context.AddFailure($"{label}: part orders must run 1..n without gaps, got {string.Join(", ", orders)}.");
                continue;
            }

            if (orders.Count < 2)
            {
                context.AddFailure($"{label}: a word with parts needs at least two parts.");
            }

            var sameOrderDifferentText = parts
                .GroupBy(l => l.PartOrder!.Value)
                .Where(g => g.Select(l => GermanWordParts.Normalize(l.GermanWordPart)).Distinct().Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (sameOrderDifferentText.Count > 0)
            {
                context.AddFailure($"{label}: part order {string.Join(", ", sameOrderDifferentText)} has different texts.");
                continue;
            }

            var spelled = string.Concat(orders.Select(o => GermanWordParts.Normalize(parts.First(l => l.PartOrder == o).GermanWordPart)));
            if (spelled != GermanWordParts.Normalize(text))
            {
                var partTexts = string.Join(" + ", orders.Select(o => parts.First(l => l.PartOrder == o).GermanWordPart));
                context.AddFailure($"{label}: the parts ({partTexts}) do not spell the word (case and hyphens are ignored).");
            }
        }
    }
}
