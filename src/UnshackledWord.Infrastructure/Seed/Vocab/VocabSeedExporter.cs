using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Infrastructure.Seed.Vocab;

/// <summary>
/// <c>export-seed --only vocab</c> (roadmap 0022.14): writes <c>vocab/entries.csv</c>, <c>vocab/keys.csv</c> and <c>vocab/meanings.csv</c>
/// from the live (not retired) entries. No ids, users, timestamps, review marks or content versions, and never a progress table
/// (cards, sessions, review log, settings, change rows). Lines are sorted by track, rank, lemma so an unchanged database exports unchanged bytes.
/// </summary>
public sealed class VocabSeedExporter
{
    public const string EntriesFile = "entries.csv";
    public const string KeysFile = "keys.csv";
    public const string MeaningsFile = "meanings.csv";

    // Live entries with their homograph number: the n-th entry with this lemma in the track, by lowest Strong's key (ordinal), then id.
    private const string LiveEntries = """
        WITH live AS (
            SELECT e.*
                 , row_number() OVER (PARTITION BY e."Track", e."Lemma"
                                      ORDER BY (SELECT min(s."StrongsKey" COLLATE "C")
                                                FROM "unshackled-word"."VocabStrongs" s
                                                WHERE s."VocabEntryId" = e."Id") NULLS LAST
                                             , e."Id")::int AS "Homograph"
            FROM "unshackled-word"."VocabEntries" e
            WHERE 1=1
                AND e."RetiredAt" IS NULL
        )
        """;

    private const string SelectEntries = LiveEntries + """
        SELECT e."Track"
             , e."Lemma"
             , e."Homograph"
             , e."LanguageId"
             , e."LemmaNormalized"
             , e."DictionaryForm"
             , e."DictionaryFormOrigin"
             , e."Transliteration"
             , e."Pos"
             , e."IsProperName"
             , e."IsMorpheme"
             , e."Occurrences"
             , e."FrequencyRank"
             , e."AudioObjectKey"
        FROM live e
        ORDER BY e."Track"
               , e."FrequencyRank" NULLS LAST
               , e."Lemma" COLLATE "C"
               , e."Id"
        """;

    private const string SelectKeys = LiveEntries + """
        SELECT e."Track"
             , e."Lemma"
             , e."Homograph"
             , s."StrongsKey"
             , s."Occurrences"
        FROM "unshackled-word"."VocabStrongs" s
            JOIN live e ON e."Id" = s."VocabEntryId"
        ORDER BY e."Track"
               , e."FrequencyRank" NULLS LAST
               , e."Lemma" COLLATE "C"
               , e."Id"
               , s."StrongsKey" COLLATE "C"
        """;

    private const string SelectMeanings = LiveEntries + """
        SELECT e."Track"
             , e."Lemma"
             , e."Homograph"
             , m."German"
             , m."GermanKey"
             , m."Rank"
             , m."Count"
             , m."Share"
             , m."IsAccepted"
             , m."Origin"
             , m."IsPinned"
        FROM "unshackled-word"."VocabMeanings" m
            JOIN live e ON e."Id" = m."VocabEntryId"
        ORDER BY e."Track"
               , e."FrequencyRank" NULLS LAST
               , e."Lemma" COLLATE "C"
               , e."Id"
               , m."Rank"
               , m."German" COLLATE "C"
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IFileService _fileService;
    private readonly ILogger<VocabSeedExporter> _logger;

    public VocabSeedExporter(IDbConnectionFactory connectionFactory, IFileService fileService, ILogger<VocabSeedExporter> logger)
    {
        _connectionFactory = connectionFactory;
        _fileService = fileService;
        _logger = logger;
    }

    public async Task ExportAsync(SeedLayout layout, CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        var data = await ReadAsync(connection, null);

        if (data.Entries.Count == 0)
        {
            _logger.LogWarning("VocabEntries is empty; the vocab seed was not exported and the files on disk stay as they are");

            return;
        }

        var folder = layout.Resolve(SeedLayout.VocabFolder);
        _fileService.CreateDirectoryIfNotExists(folder);

        foreach (var (name, bytes) in Serialize(data))
        {
            await File.WriteAllBytesAsync(Path.Combine(folder, name), bytes, token);
        }

        _logger.LogInformation("Vocab seed exported to {Folder}: {Entries} entries, {Keys} keys, {Meanings} meanings", folder, data.Entries.Count, data.Keys.Count, data.Meanings.Count);
    }

    /// <summary>The three files as bytes (name, content); used by the exporter and by the round-trip test.</summary>
    public static List<(string Name, byte[] Bytes)> Serialize(VocabSeedData data) =>
    [
        (EntriesFile, SeedCsv.Serialize<VocabSeedEntryLine, VocabSeedEntryLineMap>(data.Entries).Bytes),
        (KeysFile, SeedCsv.Serialize<VocabSeedKeyLine, VocabSeedKeyLineMap>(data.Keys).Bytes),
        (MeaningsFile, SeedCsv.Serialize<VocabSeedMeaningLine, VocabSeedMeaningLineMap>(data.Meanings).Bytes)
    ];

    /// <summary>Reads the live content. Identity across databases is <c>(Track, Lemma, Homograph)</c>; a split leaves two entries with one lemma, the homograph number tells them apart.</summary>
    public static async Task<VocabSeedData> ReadAsync(IDbConnection c, IDbTransaction? t)
    {
        var data = new VocabSeedData
        {
            Entries = (await c.QueryAsync<VocabSeedEntryLine>(SelectEntries, transaction: t)).ToList(),
            Keys = (await c.QueryAsync<VocabSeedKeyLine>(SelectKeys, transaction: t)).ToList(),
            Meanings = (await c.QueryAsync<VocabSeedMeaningLine>(SelectMeanings, transaction: t)).ToList()
        };

        return data;
    }
}
