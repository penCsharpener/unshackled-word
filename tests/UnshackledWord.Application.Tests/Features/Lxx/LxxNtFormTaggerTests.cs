using UnshackledWord.Application.Features.Lxx;

namespace UnshackledWord.Application.Tests.Features.Lxx;

/// <summary>Roadmap 0028.6: normalization of running-text forms and the "exactly one NT analysis" rule of pass 1.</summary>
public class LxxNtFormTaggerTests
{
    [Theory]
    [InlineData("τοὺς", "τούς")]                 // grave -> acute
    [InlineData("τούς", "τούς")]
    [InlineData("γῆν.", "γῆν")]                  // punctuation
    [InlineData("στερέωμ[α", "στερέωμα")]        // Swete bracket
    [InlineData("με]γάλα", "μεγάλα")]
    [InlineData("⸂ἐν⸃", "ἐν")]                   // editorial marks
    [InlineData("Βαβυλῶνος.¶", "βαβυλῶνος")]     // STEP paragraph sign
    [InlineData("Ἐν", "ἐν")]                     // sentence capital
    [InlineData("ἀπ᾽", "ἀπ")]                    // elision mark U+1FBD
    [InlineData("ἀπ’", "ἀπ")]                    // elision mark U+2019
    [InlineData("ζῴων", "ζῴων")]                 // iota subscript kept
    [InlineData("ό", "ό")]        // composed tonos, canonical decomposition
    public void Normalize_ReturnsFormInNfdAcuteLowerCase(string word, string expected) =>
        LxxFormNormalizer.Normalize(word).ShouldBe(expected.Normalize(System.Text.NormalizationForm.FormC));

    [Fact]
    public void Normalize_GraveAndOxiaAndTonosAreEqual() =>
        LxxFormNormalizer.Normalize("θεὸς").ShouldBe(LxxFormNormalizer.Normalize("θεός"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[")]
    [InlineData("12")]
    [InlineData("¶")]
    public void Normalize_NoLetters_IsEmpty(string? word) => LxxFormNormalizer.Normalize(word).ShouldBeEmpty();

    [Theory]
    [InlineData("ΑΡΧΗ", true)]
    [InlineData("ΕΝ", true)]
    [InlineData("Ο", true)]
    [InlineData("[ΕΝ", true)]
    [InlineData("Ἐν", false)]                    // breathing: ordinary running text
    [InlineData("ἐν", false)]
    [InlineData("Ιακωβ", false)]                 // mixed case, no marks: not a heading
    [InlineData("", false)]
    [InlineData("[", false)]
    public void IsUnaccentedCapitals_DetectsBookOpenings(string word, bool expected) =>
        LxxFormNormalizer.IsUnaccentedCapitals(word).ShouldBe(expected);

    [Theory]
    [InlineData("G0746", true)]
    [InlineData("G1487M", true)]
    [InlineData("G20447", false)]
    [InlineData("G746", false)]
    [InlineData("H7225", false)]
    [InlineData("G0746m", false)]
    public void IsValidStrongs_MatchesTheTableCheck(string strongs, bool expected) =>
        LxxNtFormTagger.IsValidStrongs(strongs).ShouldBe(expected);

    private static readonly NtFormAnalysis[] Nt =
    [
        new("ἐν", "ἐν", "G1722", "PREP"),
        new("ἐν,", "ἐν", "G1722", "PREP"),                 // same form with punctuation: still one analysis
        new("Ἐν", "ἐν", "G1722", "PREP"),                  // capital: still one analysis
        new("τοὺς", "ὁ", "G3588", "T-APM"),
        new("λόγου", "λόγος", "G3056", "N-GSM"),
        new("αὐτῶν", "αὐτός", "G0846", "P-GPM"),
        new("αὐτῶν", "αὐτός", "G0846", "P-GPF"),           // ambiguous
        new("ὄνομα", "ὄνομα", "G20447", "N-NSN"),          // Strong's fails the CHECK
        new("κακῶς", "κακῶς", "G2560", "V-XYZ"),           // morphology fails the validator
    ];

    private static LxxPrefillWord W(int id, string greek, int book = 1, string form = "") => new(id, book, form, greek);

    [Fact]
    public void Compute_ProposesOnlyForExactlyOneAnalysis()
    {
        var words = new[] { W(1, "ΕΝ"), W(2, "Ἐν"), W(3, "τούς"), W(4, "λόγου,"), W(5, "αὐτῶν"), W(6, "ὄνομα"), W(7, "κακῶς"), W(8, "ἄγνωστον"), W(9, "[") };

        var result = LxxNtFormTagger.Compute(Nt, words, [], out var proposals);

        proposals.Select(p => (p.LxxWordId, p.Lemma, p.Strongs, p.Morphology)).ShouldBe(
        [
            (2, "ἐν", "G1722", "PREP"),
            (3, "ὁ", "G3588", "T-APM"),
            (4, "λόγος", "G3056", "N-GSM"),
        ]);
        result.TotalWords.ShouldBe(9);
        result.SkippedCapitals.ShouldBe(1);
        result.Ambiguous.ShouldBe(1);
        result.Invalid.ShouldBe(2);
        result.Unknown.ShouldBe(1);
        result.Empty.ShouldBe(1);
        result.Proposed.ShouldBe(3);
        result.Covered.ShouldBe(3);
    }

    [Fact]
    public void Compute_KeepsConfirmedAiAndHumanRows_ButReplacesOwnProposals()
    {
        var words = new[] { W(1, "λόγου"), W(2, "λόγου"), W(3, "λόγου"), W(4, "λόγου") };
        var tags = new LxxExistingTag[] { new(1, "confirmed", "nt-form"), new(2, "proposed", "ai"), new(3, "confirmed", "human"), new(4, "proposed", "nt-form") };

        var result = LxxNtFormTagger.Compute(Nt, words, tags, out var proposals);

        proposals.Select(p => p.LxxWordId).ShouldBe([4]);
        result.KeptExisting.ShouldBe(3);
        result.Covered.ShouldBe(4);
    }

    [Fact]
    public void Compute_CoverageIsCountedPerBookAndTextForm()
    {
        var words = new[] { W(1, "λόγου", 1), W(2, "αὐτῶν", 1), W(3, "λόγου", 27, "og"), W(4, "λόγου", 27, "th") };

        var result = LxxNtFormTagger.Compute(Nt, words, [], out _);

        result.Books.ShouldBe(
        [
            new LxxPrefillBookCoverage(1, "", 2, 1, 1),
            new LxxPrefillBookCoverage(27, "og", 1, 1, 1),
            new LxxPrefillBookCoverage(27, "th", 1, 1, 1),
        ]);
    }

    [Fact]
    public async Task RunAsync_WritesWhatComputeFound()
    {
        var repository = new FakeRepository(Nt, [W(1, "λόγου"), W(2, "αὐτῶν")]);
        var tagger = new LxxNtFormTagger(repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<LxxNtFormTagger>.Instance);
        var lines = new List<string>();

        var result = await tagger.RunAsync(new Progress<string>(lines.Add), TestContext.Current.CancellationToken);

        repository.Applied.ShouldNotBeNull().Select(p => p.LxxWordId).ShouldBe([1]);
        result.Inserted.ShouldBe(1);
        await Task.Yield();
    }

    private sealed class FakeRepository(IReadOnlyList<NtFormAnalysis> analyses, IReadOnlyList<LxxPrefillWord> words) : ILxxNtFormRepository
    {
        public IReadOnlyList<LxxNtFormProposal>? Applied { get; private set; }

        public Task<IReadOnlyList<NtFormAnalysis>> GetNtAnalysesAsync(CancellationToken token = default) => Task.FromResult(analyses);

        public Task<IReadOnlyList<LxxPrefillWord>> GetLxxWordsAsync(CancellationToken token = default) => Task.FromResult(words);

        public Task<IReadOnlyList<LxxExistingTag>> GetTagsAsync(CancellationToken token = default) => Task.FromResult<IReadOnlyList<LxxExistingTag>>([]);

        public Task<LxxStatusCounts> GetStatusCountsAsync(CancellationToken token = default) => Task.FromResult(new LxxStatusCounts(0, 0, []));

        public Task<LxxNtFormApplyResult> ApplyAsync(IReadOnlyList<LxxNtFormProposal> proposals, CancellationToken token = default)
        {
            Applied = proposals;
            return Task.FromResult(new LxxNtFormApplyResult(proposals.Count, 0, 0));
        }
    }
}
