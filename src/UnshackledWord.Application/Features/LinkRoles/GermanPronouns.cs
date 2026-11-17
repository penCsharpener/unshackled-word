namespace UnshackledWord.Application.Features.LinkRoles;

/// <summary>
/// Person, number and gender a German pronoun can refer to, from its form (the Elb1871Morphology values of pronouns are not reliable
/// enough: <c>euch</c> keeps lemma <c>euch</c>, a possessive's number/gender is that of its noun). Number and Case of the morphology
/// only narrow the ambiguous forms <c>sie</c> and <c>ihr</c>.
/// </summary>
public static class GermanPronouns
{
    private static readonly Pgn I = new(1, 's', null);
    private static readonly Pgn You = new(2, 's', null);
    private static readonly Pgn He = new(3, 's', 'm');
    private static readonly Pgn She = new(3, 's', 'f');
    private static readonly Pgn It = new(3, 's', 'n');
    private static readonly Pgn We = new(1, 'p', null);
    private static readonly Pgn YouAll = new(2, 'p', null);
    private static readonly Pgn They = new(3, 'p', null);
    private static readonly Pgn ThirdSingular = new(3, 's', null);

    /// <summary>STTS of personal and reflexive pronouns.</summary>
    public static bool IsPersonal(string? stts) => stts is "PPER" or "PRF";

    /// <summary>STTS of possessive pronouns.</summary>
    public static bool IsPossessive(string? stts) => stts is "PPOSAT" or "PPOSS";

    /// <summary>Candidates for a <c>PPER</c>/<c>PRF</c>/<c>PPOSAT</c>/<c>PPOSS</c> word; empty for other tags and unknown forms.</summary>
    public static IReadOnlyList<Pgn> Candidates(LinkRoleGermanWord word)
    {
        var text = word.Text.Trim().ToLowerInvariant();
        if (IsPossessive(word.Stts))
        {
            return Possessive(word.Lemma?.Trim().ToLowerInvariant() ?? text) is { Count: > 0 } byLemma ? byLemma : Possessive(text);
        }

        if (!IsPersonal(word.Stts))
        {
            return [];
        }

        return text switch
        {
            "ich" or "mich" or "mir" or "meiner" => [I],
            "du" or "dich" or "dir" or "deiner" => [You],
            "er" or "ihn" or "ihm" => [He],
            "seiner" => [He, It],
            "es" => [It],
            "sie" => word.Number switch
            {
                "S" => [She],
                "P" => [They],
                _ => [She, They],
            },
            "ihr" => word.Case switch
            {
                "Nom" => [YouAll],
                "Dat" or "Gen" => [She],
                _ => [YouAll, She],
            },
            "ihrer" => [She, They],
            "ihnen" => [They],
            "wir" or "uns" or "unser" => [We],
            "euch" or "euer" => [YouAll],
            "sich" => [ThirdSingular, They],
            _ => [],
        };
    }

    /// <summary>True when the morphology or the form says nominative (<c>PPER</c> subject).</summary>
    public static bool IsNominative(LinkRoleGermanWord word)
    {
        if (word.Case is not null)
        {
            return word.Case == "Nom";
        }

        return word.Text.Trim().ToLowerInvariant() is "ich" or "du" or "er" or "wir";
    }

    /// <summary>German gender n matches Hebrew m or f; a null gender on either side matches anything.</summary>
    public static bool Matches(Pgn german, Pgn source) =>
        german.Person == source.Person
        && german.Number == source.Number
        && (german.Gender is null or 'n' || source.Gender is null || german.Gender == source.Gender);

    private static IReadOnlyList<Pgn> Possessive(string form)
    {
        if (form.StartsWith("unser", StringComparison.Ordinal)) return [We];
        if (form.StartsWith("euer", StringComparison.Ordinal) || form.StartsWith("eur", StringComparison.Ordinal)) return [YouAll];
        if (form.StartsWith("mein", StringComparison.Ordinal)) return [I];
        if (form.StartsWith("dein", StringComparison.Ordinal)) return [You];
        if (form.StartsWith("sein", StringComparison.Ordinal)) return [He, It];
        if (form.StartsWith("ihr", StringComparison.Ordinal)) return [She, They];
        return [];
    }
}
