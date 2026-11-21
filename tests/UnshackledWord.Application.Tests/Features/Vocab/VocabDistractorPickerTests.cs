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
}
