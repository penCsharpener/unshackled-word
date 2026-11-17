using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Application.Features.Seed.Revision;
using UnshackledWord.Infrastructure.Seed.Mappings;

namespace UnshackledWord.Infrastructure.Seed.Revision;

/// <summary>The variants of one book and their mapping lines, as the seed files hold them.</summary>
public sealed class RevisionBookData
{
    public required List<RevisionVerseSeedLine> Verses { get; init; }
    public required List<RevisionMappingSeedLine> Mapping { get; init; }
}

/// <summary>Read-only access for the revision export (roadmap 0011.11): levels, active variants and their mapping, keyed without database ids.</summary>
public sealed class RevisionExportRepository
{
    private readonly IDbReader _dbReader;

    public RevisionExportRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    private const string SelectLevels = """
        SELECT l."Level"         AS "Level"
             , l."Code"          AS "Code"
             , l."NameDe"        AS "NameDe"
             , l."NameEn"        AS "NameEn"
             , l."DescriptionDe" AS "DescriptionDe"
             , l."DescriptionEn" AS "DescriptionEn"
             , l."IsDefault"     AS "IsDefault"
        FROM "unshackled-word"."Revisions" l
        ORDER BY l."Level"
               , l."Id"
        """;

    private const string SelectVerses = """
        SELECT rv."HebRefId"                      AS "Ref"
             , rev."Level"                        AS "Level"
             , brev."Level"                       AS "BasedOnLevel"
             , rv."BaseTextHash"                  AS "BaseHash"
             , rv."VerseText"                     AS "Text"
             , rv."Note"                          AS "Note"
             , (rv."MappingReviewedAt" IS NOT NULL) AS "Reviewed"
        FROM "unshackled-word"."RevisionVerses" rv
            JOIN "unshackled-word"."Revisions" rev ON rev."Id" = rv."RevisionId"
            LEFT JOIN "unshackled-word"."Revisions" brev ON brev."Id" = rv."BasedOnRevisionId"
        WHERE rv."HebRefId" BETWEEN @FromRefId AND @ToRefId
        ORDER BY rv."HebRefId"
               , rev."Level"
        """;

    private const string SelectMapping = """
        SELECT rv."HebRefId"                                  AS "Ref"
             , rev."Level"                                    AS "Level"
             , rw."PositionInVerse"                           AS "Pos"
             , rw."PlainWord"                                 AS "Form"
             , m."GermanWordPart"                             AS "Part"
             , COALESCE(sh."LxxRefId", sg."LxxRefId")         AS "SrcRef"
             , COALESCE(sh."PositionInVerse", sg."PositionInVerse") AS "SrcPos"
             , COALESCE(sh."Hebrew", sg."Greek")              AS "SrcText"
             , m."IsAddedWord"                                AS "Added"
             , pw."PositionInVerse"                           AS "ParentPos"
             , pw."PlainWord"                                 AS "ParentForm"
             , m."Origin"                                     AS "Origin"
             , m."Role"                                       AS "Role"
             , m."StrongsNumber"                              AS "Strongs"
             , m."PartLemma"                                  AS "PartLemma"
             , m."MultiHead"                                  AS "MultiHead"
        FROM "unshackled-word"."RevisionVerses" rv
            JOIN "unshackled-word"."Revisions" rev ON rev."Id" = rv."RevisionId"
            JOIN "unshackled-word"."RevisionWords" rw ON rw."RevisionVerseId" = rv."Id"
            JOIN "unshackled-word"."RevisionMapping" m ON m."RevisionWordId" = rw."Id"
            LEFT JOIN "unshackled-word"."StepHebrewWords" sh ON m."Lang" = 'H' AND sh."Id" = m."StepWordId"
            LEFT JOIN "unshackled-word"."StepGreekWords" sg ON m."Lang" = 'G' AND sg."Id" = m."StepWordId"
            LEFT JOIN "unshackled-word"."RevisionWords" pw ON pw."Id" = m."ParentRevisionWordId"
        WHERE rv."HebRefId" BETWEEN @FromRefId AND @ToRefId
            AND (m."IsAddedWord" OR COALESCE(sh."Id", sg."Id") IS NOT NULL)
        """;

    private const string CountActiveMapping = """
        SELECT count(*)
        FROM "unshackled-word"."RevisionMapping" m
        """;

    public async Task<List<RevisionLevelSeedLine>> ReadLevelsAsync()
    {
        return (await _dbReader.ReadAsListAsync<RevisionLevelSeedLine>(SelectLevels)).ToList();
    }

    public async Task<int> CountActiveMappingRowsAsync()
    {
        return await _dbReader.ExecuteScalarAsync<int>(CountActiveMapping);
    }

    /// <summary>The active variants of one book (<paramref name="bookId"/> 1-66) and their mapping lines, sorted for a stable file.</summary>
    public async Task<RevisionBookData> ReadBookAsync(int bookId)
    {
        var range = new { FromRefId = bookId * 1_000_000, ToRefId = bookId * 1_000_000 + 999_999 };
        var verses = (await _dbReader.ReadAsListAsync<RevisionVerseSeedLine>(SelectVerses, range)).ToList();
        var rows = await _dbReader.ReadAsListAsync<MappingRow>(SelectMapping, range);

        var mapping = rows.Select(ToLine)
            .OrderBy(l => l.Ref)
            .ThenBy(l => l.Level)
            .ThenBy(l => l.Pos)
            .ThenBy(l => l.SrcRef ?? -1)
            .ThenBy(l => l.SrcPos ?? -1)
            .ThenBy(l => l.SrcHash, StringComparer.Ordinal)
            .ThenBy(l => l.Part, StringComparer.Ordinal)
            .ThenBy(l => l.Strongs, StringComparer.Ordinal)
            .ToList();

        return new RevisionBookData
        {
            Verses = verses.OrderBy(v => v.Ref).ThenBy(v => v.Level).ToList(),
            Mapping = mapping
        };
    }

    private static RevisionMappingSeedLine ToLine(MappingRow row)
    {
        var part = MappingExportRepository.CleanPart(row.Part);

        return new RevisionMappingSeedLine
        {
            Ref = row.Ref,
            Level = row.Level,
            Pos = row.Pos,
            Form = row.Form ?? string.Empty,
            Part = part,
            SrcRef = row.SrcRef,
            SrcPos = row.SrcPos,
            SrcHash = row.SrcRef.HasValue ? SourceKey.Hash(row.SrcText) : string.Empty,
            Added = row.Added,
            ParentPos = row.ParentPos,
            ParentForm = row.ParentPos.HasValue ? row.ParentForm ?? string.Empty : string.Empty,
            Origin = row.Origin,
            Role = string.IsNullOrEmpty(row.Role) ? "head" : row.Role,
            Strongs = row.Strongs ?? string.Empty,
            PartLemma = part.Length > 0 ? row.PartLemma ?? string.Empty : string.Empty,
            MultiHead = row.MultiHead
        };
    }

    private sealed class MappingRow
    {
        public int Ref { get; init; }
        public int Level { get; init; }
        public int Pos { get; init; }
        public string? Form { get; init; }
        public string? Part { get; init; }
        public int? SrcRef { get; init; }
        public int? SrcPos { get; init; }
        public string? SrcText { get; init; }
        public bool Added { get; init; }
        public int? ParentPos { get; init; }
        public string? ParentForm { get; init; }
        public string Origin { get; init; } = string.Empty;
        public string? Role { get; init; }
        public string? Strongs { get; init; }
        public string? PartLemma { get; init; }
        public bool MultiHead { get; init; }
    }
}
