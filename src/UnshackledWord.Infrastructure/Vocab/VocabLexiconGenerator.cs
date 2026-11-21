using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>
/// Builds the vocab lexicon (roadmap 0022.1, docs/roadmaps/0022-vocab-trainer/03-vocab-model.md "Generator") from
/// <c>StepStrongsLexicon</c> and the STEP word tables: one <c>VocabEntries</c> row per (track, OriginalWord), one <c>VocabStrongs</c> row per
/// Strong's key, occurrences, flags, a dictionary form where it is safe, and the frequency ranks. Idempotent: a rerun adds new keys to the
/// matching entry, never touches manual/agent forms, meanings or <c>RetiredAt</c>, and recomputes ranks without a ContentVersion bump.
/// Runs in one transaction; used by the maintainer job (<c>POST api/maintainer/vocab/generate</c>) and the SeedDb verb <c>vocab-generate</c>.
/// </summary>
public sealed class VocabLexiconGenerator
{
    public const string Kind = "vocab";
    public const string Action = "generate";

    private const int TimeoutSeconds = 900;

    private readonly IDbConnectionFactory _connectionFactory;

    public VocabLexiconGenerator(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SeedStepReport> GenerateAsync(IProgress<string>? progress, CancellationToken token)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var report = await GenerateAsync(connection, transaction, progress);
        transaction.Commit();

        return report;
    }

    /// <summary>The same run on the caller's transaction (tests roll it back).</summary>
    public static async Task<SeedStepReport> GenerateAsync(IDbConnection connection, IDbTransaction transaction, IProgress<string>? progress)
    {
        var report = new SeedStepReport { Kind = Kind, Action = Action };

        async Task<int> Run(string sql, object? parameters = null) =>
            await connection.ExecuteAsync(sql, parameters, transaction, TimeoutSeconds);

        await Run(VocabLexiconGeneratorSql.DropTemp);

        progress?.Report("Normalizing the lexicon lemmas");
        var lemmas = (await connection.QueryAsync<LemmaRow>(VocabLexiconGeneratorSql.SelectLemmas, transaction: transaction, commandTimeout: TimeoutSeconds)).ToList();
        var normalized = lemmas.Select(l => Truncate(VocabText.Normalize(l.Lemma), 50)).ToArray();

        await Run(VocabLexiconGeneratorSql.CreateLemmaNorm);
        await Run(VocabLexiconGeneratorSql.FillLemmaNorm, new
        {
            Tracks = lemmas.Select(l => l.Track).ToArray(),
            Lemmas = lemmas.Select(l => l.Lemma).ToArray(),
            Normalized = normalized
        });

        progress?.Report("Counting key occurrences in the STEP word tables");
        await Run(VocabLexiconGeneratorSql.CreateKeyOccurrences);
        await Run(VocabLexiconGeneratorSql.CreateNamedStrongs);

        progress?.Report("Building keys and lemma groups");
        await Run(VocabLexiconGeneratorSql.CreateKeys);
        await Run(VocabLexiconGeneratorSql.CreateGroups);
        await Run(VocabLexiconGeneratorSql.IndexTemp);
        await Run(VocabLexiconGeneratorSql.CreatePreExisting);
        await Run(VocabLexiconGeneratorSql.CreateEntryLemmas);

        progress?.Report("Creating entries and adding keys");
        report.Counts["entriesCreated"] = await Run(VocabLexiconGeneratorSql.InsertEntries);
        await Run(VocabLexiconGeneratorSql.CreateTargets);
        await Run(VocabLexiconGeneratorSql.CreateNewKeys);
        report.Counts["keysAdded"] = await Run(VocabLexiconGeneratorSql.InsertKeys);
        report.Counts["entriesWithNewKeys"] = await Run(VocabLexiconGeneratorSql.LogAddedKeys);
        report.Counts["formsChanged"] = await Run(VocabLexiconGeneratorSql.UpdateGeneratedForms);

        progress?.Report("Updating occurrences, flags and frequency ranks");
        report.Counts["keyOccurrencesChanged"] = await Run(VocabLexiconGeneratorSql.UpdateKeyOccurrences);
        report.Counts["entryOccurrencesChanged"] = await Run(VocabLexiconGeneratorSql.UpdateEntryOccurrences);
        report.Counts["flagsChanged"] = await Run(VocabLexiconGeneratorSql.UpdateFlags);
        report.Counts["ranksChanged"] = await Run(VocabLexiconGeneratorSql.UpdateRanks) + await Run(VocabLexiconGeneratorSql.ClearStaleRanks);

        foreach (var row in await connection.QueryAsync<TrackSummary>(VocabLexiconGeneratorSql.SelectSummary, transaction: transaction))
        {
            report.Counts[$"{row.Track}.entries"] = row.Entries;
            report.Counts[$"{row.Track}.ranked"] = row.Ranked;
            report.Counts[$"{row.Track}.morphemes"] = row.Morphemes;
            report.Counts[$"{row.Track}.properNames"] = row.ProperNames;
            report.Counts[$"{row.Track}.dictionaryForms"] = row.Forms;
            report.Counts[$"{row.Track}.multiKeyEntries"] = row.MultiKey;
        }

        progress?.Report("Done: " + string.Join(", ", report.Counts.Select(c => $"{c.Key} {c.Value}")));

        return report;
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];

    private sealed class LemmaRow
    {
        public string Track { get; init; } = string.Empty;
        public string Lemma { get; init; } = string.Empty;
    }

    private sealed class TrackSummary
    {
        public string Track { get; init; } = string.Empty;
        public int Entries { get; init; }
        public int Ranked { get; init; }
        public int Morphemes { get; init; }
        public int ProperNames { get; init; }
        public int Forms { get; init; }
        public int MultiKey { get; init; }
    }
}
