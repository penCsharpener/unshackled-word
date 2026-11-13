using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Lemmas;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.Stts;

/// <summary>The STTS tags of the table with the "is this pair allowed" question the editor and the validators ask.</summary>
public sealed class SttsCatalog
{
    private readonly Dictionary<string, SttsTag> _byTag;

    public SttsCatalog(IEnumerable<SttsTag> tags)
    {
        _byTag = tags.ToDictionary(t => t.Tag, StringComparer.Ordinal);
    }

    public bool Exists(string tag) => _byTag.ContainsKey(tag);

    public bool IsAllowed(string tag, string partOfSpeech) => _byTag.TryGetValue(tag, out var t) && t.AllowedUpos.Contains(partOfSpeech);
}

/// <summary>Rules for STTS in edits (pure, tested without a database).</summary>
public static class SttsRules
{
    /// <summary>
    /// Error text for one requested change, or null. A null Stts leaves the tag untouched (never an error). A given tag must exist; the
    /// pair with the part of speech must be allowed when the row is edited, i.e. when the tag or the part of speech differs from the
    /// stored row. A row that keeps its legacy pair unchanged is not blocked.
    /// </summary>
    public static string? Check(LemmaEditFields change, LemmaWordRow? before, SttsCatalog catalog)
    {
        if (change.Stts is null) return null;
        if (!catalog.Exists(change.Stts)) return $"Unknown STTS tag '{change.Stts}'.";

        var edited = before is null || before.Stts != change.Stts || before.PartOfSpeech != change.PartOfSpeech;
        return edited && !catalog.IsAllowed(change.Stts, change.PartOfSpeech)
            ? $"STTS tag '{change.Stts}' does not fit the part of speech '{change.PartOfSpeech}'."
            : null;
    }

    /// <summary>Fills <see cref="LemmaWordRow.ProposedStts"/> of rows whose tag does not fit their part of speech.</summary>
    public static void AddProposals(IEnumerable<LemmaWordRow> rows, SttsCatalog catalog)
    {
        foreach (var row in rows)
        {
            row.ProposedStts = SttsProposal.ProposeIfMismatch(row.Stts, row.PartOfSpeech, row.VerbForm, row.Mood, catalog.IsAllowed);
        }
    }
}
