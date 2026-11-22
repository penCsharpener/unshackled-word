using UnshackledWord.Application.Features.Vocab.Session;

namespace UnshackledWord.Application.Tests.Features.Vocab;

public class VocabDistractorPickerTests
{
    private static VocabDistractorCandidate Candidate(int id, string meaning, string? pos = "G:V", int rank = 100) => new(id, pos, rank, meaning);

    private static VocabDistractorPrompt Prompt(params string[] accepted) =>
        new(1, "G:V", 100, accepted[0], accepted);

    [Fact]
    public void Entry_with_an_overlapping_meaning_is_never_a_distractor()
    {
        // λέγω accepts "sagen" and "sprechen"; εἶπον shows "sagen" as its top meaning and must not appear as a wrong option
        var prompt = Prompt("sagen", "reden");
        var pool = new[]
        {
            Candidate(2, "Sagen"),     // case-folded equal to an accepted meaning of the prompt
            Candidate(3, "reden"),
            Candidate(4, "gehen"),
            Candidate(5, "kommen"),
            Candidate(6, "sehen"),
        };

        for (var seed = 0; seed < 50; seed++)
        {
            var picked = VocabDistractorPicker.Pick(prompt, pool, new HashSet<int>(), 3, new Random(seed));

            picked.Select(p => p.EntryId).Order().ShouldBe([4, 5, 6]);
        }
    }

    [Fact]
    public void Distractor_shown_text_never_equals_the_correct_option_or_another_distractor()
    {
        var prompt = Prompt("sagen");
        var pool = new[] { Candidate(2, "gehen"), Candidate(3, "GEHEN"), Candidate(4, " gehen "), Candidate(5, "sehen"), Candidate(6, "kommen") };

        var picked = VocabDistractorPicker.Pick(prompt, pool, new HashSet<int>(), 3, new Random(1));

        picked.Count.ShouldBe(3);
        picked.Select(p => VocabDistractorPicker.Fold(p.TopMeaning)).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public void Tiers_are_additive_wave_first_then_nearby_same_pos_then_same_pos_then_anything()
    {
        var prompt = Prompt("sagen");
        var wave = Candidate(10, "Wort", "G:N", 5000);
        var nearby = Candidate(20, "gehen", "G:V", 150);
        var samePos = Candidate(30, "kommen", "G:V", 4000);
        var other = Candidate(40, "Haus", "G:N", 4000);
        var pool = new[] { other, samePos, nearby, wave };

        var picked = VocabDistractorPicker.Pick(prompt, pool, new HashSet<int> { 10 }, 3, new Random(0));

        picked.Select(p => p.EntryId).ShouldBe([10, 20, 30]);
        VocabDistractorPicker.Pick(prompt, pool, new HashSet<int> { 10 }, 4, new Random(0)).Select(p => p.EntryId).ShouldBe([10, 20, 30, 40]);
    }

    [Fact]
    public void Fewer_candidates_than_asked_returns_what_exists_and_never_the_prompt_entry()
    {
        var prompt = Prompt("sagen");
        var pool = new[] { new VocabDistractorCandidate(1, "G:V", 100, "anders"), Candidate(2, "gehen") };

        var picked = VocabDistractorPicker.Pick(prompt, pool, new HashSet<int>(), 3, new Random(0));

        picked.Select(p => p.EntryId).ShouldBe([2]);
    }

    private static VocabDistractorCandidate Lemma(int id, string form, params string[] meanings) =>
        new(id, "G:V", 100, meanings[0], meanings, form);

    [Fact]
    public void Reverse_distractor_never_has_the_prompt_meaning_among_its_accepted_meanings()
    {
        // λέγω (prompt "sagen") must not be shown next to εἶπον, which accepts "sagen" as a second meaning, even if its top meaning differs
        var prompt = Prompt("sagen", "reden");
        var pool = new[]
        {
            Lemma(2, "εἶπον", "sprechen", "Sagen"),
            Lemma(3, "ἔρχομαι", "kommen"),
            Lemma(4, "βλέπω", "sehen"),
            Lemma(5, "πορεύομαι", "gehen"),
            Lemma(6, "λέγω", "sagen"),
        };

        for (var seed = 0; seed < 50; seed++)
        {
            var picked = VocabDistractorPicker.PickLemmas(prompt, "λέγω", pool, new HashSet<int>(), 3, new Random(seed));

            picked.Select(p => p.EntryId).Order().ShouldBe([3, 4, 5]);
        }
    }

    [Fact]
    public void Reverse_distractor_forms_are_unique_and_differ_from_the_correct_form()
    {
        var prompt = Prompt("sagen");
        var pool = new[] { Lemma(2, "ἔρχομαι", "kommen"), Lemma(3, "ἐρχομαι", "gehen"), Lemma(4, "ΒΛΈΠΩ", "sehen"), Lemma(5, "βλέπω", "schauen"), Lemma(6, "λέγω", "reden") };

        var picked = VocabDistractorPicker.PickLemmas(prompt, "λέγω", pool, new HashSet<int>(), 3, new Random(1));

        picked.Select(p => VocabDistractorPicker.Fold(p.Form!)).Distinct().Count().ShouldBe(picked.Count);
        picked.ShouldNotContain(p => p.EntryId == 6);
    }
}
