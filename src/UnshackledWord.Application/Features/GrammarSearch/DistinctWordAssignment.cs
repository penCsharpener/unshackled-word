namespace UnshackledWord.Application.Features.GrammarSearch;

/// <summary>The "two slots may not use the same word" rule: does every slot get its own candidate word?</summary>
public static class DistinctWordAssignment
{
    /// <summary>True when each slot (one set of candidate word ids per slot) can be given a different word. At most four slots, so a backtracking search is enough.</summary>
    public static bool Exists(IReadOnlyList<HashSet<int>> candidatesPerSlot) => Assign(candidatesPerSlot, 0, []);

    private static bool Assign(IReadOnlyList<HashSet<int>> candidates, int slot, HashSet<int> used)
    {
        if (slot == candidates.Count) return true;
        foreach (var word in candidates[slot])
        {
            if (!used.Add(word)) continue;
            if (Assign(candidates, slot + 1, used)) return true;
            used.Remove(word);
        }

        return false;
    }
}
