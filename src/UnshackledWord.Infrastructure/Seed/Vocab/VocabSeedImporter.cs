using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Seed.Vocab;

/// <summary>
/// Imports <c>assets/seed/vocab</c> (roadmap 0022.14) on the first start and from the seed page; the same idempotent sync serves both.
/// Entry identity across databases: <c>(Track, Lemma, Homograph)</c> first (the homograph number tells the entries of a split apart, see
/// <see cref="VocabSeedEntryLine.Homograph"/>); an entry whose lemma is not found is matched through a Strong's key it holds (renamed lemma). Never by the serial id. Unknown entries are created; entries missing from the seed are left alone.
/// A matched entry takes the seed's content. A change learners care about (accepted German set, dictionary form, key set, lemma, see
/// <see cref="VocabChangeRules"/>) bumps <c>ContentVersion</c> and writes a <c>VocabEntryChanges</c> row exactly like the editor, so cards flag it;
/// everything else (ranks, counts, shares, transliteration, occurrences, flags) is updated silently. The audio key is only set from the seed,
/// never cleared by it. Progress tables are never touched. All in one transaction.
/// </summary>
public sealed class VocabSeedImporter
{
    public const string Kind = "vocab";

    private const string SelectEntries = """
        SELECT e."Id", e."Track", e."Lemma", e."LanguageId", e."LemmaNormalized", e."DictionaryForm", e."DictionaryFormOrigin", e."Transliteration"
             , e."Pos", e."IsProperName", e."IsMorpheme", e."Occurrences", e."FrequencyRank", e."AudioObjectKey", e."RetiredAt" IS NOT NULL AS "Retired"
        FROM "unshackled-word"."VocabEntries" e
        """;

    private const string SelectKeys = """
        SELECT s."VocabEntryId" AS "EntryId", s."StrongsKey", s."Occurrences"
        FROM "unshackled-word"."VocabStrongs" s
        """;

    private const string SelectMeanings = """
        SELECT m."Id", m."VocabEntryId" AS "EntryId", m."German", m."GermanKey", m."Rank", m."Count", m."Share", m."IsAccepted", m."Origin", m."IsPinned"
        FROM "unshackled-word"."VocabMeanings" m
        """;

    private const string InsertEntry = """
        INSERT INTO "unshackled-word"."VocabEntries"
            ("Track", "LanguageId", "Lemma", "LemmaNormalized", "DictionaryForm", "DictionaryFormOrigin", "Transliteration", "Pos"
           , "IsProperName", "IsMorpheme", "Occurrences", "FrequencyRank", "AudioObjectKey")
        VALUES (@Track, @LanguageId, @Lemma, @LemmaNormalized, @DictionaryForm, @DictionaryFormOrigin, @Transliteration, @Pos
              , @IsProperName, @IsMorpheme, @Occurrences, @FrequencyRank, @AudioObjectKey)
        RETURNING "Id"
        """;

    private const string UpdateEntry = """
        UPDATE "unshackled-word"."VocabEntries"
        SET "Lemma" = @Lemma
          , "LemmaNormalized" = @LemmaNormalized
          , "LanguageId" = @LanguageId
          , "DictionaryForm" = @DictionaryForm
          , "DictionaryFormOrigin" = @DictionaryFormOrigin
          , "Transliteration" = @Transliteration
          , "Pos" = @Pos
          , "IsProperName" = @IsProperName
          , "IsMorpheme" = @IsMorpheme
          , "Occurrences" = @Occurrences
          , "FrequencyRank" = @FrequencyRank
          , "AudioObjectKey" = @AudioObjectKey
          , "UpdatedAt" = now()
        WHERE 1=1
            AND "Id" = @Id
        """;

    private const string BumpVersion = """
        UPDATE "unshackled-word"."VocabEntries"
        SET "ContentVersion" = "ContentVersion" + 1
          , "UpdatedAt" = now()
        WHERE 1=1
            AND "Id" = @Id
        RETURNING "ContentVersion"
        """;

    private const string InsertChange = """
        INSERT INTO "unshackled-word"."VocabEntryChanges" ("VocabEntryId", "ContentVersion", "Kind", "Before", "After")
        VALUES (@EntryId, @ContentVersion, @Kind, @Before::jsonb, @After::jsonb)
        """;

    private const string InsertKey = """
        INSERT INTO "unshackled-word"."VocabStrongs" ("Lang", "StrongsKey", "VocabEntryId", "Occurrences")
        VALUES (left(@StrongsKey, 1), @StrongsKey, @EntryId, @Occurrences)
        ON CONFLICT ("Lang", "StrongsKey") DO UPDATE SET "Occurrences" = EXCLUDED."Occurrences"
        WHERE "unshackled-word"."VocabStrongs"."VocabEntryId" = EXCLUDED."VocabEntryId"
        """;

    private const string DeleteKey = """
        DELETE FROM "unshackled-word"."VocabStrongs" WHERE "VocabEntryId" = @EntryId AND "StrongsKey" = @StrongsKey
        """;

    private const string UpsertMeaning = """
        INSERT INTO "unshackled-word"."VocabMeanings" ("VocabEntryId", "German", "GermanKey", "Rank", "Count", "Share", "IsAccepted", "Origin", "IsPinned")
        VALUES (@EntryId, @German, @GermanKey, @Rank, @Count, @Share, @IsAccepted, @Origin, @IsPinned)
        ON CONFLICT ("VocabEntryId", "German") DO UPDATE
        SET "GermanKey" = EXCLUDED."GermanKey"
          , "Rank" = EXCLUDED."Rank"
          , "Count" = EXCLUDED."Count"
          , "Share" = EXCLUDED."Share"
          , "IsAccepted" = EXCLUDED."IsAccepted"
          , "Origin" = EXCLUDED."Origin"
          , "IsPinned" = EXCLUDED."IsPinned"
        """;

    private const string DeleteMeaning = """
        DELETE FROM "unshackled-word"."VocabMeanings" WHERE "Id" = @Id
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _settings;
    private readonly ILogger<VocabSeedImporter> _logger;

    public VocabSeedImporter(IDbConnectionFactory connectionFactory, IFileService fileService, IOptions<AppSettings> options, ILogger<VocabSeedImporter> logger)
    {
        _connectionFactory = connectionFactory;
        _fileService = fileService;
        _settings = options.Value.DatabaseSeeding ?? new DatabaseSeedSettings();
        _logger = logger;
    }

    public SeedLayout CreateLayout(string? seedRootOverride = null) => new(_fileService, _settings, seedRootOverride);

    /// <summary>First start (SeedDb runner): syncs when the seed folder exists, otherwise does nothing.</summary>
    public async Task RunAsync(SeedLayout layout, CancellationToken token = default) => await ImportFromLayoutAsync(layout, token);

    public async Task<SeedStepReport> ImportFromLayoutAsync(SeedLayout layout, CancellationToken token = default)
    {
        var data = await LoadAsync(layout, token);

        if (data is null)
        {
            _logger.LogInformation("No vocab seed in {Folder}; skipped", layout.Resolve(SeedLayout.VocabFolder));

            return SeedStepReport.Skip(Kind, "import", SeedSkipReason.NoSeed);
        }

        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();
        var report = await ImportAsync(connection, transaction, data);
        transaction.Commit();

        _logger.LogInformation("Vocab seed imported: {Counts}", string.Join(", ", report.Counts.Select(x => $"{x.Key}={x.Value}")));

        return report;
    }

    public static async Task<VocabSeedData?> LoadAsync(SeedLayout layout, CancellationToken token = default)
    {
        var folder = layout.Resolve(SeedLayout.VocabFolder);
        var entries = Path.Combine(folder, VocabSeedExporter.EntriesFile);

        if (!File.Exists(entries))
        {
            return null;
        }

        var keys = Path.Combine(folder, VocabSeedExporter.KeysFile);
        var meanings = Path.Combine(folder, VocabSeedExporter.MeaningsFile);

        return Parse(
            await File.ReadAllBytesAsync(entries, token),
            File.Exists(keys) ? await File.ReadAllBytesAsync(keys, token) : null,
            File.Exists(meanings) ? await File.ReadAllBytesAsync(meanings, token) : null);
    }

    public static VocabSeedData Parse(byte[] entries, byte[]? keys, byte[]? meanings)
    {
        var data = new VocabSeedData
        {
            Entries = SeedCsv.Read<VocabSeedEntryLine, VocabSeedEntryLineMap>(entries),
            Keys = keys is null ? [] : SeedCsv.Read<VocabSeedKeyLine, VocabSeedKeyLineMap>(keys),
            Meanings = meanings is null ? [] : SeedCsv.Read<VocabSeedMeaningLine, VocabSeedMeaningLineMap>(meanings)
        };

        // An empty CSV field is a null in the database (the exporter writes null as an empty field).
        foreach (var e in data.Entries)
        {
            e.DictionaryForm = NullIfEmpty(e.DictionaryForm);
            e.DictionaryFormOrigin = NullIfEmpty(e.DictionaryFormOrigin);
            e.Transliteration = NullIfEmpty(e.Transliteration);
            e.Pos = NullIfEmpty(e.Pos);
            e.AudioObjectKey = NullIfEmpty(e.AudioObjectKey);
        }

        foreach (var m in data.Meanings)
        {
            m.GermanKey = NullIfEmpty(m.GermanKey);
        }

        return data;
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;

    /// <summary>The sync itself; the caller owns the transaction.</summary>
    public static async Task<SeedStepReport> ImportAsync(IDbConnection c, IDbTransaction t, VocabSeedData seed)
    {
        var db = (await c.QueryAsync<DbEntry>(SelectEntries, transaction: t)).ToList();
        var dbKeys = (await c.QueryAsync<DbKey>(SelectKeys, transaction: t)).ToLookup(k => k.EntryId);
        var dbMeanings = (await c.QueryAsync<DbMeaning>(SelectMeanings, transaction: t)).ToLookup(m => m.EntryId);
        var keyOwner = (await c.QueryAsync<DbKey>(SelectKeys, transaction: t)).ToDictionary(k => k.StrongsKey, k => k.EntryId, StringComparer.Ordinal);

        // the same homograph numbering as the exporter: live entries per (track, lemma), by lowest Strong's key (ordinal), then id
        var byLemma = db
            .Where(e => !e.Retired)
            .GroupBy(e => (e.Track, e.Lemma))
            .SelectMany(g => g
                .OrderBy(e => dbKeys[e.Id].Select(k => k.StrongsKey).Order(StringComparer.Ordinal).FirstOrDefault() ?? "\uFFFF", StringComparer.Ordinal)
                .ThenBy(e => e.Id)
                .Select((e, index) => (Key: (e.Track, e.Lemma, Homograph: index + 1), Entry: e)))
            .ToDictionary(x => x.Key, x => x.Entry);
        var seedKeys = seed.Keys.ToLookup(k => (k.Track, k.Lemma, k.Homograph));
        var seedMeanings = seed.Meanings.ToLookup(m => (m.Track, m.Lemma, m.Homograph));
        var claimed = new HashSet<int>();
        var created = 0;
        var updated = 0;
        var bumped = 0;
        var unchanged = 0;
        var keyConflicts = 0;

        foreach (var line in seed.Entries)
        {
            var identity = (line.Track, line.Lemma, line.Homograph);
            var wantedKeys = seedKeys[identity].ToList();
            var wantedMeanings = seedMeanings[identity].ToList();

            if (!byLemma.TryGetValue(identity, out var entry) || claimed.Contains(entry.Id))
            {
                entry = null;
                var owner = wantedKeys.Select(k => keyOwner.GetValueOrDefault(k.StrongsKey)).FirstOrDefault(id => id != 0 && !claimed.Contains(id));
                var viaKey = owner == 0 ? null : db.First(e => e.Id == owner);

                if (viaKey is not null && !viaKey.Retired && viaKey.Track == line.Track)
                {
                    entry = viaKey;
                }
            }

            if (entry is null)
            {
                var id = await c.QuerySingleAsync<int>(InsertEntry, line, t);
                claimed.Add(id);
                keyConflicts += await WriteKeysAsync(c, t, id, wantedKeys, keyOwner, []);
                await WriteMeaningsAsync(c, t, id, wantedMeanings, []);
                created++;

                continue;
            }

            claimed.Add(entry.Id);
            var changedSomething = false;
            var bumps = 0;

            if (!string.Equals(entry.Lemma, line.Lemma, StringComparison.Ordinal))
            {
                bumps += await BumpAsync(c, t, entry.Id, VocabChangeKinds.Lemma, new { lemma = entry.Lemma }, new { lemma = line.Lemma });
            }

            if (VocabChangeRules.FormChanged(entry.DictionaryForm, line.DictionaryForm))
            {
                bumps += await BumpAsync(c, t, entry.Id, VocabChangeKinds.Form, new { form = entry.DictionaryForm }, new { form = line.DictionaryForm });
            }

            var currentKeys = dbKeys[entry.Id].ToList();
            var keyNames = currentKeys.Select(k => k.StrongsKey).ToList();
            var ownedWanted = wantedKeys.Where(k => !keyOwner.TryGetValue(k.StrongsKey, out var o) || o == entry.Id).ToList();
            var keysChanged = VocabChangeRules.KeysChanged(keyNames, ownedWanted.Select(k => k.StrongsKey));

            if (keysChanged)
            {
                bumps += await BumpAsync(c, t, entry.Id, VocabChangeKinds.Keys, new { keys = keyNames.Order(StringComparer.Ordinal).ToList() }, new { keys = ownedWanted.Select(k => k.StrongsKey).Order(StringComparer.Ordinal).ToList() });
            }

            var currentMeanings = dbMeanings[entry.Id].ToList();
            var acceptedBefore = currentMeanings.Where(m => m.IsAccepted).OrderBy(m => m.Rank).ThenBy(m => m.Id).Select(m => m.German).ToList();
            var acceptedAfter = wantedMeanings.Where(m => m.IsAccepted).OrderBy(m => m.Rank).Select(m => m.German).ToList();

            if (VocabChangeRules.AcceptedSetChanged(acceptedBefore, acceptedAfter))
            {
                bumps += await BumpAsync(c, t, entry.Id, VocabChangeKinds.Meanings, new { meanings = acceptedBefore }, new { meanings = acceptedAfter });
            }

            var meaningsDiffer = MeaningsDiffer(currentMeanings, wantedMeanings);

            if (keysChanged || currentKeys.Count != ownedWanted.Count || ownedWanted.Any(k => currentKeys.FirstOrDefault(x => x.StrongsKey == k.StrongsKey)?.Occurrences != k.Occurrences))
            {
                keyConflicts += await WriteKeysAsync(c, t, entry.Id, wantedKeys, keyOwner, currentKeys);
                changedSomething = true;
            }

            if (meaningsDiffer)
            {
                await WriteMeaningsAsync(c, t, entry.Id, wantedMeanings, currentMeanings);
                changedSomething = true;
            }

            var audio = string.IsNullOrEmpty(line.AudioObjectKey) ? entry.AudioObjectKey : line.AudioObjectKey;

            if (!EntryColumnsEqual(entry, line, audio) || !string.Equals(entry.Lemma, line.Lemma, StringComparison.Ordinal))
            {
                await c.ExecuteAsync(UpdateEntry, new
                {
                    entry.Id,
                    line.Lemma,
                    line.LemmaNormalized,
                    line.LanguageId,
                    line.DictionaryForm,
                    line.DictionaryFormOrigin,
                    line.Transliteration,
                    line.Pos,
                    line.IsProperName,
                    line.IsMorpheme,
                    line.Occurrences,
                    line.FrequencyRank,
                    AudioObjectKey = audio
                }, t);
                changedSomething = true;
            }

            if (bumps > 0)
            {
                bumped++;
            }

            if (changedSomething || bumps > 0)
            {
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        return new SeedStepReport { Kind = Kind, Action = "import" }
            .WithCount("created", created)
            .WithCount("updated", updated)
            .WithCount("bumped", bumped)
            .WithCount("unchanged", unchanged)
            .WithCount("keyConflicts", keyConflicts);
    }

    private static bool EntryColumnsEqual(DbEntry e, VocabSeedEntryLine l, string? audio) =>
        e.LanguageId == l.LanguageId
        && e.LemmaNormalized == l.LemmaNormalized
        && e.DictionaryForm == l.DictionaryForm
        && e.DictionaryFormOrigin == l.DictionaryFormOrigin
        && e.Transliteration == l.Transliteration
        && e.Pos == l.Pos
        && e.IsProperName == l.IsProperName
        && e.IsMorpheme == l.IsMorpheme
        && e.Occurrences == l.Occurrences
        && e.FrequencyRank == l.FrequencyRank
        && e.AudioObjectKey == audio;

    private static bool MeaningsDiffer(List<DbMeaning> current, List<VocabSeedMeaningLine> wanted)
    {
        if (current.Count != wanted.Count)
        {
            return true;
        }

        var byGerman = current.ToDictionary(m => m.German, StringComparer.Ordinal);

        return wanted.Any(w => !byGerman.TryGetValue(w.German, out var m)
            || m.GermanKey != w.GermanKey || m.Rank != w.Rank || m.Count != w.Count || m.Share != w.Share
            || m.IsAccepted != w.IsAccepted || m.Origin != w.Origin || m.IsPinned != w.IsPinned);
    }

    /// <summary>Makes the entry's keys equal to the seed's; a key held by another entry stays there and is counted as a conflict.</summary>
    private static async Task<int> WriteKeysAsync(IDbConnection c, IDbTransaction t, int entryId, List<VocabSeedKeyLine> wanted, Dictionary<string, int> owner, List<DbKey> current)
    {
        var conflicts = 0;
        var wantedNames = wanted.Select(k => k.StrongsKey).ToHashSet(StringComparer.Ordinal);

        foreach (var key in current.Where(k => !wantedNames.Contains(k.StrongsKey)))
        {
            await c.ExecuteAsync(DeleteKey, new { EntryId = entryId, key.StrongsKey }, t);
            owner.Remove(key.StrongsKey);
        }

        foreach (var key in wanted)
        {
            if (owner.TryGetValue(key.StrongsKey, out var holder) && holder != entryId)
            {
                conflicts++;

                continue;
            }

            await c.ExecuteAsync(InsertKey, new { EntryId = entryId, key.StrongsKey, key.Occurrences }, t);
            owner[key.StrongsKey] = entryId;
        }

        return conflicts;
    }

    private static async Task WriteMeaningsAsync(IDbConnection c, IDbTransaction t, int entryId, List<VocabSeedMeaningLine> wanted, List<DbMeaning> current)
    {
        var wantedNames = wanted.Select(m => m.German).ToHashSet(StringComparer.Ordinal);

        foreach (var meaning in current.Where(m => !wantedNames.Contains(m.German)))
        {
            await c.ExecuteAsync(DeleteMeaning, new { meaning.Id }, t);
        }

        foreach (var meaning in wanted)
        {
            await c.ExecuteAsync(UpsertMeaning, new { EntryId = entryId, meaning.German, meaning.GermanKey, meaning.Rank, meaning.Count, meaning.Share, meaning.IsAccepted, meaning.Origin, meaning.IsPinned }, t);
        }
    }

    private static async Task<int> BumpAsync(IDbConnection c, IDbTransaction t, int entryId, string kind, object before, object after)
    {
        var version = await c.QuerySingleAsync<int>(BumpVersion, new { Id = entryId }, t);
        await c.ExecuteAsync(InsertChange, new { EntryId = entryId, ContentVersion = version, Kind = kind, Before = JsonSerializer.Serialize(before), After = JsonSerializer.Serialize(after) }, t);

        return 1;
    }

    private sealed class DbEntry
    {
        public int Id { get; init; }
        public string Track { get; init; } = string.Empty;
        public string Lemma { get; init; } = string.Empty;
        public int LanguageId { get; init; }
        public string LemmaNormalized { get; init; } = string.Empty;
        public string? DictionaryForm { get; init; }
        public string? DictionaryFormOrigin { get; init; }
        public string? Transliteration { get; init; }
        public string? Pos { get; init; }
        public bool IsProperName { get; init; }
        public bool IsMorpheme { get; init; }
        public int Occurrences { get; init; }
        public int? FrequencyRank { get; init; }
        public string? AudioObjectKey { get; init; }
        public bool Retired { get; init; }
    }

    private sealed class DbKey
    {
        public int EntryId { get; init; }
        public string StrongsKey { get; init; } = string.Empty;
        public int Occurrences { get; init; }
    }

    private sealed class DbMeaning
    {
        public int Id { get; init; }
        public int EntryId { get; init; }
        public string German { get; init; } = string.Empty;
        public string? GermanKey { get; init; }
        public int Rank { get; init; }
        public int? Count { get; init; }
        public decimal? Share { get; init; }
        public bool IsAccepted { get; init; }
        public string Origin { get; init; } = string.Empty;
        public bool IsPinned { get; init; }
    }
}
