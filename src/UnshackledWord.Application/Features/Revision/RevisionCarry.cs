using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Features.Revision;

/// <summary>A word of the parent text. For an Elb1871 parent BasePositionInVerse is the own position and Change is "same".</summary>
public sealed record RevisionCarryParentWord(int Position, string? PlainWord, int? BasePositionInVerse, string Change);

/// <summary>A mapping row of the parent. ParentWordPosition is the German parent word of an added-word row.</summary>
public sealed record RevisionCarryParentRow(int ParentPosition, int? StepWordId, bool IsAddedWord, int? ParentWordPosition, string? GermanWordPart);

public sealed record RevisionCarryToken(int Position, string? PlainWord);

/// <summary>Base position and change of a new token; Change is same|changed|new and "new" exactly when BasePositionInVerse is null.</summary>
public sealed record RevisionCarryWord(int Position, int? BasePositionInVerse, string Change);

/// <summary>A carried mapping row keyed by the NEW position (stored with Origin 'carried').</summary>
public sealed record RevisionCarryRow(int Position, int? StepWordId, bool IsAddedWord, int? ParentPosition, string? GermanWordPart);

public sealed record RevisionCarryReport(int SameCount, int ChangedCount, int NewCount, IReadOnlyList<int> LostStepWordIds);

public sealed record RevisionCarryResult(
    IReadOnlyList<RevisionCarryWord> Words,
    IReadOnlyList<RevisionCarryRow> Rows,
    RevisionCarryReport Report);

/// <summary>Pure carry-over of mapping rows from a parent text to a new text (roadmap 0011, "Design - Carry-over"). Never invents a link.</summary>
public static class RevisionCarry
{
    public const string Same = "same";
    public const string Changed = "changed";
    public const string New = "new";

    public static RevisionCarryResult Carry(
        IReadOnlyList<RevisionCarryParentWord> parentWords,
        IReadOnlyList<RevisionCarryParentRow> parentRows,
        IReadOnlyList<RevisionCarryToken> newTokens)
    {
        var oldWords = parentWords.OrderBy(w => w.Position).ToList();
        var tokens = newTokens.OrderBy(t => t.Position).ToList();
        var oldKeys = oldWords.Select(w => w.PlainWord ?? "").ToList();
        var newKeys = tokens.Select(t => t.PlainWord ?? "").ToList();
        var runs = SequenceAligner.Align(oldKeys, newKeys);

        // Per new index: source old indexes and how the word relates to them.
        var sources = new List<int>[tokens.Count];
        var step = new string?[tokens.Count]; // same | changed | new
        var keepPart = new bool[tokens.Count];
        var oldMatched = new bool[oldWords.Count];
        var newMatched = new bool[tokens.Count];

        foreach (var (o, n) in runs.Pairs)
        {
            Assign(n, [o], Same, true);
            oldMatched[o] = true;
            newMatched[n] = true;
        }

        // Rule 6: moved word, unique among the leftovers on both sides.
        var movedOld = Leftover(oldKeys, oldMatched);
        var movedNew = Leftover(newKeys, newMatched);

        foreach (var (key, o) in movedOld)
        {
            if (movedNew.TryGetValue(key, out var n))
            {
                Assign(n, [o], Same, true);
                oldMatched[o] = true;
                newMatched[n] = true;
            }
        }

        // Gaps between consecutive LCS anchors (moved words removed from the gaps).
        var oi = 0;
        var ni = 0;
        var pairs = runs.Pairs.Append((oldWords.Count, tokens.Count));

        foreach (var (po, pn) in pairs)
        {
            var gapOld = Enumerable.Range(oi, po - oi).Where(i => !oldMatched[i]).ToList();
            var gapNew = Enumerable.Range(ni, pn - ni).Where(i => !newMatched[i]).ToList();
            ResolveGap(gapOld, gapNew);
            oi = po + 1;
            ni = pn + 1;
        }

        // Words and old -> new carriers.
        var words = new List<RevisionCarryWord>();
        var oldToNew = new Dictionary<int, List<int>>(); // old position -> new positions

        for (var n = 0; n < tokens.Count; n++)
        {
            var src = sources[n];
            var position = tokens[n].Position;

            if (src is null || src.Count == 0)
            {
                words.Add(new RevisionCarryWord(position, null, New));
                continue;
            }

            var first = oldWords[src[0]];
            var parentBase = src.Select(i => oldWords[i].BasePositionInVerse).FirstOrDefault(b => b is not null);

            if (src.Count == 1)
            {
                parentBase = first.BasePositionInVerse;
            }

            string change;

            if (parentBase is null)
            {
                change = New;
            }
            else if (step[n] == Same)
            {
                change = first.Change == Same ? Same : Changed;
            }
            else
            {
                change = Changed;
            }

            words.Add(new RevisionCarryWord(position, parentBase, change));

            foreach (var i in src)
            {
                var key = oldWords[i].Position;

                if (!oldToNew.TryGetValue(key, out var list))
                {
                    oldToNew[key] = list = [];
                }

                list.Add(position);
            }
        }

        var rowsByOld = parentRows.GroupBy(r => r.ParentPosition).ToDictionary(g => g.Key, g => g.ToList());
        var rows = new List<RevisionCarryRow>();
        var seen = new HashSet<(int, int?)>();

        for (var n = 0; n < tokens.Count; n++)
        {
            var src = sources[n];

            if (src is null || src.Count == 0)
            {
                continue;
            }

            var position = tokens[n].Position;

            foreach (var i in src)
            {
                if (!rowsByOld.TryGetValue(oldWords[i].Position, out var list))
                {
                    continue;
                }

                foreach (var r in list)
                {
                    if (!seen.Add((position, r.StepWordId)))
                    {
                        continue;
                    }

                    int? parent = null;

                    if (r.IsAddedWord && r.ParentWordPosition is { } pw && oldToNew.TryGetValue(pw, out var carriers))
                    {
                        parent = carriers.FirstOrDefault(c => c != position, -1);

                        if (parent == -1)
                        {
                            parent = null;
                        }
                    }

                    rows.Add(new RevisionCarryRow(position, r.StepWordId, r.IsAddedWord, parent, keepPart[n] ? r.GermanWordPart : null));
                }
            }
        }

        var kept = rows.Where(r => r.StepWordId is not null).Select(r => r.StepWordId!.Value).ToHashSet();
        var lost = parentRows.Where(r => r.StepWordId is not null).Select(r => r.StepWordId!.Value).Distinct().Where(id => !kept.Contains(id)).ToList();
        var report = new RevisionCarryReport(
            words.Count(w => w.Change == Same),
            words.Count(w => w.Change == Changed),
            words.Count(w => w.Change == New),
            lost);

        return new RevisionCarryResult(words, rows, report);

        void Assign(int n, List<int> from, string kind, bool part)
        {
            sources[n] = from;
            step[n] = kind;
            keepPart[n] = part;
        }

        void ResolveGap(List<int> gapOld, List<int> gapNew)
        {
            if (gapOld.Count == 0 || gapNew.Count == 0)
            {
                return; // rule 7 (insert) / rule 8 (delete): nothing carried
            }

            if (gapNew.Count == 1)
            {
                Assign(gapNew[0], gapOld, Changed, false); // rule 2 (1:1) and rule 3 (n:1)
            }
            else if (gapOld.Count == 1)
            {
                foreach (var n in gapNew)
                {
                    Assign(n, gapOld, Changed, false); // rule 4 (1:m)
                }
            }
            // rule 5: n:m, new words stay 'new', nothing copied
        }
    }

    private static Dictionary<string, int> Leftover(List<string> keys, bool[] matched)
    {
        var count = new Dictionary<string, int>();
        var index = new Dictionary<string, int>();

        for (var i = 0; i < keys.Count; i++)
        {
            if (matched[i])
            {
                continue;
            }

            count[keys[i]] = count.GetValueOrDefault(keys[i]) + 1;
            index[keys[i]] = i;
        }

        return index.Where(kv => count[kv.Key] == 1).ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
