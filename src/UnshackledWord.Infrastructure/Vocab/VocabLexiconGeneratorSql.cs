namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>
/// The SQL steps of <see cref="VocabLexiconGenerator"/>, run in order inside one transaction on temp tables (roadmap 0022.1,
/// docs/roadmaps/0022-vocab-trainer/03-vocab-model.md "Generator").
/// </summary>
internal static class VocabLexiconGeneratorSql
{
    /// <summary>
    /// The temp tables live until the transaction ends; a second run in the same transaction (tests) starts clean.
    /// </summary>
    public const string DropTemp = """
        DROP TABLE IF EXISTS pg_temp."VocabLemmaNorm", pg_temp."VocabKeyOccurrences", pg_temp."VocabNamedStrongs", pg_temp."VocabKeys",
            pg_temp."VocabGroups", pg_temp."VocabPreExisting", pg_temp."VocabEntryLemmas", pg_temp."VocabTargets", pg_temp."VocabNewKeys"
        """;

    /// <summary>The distinct (track, lemma) pairs of the lexicon; the generator normalizes them in code.</summary>
    public const string SelectLemmas = """
        SELECT DISTINCT CASE WHEN l."LanguageId" = 2 THEN 'G' ELSE 'H' END AS "Track"
             , l."OriginalWord"                                           AS "Lemma"
        FROM "unshackled-word"."StepStrongsLexicon" l
        WHERE 1=1
            AND l."OriginalWord" <> ''
        """;

    public const string CreateLemmaNorm = """
        CREATE TEMP TABLE "VocabLemmaNorm"
        (
            "Track"      char(1)                         NOT NULL,
            "Lemma"      varchar(50) COLLATE "und-x-icu" NOT NULL,
            "Normalized" varchar(50) COLLATE "und-x-icu" NOT NULL
        ) ON COMMIT DROP
        """;

    public const string FillLemmaNorm = """
        INSERT INTO "VocabLemmaNorm" ("Track", "Lemma", "Normalized")
        SELECT x."Track", x."Lemma", x."Normalized"
        FROM unnest(@Tracks::char(1)[], @Lemmas::varchar[], @Normalized::varchar[]) AS x("Track", "Lemma", "Normalized")
        """;

    /// <summary>
    /// Occurrences per Strong's key from the STEP word tables. Greek: "DisambiguatedStrongs". Hebrew: the main morpheme of
    /// "ExpandedStrongTags" (HebrewMorphemes, 0032), otherwise the braced part of "DisambiguatedStrongs": the same rule as
    /// "StatsVocabRenderings" (0038), so the keys match.
    /// </summary>
    public const string CreateKeyOccurrences = """
        CREATE TEMP TABLE "VocabKeyOccurrences" ON COMMIT DROP AS
        SELECT 'G'::char(1)         AS "Track"
             , upper(g."DisambiguatedStrongs")::varchar AS "StrongsKey"
             , count(*)::int        AS "Occurrences"
        FROM "unshackled-word"."StepGreekWords" g
        GROUP BY upper(g."DisambiguatedStrongs")
        UNION ALL
        SELECT 'H'::char(1)
             , x."StrongsKey"
             , count(*)::int
        FROM (
            SELECT upper(COALESCE((SELECT hm."Strongs"
                                   FROM "unshackled-word"."HebrewMorphemes"(h."ExpandedStrongTags") hm
                                   WHERE hm."IsMain"
                                   ORDER BY hm."Ord"
                                   LIMIT 1),
                                  CASE WHEN position('{' IN h."DisambiguatedStrongs") > 0
                                       THEN NULLIF(split_part(split_part(h."DisambiguatedStrongs", '{', 2), '}', 1), '')
                                       ELSE h."DisambiguatedStrongs" END))::varchar AS "StrongsKey"
            FROM "unshackled-word"."StepHebrewWords" h
        ) x
        WHERE 1=1
            AND x."StrongsKey" IS NOT NULL
        GROUP BY x."StrongsKey"
        """;

    /// <summary>Strong's base numbers (G2424, H175) of the STEP person and place lexicons: names.</summary>
    public const string CreateNamedStrongs = """
        CREATE TEMP TABLE "VocabNamedStrongs" ON COMMIT DROP AS
        SELECT DISTINCT upper(left(btrim(x."Strongs"), 1)) || (substring(x."Strongs" FROM '\d+')::int)::text AS "Base"
        FROM (
            SELECT p."Strongs" FROM "unshackled-word"."StepPersonLexicon" p
            UNION ALL
            SELECT l."Strongs" FROM "unshackled-word"."StepPlaceLexicon" l
        ) x
        WHERE 1=1
            AND btrim(x."Strongs") ~ '^[GgHh]\d+'
        """;

    /// <summary>
    /// One row per Strong's key of the lexicon. The key is built like "StatsStrongsRenderings"/"StatsVocabRenderings": G/H, the number
    /// zero-padded to four digits, then the upper-case lexicon "Extra" (H1254A, G0846, G3004G).
    /// </summary>
    public const string CreateKeys = """
        CREATE TEMP TABLE "VocabKeys" ON COMMIT DROP AS
        SELECT k."Track"
             , k."StrongsKey"
             , k."LexiconId"
             , k."LanguageId"
             , k."Number"
             , k."OriginalWord"
             , k."Transliteration"
             , k."Morphology"
             , k."Lexicon"
             , COALESCE(o."Occurrences", 0)                                   AS "Occurrences"
             , (k."Track" = 'H' AND k."Number" BETWEEN 9001 AND 9049)         AS "IsMorpheme"
             , (   k."Morphology" ~ '^[A-Z]:N-.*-P(RI|G|L)?( |$)'
                OR EXISTS (SELECT 1 FROM "VocabNamedStrongs" n WHERE n."Base" = k."Track" || k."Number"::text)) AS "IsProper"
        FROM (
            SELECT DISTINCT ON (a."Track", a."StrongsKey") a.*
            FROM (
                SELECT CASE WHEN l."LanguageId" = 2 THEN 'G' ELSE 'H' END::char(1) AS "Track"
                     , (CASE WHEN l."LanguageId" = 2 THEN 'G' ELSE 'H' END
                        || CASE WHEN l."Number" >= 10000 THEN l."Number"::text ELSE lpad(l."Number"::text, 4, '0') END
                        || upper(COALESCE(l."Extra", '')))::varchar                AS "StrongsKey"
                     , l."Id"                                                      AS "LexiconId"
                     , l."LanguageId"                                              AS "LanguageId"
                     , l."Number"                                                  AS "Number"
                     , l."OriginalWord"                                            AS "OriginalWord"
                     , l."Transliteration"                                         AS "Transliteration"
                     , l."Morphology"                                              AS "Morphology"
                     , l."Lexicon"                                                 AS "Lexicon"
                FROM "unshackled-word"."StepStrongsLexicon" l
                WHERE 1=1
                    AND l."OriginalWord" <> ''
            ) a
            WHERE 1=1
                AND a."StrongsKey" ~ '^[GH]\d+[A-Z]?$'
            ORDER BY a."Track", a."StrongsKey", a."LexiconId"
        ) k
            LEFT JOIN "VocabKeyOccurrences" o ON o."Track" = k."Track" AND o."StrongsKey" = k."StrongsKey"
        """;

    /// <summary>
    /// One row per (track, lemma): the representative key (most occurrences, then lowest number) gives transliteration, POS, language and
    /// the lexicon line. A dictionary form is generated only for Greek nouns ("λόγος, -ου, ὁ") and three-ending adjectives
    /// ("μέσος, -η, -ον") whose lexicon line starts with exactly the lemma followed by those endings; anything else stays null.
    /// </summary>
    public const string CreateGroups = """
        CREATE TEMP TABLE "VocabGroups" ON COMMIT DROP AS
        SELECT DISTINCT ON (a."Track", a."OriginalWord")
               a."Track"
             , a."OriginalWord"                       AS "Lemma"
             , a."LanguageId"
             , a."Transliteration"
             , left(a."Morphology", 20)               AS "Pos"
             , a."GroupOccurrences"                   AS "Occurrences"
             , a."GroupIsMorpheme"                    AS "IsMorpheme"
             , a."GroupIsProper"                      AS "IsProperName"
             , COALESCE(
                   (SELECT CASE WHEN m[1] = a."OriginalWord" THEN m[1] || ', ' || m[2] || ', ' || m[3] END
                    FROM regexp_match(a."Lexicon", '^<b>([^<]+)</b>, (-[Ͱ-Ͽἀ-῿]+), (ὁ|ἡ|τό)(?=[ ,(<]|$)') m
                    WHERE a."Track" = 'G' AND a."Morphology" ~ '^G:N-'),
                   (SELECT CASE WHEN m[1] = a."OriginalWord" THEN m[1] || ', ' || m[2] || ', ' || m[3] END
                    FROM regexp_match(a."Lexicon", '^<b>([^<]+)</b>, (-[Ͱ-Ͽἀ-῿]+), (-[Ͱ-Ͽἀ-῿]+)(?=[ ,(<]|$)') m
                    WHERE a."Track" = 'G' AND a."Morphology" ~ '^G:A-'))::varchar AS "GeneratedForm"
        FROM (
            SELECT k.*
                 , sum(k."Occurrences") OVER (PARTITION BY k."Track", k."OriginalWord")::int AS "GroupOccurrences"
                 , bool_or(k."IsMorpheme") OVER (PARTITION BY k."Track", k."OriginalWord")   AS "GroupIsMorpheme"
                 , bool_or(k."IsProper") OVER (PARTITION BY k."Track", k."OriginalWord")     AS "GroupIsProper"
            FROM "VocabKeys" k
        ) a
        ORDER BY a."Track", a."OriginalWord", a."Occurrences" DESC, a."Number", a."StrongsKey"
        """;

    /// <summary>Indexes and statistics for the temp tables: without them the lemma lookups below scan 12,000 x 12,000 collated strings.</summary>
    public const string IndexTemp = """
        CREATE INDEX ON "VocabKeys" ("Track", "OriginalWord");
        CREATE INDEX ON "VocabKeys" ("Track", "StrongsKey");
        CREATE INDEX ON "VocabGroups" ("Track", "Lemma");
        CREATE INDEX ON "VocabLemmaNorm" ("Track", "Lemma");
        ANALYZE "VocabKeys";
        ANALYZE "VocabGroups";
        ANALYZE "VocabLemmaNorm"
        """;

    public const string CreatePreExisting = """
        CREATE TEMP TABLE "VocabPreExisting" ON COMMIT DROP AS
        SELECT e."Id" FROM "unshackled-word"."VocabEntries" e
        """;

    /// <summary>The (track, lemma) lookup of the entries, kept current while entries are created.</summary>
    public const string CreateEntryLemmas = """
        CREATE TEMP TABLE "VocabEntryLemmas" ON COMMIT DROP AS
        SELECT e."Id", e."Track", e."Lemma", e."RetiredAt"
        FROM "unshackled-word"."VocabEntries" e;
        CREATE INDEX ON "VocabEntryLemmas" ("Track", "Lemma");
        ANALYZE "VocabEntryLemmas"
        """;

    /// <summary>New entries: groups with text occurrences (or a morpheme) that no existing key and no existing entry of the same lemma covers.</summary>
    public const string InsertEntries = """
        WITH "Inserted" AS
        (
        INSERT INTO "unshackled-word"."VocabEntries"
            ("Track", "LanguageId", "Lemma", "LemmaNormalized", "DictionaryForm", "DictionaryFormOrigin", "Transliteration", "Pos", "IsProperName", "IsMorpheme", "Occurrences")
        SELECT g."Track"
             , g."LanguageId"
             , g."Lemma"
             , n."Normalized"
             , g."GeneratedForm"
             , CASE WHEN g."GeneratedForm" IS NULL THEN NULL ELSE 'generated' END
             , NULLIF(g."Transliteration", '')
             , NULLIF(g."Pos", '')
             , g."IsProperName"
             , g."IsMorpheme"
             , g."Occurrences"
        FROM "VocabGroups" g
            JOIN "VocabLemmaNorm" n ON n."Track" = g."Track" AND n."Lemma" = g."Lemma"
        WHERE 1=1
            AND n."Normalized" <> ''
            AND (g."Occurrences" > 0 OR g."IsMorpheme")
            AND NOT EXISTS (SELECT 1
                            FROM "VocabEntryLemmas" e
                            WHERE e."Track" = g."Track" AND e."Lemma" = g."Lemma")
            AND NOT EXISTS (SELECT 1
                            FROM "VocabKeys" k
                                JOIN "unshackled-word"."VocabStrongs" vs ON vs."Lang" = k."Track" AND vs."StrongsKey" = k."StrongsKey"
                            WHERE k."Track" = g."Track" AND k."OriginalWord" = g."Lemma")
        ORDER BY g."Track", g."Occurrences" DESC, g."Lemma"
        RETURNING "Id", "Track", "Lemma", "RetiredAt"
        )
        INSERT INTO "VocabEntryLemmas" ("Id", "Track", "Lemma", "RetiredAt")
        SELECT i."Id", i."Track", i."Lemma", i."RetiredAt" FROM "Inserted" i
        """;

    /// <summary>The entry a lemma group belongs to: the entry of one of its existing keys, otherwise the (live) entry with the same lemma.</summary>
    public const string CreateTargets = """
        CREATE TEMP TABLE "VocabTargets" ON COMMIT DROP AS
        SELECT g."Track"
             , g."Lemma"
             , COALESCE(
                   (SELECT min(vs."VocabEntryId")
                    FROM "VocabKeys" k
                        JOIN "unshackled-word"."VocabStrongs" vs ON vs."Lang" = k."Track" AND vs."StrongsKey" = k."StrongsKey"
                    WHERE k."Track" = g."Track" AND k."OriginalWord" = g."Lemma"),
                   (SELECT e."Id"
                    FROM "VocabEntryLemmas" e
                    WHERE e."Track" = g."Track" AND e."Lemma" = g."Lemma"
                    ORDER BY (e."RetiredAt" IS NOT NULL), e."Id"
                    LIMIT 1)) AS "EntryId"
        FROM "VocabGroups" g
        """;

    public const string CreateNewKeys = """
        CREATE TEMP TABLE "VocabNewKeys"
        (
            "EntryId"    int4        NOT NULL,
            "StrongsKey" varchar(10) NOT NULL
        ) ON COMMIT DROP
        """;

    public const string InsertKeys = """
        WITH "Inserted" AS
        (
            INSERT INTO "unshackled-word"."VocabStrongs" ("Lang", "StrongsKey", "VocabEntryId", "Occurrences")
            SELECT k."Track", k."StrongsKey", t."EntryId", k."Occurrences"
            FROM "VocabKeys" k
                JOIN "VocabTargets" t ON t."Track" = k."Track" AND t."Lemma" = k."OriginalWord"
            WHERE 1=1
                AND t."EntryId" IS NOT NULL
                AND NOT EXISTS (SELECT 1
                                FROM "unshackled-word"."VocabStrongs" vs
                                WHERE vs."Lang" = k."Track" AND vs."StrongsKey" = k."StrongsKey")
            ON CONFLICT DO NOTHING
            RETURNING "VocabEntryId", "StrongsKey"
        )
        INSERT INTO "VocabNewKeys" ("EntryId", "StrongsKey")
        SELECT i."VocabEntryId", i."StrongsKey" FROM "Inserted" i
        """;

    /// <summary>New keys on an entry that existed before this run change what the entry covers: ContentVersion + log row.</summary>
    public const string LogAddedKeys = """
        WITH "Added" AS
        (
            SELECT n."EntryId", jsonb_agg(n."StrongsKey" ORDER BY n."StrongsKey") AS "Keys"
            FROM "VocabNewKeys" n
            WHERE n."EntryId" IN (SELECT p."Id" FROM "VocabPreExisting" p)
            GROUP BY n."EntryId"
        )
        , "Bumped" AS
        (
            UPDATE "unshackled-word"."VocabEntries" e
            SET "ContentVersion" = e."ContentVersion" + 1
              , "UpdatedAt" = now()
            FROM "Added" a
            WHERE e."Id" = a."EntryId"
            RETURNING e."Id", e."ContentVersion"
        )
        INSERT INTO "unshackled-word"."VocabEntryChanges" ("VocabEntryId", "ContentVersion", "Kind", "After")
        SELECT b."Id", b."ContentVersion", 'keys', jsonb_build_object('added', a."Keys")
        FROM "Bumped" b
            JOIN "Added" a ON a."EntryId" = b."Id"
        """;

    /// <summary>A rerun refreshes only generated forms (or fills a null one); manual and agent forms are never touched.</summary>
    public const string UpdateGeneratedForms = """
        WITH "Candidates" AS
        (
            SELECT DISTINCT ON (e."Id") e."Id", e."DictionaryForm" AS "Before", g."GeneratedForm" AS "After"
            FROM "unshackled-word"."VocabEntries" e
                JOIN "VocabTargets" t ON t."EntryId" = e."Id"
                JOIN "VocabGroups" g ON g."Track" = t."Track" AND g."Lemma" = t."Lemma"
            WHERE 1=1
                AND g."GeneratedForm" IS NOT NULL
                AND (e."DictionaryForm" IS NULL OR e."DictionaryFormOrigin" = 'generated')
                AND e."DictionaryForm" IS DISTINCT FROM g."GeneratedForm"
            ORDER BY e."Id", g."Occurrences" DESC, g."Lemma"
        )
        , "Updated" AS
        (
            UPDATE "unshackled-word"."VocabEntries" e
            SET "DictionaryForm" = c."After"
              , "DictionaryFormOrigin" = 'generated'
              , "ContentVersion" = e."ContentVersion" + 1
              , "UpdatedAt" = now()
            FROM "Candidates" c
            WHERE e."Id" = c."Id"
            RETURNING e."Id", e."ContentVersion", c."Before", c."After"
        )
        INSERT INTO "unshackled-word"."VocabEntryChanges" ("VocabEntryId", "ContentVersion", "Kind", "Before", "After")
        SELECT u."Id"
             , u."ContentVersion"
             , 'form'
             , CASE WHEN u."Before" IS NULL THEN NULL ELSE jsonb_build_object('dictionaryForm', u."Before"::text) END
             , jsonb_build_object('dictionaryForm', u."After"::text)
        FROM "Updated" u
        """;

    public const string UpdateKeyOccurrences = """
        UPDATE "unshackled-word"."VocabStrongs" vs
        SET "Occurrences" = x."Occurrences"
        FROM (
            SELECT v."Lang", v."StrongsKey", COALESCE(o."Occurrences", 0) AS "Occurrences"
            FROM "unshackled-word"."VocabStrongs" v
                LEFT JOIN "VocabKeyOccurrences" o ON o."Track" = v."Lang" AND o."StrongsKey" = v."StrongsKey"
        ) x
        WHERE 1=1
            AND vs."Lang" = x."Lang"
            AND vs."StrongsKey" = x."StrongsKey"
            AND vs."Occurrences" IS DISTINCT FROM x."Occurrences"
        """;

    public const string UpdateEntryOccurrences = """
        UPDATE "unshackled-word"."VocabEntries" e
        SET "Occurrences" = s."Occurrences"
        FROM (
            SELECT e2."Id", COALESCE(sum(vs."Occurrences"), 0)::int AS "Occurrences"
            FROM "unshackled-word"."VocabEntries" e2
                LEFT JOIN "unshackled-word"."VocabStrongs" vs ON vs."VocabEntryId" = e2."Id"
            GROUP BY e2."Id"
        ) s
        WHERE 1=1
            AND e."Id" = s."Id"
            AND e."Occurrences" IS DISTINCT FROM s."Occurrences"
        """;

    /// <summary>Flags only ever turn on here: a maintainer who marked a name by hand keeps it.</summary>
    public const string UpdateFlags = """
        UPDATE "unshackled-word"."VocabEntries" e
        SET "IsMorpheme" = e."IsMorpheme" OR f."IsMorpheme"
          , "IsProperName" = e."IsProperName" OR f."IsProper"
          , "FrequencyRank" = CASE WHEN e."IsMorpheme" OR f."IsMorpheme" THEN NULL ELSE e."FrequencyRank" END
        FROM (
            SELECT vs."VocabEntryId" AS "Id", bool_or(k."IsMorpheme") AS "IsMorpheme", bool_or(k."IsProper") AS "IsProper"
            FROM "unshackled-word"."VocabStrongs" vs
                JOIN "VocabKeys" k ON k."Track" = vs."Lang" AND k."StrongsKey" = vs."StrongsKey"
            GROUP BY vs."VocabEntryId"
        ) f
        WHERE 1=1
            AND e."Id" = f."Id"
            AND ((f."IsMorpheme" AND NOT e."IsMorpheme") OR (f."IsProper" AND NOT e."IsProperName"))
        """;

    /// <summary>
    /// Course order per track: occurrences desc, then the lowest Strong's number of the entry, then id. Morphemes, retired entries and
    /// entries without text occurrences stay unranked. No ContentVersion bump: a rank is a position, not content. The unique constraint
    /// is deferred, so the renumbering is one statement.
    /// </summary>
    public const string UpdateRanks = """
        WITH "Ranked" AS
        (
            SELECT e."Id"
                 , row_number() OVER (PARTITION BY e."Track" ORDER BY e."Occurrences" DESC, k."MinNumber", e."Id")::int AS "Rank"
            FROM "unshackled-word"."VocabEntries" e
                JOIN (SELECT vs."VocabEntryId", min(substring(vs."StrongsKey" FROM '^[GH](\d+)')::int) AS "MinNumber"
                      FROM "unshackled-word"."VocabStrongs" vs
                      GROUP BY vs."VocabEntryId") k ON k."VocabEntryId" = e."Id"
            WHERE 1=1
                AND NOT e."IsMorpheme"
                AND e."RetiredAt" IS NULL
                AND e."Occurrences" > 0
        )
        UPDATE "unshackled-word"."VocabEntries" e
        SET "FrequencyRank" = r."Rank"
        FROM "Ranked" r
        WHERE 1=1
            AND e."Id" = r."Id"
            AND e."FrequencyRank" IS DISTINCT FROM r."Rank"
        """;

    public const string ClearStaleRanks = """
        UPDATE "unshackled-word"."VocabEntries" e
        SET "FrequencyRank" = NULL
        WHERE 1=1
            AND e."FrequencyRank" IS NOT NULL
            AND (e."IsMorpheme" OR e."RetiredAt" IS NOT NULL OR e."Occurrences" <= 0
                 OR NOT EXISTS (SELECT 1 FROM "unshackled-word"."VocabStrongs" vs WHERE vs."VocabEntryId" = e."Id"))
        """;

    public const string SelectSummary = """
        SELECT e."Track"
             , count(*)::int                                          AS "Entries"
             , count(e."FrequencyRank")::int                          AS "Ranked"
             , count(*) FILTER (WHERE e."IsMorpheme")::int            AS "Morphemes"
             , count(*) FILTER (WHERE e."IsProperName")::int          AS "ProperNames"
             , count(e."DictionaryForm")::int                         AS "Forms"
             , count(*) FILTER (WHERE (SELECT count(*) FROM "unshackled-word"."VocabStrongs" vs WHERE vs."VocabEntryId" = e."Id") > 1)::int AS "MultiKey"
        FROM "unshackled-word"."VocabEntries" e
        GROUP BY e."Track"
        ORDER BY e."Track"
        """;
}
