using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

/// <summary>
/// The LXX fixture of roadmap 0028.7, <c>Fixtures/lxx-prompt-examples.json</c>, exported from the dev database by
/// <c>LxxAgentDatabaseTests.ExportPromptFixture</c> (Infrastructure tests, <c>UNSHACKLEDWORD_LXX_FIXTURE_OUT</c>): the units Gen 1:1, 1:3, Ruth 1:1, 1:2, 1:4 and
/// the addition Job 36:28.1 with their words (and the pass-1 tags of that day), the Hebrew words, and the part of TBESG and the NT forms the candidate
/// index can reach for these words (with the full NT frequencies). An in-memory <see cref="ILxxAgentDataRepository"/> serves it.
/// </summary>
public sealed class LxxAgentTestData : ILxxAgentDataRepository
{
    public const int Genesis = 1;
    public const int Ruth = 8;
    public const int Job = 18;

    private static readonly Lazy<Fixture> Shared = new(Load);

    public LxxAgentTestData()
    {
        var f = Shared.Value;
        // Fresh row objects per instance: the rows have setters, and a test (or the code under test) that changes one must not change it for the next test.
        Units = [.. f.Units.Select(Copy)];
        Words = [.. f.Words.Select(Copy)];
        Hebrew = [.. f.Hebrew.Select(Copy)];
        Lexicon = [.. f.Lexicon];
        NtForms = [.. f.NtForms];
    }

    private static T Copy<T>(T row) => System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(row))!;

    public List<LxxAgentUnitRow> Units { get; }
    public List<LxxAgentWordRow> Words { get; }
    public List<AgentSourceWordRow> Hebrew { get; }
    public List<LxxLexiconEntry> Lexicon { get; }
    public List<LxxNtFormCount> NtForms { get; }

    /// <summary>Unit ids <see cref="GetProposedUnitIdsAsync"/> reports for every run (resume tests).</summary>
    public HashSet<int> Proposed { get; } = [];

    public int LexiconLoads { get; private set; }

    public LxxAgentUnitRow Unit(int book, int chapter, int verse, int subverse = 0) =>
        Units.Single(u => u.BibleBookId == book && u.Chapter == chapter && u.Verse == verse && u.Subverse == subverse);

    public Task<LxxAgentRangeData> GetRangeAsync(int bibleBookId, int fromChapter, int fromVerse, int toChapter, int toVerse, CancellationToken token = default)
    {
        var units = Units.Where(u => u.BibleBookId == bibleBookId
                                     && (u.Chapter, u.Verse).CompareTo((fromChapter, fromVerse)) >= 0
                                     && (u.Chapter, u.Verse).CompareTo((toChapter, toVerse)) <= 0)
            .OrderBy(u => u.SortOrder)
            .ToList();
        return Task.FromResult(With(units));
    }

    public Task<LxxAgentRangeData> GetUnitsAsync(IReadOnlyCollection<int> lxxVerseIds, CancellationToken token = default) =>
        Task.FromResult(With(Units.Where(u => lxxVerseIds.Contains(u.Id)).OrderBy(u => u.SortOrder).ToList()));

    public Task<IReadOnlyList<LxxLexiconEntry>> GetLexiconAsync(CancellationToken token = default)
    {
        LexiconLoads++;
        return Task.FromResult<IReadOnlyList<LxxLexiconEntry>>(Lexicon);
    }

    public Task<IReadOnlyList<LxxNtFormCount>> GetNtFormsAsync(CancellationToken token = default) => Task.FromResult<IReadOnlyList<LxxNtFormCount>>(NtForms);

    public Task<IReadOnlySet<int>> GetProposedUnitIdsAsync(int runId, CancellationToken token = default) => Task.FromResult<IReadOnlySet<int>>(Proposed.ToHashSet());

    private LxxAgentRangeData With(List<LxxAgentUnitRow> units)
    {
        var ids = units.Select(u => u.Id).ToHashSet();
        var heb = units.Where(u => u.HebRefId is not null).Select(u => u.HebRefId!.Value).ToHashSet();
        return new LxxAgentRangeData
        {
            Units = units,
            Words = Words.Where(w => ids.Contains(w.LxxVerseId)).ToList(),
            HebrewWords = Hebrew.Where(h => heb.Contains(h.RefId)).ToList(),
        };
    }

    /// <summary>A package builder over this data and a store substitute that records the inserted packages (run id 77).</summary>
    public (LxxAgentPackageBuilder Builder, List<AgentPackageInsert> Packages, IAgentStoreRepository Store) Builder(IConfiguration? configuration = null)
    {
        var store = Substitute.For<IAgentStoreRepository>();
        store.CreateRunAsync(Arg.Any<AgentRunInsert>(), Arg.Any<CancellationToken>()).Returns(77);
        var packages = new List<AgentPackageInsert>();
        store.InsertPackageAsync(Arg.Do<AgentPackageInsert>(packages.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        return (new LxxAgentPackageBuilder(this, store, configuration ?? Substitute.For<IConfiguration>(), TimeProvider.System), packages, store);
    }

    /// <summary>Builds one package of the verses (chapter, verse) of one book and returns it with its stored map.</summary>
    public async Task<(AgentPackageEnvelope Envelope, AgentPackageMap Map)> BuildOneAsync(int book, int chapter, int fromVerse, int toVerse)
    {
        var (builder, packages, _) = Builder();
        var built = await builder.BuildAsync(new AgentBuildRequest
        {
            Range = new AgentRange { BibleBookId = book, FromChapter = chapter, FromVerse = fromVerse, ToChapter = chapter, ToVerse = toVerse },
            Mode = AgentMode.LxxTag,
            UserId = 3,
        });

        built.ShouldNotBeNull();
        built.Packages.Count.ShouldBe(1);
        return (built.Packages[0], AgentPackageMap.FromJson(packages.Single().LocalIdMapJson));
    }

    public static string Serialize(AgentPackageDto package) => JsonSerializer.Serialize(package, AgentPackageJson.Options);

    private sealed class Fixture
    {
        public List<LxxAgentUnitRow> Units { get; set; } = [];
        public List<LxxAgentWordRow> Words { get; set; } = [];
        public List<AgentSourceWordRow> Hebrew { get; set; } = [];
        public List<LxxLexiconEntry> Lexicon { get; set; } = [];
        public List<LxxNtFormCount> NtForms { get; set; } = [];
    }

    private static Fixture Load() => Load(null);

    private static Fixture Load(string? _, [CallerFilePath] string here = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "lxx-prompt-examples.json");
        return JsonSerializer.Deserialize<Fixture>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new Fixture();
    }
}
