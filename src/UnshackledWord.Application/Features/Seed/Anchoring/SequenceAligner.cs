namespace UnshackledWord.Application.Features.Seed.Anchoring;

/// <summary>
/// Myers' O(ND) diff over two string sequences. Returns the equal elements as index pairs that are strictly ascending on both sides
/// (a longest common subsequence), so repeated words never cross: the n-th "und" of the old side can only match the n-th or a later
/// "und" of the new side.
/// </summary>
public static class SequenceAligner
{
    /// <summary>
    /// Upper bound of edits (insertions plus deletions) searched in the part of the chapter between common prefix and suffix. The trace
    /// costs O(edits squared) memory; a chapter that differs by more than this is not a re-anchoring case but a different text.
    /// </summary>
    public const int DefaultMaxEdits = 4000;

    public static AlignedRuns Align(IReadOnlyList<string> oldItems, IReadOnlyList<string> newItems, int maxEdits = DefaultMaxEdits)
    {
        var pairs = new List<(int OldIndex, int NewIndex)>();
        var prefix = 0;

        while (prefix < oldItems.Count && prefix < newItems.Count
               && string.Equals(oldItems[prefix], newItems[prefix], StringComparison.Ordinal))
        {
            pairs.Add((prefix, prefix));
            prefix++;
        }

        var suffix = 0;

        while (suffix < oldItems.Count - prefix && suffix < newItems.Count - prefix
               && string.Equals(oldItems[^(suffix + 1)], newItems[^(suffix + 1)], StringComparison.Ordinal))
        {
            suffix++;
        }

        var oldCore = oldItems.Count - prefix - suffix;
        var newCore = newItems.Count - prefix - suffix;
        var complete = true;

        if (oldCore > 0 && newCore > 0)
        {
            var middle = Myers(oldItems, newItems, prefix, oldCore, newCore, maxEdits);

            if (middle is null)
            {
                complete = false;
            }
            else
            {
                pairs.AddRange(middle);
            }
        }

        for (var i = suffix; i >= 1; i--)
        {
            pairs.Add((oldItems.Count - i, newItems.Count - i));
        }

        return new AlignedRuns(pairs, complete);
    }

    /// <summary>The shortest edit script over the sub-ranges [offset, offset + n) and [offset, offset + m); null when it needs more than <paramref name="maxEdits"/> edits.</summary>
    private static List<(int OldIndex, int NewIndex)>? Myers(IReadOnlyList<string> a, IReadOnlyList<string> b, int offset, int n, int m, int maxEdits)
    {
        var max = Math.Min(n + m, maxEdits);
        var centre = max + 1;
        var v = new int[2 * max + 3];
        var trace = new List<int[]>();

        for (var d = 0; d <= max; d++)
        {
            // Values of the previous round for k in [-d-1, d+1]; backtracking needs exactly this window.
            var window = new int[2 * d + 3];
            Array.Copy(v, centre - d - 1, window, 0, window.Length);
            trace.Add(window);

            for (var k = -d; k <= d; k += 2)
            {
                var x = k == -d || (k != d && v[centre + k - 1] < v[centre + k + 1])
                    ? v[centre + k + 1]
                    : v[centre + k - 1] + 1;
                var y = x - k;

                while (x < n && y < m && string.Equals(a[offset + x], b[offset + y], StringComparison.Ordinal))
                {
                    x++;
                    y++;
                }

                v[centre + k] = x;

                if (x >= n && y >= m)
                {
                    return Backtrack(trace, offset, n, m, d);
                }
            }
        }

        return null;
    }

    private static List<(int OldIndex, int NewIndex)> Backtrack(List<int[]> trace, int offset, int n, int m, int editCount)
    {
        var pairs = new List<(int OldIndex, int NewIndex)>();
        var x = n;
        var y = m;

        for (var d = editCount; d >= 0; d--)
        {
            var window = trace[d];
            var k = x - y;
            int previousK;

            if (k == -d || (k != d && window[k - 1 + d + 1] < window[k + 1 + d + 1]))
            {
                previousK = k + 1;
            }
            else
            {
                previousK = k - 1;
            }

            var previousX = window[previousK + d + 1];
            var previousY = previousX - previousK;

            while (x > previousX && y > previousY)
            {
                x--;
                y--;
                pairs.Add((offset + x, offset + y));
            }

            x = previousX;
            y = previousY;
        }

        pairs.Reverse();

        return pairs;
    }
}

/// <summary>Equal elements of two sequences (ascending on both sides) and whether the search finished within its edit budget.</summary>
/// <param name="Pairs">Index in the old sequence and index in the new sequence of every equal element.</param>
/// <param name="Complete">False when the middle part differed by more edits than allowed; only common prefix and suffix are matched then.</param>
public sealed record AlignedRuns(IReadOnlyList<(int OldIndex, int NewIndex)> Pairs, bool Complete);
