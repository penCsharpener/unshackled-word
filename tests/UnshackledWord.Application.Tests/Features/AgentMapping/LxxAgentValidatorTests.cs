using System.Text.Json;
using System.Text.Json.Nodes;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

/// <summary>
/// The <c>lxx</c> branch of <see cref="AgentProposalValidator"/> (roadmap 0028.7) on Ruth 1:1 of the fixture: a failing word is dropped with a flag and the
/// rest of the verse is kept; only an unreadable verse, a verse answered twice or one without any good word is rejected.
/// </summary>
public class LxxAgentValidatorTests
{
    private readonly LxxAgentTestData _data = new();

    [Fact]
    public async Task AGoodAnswer_IsAccepted_WithRealIdsTbesgLemmasAndHebrewLinks()
    {
        var (map, answer) = await RuthOneOneAsync();

        var result = Validate(map, answer);

        result.Status.ShouldBe(AgentVerseStatus.Flagged, "Μωάβ has no TBESG entry");
        result.Flags.Select(f => (f.Code, f.Position)).ShouldBe([(LxxFlagCodes.NoStrongs, 25)]);
        var unit = map.LxxVerses!["v1"];
        var words = result.Proposal!.Words;
        words.Count.ShouldBe(33);
        words.ShouldAllBe(w => w.LxxWordId == unit.Words[w.Position]);
        words.Single(w => w.Position == 2).Prefilled.ShouldBeTrue();
        words.Single(w => w.Position == 1).ShouldSatisfyAllConditions(w => w.Lemma.ShouldBe("καί"), w => w.Strongs.ShouldBe("G2532"), w => w.Prefilled.ShouldBeFalse());
        var link = result.Proposal.Links.Single(l => l.Position == 1);
        link.HebNumber.ShouldBe(1);
        link.StepHebrewWordId.ShouldBe(unit.Hebrew[1]);
    }

    [Fact]
    public async Task UnknownStrongs_WrongLemma_BadCode_AndUnknownHebrew_DropOnlyThoseWords()
    {
        var (map, answer) = await RuthOneOneAsync();
        Set(answer, 3, strongs: "G99999");           // not in TBESG
        Set(answer, 5, morph: "V-XYZ");              // not a Robinson code
        Set(answer, 7, heb: [99]);                   // the Hebrew verse has 19 words
        Set(answer, 8, lemma: "λόγος");              // G2532 is καί

        var result = Validate(map, answer);

        result.Status.ShouldBe(AgentVerseStatus.Flagged);
        result.Flags.Select(f => (f.Code, f.Position)).ShouldBe([
            (LxxFlagCodes.StrongsUnknown, 3), (LxxFlagCodes.MorphInvalid, 5), (LxxFlagCodes.HebUnknown, 7), (LxxFlagCodes.LemmaMismatch, 8), (LxxFlagCodes.NoStrongs, 25),
        ]);
        result.Flags.Single(f => f.Position == 5).Message.ShouldContain("V-XYZ");
        result.Flags.Single(f => f.Position == 8).Message.ShouldContain("καί");
        result.Proposal!.Words.Count.ShouldBe(29);
        result.Proposal.Words.Select(w => w.Position).ShouldNotContain(3);
        result.Proposal.Links.Select(l => l.Position).ShouldNotContain(7);
    }

    [Fact]
    public async Task MissingAndDuplicateWords_AreFlagged_TheRestKept()
    {
        var (map, answer) = await RuthOneOneAsync();
        var words = Words(answer);
        words.RemoveAt(9);                            // position 10, λιμὸς
        words.Add(words[0]!.DeepClone());             // position 1 twice

        var result = Validate(map, answer);

        result.Flags.Select(f => (f.Code, f.Position)).ShouldBe([(LxxFlagCodes.WordDuplicate, 1), (LxxFlagCodes.NoStrongs, 25), (LxxFlagCodes.WordMissing, 10)]);
        result.Proposal!.Words.Count.ShouldBe(31);
    }

    [Fact]
    public async Task UnknownPositions_ShapeErrors_AndOddConfidence()
    {
        var (map, answer) = await RuthOneOneAsync();
        Words(answer).Add(new JsonArray(77, "καί", "G2532", "CONJ", new JsonArray(), "high"));
        Words(answer)[1] = new JsonArray(2, "γίνομαι");
        Set(answer, 4, confidence: "maybe");
        Set(answer, 6, strongs: "g3588");

        var result = Validate(map, answer);

        result.Flags.Select(f => (f.Code, f.Position)).ShouldBe([(LxxFlagCodes.RowShape, 2), (LxxFlagCodes.Confidence, 4), (LxxFlagCodes.NoStrongs, 25), (LxxFlagCodes.WordUnknown, 77)]);
        result.Proposal!.Words.Single(w => w.Position == 4).Confidence.ShouldBe("low");
        result.Proposal.Words.Single(w => w.Position == 6).Strongs.ShouldBe("G3588", "g3588 is normalised");
    }

    [Fact]
    public async Task AVerseWithoutAnyGoodWord_TwiceAnswered_OrWithoutWords_IsRejected()
    {
        var (map, answer) = await RuthOneOneAsync();
        foreach (var row in Words(answer))
        {
            row![2] = "G99999";
        }

        var none = Validate(map, answer);
        none.Status.ShouldBe(AgentVerseStatus.Rejected);
        none.Messages[0].ShouldBe("v1: no word passed the checks.");
        none.Messages.Count.ShouldBe(35);
        none.Proposal.ShouldBeNull();

        var twice = Parse("""{"packageId":"p","verses":[{"v":"v1","words":[]},{"v":"v1","words":[]}]}""");
        new AgentProposalValidator().ValidateLxx(map, twice, Lexicon()).Single(r => r.V == "v1").Status.ShouldBe(AgentVerseStatus.Rejected);

        var noWords = Parse("""{"packageId":"p","verses":[{"v":"v1"},{"v":"v9","words":[]}]}""");
        var results = new AgentProposalValidator().ValidateLxx(map, noWords, Lexicon());
        results.Single(r => r.V == "v1").Messages.Single().ShouldContain("words must be an array");
        results.Single(r => r.V == "v9").Messages.Single().ShouldBe("v9: not a verse of this package.");
    }

    [Fact]
    public void AdditionsTakeNoHebrew()
    {
        var map = new AgentPackageMap { Mode = AgentModes.LxxTag, Language = "lxx", BookId = 18, LxxVerses = new() { ["v1"] = new LxxVerseMap { Ref = "Hi 36:28.1", Words = { [1] = 10, [2] = 11 } } } };
        var answer = Parse("""{"packageId":"p","verses":[{"v":"v1","words":[[1,"ὥρα","G5610","N-ASF",[],"high"],[2,"τίθημι","G5087","V-2AMI-3S",[1],"high"]]}]}""");

        var result = new AgentProposalValidator().ValidateLxx(map, answer, Lexicon()).Single();

        result.Flags.Single().ShouldSatisfyAllConditions(f => f.Code.ShouldBe(LxxFlagCodes.HebUnknown), f => f.Message.ShouldContain("no Hebrew verse"));
        result.Proposal!.Words.Single().Position.ShouldBe(1);
    }

    private LxxLexicon Lexicon() => LxxLexicon.Build(_data.Lexicon);

    private LxxVerseValidation Validate(AgentPackageMap map, JsonObject answer) =>
        new AgentProposalValidator().ValidateLxx(map, LxxAgentAnswerParser.Parse(JsonSerializer.SerializeToElement(answer)), Lexicon()).Single();

    private static LxxAgentAnswer Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return LxxAgentAnswerParser.Parse(document.RootElement.Clone());
    }

    /// <summary>Ruth 1:1 and an answer that takes every prefill and otherwise the first candidate (morph N-NSM), with Hebrew 1 for word 1.</summary>
    private async Task<(AgentPackageMap Map, JsonObject Answer)> RuthOneOneAsync()
    {
        var (envelope, map) = await _data.BuildOneAsync(LxxAgentTestData.Ruth, 1, 1, 1);
        var package = JsonNode.Parse(LxxAgentTestData.Serialize(envelope.Package))!;
        return (map, FakeModel.Answer(package));
    }

    private static JsonArray Words(JsonObject answer) => answer["verses"]![0]!["words"]!.AsArray();

    private static void Set(JsonObject answer, int position, string? lemma = null, string? strongs = null, string? morph = null, int[]? heb = null, string? confidence = null)
    {
        var row = Words(answer).Single(r => r![0]!.GetValue<int>() == position)!.AsArray();
        if (lemma is not null) row[1] = lemma;
        if (strongs is not null) row[2] = strongs;
        if (morph is not null) row[3] = morph;
        if (heb is not null) row[4] = new JsonArray(heb.Select(h => (JsonNode?)h).ToArray());
        if (confidence is not null) row[5] = confidence;
    }
}

/// <summary>
/// A stand-in for the model in tests (no provider is ever called): answers every LXX word of a package with its prefill, else its first candidate and
/// <c>N-NSM</c>, else an empty Strong's; the first word of a verse links to Hebrew 1 when the verse has Hebrew.
/// </summary>
public static class FakeModel
{
    public static JsonObject Answer(JsonNode package)
    {
        var verses = new JsonArray();
        foreach (var verse in package["verses"]!.AsArray())
        {
            var hasHebrew = verse!["heb"] is not null;
            var words = new JsonArray();
            foreach (var row in verse["lxx"]!.AsArray())
            {
                var cells = row!.AsArray();
                var position = cells[0]!.GetValue<int>();
                var heb = new JsonArray();
                if (position == 1 && hasHebrew)
                {
                    heb.Add(1);
                }

                if (cells.Count > 2 && cells[2] is JsonArray pre)
                {
                    words.Add(new JsonArray(position, pre[0]!.GetValue<string>(), pre[1]!.GetValue<string>(), pre[2]!.GetValue<string>(), heb, "high"));
                }
                else if (cells.Count > 3 && cells[3] is JsonArray candidates)
                {
                    var first = candidates[0]!.AsArray();
                    words.Add(new JsonArray(position, first[1]!.GetValue<string>(), first[0]!.GetValue<string>(), "N-NSM", heb, "low"));
                }
                else
                {
                    words.Add(new JsonArray(position, "", "", "N-NSM", heb, "low"));
                }
            }

            verses.Add(new JsonObject { ["v"] = verse["v"]!.GetValue<string>(), ["words"] = words });
        }

        return new JsonObject { ["packageId"] = package["packageId"]!.GetValue<string>(), ["verses"] = verses };
    }
}
