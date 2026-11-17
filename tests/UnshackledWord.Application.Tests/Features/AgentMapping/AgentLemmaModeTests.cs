using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Domain.WebApi.Maintainer;
using static UnshackledWord.Application.Tests.Features.AgentMapping.AgentTestData;
using static UnshackledWord.Application.Tests.Features.AgentMapping.LemmaExampleVerses;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

/// <summary>Run mode <c>lemma</c> (roadmap 0018.8) on the 10 example verses of 0016: work filter, package, validator, merge.</summary>
public class AgentLemmaModeTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private static string G(AgentPackageMap map, string text, int nth = 0) => map.Verses["v1"].German.Where(g => g.Value.Text == text).ElementAt(nth).Key;

    private static AgentVerseValidation ValidateOne(AgentPackageMap map, string verseJson, AgentValidationContext? context = null) =>
        Verse(new AgentProposalValidator().Validate(map, Answer(verseJson), context ?? Context()), "v1");

    private static async Task<AgentPackageMap> MapAsync(int book, int chapter, int verse, Action<ExampleVerse>? change = null)
    {
        var package = await PackageAsync(book, chapter, verse, [], change);
        package.ShouldNotBeNull();
        return package.Value.Map;
    }

    // ---- work filter and package -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Build_TrustedLemmas_SendsOnlyVersesWithWork_AndCountsTheSkippedOnes()
    {
        var builder = Builder(TrustedLexicon());
        var sent = new List<string>();
        var skipped = 0;

        foreach (var book in new[] { Mark, Genesis, Ruth })
        {
            var verses = Verses.Where(v => v.Book == book).ToList();
            var built = await builder.BuildAsync(new AgentBuildRequest
            {
                Range = new AgentRange { BibleBookId = book, FromChapter = 1, FromVerse = verses.Min(v => v.Verse), ToChapter = 1, ToVerse = verses.Max(v => v.Verse) },
                Mode = AgentMode.Lemma,
                DryRun = true,
            });

            built.ShouldNotBeNull();
            built.Mode.ShouldBe("lemma");
            skipped += built.Stats.SkippedVerses;
            sent.AddRange(built.Packages.SelectMany(p => p.Package.Verses).Select(v => $"{v.Ref} {string.Join(",", v.Work!)}"));
        }

        // Mk 1:1, 1:6 and 1:8 have no work once every lemma is trusted: the role rules decide habe/wird (rule 2), no particle, no homograph.
        sent.ShouldBe(
        [
            "Mk 1:29 ptkvz",
            "Mk 1:35 roles,ptkvz,homograph",
            "1Mo 1:1 homograph",
            "1Mo 1:3 homograph",
            "Rt 1:4 roles",
            "Rt 1:5 roles,ptkvz,homograph",
            "Rt 1:22 roles,ptkvz",
        ]);
        skipped.ShouldBe(3);
    }

    [Fact]
    public async Task Build_LemmaSharpOnly_SendsEveryExampleVerse()
    {
        foreach (var verse in Verses)
        {
            var package = await PackageAsync(verse.Book, verse.Chapter, verse.Verse);

            package.ShouldNotBeNull($"{verse.Book} {verse.Chapter}:{verse.Verse}");
            package.Value.Envelope.Package.Verses[0].Work!.ShouldContain(AgentLemmaWork.LemmaSharp);
        }
    }

    [Fact]
    public async Task Build_GreekPackage_SendsFixedLinksWithRoles_AuxiliaryDecidedByTheRules()
    {
        var package = await PackageAsync(Mark, 1, 8);
        package.ShouldNotBeNull();
        var (envelope, map) = package.Value;
        var dto = envelope.Package.Verses[0];
        var links = dto.Links!.Select(l => string.Join(" ", l.Select(x => x?.ToString()))).ToList();

        links.ShouldContain($"{G(map, "habe")} s3 unit");
        links.ShouldContain($"{G(map, "getauft")} s3 head");
        links.ShouldContain($"{G(map, "wird")} s9 unit");
        dto.Hints.ShouldBeEmpty();
        map.Mode.ShouldBe(AgentModes.Lemma);

        var habe = map.Verses["v1"].Fixed.Single(l => l.G == G(map, "habe"));
        habe.Decided.ShouldBeTrue();
        habe.StoredRole.ShouldBe("head");
        map.Verses["v1"].German.Values.ShouldAllBe(g => g.Status == AgentTokenStatus.Fixed);
    }

    [Fact]
    public async Task Build_HebrewPackage_SendsMorphemes_AndAddedWordsAsFixed()
    {
        var package = await PackageAsync(Genesis, 1, 3);
        package.ShouldNotBeNull();
        var (envelope, map) = package.Value;
        var dto = envelope.Package.Verses[0];

        // וַיֹּאמֶר: "Und" renders the prefix H9001, "sprach" the main morpheme H0559 (rule 1).
        dto.Links!.Select(l => string.Join(" ", l.Select(x => x?.ToString()))).ShouldContain($"{G(map, "Und")} s1 head H9001");
        dto.Links!.Select(l => string.Join(" ", l.Select(x => x?.ToString()))).ShouldContain($"{G(map, "sprach")} s1 head H0559");
        ((string[])dto.Src[0][6]!).ShouldBe(["H9001", "{H0559}"]);
        dto.Fixed.ShouldContain(f => f[0] == G(map, "Es") && f[1] == G(map, "werde"));
        map.Verses["v1"].Source["s5"].Morphemes!.Select(m => m.Strongs).ShouldBe(["H9001", "H1961"]);
    }

    // ---- validator: hard rules ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Validate_Silence_KeepsEverything_AndProposesTheRuleDecisionsRoleAware()
    {
        var map = await MapAsync(Mark, 1, 8);

        var result = ValidateOne(map, """{"v":"v1"}""");

        result.Status.ShouldBe(AgentVerseStatus.Accepted);
        var proposal = result.Proposal!;
        proposal.Links.Where(l => !l.IsAddedWord).ShouldAllBe(l => l.Role != null);
        proposal.Links.Single(l => l.ElbWordId == map.Verses["v1"].German[G(map, "habe")].Id).Role.ShouldBe("unit");
        proposal.Roles.Select(r => (r.Role, r.BeforeRole, r.Source)).ShouldBe([("unit", "head", "rules"), ("unit", "head", "rules")]);
        proposal.Lemmas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Validate_RoleOnAWordThatIsNotLinkedThere_IsANewLink_Rejected()
    {
        var map = await MapAsync(Mark, 1, 8);

        var result = ValidateOne(map, $$"""{"v":"v1","roles":[["{{G(map, "Ich")}}","s3","unit"]]}""");

        result.Status.ShouldBe(AgentVerseStatus.Rejected);
        result.Messages.ShouldContain(m => m.Contains("is not a link"));
    }

    [Fact]
    public async Task Validate_LinksInTheAnswer_Rejected()
    {
        var map = await MapAsync(Mark, 1, 8);

        var result = ValidateOne(map, """{"v":"v1","links":[["g1","s1"]]}""");

        result.Status.ShouldBe(AgentVerseStatus.Rejected);
        result.Messages.ShouldContain(m => m.Contains("links are fixed"));
    }

    [Fact]
    public async Task Validate_TwoHeadsOnOneMorpheme_Rejected_UnlessMultiHead_ThenFlagged()
    {
        var map = await MapAsync(Mark, 1, 8);
        var habe = G(map, "habe");

        var twoHeads = ValidateOne(map, $$"""{"v":"v1","roles":[["{{habe}}","s3","head"]]}""");
        twoHeads.Status.ShouldBe(AgentVerseStatus.Rejected);
        twoHeads.Messages.ShouldContain(m => m.StartsWith("v1 s3") && m.Contains("head links"));

        var multi = ValidateOne(map, $$"""{"v":"v1","roles":[["{{habe}}","s3","multi-head"]]}""");
        multi.Status.ShouldBe(AgentVerseStatus.Flagged);
        multi.Flags.ShouldContain(f => f.Code == "multi-head");
        multi.Proposal!.Links.Single(l => l.ElbWordId == map.Verses["v1"].German[habe].Id).AllowMultiHead.ShouldBe(true);
    }

    [Fact]
    public async Task Validate_UnitWithoutHead_Rejected()
    {
        var map = await MapAsync(Mark, 1, 8);

        var result = ValidateOne(map, $$"""{"v":"v1","roles":[["{{G(map, "getauft")}}","s3","unit"]]}""");

        result.Status.ShouldBe(AgentVerseStatus.Rejected);
        result.Messages.ShouldContain(m => m.Contains("no head link"));
    }

    [Fact]
    public async Task Validate_RoleOnAnAddedWord_Rejected()
    {
        var map = await MapAsync(Mark, 1, 8);
        var er = G(map, "er");

        var onNewAdded = ValidateOne(map, $$"""{"v":"v1","added":[["{{er}}","{{G(map, "wird")}}"]],"roles":[["{{er}}","s7","head"]]}""");
        onNewAdded.Status.ShouldBe(AgentVerseStatus.Rejected);
        onNewAdded.Messages.ShouldContain(m => m.Contains("added word has no role"));

        var gen = await MapAsync(Genesis, 1, 3);
        var onStoredAdded = ValidateOne(gen, $$"""{"v":"v1","roles":[["{{G(gen, "Es")}}","s3","unit"]]}""");
        onStoredAdded.Status.ShouldBe(AgentVerseStatus.Rejected);
        onStoredAdded.Messages.ShouldContain(m => m.Contains("added word has no role"));
    }

    [Theory]
    [InlineData("H9002")]
    [InlineData("H9016")]
    public async Task Validate_HebrewMorphemeNotInTheWord_Rejected(string morpheme)
    {
        var map = await MapAsync(Genesis, 1, 3);
        var licht = G(map, "Licht", 1);

        var result = ValidateOne(map, $$"""{"v":"v1","roles":[["{{licht}}","s6","head","{{morpheme}}"]]}""");

        result.Status.ShouldBe(AgentVerseStatus.Rejected);
        result.Messages.ShouldContain(m => m.Contains($"morpheme {morpheme} is not in source s6"));
    }

    [Fact]
    public async Task Validate_GreekStrongsOtherThanTheWord_Rejected()
    {
        var map = await MapAsync(Mark, 1, 8);

        var result = ValidateOne(map, $$"""{"v":"v1","roles":[["{{G(map, "getauft")}}","s3","head","G1234"]]}""");

        result.Status.ShouldBe(AgentVerseStatus.Rejected);
        result.Messages.ShouldContain(m => m.Contains("Greek link takes the Strong's"));
    }

    [Fact]
    public async Task Validate_HebrewMorphemeChange_MovesTheLink_AndKeepsInvariants()
    {
        var map = await MapAsync(Genesis, 1, 3);
        var und = G(map, "Und");

        // "Und" moved to the main morpheme next to "sprach": two heads on H0559.
        var result = ValidateOne(map, $$"""{"v":"v1","roles":[["{{und}}","s1","head","{H0559}"]]}""");

        result.Status.ShouldBe(AgentVerseStatus.Rejected);
        result.Messages.ShouldContain(m => m.StartsWith("v1 s1 morpheme H0559") && m.Contains("head links"));
    }

    [Fact]
    public async Task Validate_ParticleLemmaRule_FromTheLemmaRules_Rejected()
    {
        var map = await MapAsync(Mark, 1, 29);

        var result = ValidateOne(map, $$"""{"v":"v1","lemmas":[["{{G(map, "aus")}}","ausgehen"]]}""");

        result.Status.ShouldBe(AgentVerseStatus.Rejected);
        result.Messages.ShouldContain(m => m.Contains("keeps itself as lemma"));
    }

    // ---- validator: soft rules ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Validate_PrepositionNotParticle_Mk1_29_Accepted()
    {
        var map = await MapAsync(Mark, 1, 29);
        var aus = G(map, "aus");

        var result = ValidateOne(map, $$"""{"v":"v1","lemmas":[["{{aus}}","aus","APPR","ADP"]]}""", Context(known: ["aus"]));

        result.Status.ShouldBe(AgentVerseStatus.Accepted);
        result.Proposal!.Lemmas.Single().ShouldSatisfyAllConditions(c => c.Stts.ShouldBe("APPR"), c => c.BeforeStts.ShouldBe("PTKVZ"));
    }

    [Fact]
    public async Task Validate_SubjectPronounToAddedWord_Flagged_AndProposedAsAddedWord()
    {
        var map = await MapAsync(Mark, 1, 8);
        var er = G(map, "er");
        var wird = G(map, "wird");

        var result = ValidateOne(map, $$"""{"v":"v1","added":[["{{er}}","{{wird}}"]]}""");

        result.Status.ShouldBe(AgentVerseStatus.Flagged);
        result.Flags.ShouldContain(f => f.Code == "pronoun-added");
        var verse = map.Verses["v1"];
        var link = result.Proposal!.Links.Single(l => l.ElbWordId == verse.German[er].Id);
        link.IsAddedWord.ShouldBeTrue();
        link.ParentElbWordId.ShouldBe(verse.German[wird].Id);
        result.Proposal.Roles.ShouldContain(r => r.ElbWordId == verse.German[er].Id && r.AddedParentElbWordId == verse.German[wird].Id);
    }

    [Fact]
    public async Task Validate_NonPronounToAddedWord_Rejected()
    {
        var map = await MapAsync(Mark, 1, 8);

        var result = ValidateOne(map, $$"""{"v":"v1","added":[["{{G(map, "Wasser")}}","{{G(map, "getauft")}}"]]}""");

        result.Status.ShouldBe(AgentVerseStatus.Rejected);
        result.Messages.ShouldContain(m => m.Contains("only a subject pronoun"));
    }

    [Fact]
    public async Task Validate_HumanEditedLemmaChanged_IsSoftInLemmaMode()
    {
        var map = await MapAsync(Mark, 1, 8, v =>
        {
            foreach (var word in v.German.Where(w => w.Plain == "Geiste"))
            {
                word.LemmaHumanEdited = true;
            }
        });

        var result = ValidateOne(map, $$"""{"v":"v1","lemmas":[["{{G(map, "Geiste")}}","Gespenst"]]}""", Context(known: ["Geist"]));

        result.Status.ShouldBe(AgentVerseStatus.Flagged);
        result.Flags.Select(f => f.Code).ShouldBe(["lemma-unknown", "lemma-human-edited"], ignoreOrder: true);
        result.Proposal!.Lemmas.Single().Lemma.ShouldBe("Gespenst");
    }

    [Fact]
    public async Task Validate_HomographRedecided_Flagged()
    {
        var map = await MapAsync(Genesis, 1, 3);

        var result = ValidateOne(map, $$"""{"v":"v1","lemmas":[["{{G(map, "Gott")}}","Gott","NE","PROPN"]]}""", Context(known: ["Gott"]));

        result.Status.ShouldBe(AgentVerseStatus.Flagged);
        result.Flags.ShouldContain(f => f.Code == "homograph");
    }

    [Fact]
    public async Task Validate_PartLemmas_AreSetOnTheParts()
    {
        var map = await MapAsync(Mark, 1, 6, SplitKamelhaaren);
        var word = G(map, "Kamelhaaren");

        var result = ValidateOne(map, $$"""{"v":"v1","partLemmas":[["{{word}}",1,"Kamel"],["{{word}}",2,"Haar"]]}""");

        result.Status.ShouldBe(AgentVerseStatus.Accepted);
        result.Proposal!.Links.Where(l => l.GermanWordPart is not null).Select(l => (l.PartOrder, l.PartLemma)).OrderBy(x => x.PartOrder).ShouldBe([(1, "Kamel"), (2, "Haar")]);
        result.Proposal.Roles.Where(r => r.PartLemma is not null).Select(r => r.Source).ShouldAllBe(s => s == "model");

        var missing = ValidateOne(map, $$"""{"v":"v1","partLemmas":[["{{word}}",3,"Kamel"]]}""");
        missing.Status.ShouldBe(AgentVerseStatus.Rejected);
    }

    // ---- store and merge ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Merge_AppliesRolesThroughTheRoleAwareSave_AndLemmasThroughTheLemmaPath()
    {
        var map = (await PackageAsync(Mark, 1, 8))!.Value.Map;
        var verse = map.Verses["v1"];
        var euch = G(map, "euch");
        var euchId = verse.German[euch].Id;
        var habeId = verse.German[G(map, "habe")].Id;
        var validation = ValidateOne(map, $$"""{"v":"v1","lemmas":[["{{euch}}","ihr"]],"roles":[["{{G(map, "wird")}}","s9","unit"]]}""", Context(known: ["ihr"]));
        validation.Status.ShouldBe(AgentVerseStatus.Accepted, string.Join("; ", validation.Messages));

        var harness = new MergeHarness(map, Mark, 1, 8);
        harness.Lemmas.GetVerseAsync(Mark, 1, 8, Arg.Any<CancellationToken>()).Returns([new LemmaWordRow { WordId = euchId, PlainWord = "euch", Lemma = "euch", PartOfSpeech = "PRON", Stts = "PPER" }]);
        var record = harness.Record(AgentProposalStore.ToInsert(map, validation));
        record.RolesJson.ShouldNotBeNull();

        var result = await harness.Create().MergeAsync(record.Id, 5);

        result.Outcome.ShouldBe(AgentReviewOutcome.Ok, string.Join("; ", result.Errors));
        await harness.Mapping.Received(1).SaveAsync(Mark, 1, 8,
            Arg.Is<IReadOnlyList<VerseMappingLink>>(l => MappingRoles.IsRoleAware(l) && l.Single(x => x.ElbWordId == habeId).Role == "unit" && l.Count(x => x.Role == "unit") == 2),
            77, false, Arg.Any<CancellationToken>());
        await harness.Lemmas.Received(1).ApplyAsync(Arg.Is<IReadOnlyList<LemmaWordChange>>(c => c.Single().WordId == euchId && c.Single().Lemma == "ihr"), 77, 5, Arg.Any<CancellationToken>());

        var diff = await harness.Create().GetDiffAsync(record.Id);
        diff!.RoleChanges.Select(r => (r.ElbWordId == habeId, r.Role, r.BeforeRole, r.Source)).ShouldBe([(true, "unit", "head", "rules"), (false, "unit", "head", "model")]);
    }

    [Fact]
    public async Task Merge_LinksChangedSincePackage_IsStale()
    {
        var package = await PackageAsync(Mark, 1, 8);
        var map = package!.Value.Map;
        var validation = ValidateOne(map, """{"v":"v1"}""");
        var harness = new MergeHarness(map, Mark, 1, 8, v => v.Mappings.RemoveAt(0));
        var record = harness.Record(AgentProposalStore.ToInsert(map, validation));

        var result = await harness.Create().MergeAsync(record.Id, 5);

        result.Outcome.ShouldBe(AgentReviewOutcome.Stale);
        result.Errors.ShouldContain(e => e.Contains("links of the verse changed"));
        await harness.Mapping.DidNotReceiveWithAnyArgs().SaveAsync(default, default, default, default!, default, default, default);
    }

    [Fact]
    public async Task EditMerge_WritesTheMaintainersRoles()
    {
        var map = (await PackageAsync(Mark, 1, 8))!.Value.Map;
        var harness = new MergeHarness(map, Mark, 1, 8);
        harness.Lemmas.GetVerseAsync(Mark, 1, 8, Arg.Any<CancellationToken>()).Returns([]);
        var record = harness.Record(AgentProposalStore.ToInsert(map, ValidateOne(map, """{"v":"v1"}""")));
        var links = JsonSerializer.Deserialize<List<VerseMappingLink>>(record.LinksJson, Web)!;
        links.Single(l => l.ElbWordId == map.Verses["v1"].German[G(map, "wird")].Id).Role = "head";
        links.Single(l => l.ElbWordId == map.Verses["v1"].German[G(map, "taufen")].Id).Role = "unit";

        var result = await harness.Create().EditMergeAsync(new EditMergeAgentProposalRequest { Id = record.Id, Links = links, Lemmas = [] }, 5);

        result.Outcome.ShouldBe(AgentReviewOutcome.Ok);
        await harness.Mapping.Received(1).SaveAsync(Mark, 1, 8, Arg.Is<IReadOnlyList<VerseMappingLink>>(l => l.Count(x => x.Role == "unit") == 2 && l.Single(x => x.Role == "unit" && x.StepWordId == map.Verses["v1"].Source["s9"].Id).ElbWordId == map.Verses["v1"].German[G(map, "taufen")].Id), 5, false, Arg.Any<CancellationToken>());
    }

    // ---- answer, prompts, run note -----------------------------------------------------------------------------------------------

    [Fact]
    public void Parse_RolesAndPartLemmas()
    {
        var verse = Answer("""{"v":"v1","roles":[["g1","s1","unit"],["g2","s1","head","H9001"]],"partLemmas":[["g3",1,"Gerste"]]}""").Verses[0];

        verse.ParseErrors.ShouldBeEmpty();
        verse.Roles.ShouldBe([new AgentAnswerRole("g1", "s1", "unit", null), new AgentAnswerRole("g2", "s1", "head", "H9001")]);
        verse.PartLemmas.ShouldBe([new AgentAnswerPartLemma("g3", 1, "Gerste")]);

        var broken = Answer("""{"v":"v1","roles":[["g1","s1"]],"partLemmas":[["g3","1","x"]]}""").Verses[0];
        broken.ParseErrors.Count.ShouldBe(2);
    }

    [Fact]
    public void Prompts_LemmaMode_UsesTheLemmaPromptFiles_AndTheLemmaSchema()
    {
        var dir = Directory.CreateTempSubdirectory("uw-lemma-prompts-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "greek-v1.md"), "mapping {{ANSWER_SCHEMA}}");
            File.WriteAllText(Path.Combine(dir, "lemma-greek-v1.md"), "placeholder lemma v1 {{ANSWER_SCHEMA}}");
            File.WriteAllText(Path.Combine(dir, "lemma-greek-v2.md"), "placeholder lemma v2 {{ANSWER_SCHEMA}}");
            var provider = new AgentPromptProvider(dir);

            AgentPromptProvider.LanguageFor(41, AgentMode.Lemma).ShouldBe("lemma-greek");
            AgentPromptProvider.LanguageFor(8, AgentMode.Lemma).ShouldBe("lemma-hebrew");
            AgentPromptProvider.LanguageFor(41, AgentMode.Redo).ShouldBe("greek");

            var lemma = provider.Get("lemma-greek");
            lemma.Version.ShouldBe("lemma-greek-v2");
            lemma.StaticPrefix.ShouldContain(AgentAnswerSchema.LemmaJson.Trim());
            provider.Get("greek").Version.ShouldBe("greek-v1");
            provider.Get("greek").StaticPrefix.ShouldContain(AgentAnswerSchema.Json.Trim());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Modes_LemmaRoundTrips_AndRunNoteReportsSkippedVerses()
    {
        AgentModes.TryParse("lemma", out var mode).ShouldBeTrue();
        mode.ShouldBe(AgentMode.Lemma);
        mode.ToDb().ShouldBe("lemma");
        AgentRunExecutor.SkippedNote(AgentMode.Lemma, 3).ShouldBe("3 verse(s) without work skipped.");
        AgentRunExecutor.SkippedNote(AgentMode.Redo, 3).ShouldBeNull();
    }

    /// <summary>Kamelhaaren (Mk 1:6) as two parts: Kamel → καμήλου, haaren → τρίχας.</summary>
    private static void SplitKamelhaaren(ExampleVerse verse)
    {
        if (verse.German.SingleOrDefault(w => w.Plain == "Kamelhaaren") is not { } word)
        {
            return;
        }

        var camel = verse.Source.Single(s => s.Strongs == "G2574");
        var hair = verse.Source.Single(s => s.Strongs == "G2359");
        verse.Mappings.RemoveAll(m => m.ElbWordId == word.Id);
        verse.Mappings.Add(new AgentMappingRow { ElbWordId = word.Id, StepWordId = camel.Id, GermanWordPart = "Kamel", PartOrder = 1 });
        verse.Mappings.Add(new AgentMappingRow { ElbWordId = word.Id, StepWordId = hair.Id, GermanWordPart = "haaren", PartOrder = 2 });
    }

    /// <summary>An <see cref="AgentReviewService"/> over one example verse with the package map stored and live data from the fixture.</summary>
    private sealed class MergeHarness
    {
        private static readonly Guid PackageId = Guid.Parse("aaaaaaaa-2222-3333-4444-555555555555");
        private readonly IAgentReviewRepository _proposals = Substitute.For<IAgentReviewRepository>();
        private readonly IAgentStoreRepository _store = Substitute.For<IAgentStoreRepository>();
        private readonly IAgentDataRepository _data = Substitute.For<IAgentDataRepository>();
        private readonly Dictionary<long, AgentProposalRecord> _records = [];
        private readonly int _book;
        private readonly int _chapter;
        private readonly int _verse;

        public IVerseMappingService Mapping { get; } = Substitute.For<IVerseMappingService>();
        public IAgentLemmaWriter Lemmas { get; } = Substitute.For<IAgentLemmaWriter>();

        public MergeHarness(AgentPackageMap map, int book, int chapter, int verse, Action<ExampleVerse>? liveChange = null)
        {
            (_book, _chapter, _verse) = (book, chapter, verse);
            var live = Range(book, v =>
            {
                if (v.Chapter == chapter && v.Verse == verse)
                {
                    liveChange?.Invoke(v);
                }
            });
            var ids = live.GermanWords.Where(w => w.Verse == verse).Select(w => w.Id).ToHashSet();
            var refs = live.GermanWords.Where(w => w.Verse == verse).Select(w => w.HebRefId).ToHashSet();
            _data.GetRangeAsync(book, chapter, verse, chapter, verse, Arg.Any<CancellationToken>()).Returns(new AgentRangeData
            {
                GermanWords = live.GermanWords.Where(w => w.Verse == verse).ToList(),
                SourceWords = live.SourceWords.Where(s => refs.Contains(s.RefId)).ToList(),
                Mappings = live.Mappings.Where(m => ids.Contains(m.ElbWordId)).ToList(),
            });
            _store.GetPackageAsync(PackageId, Arg.Any<CancellationToken>()).Returns(new AgentPackageRecord { Id = PackageId, RunId = 5, LocalIdMap = map.ToJson(), WordIdsHash = "x", Status = AgentPackageStatuses.Completed, ExpiresAt = DateTime.UtcNow.AddHours(1) });
            _proposals.GetAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(c => _records.GetValueOrDefault(c.ArgAt<long>(0)));
            _proposals.TryCloseAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
            Mapping.SaveAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<VerseMappingLink>>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(new SaveVerseMappingResult { Outcome = SaveVerseMappingOutcome.Saved });
            Mapping.GetAsync(book, chapter, verse, false, Arg.Any<CancellationToken>()).Returns(new GetVerseMappingResponse
            {
                BibleBookId = book, Chapter = chapter, Verse = verse,
                GermanWords = live.GermanWords.Where(w => w.Verse == verse).Select(w => new VerseMappingGermanWord { Id = w.Id, Text = w.Plain, PositionInVerse = w.PositionInVerse }).ToList(),
            });
            Lemmas.CheckAsync(Arg.Any<IReadOnlyList<LemmaWordChange>>(), Arg.Any<CancellationToken>()).Returns([]);
            Lemmas.ApplyAsync(Arg.Any<IReadOnlyList<LemmaWordChange>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        }

        public AgentReviewService Create() => new(_proposals, _store, _data, Mapping, Lemmas, NullLogger<AgentReviewService>.Instance);

        public AgentProposalRecord Record(AgentProposalInsert insert)
        {
            var record = new AgentProposalRecord
            {
                Id = _records.Count + 1, RunId = 5, PackageId = PackageId, BibleBookId = _book, Chapter = _chapter, Verse = _verse, Language = insert.Language,
                LinksJson = insert.LinksJson, LemmasJson = insert.LemmasJson, RolesJson = insert.RolesJson, PartsJson = insert.PartsJson, FlagsJson = insert.FlagsJson,
                Status = AgentProposalStatuses.Open, CreatedAt = DateTime.UtcNow, AiUserId = 77, RunProfile = "ollama", RunModel = "gemma4",
            };
            _records[record.Id] = record;
            return record;
        }
    }
}
