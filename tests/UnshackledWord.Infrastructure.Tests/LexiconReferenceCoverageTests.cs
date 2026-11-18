using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using UnshackledWord.Domain.Models.BibleStructure.References;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Infrastructure.Services;
using Xunit.Sdk;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0019.10: how many <c>&lt;ref&gt;</c> tags of the whole <c>StepStrongsLexicon</c> become links. Explicit (never part of a normal run);
/// needs a copy of the dev database in <c>UNSHACKLEDWORD_TEST_POSTGRES</c>, otherwise it is skipped. Prints the linked ratio and failures by pattern.
/// </summary>
public partial class LexiconReferenceCoverageTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const double RequiredRatio = 0.995;

    [GeneratedRegex(@"<ref='(?<osis>[^']*)'>.*?</ref>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RefTag();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    private sealed record Row(int LanguageId, int Number, string? Extra, string Lexicon);

    [Fact(Explicit = true)]
    public async Task WholeLexicon_LinksAtLeast995PerMilleOfTheRefTags()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set (needs a copy of the dev database).");

        await using var connection = new NpgsqlConnection(connectionString);
        var rows = (await connection.QueryAsync<Row>(
            """
            SELECT "LanguageId", "Number", "Extra", "Lexicon"
            FROM "unshackled-word"."StepStrongsLexicon"
            WHERE "Lexicon" LIKE '%<ref%'
            """)).ToList();

        using var factory = new VerseCountingRepositoryTests.DirectFactory(connectionString!);
        var counting = await new VerseCountingRepository(new DbReader(factory, NullLogger<DbReader>.Instance)).GetDifferingAsync();
        var map = new VerseCountingMap(counting);

        var total = 0;
        var linked = 0;
        var failures = new Dictionary<string, List<string>>();
        foreach (var row in rows)
        {
            foreach (Match tag in RefTag().Matches(row.Lexicon))
            {
                total++;
                var (segments, _) = LexiconTextSegmenter.Segment(tag.Value, int.MaxValue, map);
                if (segments.Any(s => s.Ref is not null))
                {
                    linked++;
                    continue;
                }

                var osis = tag.Groups["osis"].Value;
                var pattern = Digits().Replace(osis, "#");
                if (!failures.TryGetValue(pattern, out var examples))
                {
                    failures[pattern] = examples = [];
                }

                examples.Add($"{row.LanguageId}/{row.Number}{row.Extra}: {osis}");
            }
        }

        var ratio = total == 0 ? 1.0 : (double)linked / total;
        var report = $"entries with <ref>: {rows.Count}; tags: {total}; linked: {linked}; ratio: {ratio:P2}" + Environment.NewLine +
                     string.Join(Environment.NewLine, failures
                         .OrderByDescending(f => f.Value.Count)
                         .Take(40)
                         .Select(f => $"  {f.Value.Count,5}  {f.Key}   e.g. {string.Join(" | ", f.Value.Take(3))}"));
        Console.WriteLine(report);

        if (ratio < RequiredRatio)
        {
            throw new XunitException($"Only {ratio:P2} of the <ref> tags are linked (required {RequiredRatio:P1}).{Environment.NewLine}{report}");
        }
    }
}
