using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

/// <summary>
/// The 10 example verses of roadmap 0016 (02-decisions.md "Example verses": Mk 1:1, 1:6, 1:8, 1:29, 1:35, Gen 1:1, 1:3, Rt 1:4, 1:5, 1:22) as
/// <see cref="AgentRangeData"/>, exported from the test database uw_0018_test (German words with morphology, original words with Strong's and
/// morphology codes, mapping rows with role columns). Original text, lemmas and glosses are left out; ExpandedStrongTags keep only the codes.
/// </summary>
internal static class LemmaExampleVerses
{
    public const int Mark = 41;
    public const int Genesis = 1;
    public const int Ruth = 8;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<IReadOnlyList<ExampleVerse>> All = new(() => Load());

    public sealed class ExampleVerse
    {
        public int Book { get; set; }
        public int Chapter { get; set; }
        public int Verse { get; set; }
        public List<AgentGermanWordRow> German { get; set; } = [];
        public List<AgentSourceWordRow> Source { get; set; } = [];
        public List<AgentMappingRow> Mappings { get; set; } = [];
    }

    public static IReadOnlyList<ExampleVerse> Verses => All.Value;

    public static ExampleVerse Get(int book, int chapter, int verse) => Verses.Single(v => v.Book == book && v.Chapter == chapter && v.Verse == verse);

    /// <summary>All example verses of one book as one range (fresh copies, so a test may change rows).</summary>
    public static AgentRangeData Range(int book, Action<ExampleVerse>? change = null)
    {
        var verses = Load().Where(v => v.Book == book).ToList();
        foreach (var verse in verses)
        {
            change?.Invoke(verse);
        }

        return new AgentRangeData
        {
            GermanWords = verses.SelectMany(v => v.German).ToList(),
            SourceWords = verses.SelectMany(v => v.Source).ToList(),
            Mappings = verses.SelectMany(v => v.Mappings).ToList(),
        };
    }

    /// <summary>A trusted lexicon row for every German form of the examples with its stored lemma: no token is "LemmaSharp only".</summary>
    public static IReadOnlyList<AgentFormLemmaRow> TrustedLexicon() =>
        Verses.SelectMany(v => v.German)
            .Where(w => !string.IsNullOrWhiteSpace(w.Lemma) && w.Plain.Any(char.IsLetterOrDigit))
            .GroupBy(w => w.Plain.ToLowerInvariant())
            .Select(g => new AgentFormLemmaRow { Form = g.Key, Lemma = g.First().Lemma!, Count = 1, Trusted = true })
            .ToList();

    /// <summary>A package builder over the example verses; <paramref name="lexicon"/> is what <c>GetFormLemmasAsync</c> returns.</summary>
    public static AgentPackageBuilder Builder(IReadOnlyList<AgentFormLemmaRow> lexicon, Action<ExampleVerse>? change = null, IAgentStoreRepository? store = null)
    {
        var data = Substitute.For<IAgentDataRepository>();
        foreach (var book in new[] { Mark, Genesis, Ruth })
        {
            var range = Range(book, change);
            data.GetRangeAsync(book, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(c =>
            {
                var (fc, fv, tc, tv) = (c.ArgAt<int>(1), c.ArgAt<int>(2), c.ArgAt<int>(3), c.ArgAt<int>(4));
                bool In(int chapter, int verse) => (chapter, verse).CompareTo((fc, fv)) >= 0 && (chapter, verse).CompareTo((tc, tv)) <= 0;
                var german = range.GermanWords.Where(w => In(w.Chapter, w.Verse)).ToList();
                var ids = german.Select(w => w.Id).ToHashSet();
                var refs = german.Select(w => w.HebRefId).ToHashSet();
                return new AgentRangeData
                {
                    GermanWords = german,
                    SourceWords = range.SourceWords.Where(s => refs.Contains(s.RefId)).ToList(),
                    Mappings = range.Mappings.Where(m => ids.Contains(m.ElbWordId)).ToList(),
                };
            });
        }

        data.GetFormLemmasAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(lexicon);
        data.GetConfirmedPairsAsync(Arg.Any<bool>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns([]);

        if (store is null)
        {
            store = Substitute.For<IAgentStoreRepository>();
            store.CreateRunAsync(Arg.Any<AgentRunInsert>(), Arg.Any<CancellationToken>()).Returns(77);
        }

        return new AgentPackageBuilder(data, store, Substitute.For<IConfiguration>(), TimeProvider.System);
    }

    /// <summary>Builds the lemma-mode package of one example verse (dry run) and returns its envelope and map; null when the verse has no work.</summary>
    public static async Task<(AgentPackageEnvelope Envelope, AgentPackageMap Map)?> PackageAsync(int book, int chapter, int verse, IReadOnlyList<AgentFormLemmaRow>? lexicon = null, Action<ExampleVerse>? change = null)
    {
        var store = Substitute.For<IAgentStoreRepository>();
        store.CreateRunAsync(Arg.Any<AgentRunInsert>(), Arg.Any<CancellationToken>()).Returns(77);
        var maps = new List<AgentPackageInsert>();
        store.InsertPackageAsync(Arg.Do<AgentPackageInsert>(maps.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var built = await Builder(lexicon ?? [], change, store).BuildAsync(new AgentBuildRequest
        {
            Range = new AgentRange { BibleBookId = book, FromChapter = chapter, FromVerse = verse, ToChapter = chapter, ToVerse = verse },
            Mode = AgentMode.Lemma,
            UserId = 3,
        });

        if (built is null || built.Packages.Count == 0)
        {
            return null;
        }

        return (built.Packages[0], AgentPackageMap.FromJson(maps[0].LocalIdMapJson));
    }

    private static IReadOnlyList<ExampleVerse> Load([CallerFilePath] string here = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "lemma-example-verses.json");
        return JsonSerializer.Deserialize<List<ExampleVerse>>(File.ReadAllText(path), Json) ?? [];
    }
}
