using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Domain.Tests.Model.Grammar;

public class LemmaBulkRulesTests
{
    [Theory]
    [InlineData("Jesus", true)]
    [InlineData("  Jesus ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("a\tb", false)]
    [InlineData("a\nb", false)]
    public void IsValidLemma_rejects_empty_and_control_chars(string lemma, bool expected) =>
        LemmaBulkRules.IsValidLemma(lemma).ShouldBe(expected);

    [Fact]
    public void IsValidLemma_limits_length_after_trim()
    {
        LemmaBulkRules.IsValidLemma(new string('a', 255)).ShouldBeTrue();
        LemmaBulkRules.IsValidLemma(" " + new string('a', 255) + " ").ShouldBeTrue();
        LemmaBulkRules.IsValidLemma(new string('a', 256)).ShouldBeFalse();
    }

    [Fact]
    public void NormalizeFrom_trims_and_dedupes_case_sensitively()
    {
        LemmaBulkRules.NormalizeFrom([" Jesu", "Jesu", "jesu", "Jesum"]).ShouldBe(["Jesu", "jesu", "Jesum"]);
    }

    [Theory]
    [InlineData(null, null, 1, 66)]
    [InlineData("ot", null, 1, 39)]
    [InlineData("nt", null, 40, 66)]
    [InlineData("nt", 41, 41, 41)]
    [InlineData(null, 1, 1, 1)]
    public void BookRange_resolves_scope(string? testament, int? bookId, int from, int to) =>
        LemmaBulkRules.BookRange(testament, bookId).ShouldBe((from, to));

    [Fact]
    public void LikePrefix_escapes_wildcards() =>
        LemmaBulkRules.LikePrefix("a%b_c\\").ShouldBe("a\\%b\\_c\\\\%");

    [Fact]
    public void FindStale_flags_missing_and_changed_lemmas()
    {
        var current = new Dictionary<int, string> { [1] = "Jesu", [2] = "Jesus", [3] = "Jesum" };
        LemmaBulkRules.FindStale([1, 2, 3, 4], current, ["Jesu", "Jesum"]).ShouldBe([2, 4]);
    }
}
