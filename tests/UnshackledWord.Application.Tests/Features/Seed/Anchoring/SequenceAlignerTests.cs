using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Tests.Features.Seed.Anchoring;

public class SequenceAlignerTests
{
    private static string[] Split(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Align_ShouldMatchEverything_WhenSequencesAreEqual()
    {
        var runs = SequenceAligner.Align(Split("a b c"), Split("a b c"));

        runs.Pairs.ShouldBe([(0, 0), (1, 1), (2, 2)]);
        runs.Complete.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "a b", 0)]
    [InlineData("a b", "", 0)]
    [InlineData("", "", 0)]
    [InlineData("a b c a b b a", "c b a b a c", 4)]
    [InlineData("a b c d", "d c b a", 1)]
    [InlineData("und die und", "und und", 2)]
    public void Align_ShouldFindALongestCommonSubsequence_WhenSequencesDiffer(string left, string right, int expectedLength)
    {
        var oldItems = Split(left);
        var newItems = Split(right);

        var runs = SequenceAligner.Align(oldItems, newItems);

        runs.Pairs.Count.ShouldBe(expectedLength);
        runs.Pairs.All(p => oldItems[p.OldIndex] == newItems[p.NewIndex]).ShouldBeTrue();
    }

    [Fact]
    public void Align_ShouldAscendOnBothSides_WhenWordsRepeat()
    {
        var runs = SequenceAligner.Align(Split("und die und das und"), Split("und und die das und und"));

        runs.Pairs.Select(p => p.OldIndex).ShouldBe(runs.Pairs.Select(p => p.OldIndex).Order());
        runs.Pairs.Select(p => p.NewIndex).ShouldBe(runs.Pairs.Select(p => p.NewIndex).Order());
        runs.Pairs.Select(p => p.NewIndex).Distinct().Count().ShouldBe(runs.Pairs.Count);
    }

    [Fact]
    public void Align_ShouldReachTheOptimumOfADynamicProgram_WhenInputIsRandom()
    {
        var random = new Random(12345);
        var alphabet = new[] { "a", "b", "c", "d", "e" };

        for (var round = 0; round < 200; round++)
        {
            var oldItems = Enumerable.Range(0, random.Next(0, 40)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray();
            var newItems = Enumerable.Range(0, random.Next(0, 40)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray();

            var runs = SequenceAligner.Align(oldItems, newItems);

            runs.Complete.ShouldBeTrue();
            runs.Pairs.Count.ShouldBe(LcsLength(oldItems, newItems));
            runs.Pairs.All(p => oldItems[p.OldIndex] == newItems[p.NewIndex]).ShouldBeTrue();
            runs.Pairs.Zip(runs.Pairs.Skip(1)).All(z => z.First.OldIndex < z.Second.OldIndex && z.First.NewIndex < z.Second.NewIndex).ShouldBeTrue();
        }
    }

    [Fact]
    public void Align_ShouldReportIncomplete_WhenMiddlePartExceedsTheEditBudget()
    {
        var runs = SequenceAligner.Align(Split("start a b c d e f g end"), Split("start p q r s t u v end"), maxEdits: 4);

        runs.Complete.ShouldBeFalse();
        runs.Pairs.ShouldBe([(0, 0), (8, 8)]);
    }

    private static int LcsLength(string[] a, string[] b)
    {
        var table = new int[a.Length + 1, b.Length + 1];

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                table[i, j] = a[i - 1] == b[j - 1] ? table[i - 1, j - 1] + 1 : Math.Max(table[i - 1, j], table[i, j - 1]);
            }
        }

        return table[a.Length, b.Length];
    }
}
