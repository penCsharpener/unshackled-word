using UnshackledWord.Application.Features.LinkRoles;

namespace UnshackledWord.Application.Tests.Features.LinkRoles;

/// <summary>Turns a <see cref="LinkRoleFixtures"/> text into a <see cref="LinkRoleVerse"/> and changes single rows for variants.</summary>
internal static class LinkRoleVerseBuilder
{
    public static LinkRoleVerse Load(string data, int hebRefId, SourceLanguage language)
    {
        var words = new List<LinkRoleGermanWord>();
        var links = new List<LinkRoleLink>();
        var sources = new List<LinkRoleSourceWord>();
        foreach (var line in data.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = line.TrimEnd('\r').Split('\t');
            string? At(int i) => i < f.Length && f[i].Trim().Length > 0 ? f[i].Trim() : null;
            switch (f[0].Trim())
            {
                case "W":
                    words.Add(new LinkRoleGermanWord(int.Parse(f[1]), int.Parse(f[2]), f[3], At(4), At(5), At(6), At(7), At(8), At(9)));
                    break;
                case "L":
                    links.Add(new LinkRoleLink(int.Parse(f[1]), int.Parse(f[2]), At(3) is { } step ? int.Parse(step) : null, At(4), At(5) == "1", At(6)));
                    break;
                case "S":
                    sources.Add(new LinkRoleSourceWord(int.Parse(f[1]), At(2), At(3)));
                    break;
            }
        }

        return new LinkRoleVerse(hebRefId, language, words, links, sources);
    }

    public static LinkRoleVerse Word(this LinkRoleVerse verse, int wordId, Func<LinkRoleGermanWord, LinkRoleGermanWord> change) =>
        verse with { Words = verse.Words.Select(w => w.Id == wordId ? change(w) : w).ToList() };

    /// <summary>The link of a German word (each fixture word has at most one).</summary>
    public static LinkRoleVerse LinkOf(this LinkRoleVerse verse, int wordId, Func<LinkRoleLink, LinkRoleLink> change) =>
        verse with { Links = verse.Links.Select(l => l.GermanWordId == wordId ? change(l) : l).ToList() };

    /// <summary>Turns an added word back into a real link, the state before a model or human marked it added.</summary>
    public static LinkRoleVerse Linked(this LinkRoleVerse verse, int wordId, int stepWordId, string? strongs = null) =>
        verse.LinkOf(wordId, l => l with { IsAddedWord = false, StepWordId = stepWordId, StrongsNumber = strongs });

    /// <summary>"1 head H9001", "1-main head H0559", "2 unit G0907", "5 added 35", "undecided 1,4", or "none".</summary>
    public static string Describe(this LinkRoleVerseResult result, int wordId)
    {
        var decision = result.Decisions.SingleOrDefault(d => d.GermanWordId == wordId);
        if (decision is not null)
        {
            return decision.BecomesAddedWord
                ? $"{decision.Rule.Code()} added {decision.AddedWordParentId}"
                : $"{decision.Rule.Code()} {decision.Role.ToDbValue()} {decision.StrongsNumber}";
        }

        var undecided = result.Undecided.SingleOrDefault(u => u.GermanWordId == wordId);
        return undecided is not null ? $"undecided {string.Join(",", undecided.FittingRules.Select(r => r.Code()))}" : "none";
    }
}
