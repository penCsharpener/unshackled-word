namespace UnshackledWord.Domain.Models.Grammar;

/// <summary>One proposal rule: the STTS tag matching a UD part of speech, optionally refined by verb form or mood.</summary>
public sealed record SttsProposalRule(string PartOfSpeech, string? VerbForm, string? Mood, string Stts);

/// <summary>
/// Proposes the STTS tag that matches a UD part of speech (refined by the edited features). A proposal is never
/// written automatically; the user has to accept it.
/// </summary>
public static class SttsProposal
{
    /// <summary>Most specific first: mood, verb form, then the plain part of speech.</summary>
    public static readonly IReadOnlyList<SttsProposalRule> Rules =
    [
        new("VERB", null, "Imp", "VVIMP"),
        new("VERB", "Fin", null, "VVFIN"),
        new("VERB", "Inf", null, "VVINF"),
        new("VERB", "Part", null, "VVPP"),
        new("VERB", null, null, "VVFIN"),
        new("AUX", null, "Imp", "VAIMP"),
        new("AUX", "Fin", null, "VAFIN"),
        new("AUX", "Inf", null, "VAINF"),
        new("AUX", "Part", null, "VAPP"),
        new("AUX", null, null, "VAFIN"),
        new("NOUN", null, null, "NN"),
        new("PROPN", null, null, "NE"),
        new("ADJ", null, null, "ADJA"),
        new("ADV", null, null, "ADV"),
        new("ADP", null, null, "APPR"),
        new("DET", null, null, "ART"),
        new("PRON", null, null, "PPER"),
        new("CCONJ", null, null, "KON"),
        new("SCONJ", null, null, "KOUS"),
        new("NUM", null, null, "CARD"),
        new("INTJ", null, null, "ITJ"),
        new("PART", null, null, "PTKNEG"),
        new("X", null, null, "FM"),
    ];

    /// <summary>The proposed tag, or null when the part of speech is unknown.</summary>
    public static string? Propose(string? partOfSpeech, string? verbForm, string? mood) =>
        Rules.FirstOrDefault(r => r.PartOfSpeech == partOfSpeech
                                  && (r.VerbForm is null || r.VerbForm == verbForm)
                                  && (r.Mood is null || r.Mood == mood))?.Stts;

    /// <summary>The tag when it does not fit the part of speech: the proposal, otherwise null.</summary>
    public static string? ProposeIfMismatch(string? stts, string? partOfSpeech, string? verbForm, string? mood, Func<string, string, bool> isAllowed)
    {
        if (stts is null || partOfSpeech is null || isAllowed(stts, partOfSpeech)) return null;
        var proposal = Propose(partOfSpeech, verbForm, mood);
        return proposal is not null && proposal != stts && isAllowed(proposal, partOfSpeech) ? proposal : null;
    }
}
