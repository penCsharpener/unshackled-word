using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

/// <summary>Packages of run mode <c>lxx-tag</c> (roadmap 0028.7) over the real Ruth/Genesis/Job rows of the fixture.</summary>
public class LxxAgentPackageBuilderTests
{
    [Fact]
    public async Task RuthOneOne_HasTheSweteWords_PrefillsAndCandidates_AndTheHebrewVerse()
    {
        var data = new LxxAgentTestData();
        var (envelope, map) = await data.BuildOneAsync(LxxAgentTestData.Ruth, 1, 1, 1);
        var unit = data.Unit(LxxAgentTestData.Ruth, 1, 1);

        envelope.FromRefId.ShouldBe(8_001_001);
        envelope.ToRefId.ShouldBe(8_001_001);
        envelope.Package.Lang.ShouldBe("lxx");
        var verse = envelope.Package.Verses.Single();
        verse.Ref.ShouldBe("Rt 1:1");
        verse.HebRef.ShouldBe("Rt 1:1");
        verse.Map.ShouldBeNull();

        // Swete words by position: ΚΑΙ (an unaccented opening, no prefill) gets καί as first candidate; ἐγένετο the pass-1 prefill; Μωάβ nothing.
        verse.Lxx!.Count.ShouldBe(34);
        Row(verse, 1)[1].ShouldBe("ΚΑΙ");
        Row(verse, 1)[2].ShouldBeNull();
        ((object[][])Row(verse, 1)[3]!)[0].ShouldBe(["G2532", "καί", "and"]);
        ((object[])Row(verse, 2)[2]!).ShouldBe(["γίνομαι", "G1096", "V-2ADI-3S"]);
        Row(verse, 25).Length.ShouldBe(2, "Μωάβ: no prefill, no candidate");
        Row(verse, 25)[1].ShouldBe(data.Words.Single(w => w.LxxVerseId == unit.Id && w.Position == 25).Greek);
        verse.Lxx.Where(r => r.Length > 3).ShouldAllBe(r => ((object[][])r[3]!).Length <= LxxCandidateIndex.MaxCandidates);

        // The Hebrew verse of HebRefId 8001001: 19 STEP words numbered 1.., morphemes only for words with a prefix or suffix.
        verse.Heb!.Count.ShouldBe(19);
        verse.Heb[0].ShouldBe([1, "וַיְהִ֗י", "H1961_A", "and/ it was", "Hc/Vqw3ms", new[] { "H9001", "{H1961}" }]);
        verse.Heb[2].Length.ShouldBe(5, "שְׁפֹט has no prefix");

        var json = LxxAgentTestData.Serialize(envelope.Package);
        json.ShouldNotContain("\"de\"");
        json.ShouldNotContain("\"src\"");
        json.ShouldNotContain("\"fixed\"");
        json.ShouldNotContain("\"hints\"");

        var unitMap = map.LxxVerses!["v1"];
        map.Mode.ShouldBe(AgentModes.LxxTag);
        map.Language.ShouldBe("lxx");
        map.Verses.ShouldBeEmpty();
        unitMap.LxxVerseId.ShouldBe(unit.Id);
        unitMap.HebRefId.ShouldBe(8_001_001);
        unitMap.Words.Count.ShouldBe(34);
        unitMap.Words[1].ShouldBe(data.Words.Single(w => w.LxxVerseId == unit.Id && w.Position == 1).Id);
        unitMap.Hebrew[1].ShouldBe(data.Hebrew.Single(h => h.RefId == 8_001_001 && h.Position == 1).Id);
        unitMap.Prefill[2].Strongs.ShouldBe("G1096");
        unitMap.Prefill.ContainsKey(1).ShouldBeFalse();

        envelope.Stats.GermanTokens.ShouldBe(34, "LXX words");
        envelope.Stats.Hint.ShouldBe(18, "the pass-1 proposals of Ruth 1:1");
        envelope.Stats.Open.ShouldBe(16);
        envelope.EstimatedTokens.ShouldBeGreaterThan(500);
    }

    [Fact]
    public async Task AnAddition_HasNoHebrew()
    {
        var data = new LxxAgentTestData();
        var (envelope, map) = await data.BuildOneAsync(LxxAgentTestData.Job, 36, 28, 28);

        var verse = envelope.Package.Verses.Single();
        verse.Ref.ShouldBe("Hi 36:28.1");
        verse.HebRef.ShouldBeNull();
        verse.Heb.ShouldBeNull();
        LxxAgentTestData.Serialize(envelope.Package).ShouldNotContain("heb");
        map.LxxVerses!["v1"].Hebrew.ShouldBeEmpty();
        map.LxxVerses["v1"].Subverse.ShouldBe(1);
    }

    [Fact]
    public async Task APrefill_ShowsTheTbesgLemma()
    {
        var data = new LxxAgentTestData();
        // STEP writes some lemmas otherwise than TBESG (G2036 as a list, G3165 ἐγώ for μέ): the prefill must show what the validator accepts.
        var word = data.Words.First(w => w.LxxVerseId == data.Unit(LxxAgentTestData.Genesis, 1, 3).Id && w.Position == 2);
        word.TagLemma.ShouldBe("ἔπω, ἐρῶ, εἶπον");

        var (envelope, _) = await data.BuildOneAsync(LxxAgentTestData.Genesis, 1, 3, 3);

        ((object[])Row(envelope.Package.Verses.Single(), 2)[2]!)[0].ShouldBe("εἶπον");
    }

    [Fact]
    public async Task Packages_StayInsideAChapter_HoldAtMost40Units_AndKeepSubversesTogether()
    {
        var data = new LxxAgentTestData();
        var template = data.Unit(LxxAgentTestData.Ruth, 1, 1);
        var sort = 900_000;
        var id = 900_000;
        foreach (var (verse, subverse) in Enumerable.Range(1, 45).SelectMany(v => v == 40 ? new[] { (v, 0), (v, 1) } : [(v, 0)]))
        {
            var unit = new LxxAgentUnitRow { Id = ++id, BibleBookId = 8, Chapter = 2, Verse = verse, Subverse = subverse, MapKind = "one", SortOrder = ++sort, HebRefId = null };
            data.Units.Add(unit);
            data.Words.Add(new LxxAgentWordRow { Id = id, LxxVerseId = id, Position = 1, Greek = "καὶ" });
        }

        var (builder, packages, _) = data.Builder();
        var built = await builder.BuildAsync(new AgentBuildRequest
        {
            Range = new AgentRange { BibleBookId = 8, FromChapter = 1, FromVerse = 1, ToChapter = 2, ToVerse = 999 },
            Mode = AgentMode.LxxTag,
            UserId = 3,
        });

        built!.Packages.Select(p => p.Package.Verses.Count).ShouldBe([3, 41, 5]);
        built.Packages[1].Package.Verses[^1].Ref.ShouldBe("Rt 2:40.1");
        built.Packages.Select(p => (p.FromRefId, p.ToRefId)).ShouldBe([(8_001_001, 8_001_004), (8_002_001, 8_002_040), (8_002_041, 8_002_045)]);
        packages.Count.ShouldBe(3);
        built.Mode.ShouldBe(AgentModes.LxxTag);
        built.Stats.Verses.ShouldBe(49);

        var smaller = await builder.BuildAsync(new AgentBuildRequest
        {
            Range = new AgentRange { BibleBookId = 8, FromChapter = 1, FromVerse = 1, ToChapter = 1, ToVerse = 999 },
            Mode = AgentMode.LxxTag,
            MaxVersesPerPackage = 2,
            DryRun = true,
        });
        smaller!.Packages.Select(p => p.Package.Verses.Count).ShouldBe([2, 1]);
        packages.Count.ShouldBe(3, "a dry run stores nothing");
        data.LexiconLoads.ShouldBe(1, "the candidate index is built once per builder");
    }

    [Fact]
    public async Task Resume_SkipsUnitsThatHaveAProposalOfTheRun()
    {
        var data = new LxxAgentTestData();
        data.Proposed.Add(data.Unit(LxxAgentTestData.Ruth, 1, 1).Id);
        var (builder, _, store) = data.Builder();

        var built = await builder.BuildAsync(new AgentBuildRequest
        {
            Range = new AgentRange { BibleBookId = 8, FromChapter = 1, FromVerse = 1, ToChapter = 1, ToVerse = 4 },
            Mode = AgentMode.LxxTag,
            ExistingRunId = 5,
            // The run loop's verse-level skip set is ignored: subverses and Daniel's text forms share a BBCCCVVV.
            SkipRefIds = new HashSet<int> { 8_001_002 },
        });

        built!.Packages.Single().Package.Verses.Select(v => v.Ref).ShouldBe(["Rt 1:2", "Rt 1:4"]);
        await store.DidNotReceive().CreateRunAsync(Arg.Any<AgentRunInsert>(), Arg.Any<CancellationToken>());
    }

    private static void Confirm(LxxAgentTestData data, LxxAgentUnitRow unit, Func<LxxAgentWordRow, bool>? which = null)
    {
        // The fixture list is shared between tests: replace rows, never mutate them.
        for (var i = 0; i < data.Words.Count; i++)
        {
            var w = data.Words[i];
            if (w.LxxVerseId == unit.Id && (which?.Invoke(w) ?? true))
            {
                data.Words[i] = new LxxAgentWordRow
                {
                    Id = w.Id, LxxVerseId = w.LxxVerseId, Position = w.Position, Greek = w.Greek,
                    TagLemma = "καί", TagStrongs = "G2532", TagMorphology = "CONJ", TagStatus = "confirmed", TagSource = "human",
                };
            }
        }
    }

    private static AgentBuildRequest Ruth1To4(bool includeConfirmed = false, string? textForm = null) => new()
    {
        Range = new AgentRange { BibleBookId = 8, FromChapter = 1, FromVerse = 1, ToChapter = 1, ToVerse = 4 },
        Mode = AgentMode.LxxTag,
        DryRun = true,
        IncludeConfirmed = includeConfirmed,
        TextForm = textForm,
    };

    [Fact]
    public async Task UnitsWhoseWordsAreAllConfirmed_AreSkippedByDefault_AndSentWithIncludeConfirmed()
    {
        var data = new LxxAgentTestData();
        Confirm(data, data.Unit(LxxAgentTestData.Ruth, 1, 2));
        var (builder, _, _) = data.Builder();

        var skipped = await builder.BuildAsync(Ruth1To4());
        var included = await builder.BuildAsync(Ruth1To4(includeConfirmed: true));

        skipped!.Packages.Single().Package.Verses.Select(v => v.Ref).ShouldBe(["Rt 1:1", "Rt 1:4"]);
        skipped.Stats.Verses.ShouldBe(2);
        included!.Packages.Single().Package.Verses.Select(v => v.Ref).ShouldBe(["Rt 1:1", "Rt 1:2", "Rt 1:4"]);
    }

    [Fact]
    public async Task TextForm_BuildsOnlyTheUnitsOfThatForm()
    {
        var data = new LxxAgentTestData();
        data.Unit(LxxAgentTestData.Ruth, 1, 1).TextForm = "og";
        data.Unit(LxxAgentTestData.Ruth, 1, 2).TextForm = "th";
        data.Unit(LxxAgentTestData.Ruth, 1, 4).TextForm = "og";
        var (builder, _, _) = data.Builder();

        var both = await builder.BuildAsync(Ruth1To4(includeConfirmed: true));
        var og = await builder.BuildAsync(Ruth1To4(textForm: "og"));
        var th = await builder.BuildAsync(Ruth1To4(textForm: "th"));

        both!.Packages.Single().Package.Verses.Select(v => v.Ref).Count().ShouldBeGreaterThanOrEqualTo(3);
        og!.Packages.Single().Package.Verses.Select(v => v.Ref).ShouldBe(["Rt 1:1 (og)", "Rt 1:4 (og)"]);
        th!.Packages.Single().Package.Verses.Select(v => v.Ref).ShouldBe(["Rt 1:2 (th)"]);
        (await builder.BuildAsync(Ruth1To4(textForm: "th")))!.Stats.Verses.ShouldBe(1);
    }

    [Fact]
    public async Task AUnitWithSomeConfirmedWords_IsSent_WithThoseWordsMarkedConfirmed()
    {
        var data = new LxxAgentTestData();
        Confirm(data, data.Unit(LxxAgentTestData.Ruth, 1, 4), w => w.Position == 1);
        var (builder, _, _) = data.Builder();

        var built = await builder.BuildAsync(Ruth1To4());

        var verse = built!.Packages.Single().Package.Verses.Single(v => v.Ref == "Rt 1:4");
        verse.Lxx!.Single(r => (int)r[0] == 1)[2].ShouldBe(new object[] { "καί", "G2532", "CONJ", "confirmed" });
        verse.Lxx!.Where(r => r.Length > 2 && r[2] is object[] a && a.Length == 4).Count().ShouldBe(1);
        built.Packages.Single().Stats.Fixed.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task TheMappingBuilder_HandsLxxRequestsOn()
    {
        var data = new LxxAgentTestData();
        var (lxx, _, store) = data.Builder();
        var mapping = new AgentPackageBuilder(Substitute.For<IAgentDataRepository>(), store, Substitute.For<IConfiguration>(), TimeProvider.System, lxx);

        var built = await mapping.BuildAsync(new AgentBuildRequest
        {
            Range = new AgentRange { BibleBookId = 8, FromChapter = 1, FromVerse = 1, ToChapter = 1, ToVerse = 2 },
            Mode = AgentMode.LxxTag,
            DryRun = true,
        });

        built!.Packages.Single().Package.Lang.ShouldBe("lxx");
        (await new AgentPackageBuilder(Substitute.For<IAgentDataRepository>(), store, Substitute.For<IConfiguration>(), TimeProvider.System).BuildAsync(new AgentBuildRequest
        {
            Range = new AgentRange { BibleBookId = 8, FromChapter = 1, FromVerse = 1, ToChapter = 1, ToVerse = 2 },
            Mode = AgentMode.LxxTag,
            DryRun = true,
        })).ShouldBeNull("without an LXX builder there are no LXX packages");
    }

    [Fact]
    public void CandidateIndex_FindsFormsAccentlessCapitalsAndStems()
    {
        var data = new LxxAgentTestData();
        var index = new LxxCandidateIndex(LxxLexicon.Build(data.Lexicon), data.NtForms);

        index.Candidates("ΑΡΧΗ")[0].Strongs.ShouldBe("G0746");
        index.Candidates("ἐλάβοσαν")[0].Strongs.ShouldBe("G2983", "the NT stem ἔλαβ- of λαμβάνω");
        index.Candidates("κατῴκησαν")[0].Strongs.ShouldBe("G2730");
        index.Candidates("Μωάβ,").ShouldBeEmpty("TBESG has no Moab");
        index.Candidates("τὸν").Select(c => c.Strongs).ShouldBe(["G3588"]);
    }

    [Theory]
    [InlineData("G746", "G0746")]
    [InlineData("g1487g", "G1487G")]
    [InlineData("G20969", "G20969")]
    [InlineData("G00746", "G0746")]
    [InlineData("H0746", null)]
    [InlineData("G1234567", null)]
    [InlineData("", null)]
    public void NormalizeStrongs(string input, string? expected) => LxxLexicon.NormalizeStrongs(input).ShouldBe(expected);

    private static object?[] Row(AgentVerseDto verse, int position) => verse.Lxx!.Single(r => (int)r[0]! == position);
}
