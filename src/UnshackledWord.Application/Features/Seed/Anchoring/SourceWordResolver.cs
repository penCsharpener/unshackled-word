namespace UnshackledWord.Application.Features.Seed.Anchoring;

/// <summary>
/// Finds the STEP word of this installation that a seed key (<see cref="SourceKey"/>: <c>LxxRefId</c>, position, hash) was exported from.
/// The seed only knows the STEP words that were mapped, so the old side of a verse is sparse. Per verse, in this order:
/// 1. the word at the same position with the same hash (<see cref="AnchorClass.Exact"/>);
/// 2. the only remaining word with this hash in the verse, if the seed has exactly one such key (<see cref="AnchorClass.Shifted"/>);
/// 3. the sequence alignment of the verse's hashes (<see cref="ChapterAligner"/>, which also yields <see cref="AnchorClass.Modified"/>
///    for a lone changed word between equal neighbours); everything else is <see cref="AnchorClass.Orphaned"/>.
/// Same rule order as the registry sync of roadmap 0005, so seed import and database sync agree.
/// </summary>
public sealed class SourceWordResolver
{
    private readonly Dictionary<SourceKey, TokenMatch> _resolved;

    private SourceWordResolver(Dictionary<SourceKey, TokenMatch> resolved)
    {
        _resolved = resolved;
    }

    public static SourceWordResolver Create(IEnumerable<SourceKey> seedKeys, IEnumerable<AnchorToken> newTokens)
    {
        var newByVerse = newTokens.GroupBy(t => t.Ref).ToDictionary(g => g.Key, g => g.OrderBy(t => t.Pos).ToList());
        var resolved = new Dictionary<SourceKey, TokenMatch>();

        foreach (var verse in seedKeys.Distinct().GroupBy(k => k.LxxRefId).OrderBy(g => g.Key))
        {
            var oldList = verse.OrderBy(k => k.Position).Select(k => k.ToToken()).ToList();
            var newList = newByVerse.GetValueOrDefault(verse.Key) ?? [];

            foreach (var match in ResolveVerse(verse.Key, oldList, newList))
            {
                resolved[new SourceKey(match.Old.Ref, match.Old.Pos, match.Old.Text)] = match;
            }
        }

        return new SourceWordResolver(resolved);
    }

    public TokenMatch Resolve(SourceKey key)
    {
        return _resolved.TryGetValue(key, out var match) ? match : new TokenMatch(key.ToToken(), null, AnchorClass.Orphaned);
    }

    private static IEnumerable<TokenMatch> ResolveVerse(int verse, List<AnchorToken> oldList, List<AnchorToken> newList)
    {
        var matches = new Dictionary<AnchorToken, TokenMatch>();
        var claimedNew = new HashSet<AnchorToken>();
        var newByPosition = newList.GroupBy(t => (t.Pos, t.Text)).ToDictionary(g => g.Key, g => g.First());

        foreach (var old in oldList)
        {
            if (newByPosition.TryGetValue((old.Pos, old.Text), out var same))
            {
                matches[old] = new TokenMatch(old, same, AnchorClass.Exact);
                claimedNew.Add(same);
            }
        }

        var open = oldList.Where(o => !matches.ContainsKey(o)).ToList();

        foreach (var old in open)
        {
            var oldWithHash = open.Where(o => o.Text == old.Text).ToList();
            var newWithHash = newList.Where(n => n.Text == old.Text && !claimedNew.Contains(n)).ToList();

            if (oldWithHash.Count == 1 && newWithHash.Count == 1)
            {
                matches[old] = new TokenMatch(old, newWithHash[0], AnchorClass.Shifted);
                claimedNew.Add(newWithHash[0]);
            }
        }

        open = oldList.Where(o => !matches.ContainsKey(o)).ToList();

        if (open.Count > 0)
        {
            var alignment = ChapterAligner.Align(verse, oldList, newList);

            foreach (var match in alignment.Matches.Where(m => open.Contains(m.Old)))
            {
                if (match.New is { } counterpart && claimedNew.Contains(counterpart))
                {
                    matches[match.Old] = new TokenMatch(match.Old, null, AnchorClass.Orphaned);
                    continue;
                }

                matches[match.Old] = match;

                if (match.New is { } taken)
                {
                    claimedNew.Add(taken);
                }
            }
        }

        return oldList.Select(o => matches[o]);
    }
}
