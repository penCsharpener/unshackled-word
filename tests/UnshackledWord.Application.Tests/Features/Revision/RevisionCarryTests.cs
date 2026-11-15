using UnshackledWord.Application.Features.Revision;

namespace UnshackledWord.Application.Tests.Features.Revision;

public class RevisionCarryTests
{
    private static List<RevisionCarryParentWord> Parent(params string[] words) =>
        words.Select((w, i) => new RevisionCarryParentWord(i + 1, w, i + 1, "same")).ToList();

    private static List<RevisionCarryToken> Tokens(params string[] words) =>
        words.Select((w, i) => new RevisionCarryToken(i + 1, w)).ToList();

    private static RevisionCarryParentRow Row(int pos, int? step, bool added = false, int? parent = null, string? part = null) =>
        new(pos, step, added, parent, part);

    private static string Shape(RevisionCarryResult r) =>
        string.Join(" ", r.Words.Select(w => $"{w.Position}:{w.Change}/{w.BasePositionInVerse?.ToString() ?? "-"}"));

    private static void ShouldSatisfyCheck(RevisionCarryResult r) =>
        r.Words.ShouldAllBe(w => (w.Change == "new") == (w.BasePositionInVerse == null));

    [Fact]
    public void Unchanged_verse_is_all_same_with_the_same_rows()
    {
        var rows = new[] { Row(1, 10), Row(2, 20), Row(2, 21), Row(3, 30) };
        var r = RevisionCarry.Carry(Parent("a", "b", "c"), rows, Tokens("a", "b", "c"));

        r.Report.SameCount.ShouldBe(3);
        r.Report.ChangedCount.ShouldBe(0);
        r.Report.NewCount.ShouldBe(0);
        r.Rows.Count.ShouldBe(rows.Length);
        r.Report.LostStepWordIds.ShouldBeEmpty();
        ShouldSatisfyCheck(r);
    }

    [Fact]
    public void One_word_replaced_is_changed_with_rows_and_cleared_part()
    {
        var rows = new[] { Row(1, 10), Row(2, 20, part: "x"), Row(3, 30) };
        var r = RevisionCarry.Carry(Parent("a", "eingeborenen", "c"), rows, Tokens("a", "einzigen", "c"));

        Shape(r).ShouldBe("1:same/1 2:changed/2 3:same/3");
        r.Rows.Single(x => x.Position == 2).StepWordId.ShouldBe(20);
        r.Rows.Single(x => x.Position == 2).GermanWordPart.ShouldBeNull();
        r.Rows.Count.ShouldBe(3);
    }

    [Fact]
    public void Inserted_word_is_new_without_rows()
    {
        var r = RevisionCarry.Carry(Parent("a", "c"), [Row(1, 10), Row(2, 30)], Tokens("a", "b", "c"));

        Shape(r).ShouldBe("1:same/1 2:new/- 3:same/2");
        r.Rows.Select(x => (x.Position, x.StepWordId)).ShouldBe([(1, (int?)10), (3, 30)]);
        r.Report.NewCount.ShouldBe(1);
        ShouldSatisfyCheck(r);
    }

    [Fact]
    public void Deleted_word_loses_its_original_words()
    {
        var r = RevisionCarry.Carry(Parent("a", "b", "c"), [Row(1, 10), Row(2, 20), Row(3, 30)], Tokens("a", "c"));

        Shape(r).ShouldBe("1:same/1 2:same/3");
        r.Report.LostStepWordIds.ShouldBe([20]);
        r.Rows.Count.ShouldBe(2);
    }

    [Fact]
    public void Two_words_to_one_gets_the_union_and_the_first_base()
    {
        var r = RevisionCarry.Carry(Parent("x", "in", "dem", "y"), [Row(2, 20), Row(3, 30), Row(3, 20)], Tokens("x", "im", "y"));

        r.Words[1].ShouldBe(new RevisionCarryWord(2, 2, "changed"));
        r.Rows.Where(x => x.Position == 2).Select(x => x.StepWordId).ShouldBe([20, 30]);
        r.Report.LostStepWordIds.ShouldBeEmpty();
    }

    [Fact]
    public void One_word_to_two_copies_rows_to_both()
    {
        var r = RevisionCarry.Carry(Parent("x", "im", "y"), [Row(2, 20)], Tokens("x", "in", "dem", "y"));

        Shape(r).ShouldBe("1:same/1 2:changed/2 3:changed/2 4:same/3");
        r.Rows.Select(x => (x.Position, x.StepWordId)).ShouldBe([(2, (int?)20), (3, 20)]);
    }

    [Fact]
    public void N_to_m_replace_makes_new_words_and_reports_lost_links()
    {
        var r = RevisionCarry.Carry(Parent("x", "a", "b", "y"), [Row(2, 20), Row(3, 30)], Tokens("x", "c", "d", "y"));

        Shape(r).ShouldBe("1:same/1 2:new/- 3:new/- 4:same/4");
        r.Rows.ShouldBeEmpty();
        r.Report.LostStepWordIds.ShouldBe([20, 30]);
        ShouldSatisfyCheck(r);
    }

    [Fact]
    public void Moved_word_is_same_and_keeps_its_row_and_part()
    {
        var r = RevisionCarry.Carry(Parent("a", "b", "c", "d"), [Row(1, 10, part: "p"), Row(2, 20)], Tokens("b", "c", "d", "a"));

        r.Words.Last().ShouldBe(new RevisionCarryWord(4, 1, "same"));
        r.Rows.Single(x => x.Position == 4).StepWordId.ShouldBe(10);
        r.Rows.Single(x => x.Position == 4).GermanWordPart.ShouldBe("p");
        r.Report.LostStepWordIds.ShouldBeEmpty();
    }

    [Fact]
    public void Added_word_whose_parent_was_deleted_is_kept_with_null_parent()
    {
        var rows = new[] { Row(1, 10), Row(2, 20, added: true, parent: 1) };
        var r = RevisionCarry.Carry(Parent("a", "b"), rows, Tokens("b"));

        r.Rows.ShouldBe([new RevisionCarryRow(1, 20, true, null, null)]);
        r.Report.LostStepWordIds.ShouldBe([10]);
    }

    [Fact]
    public void Added_word_whose_parent_was_carried_points_to_the_new_position()
    {
        var rows = new[] { Row(1, 10), Row(2, 20, added: true, parent: 1) };
        var r = RevisionCarry.Carry(Parent("a", "b"), rows, Tokens("x", "a", "b"));

        r.Rows.Single(x => x.IsAddedWord).ShouldBe(new RevisionCarryRow(3, 20, true, 2, null));
    }

    [Fact]
    public void Compound_part_is_kept_on_same_and_cleared_on_changed()
    {
        var rows = new[] { Row(1, 10, part: "Haus"), Row(2, 20, part: "tür") };
        var r = RevisionCarry.Carry(Parent("a", "b"), rows, Tokens("a", "z"));

        r.Rows.Single(x => x.Position == 1).GermanWordPart.ShouldBe("Haus");
        r.Rows.Single(x => x.Position == 2).GermanWordPart.ShouldBeNull();
    }

    [Fact]
    public void Parent_variant_composes_change_and_base()
    {
        var parent = new List<RevisionCarryParentWord>
        {
            new(1, "a", 1, "same"),
            new(2, "einzigen", 2, "changed"),
            new(3, "extra", null, "new"),
            new(4, "d", 3, "same"),
        };
        var rows = new[] { Row(1, 10), Row(2, 20), Row(4, 30) };
        var r = RevisionCarry.Carry(parent, rows, Tokens("a", "einzigen", "extra", "d"));

        Shape(r).ShouldBe("1:same/1 2:changed/2 3:new/- 4:same/3");
        r.Report.SameCount.ShouldBe(2);
        ShouldSatisfyCheck(r);
    }

    [Fact]
    public void Union_rows_are_deduplicated_per_position_and_original_word()
    {
        var r = RevisionCarry.Carry(Parent("in", "dem"), [Row(1, 5), Row(2, 5)], Tokens("im"));

        r.Rows.Count.ShouldBe(1);
    }
}
