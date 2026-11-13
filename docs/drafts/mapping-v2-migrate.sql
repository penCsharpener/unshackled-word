-- =====================================================================================================================
-- Mapping v2: migrate legacy Elb1871GreekMapping / Elb1871HebrewMapping into the v2 tables
-- Draft for roadmap 0005. Target script name: scripts/0010_MappingV2Migrate.sql (runs after 0009_MappingV2Schema.sql).
-- Requires docs/drafts/mapping-v2-schema.sql to be applied. Legacy tables are only READ, never changed.
--
-- Idempotent: every write is skipped when "MappingLegacyRows" already has rows (temp table mig_guard), except the
-- SourceWords sync ("SyncSourceWords" from the schema script), which is idempotent by itself and also re-matches
-- after a STEP re-import. On a fresh install the migration scripts run before the seed, so this script is a no-op
-- there; SeedDb runs the same SQL after the legacy mapping import (roadmap 0005.3).
-- DbUp (Tooling.Postgres.Migration/Worker.cs) runs without a DbUp transaction and with the default 30 s command
-- timeout, so the script's own BEGIN/COMMIT stays, Worker.cs needs .WithExecutionTimeout(...) (roadmap 0005.2), and
-- the verification block moves out of the shipped script into the roadmap step (it only reads).
-- Runtime on a full copy of the dev DB (726,487 legacy rows, postgres:18 container): ~3.5 minutes including the
-- verification block; the component labelling loop (13 rounds) is the largest part.
--
-- ---------------------------------------------------------------------------------------------------------------------
-- HOW LEGACY ROWS BECOME LINKS
-- ---------------------------------------------------------------------------------------------------------------------
-- Legacy row = one edge (ElbWordId, StepWordId) or one "added word" row (StepWordId NULL, IsAddedWord, Parent...).
-- 1. Every row with a StepWordId is an EDGE between a German node (word, part) and a source node (SourceWords row).
--    German "part" = GermanWordPart when it is a real part of the word; whole-word copies and '*' count as whole word.
-- 2. Connected components of the edge graph become 'aligned' links. That is the only lossless grouping under the v2
--    invariant "a source word is in at most one active link" ("Und die Erde" -> וְהָאָרֶץ = one 3:1 link).
--    Measured on the DB copy: 419,739 components, 3,634 with >= 6 tokens, 198 with >= 10, largest 46 (over-assigned
--    AI rows such as Joh 18:19 "Der" -> Ὁ, τὸν, τῶν, τῆς chain neighbouring words together); 200 span two German verses.
-- 3. Added-word rows (StepWordId NULL, IsAddedWord) join the component of their parent as role 'added'. When the
--    parent has no component itself, the chain of added parents is followed (max 20 steps). When no component is
--    reached, or the parent is missing / the word itself / in another verse, the added words form an 'added' link per
--    chain root.
-- 4. Every legacy row gets one "MappingLegacyRows" entry with its link and a treatment code. Counts are Greek / Hebrew
--    rows from the run on a full copy of the dev DB (2026-11-10):
--      link                          edge, kept as is (131,169 / 495,683)
--      link_added_flag_ignored       IsAddedWord = TRUE but a StepWordId is set (624 / 49,852): treated as a normal core
--                                    link, exactly as AiMappingImportRunner and the readers do today
--      link_parent_dropped           IsAddedWord = FALSE with a parent and a StepWordId (5,906 / 1,597): parent ignored
--      added_attached                added word inside its parent's link (31,753 / 8,228)
--      added_attached_via_chain      parent was itself an added word; attached to an ancestor's link (363 / 52)
--      added_standalone_no_parent    IsAddedWord without parent (50 / 26)
--      added_standalone_parent_self  parent = the word itself (4 / 28)
--      added_standalone_parent_other_verse  parent in another verse (32 / 112)
--      added_standalone_parent_unmapped     parent (chain) has no link (12 / 117)
--      added_dropped_word_has_link   the word also has edge rows (157 / 613); the edge wins
--      empty_row                     no StepWordId, not added (1 / 108): carries no information
--      orphan_word / orphan_step     ElbWordId or StepWordId does not exist (0 / 0)
--    Result: 128,489 aligned + 86 added Greek links, 291,608 aligned + 195 added Hebrew links; 0 lost edges;
--    unmapped source words unchanged (13,579 Greek / 11,378 Hebrew); v2 tables incl. indexes ~470 MB
--    (legacy mapping tables: 103 MB; MappingLegacyRows alone is 82 MB and may be dropped once the cut-over is done).
-- 5. Provenance: one AiRun + one merged Changeset per (language, day of CreatedAt) under user 2 'ai-legacy'
--    (Greek: 3 days, Hebrew: 5 days). A link's changeset is the run of its newest row. All links are 'active' and
--    unconfirmed (no confirmation rows): the legacy data is AI output or indistinguishable hand fixes.
--
-- ---------------------------------------------------------------------------------------------------------------------
-- DROPPED OR APPROXIMATED (deliberately)
-- ---------------------------------------------------------------------------------------------------------------------
-- * Pairing inside n:m groups: v2 stores set <-> set. A group of 2 German and 2 source words no longer says which
--   German word went with which source word. Compat views expand groups as cross products (more pairs than before).
-- * Parent of non-added rows (link_parent_dropped) and the IsAddedWord flag on rows that have a StepWordId.
-- * Parent pointer of standalone added words whose parent is in another verse or the word itself.
-- * GermanWordPart when it equals the whole word (43,395 Hebrew + 65 Greek rows) or is '*' (7 Greek rows):
--   PartDropped = TRUE. Real parts (4,204 Hebrew, 3 Greek rows) are kept as PartText with a PartIndex derived from the
--   part's position inside the word (PartOrder was never stored).
-- * "StrongsNumber" (Greek): derived from STEP, 3,113 rows stale; v2 reads Strong's from STEP at query time.
-- * "HebRefId"/"PositionInVerse" of the mapping row: taken from Elb1871Words instead (275 Hebrew rows disagreed).
-- * "CreatedAt" per row: a link keeps the earliest CreatedAt of its rows; the AiRun keeps the day range. After a seed
--   import CreatedAt is the seed time, not the AI time (known since 0002).
-- * No 'covered' links are created: the legacy model had no such state. 13,579 Greek / 11,378 Hebrew source words
--   stay unmapped and are offered to the rule-based suggester (roadmap 0005.8).
-- =====================================================================================================================

BEGIN;

CREATE TEMP TABLE mig_guard ON COMMIT DROP AS
SELECT EXISTS (SELECT 1 FROM "unshackled-word"."MappingLegacyRows") AS done;

-- ---------------------------------------------------------------------------------------------------------------------
-- 1. Source word registry (idempotent on its own; first call inserts every STEP word, later calls re-match)
-- ---------------------------------------------------------------------------------------------------------------------
-- (A plain "INSERT ... ON CONFLICT DO NOTHING" top-up is not enough: after a changed STEP import the old rows keep
-- their numeric ExternalId, which then names a different word. Measured on a simulated re-import with one word
-- removed and one inserted: 50,914 registry rows / 45,932 active links pointed at the wrong word, V8 still 0.)
SELECT * FROM "unshackled-word"."SyncSourceWords"(1::smallint);
SELECT * FROM "unshackled-word"."SyncSourceWords"(2::smallint);

-- ---------------------------------------------------------------------------------------------------------------------
-- 2. Normalised legacy rows (temp)
-- ---------------------------------------------------------------------------------------------------------------------
CREATE TEMP TABLE mig_rows ON COMMIT DROP AS
SELECT r.lang
     , r.source_text_id
     , r.legacy_id
     , r.word_id
     , r.step_id
     , r.is_added
     , r.parent_id
     , r.part_raw
     , r.created_at
     , ew."Id" IS NOT NULL                                    word_exists
     , ew."HebRefId"                                          ref_id
     , sw."Id"                                                source_word_id
     , lower(coalesce(ew."PlainWord", ew."WordInContext"))    word_lower
     , CASE
           WHEN r.part_raw IS NULL OR r.part_raw = '*' THEN NULL
           WHEN lower(r.part_raw) = lower(ew."WordInContext") THEN NULL
           WHEN lower(r.part_raw) = lower(coalesce(ew."PlainWord", '')) THEN NULL
           WHEN lower(r.part_raw) = lower(regexp_replace(ew."WordInContext", '[^[:alpha:]]', '', 'g')) THEN NULL
           ELSE r.part_raw
           END                                                part_text
FROM (SELECT 'greek'::varchar(10)                          lang
           , 1::smallint                                   source_text_id
           , egm."Id"                                      legacy_id
           , egm."ElbWordId"                               word_id
           , egm."StepWordId"                              step_id
           , coalesce(egm."IsAddedWord", FALSE)            is_added
           , egm."ParentGermanWordId"                      parent_id
           , nullif(btrim(egm."GermanWordPart"), '')       part_raw
           , egm."CreatedAt"                               created_at
      FROM "unshackled-word"."Elb1871GreekMapping" egm
      UNION ALL
      SELECT 'hebrew'
           , 2::smallint
           , ehm."Id"
           , ehm."ElbWordId"
           , ehm."StepWordId"
           , coalesce(ehm."IsAddedWord", FALSE)
           , ehm."ParentGermanWordId"
           , nullif(btrim(ehm."GermanWordPart"), '')
           , ehm."CreatedAt"
      FROM "unshackled-word"."Elb1871HebrewMapping" ehm) r
    LEFT JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = r.word_id
    LEFT JOIN "unshackled-word"."SourceWords" sw ON sw."SourceTextId" = r.source_text_id AND sw."ExternalId" = r.step_id
WHERE 1=1
    AND NOT (SELECT done FROM mig_guard);

-- Edges: German node = word_id * 100 + part_index (parts are < 100), source node = -SourceWords.Id
CREATE TEMP TABLE mig_edge ON COMMIT DROP AS
SELECT e.*
     , e.word_id::bigint * 100 + e.part_index AS gnode
     , -e.source_word_id::bigint              AS snode
FROM (SELECT r.*
           , CASE
                 WHEN r.part_text IS NULL THEN 0
                 ELSE dense_rank() OVER (PARTITION BY r.lang, r.word_id, (r.part_text IS NULL)
                                         ORDER BY strpos(r.word_lower, lower(r.part_text)) = 0
                                                , strpos(r.word_lower, lower(r.part_text))
                                                , lower(r.part_text))
                 END::smallint AS part_index
      FROM mig_rows r
      WHERE 1=1
          AND r.word_exists
          AND r.source_word_id IS NOT NULL) e;

CREATE INDEX ON mig_edge (gnode);
CREATE INDEX ON mig_edge (snode);
ANALYZE mig_edge;

-- ---------------------------------------------------------------------------------------------------------------------
-- 3. Connected components (min-label propagation; converges in ~13 rounds on real data)
-- ---------------------------------------------------------------------------------------------------------------------
CREATE TEMP TABLE mig_lbl ON COMMIT DROP AS
SELECT n, n AS l
FROM (SELECT gnode AS n FROM mig_edge UNION SELECT snode FROM mig_edge) x;

CREATE UNIQUE INDEX ON mig_lbl (n);
ANALYZE mig_lbl;

DO
$$
    DECLARE
        changed INT := 1;
        rounds  INT := 0;
    BEGIN
        WHILE changed > 0 LOOP
            rounds := rounds + 1;
            WITH nb AS (SELECT e.gnode AS n, min(ls.l) AS l FROM mig_edge e JOIN mig_lbl ls ON ls.n = e.snode GROUP BY e.gnode
                        UNION ALL
                        SELECT e.snode, min(lg.l) FROM mig_edge e JOIN mig_lbl lg ON lg.n = e.gnode GROUP BY e.snode)
               , best AS (SELECT n, min(l) AS l FROM nb GROUP BY n)
            UPDATE mig_lbl
            SET l = best.l
            FROM best
            WHERE 1=1
                AND mig_lbl.n = best.n
                AND best.l < mig_lbl.l;
            GET DIAGNOSTICS changed = ROW_COUNT;
            IF rounds > 200 THEN
                RAISE EXCEPTION 'component labelling did not converge';
            END IF;
        END LOOP;
        RAISE NOTICE 'components labelled in % rounds', rounds;
    END
$$;

-- ---------------------------------------------------------------------------------------------------------------------
-- 4. Provenance: one AiRun + merged Changeset per (language, day)
-- ---------------------------------------------------------------------------------------------------------------------
CREATE TEMP TABLE mig_runs ON COMMIT DROP AS
SELECT x.*
     , nextval(pg_get_serial_sequence('"unshackled-word"."AiRuns"', 'Id'))::int     AS run_id
     , nextval(pg_get_serial_sequence('"unshackled-word"."Changesets"', 'Id'))::int AS changeset_id
FROM (SELECT r.lang
           , min(r.source_text_id) AS source_text_id
           , (r.created_at AT TIME ZONE 'UTC')::date AS day
           , min(r.created_at) AS first_at
           , max(r.created_at) AS last_at
           , min(r.ref_id) AS from_ref
           , max(r.ref_id) AS to_ref
           , count(*) AS row_count
      FROM mig_rows r
      WHERE 1=1
          AND r.word_exists
      GROUP BY r.lang, (r.created_at AT TIME ZONE 'UTC')::date
      ORDER BY 1, 3) x;

INSERT INTO "unshackled-word"."AiRuns" ("Id", "AiUserId", "RequestedByUserId", "SourceTextId", "FromRefId", "ToRefId", "Provider", "Model", "PromptVersion",
                                        "PromptHash", "Parameters", "Status", "RequestedAt", "StartedAt", "FinishedAt", "Notes")
SELECT mr.run_id
     , 2
     , NULL
     , mr.source_text_id
     , mr.from_ref
     , mr.to_ref
     , 'google'
     , 'gemini-legacy-unknown'
     , 'legacy'
     , NULL
     , jsonb_build_object('legacyRows', mr.row_count, 'day', mr.day)
     , 'completed'
     , mr.first_at
     , mr.first_at
     , mr.last_at
     , 'Migrated from Elb1871' || initcap(mr.lang) || 'Mapping rows created on ' || mr.day || '. Model not recorded (gemini-2.5-flash, gemini-3-flash-preview or gemini-3.1-flash-lite-preview).'
FROM mig_runs mr
WHERE 1=1
    AND NOT (SELECT done FROM mig_guard);

INSERT INTO "unshackled-word"."Changesets" ("Id", "AuthorUserId", "AiRunId", "Title", "Comment", "FromRefId", "ToRefId", "Status", "CreatedAt",
                                            "SubmittedAt", "ReviewedByUserId", "ReviewedAt", "ReviewComment", "MergedAt")
SELECT mr.changeset_id
     , 2
     , mr.run_id
     , 'Legacy ' || mr.lang || ' mapping ' || mr.day
     , 'Mapping v2 migration of legacy rows; unconfirmed AI suggestions.'
     , mr.from_ref
     , mr.to_ref
     , 'merged'
     , mr.first_at
     , mr.first_at
     , 1
     , now()
     , 'Merged by migration'
     , now()
FROM mig_runs mr
WHERE 1=1
    AND NOT (SELECT done FROM mig_guard);

-- ---------------------------------------------------------------------------------------------------------------------
-- 5. Components -> aligned links
-- ---------------------------------------------------------------------------------------------------------------------
CREATE TEMP TABLE mig_comp ON COMMIT DROP AS
SELECT c.*
     , nextval(pg_get_serial_sequence('"unshackled-word"."MappingLinks"', 'Id'))::int AS link_id
FROM (SELECT l.l AS label
           , min(e.lang) AS lang
           , min(e.source_text_id) AS source_text_id
           , min(e.ref_id) AS ref_id
           , min(e.created_at) AS created_at
           , (max(e.created_at) AT TIME ZONE 'UTC')::date AS last_day
      FROM mig_edge e
          JOIN mig_lbl l ON l.n = e.gnode
      GROUP BY l.l
      ORDER BY min(e.ref_id), l.l) c;

CREATE UNIQUE INDEX ON mig_comp (label);

-- word -> its component (prefer the whole-word node)
CREATE TEMP TABLE mig_word_comp ON COMMIT DROP AS
SELECT DISTINCT ON (e.lang, e.word_id) e.lang, e.word_id, c.link_id
FROM mig_edge e
    JOIN mig_lbl l ON l.n = e.gnode
    JOIN mig_comp c ON c.label = l.l
ORDER BY e.lang, e.word_id, e.part_index, c.link_id;

CREATE UNIQUE INDEX ON mig_word_comp (lang, word_id);

-- ---------------------------------------------------------------------------------------------------------------------
-- 6. Added words: attach to the parent's (or an ancestor's) link, otherwise group per chain root
-- ---------------------------------------------------------------------------------------------------------------------
CREATE TEMP TABLE mig_added ON COMMIT DROP AS
SELECT r.lang
     , r.source_text_id
     , r.legacy_id
     , r.word_id
     , r.ref_id
     , r.parent_id
     , r.created_at
     , EXISTS (SELECT 1 FROM mig_word_comp wc WHERE wc.lang = r.lang AND wc.word_id = r.word_id) AS has_link
     , (r.parent_id IS NOT NULL AND r.parent_id <> r.word_id AND p."HebRefId" = r.ref_id)          AS valid_parent
     , CASE
           WHEN r.parent_id IS NULL THEN 'no_parent'
           WHEN r.parent_id = r.word_id THEN 'parent_self'
           WHEN p."Id" IS NULL OR p."HebRefId" <> r.ref_id THEN 'parent_other_verse'
           END AS parent_problem
FROM mig_rows r
    LEFT JOIN "unshackled-word"."Elb1871Words" p ON p."Id" = r.parent_id
WHERE 1=1
    AND r.word_exists
    AND r.step_id IS NULL
    AND r.is_added;

CREATE INDEX ON mig_added (lang, word_id);

CREATE TEMP TABLE mig_added_res ON COMMIT DROP AS
WITH RECURSIVE chain AS (SELECT a.lang, a.word_id AS start_word, a.word_id AS cur, 0 AS depth, ARRAY [a.word_id] AS path
                         FROM mig_added a
                         WHERE NOT a.has_link
                         UNION ALL
                         SELECT c.lang, c.start_word, a.parent_id, c.depth + 1, c.path || a.parent_id
                         FROM chain c
                             JOIN mig_added a ON a.lang = c.lang AND a.word_id = c.cur AND a.valid_parent
                         WHERE 1=1
                             AND c.depth < 20
                             AND NOT a.parent_id = ANY (c.path)
                             AND NOT EXISTS (SELECT 1 FROM mig_word_comp wc WHERE wc.lang = c.lang AND wc.word_id = c.cur))
   , final AS (SELECT DISTINCT ON (c.lang, c.start_word) c.lang, c.start_word, c.cur AS root_word, c.depth
               FROM chain c
               ORDER BY c.lang, c.start_word, c.depth DESC)
SELECT a.lang
     , a.source_text_id
     , a.legacy_id
     , a.word_id
     , a.ref_id
     , a.created_at
     , f.root_word
     , wc.link_id AS attach_link_id
     , CASE
           WHEN a.has_link THEN 'added_dropped_word_has_link'
           WHEN wc.link_id IS NOT NULL AND f.depth = 1 THEN 'added_attached'
           WHEN wc.link_id IS NOT NULL THEN 'added_attached_via_chain'
           WHEN a.parent_problem IS NOT NULL THEN 'added_standalone_' || a.parent_problem
           ELSE 'added_standalone_parent_unmapped'
           END AS treatment
FROM mig_added a
    LEFT JOIN final f ON f.lang = a.lang AND f.start_word = a.word_id
    LEFT JOIN mig_word_comp wc ON wc.lang = f.lang AND wc.word_id = f.root_word AND f.depth > 0;

CREATE TEMP TABLE mig_added_group ON COMMIT DROP AS
SELECT g.*
     , nextval(pg_get_serial_sequence('"unshackled-word"."MappingLinks"', 'Id'))::int AS link_id
FROM (SELECT ar.lang
           , ar.root_word
           , min(ar.source_text_id) AS source_text_id
           , min(ar.ref_id) AS ref_id
           , min(ar.created_at) AS created_at
           , (max(ar.created_at) AT TIME ZONE 'UTC')::date AS last_day
      FROM mig_added_res ar
      WHERE 1=1
          AND ar.treatment LIKE 'added_standalone%'
      GROUP BY ar.lang, ar.root_word
      ORDER BY min(ar.ref_id), ar.root_word) g;

-- ---------------------------------------------------------------------------------------------------------------------
-- 7. Write links and members
-- ---------------------------------------------------------------------------------------------------------------------
INSERT INTO "unshackled-word"."MappingLinks" ("Id", "ChangesetId", "SourceTextId", "RefId", "Level", "Kind", "Reason", "State", "IsActive",
                                              "CreatedAt", "ActivatedAt")
SELECT c.link_id
     , mr.changeset_id
     , c.source_text_id
     , c.ref_id
     , 0
     , 'aligned'
     , NULL
     , 'active'
     , TRUE
     , c.created_at
     , now()
FROM mig_comp c
    JOIN mig_runs mr ON mr.lang = c.lang AND mr.day = c.last_day
WHERE 1=1
    AND NOT (SELECT done FROM mig_guard)
UNION ALL
SELECT g.link_id
     , mr.changeset_id
     , g.source_text_id
     , g.ref_id
     , 0
     , 'added'
     , NULL
     , 'active'
     , TRUE
     , g.created_at
     , now()
FROM mig_added_group g
    JOIN mig_runs mr ON mr.lang = g.lang AND mr.day = g.last_day
WHERE 1=1
    AND NOT (SELECT done FROM mig_guard);

-- German core members (one per word part and link)
INSERT INTO "unshackled-word"."MappingLinkGermanWords" ("LinkId", "LinkLevel", "IsActive", "BaseWordId", "PartIndex", "PartText", "Role")
SELECT DISTINCT ON (c.link_id, e.word_id, e.part_index) c.link_id
                                                      , 0
                                                      , TRUE
                                                      , e.word_id
                                                      , e.part_index
                                                      , e.part_text
                                                      , 'core'
FROM mig_edge e
    JOIN mig_lbl l ON l.n = e.gnode
    JOIN mig_comp c ON c.label = l.l
WHERE 1=1
    AND NOT (SELECT done FROM mig_guard)
ORDER BY c.link_id, e.word_id, e.part_index, e.part_text;

-- German added members (attached to a link, or in their standalone group)
INSERT INTO "unshackled-word"."MappingLinkGermanWords" ("LinkId", "LinkLevel", "IsActive", "BaseWordId", "PartIndex", "PartText", "Role")
SELECT coalesce(ar.attach_link_id, g.link_id)
     , 0
     , TRUE
     , ar.word_id
     , 0
     , NULL
     , 'added'
FROM mig_added_res ar
    LEFT JOIN mig_added_group g ON g.lang = ar.lang AND g.root_word = ar.root_word AND ar.treatment LIKE 'added_standalone%'
WHERE 1=1
    AND NOT (SELECT done FROM mig_guard)
    AND ar.treatment <> 'added_dropped_word_has_link';

-- Source members
INSERT INTO "unshackled-word"."MappingLinkSourceWords" ("LinkId", "SourceTextId", "LinkLevel", "IsActive", "SourceWordId")
SELECT DISTINCT c.link_id
              , e.source_text_id
              , 0::smallint
              , TRUE
              , e.source_word_id
FROM mig_edge e
    JOIN mig_lbl l ON l.n = e.snode
    JOIN mig_comp c ON c.label = l.l
WHERE 1=1
    AND NOT (SELECT done FROM mig_guard);

-- ---------------------------------------------------------------------------------------------------------------------
-- 8. Bookkeeping: every legacy row with its link and treatment
-- ---------------------------------------------------------------------------------------------------------------------
INSERT INTO "unshackled-word"."MappingLegacyRows" ("Language", "LegacyId", "LinkId", "Treatment", "PartDropped")
SELECT r.lang
     , r.legacy_id
     , coalesce(c.link_id, ar.attach_link_id, g.link_id)
     , CASE
           WHEN NOT r.word_exists THEN 'orphan_word'
           WHEN r.step_id IS NOT NULL AND r.source_word_id IS NULL THEN 'orphan_step'
           WHEN r.step_id IS NOT NULL AND r.is_added THEN 'link_added_flag_ignored'
           WHEN r.step_id IS NOT NULL AND r.parent_id IS NOT NULL THEN 'link_parent_dropped'
           WHEN r.step_id IS NOT NULL THEN 'link'
           WHEN r.is_added THEN ar.treatment
           ELSE 'empty_row'
           END
     , (r.part_raw IS NOT NULL AND r.part_text IS NULL)
FROM mig_rows r
    LEFT JOIN mig_edge e ON e.lang = r.lang AND e.legacy_id = r.legacy_id
    LEFT JOIN mig_lbl l ON l.n = e.gnode
    LEFT JOIN mig_comp c ON c.label = l.l
    LEFT JOIN mig_added_res ar ON ar.lang = r.lang AND ar.legacy_id = r.legacy_id
    LEFT JOIN mig_added_group g ON g.lang = ar.lang AND g.root_word = ar.root_word AND ar.treatment LIKE 'added_standalone%'
WHERE 1=1
    AND NOT (SELECT done FROM mig_guard);

COMMIT;


-- =====================================================================================================================
-- VERIFICATION (read-only; run after COMMIT; expected values from the run on the DB copy of 2026-11-10)
-- =====================================================================================================================

-- V1. Every legacy row accounted for (expected: both differences 0).
SELECT 'V1 legacy rows' AS "Check"
     , (SELECT count(*) FROM "unshackled-word"."Elb1871GreekMapping")
           - (SELECT count(*) FROM "unshackled-word"."MappingLegacyRows" WHERE "Language" = 'greek')  AS "GreekMissing"
     , (SELECT count(*) FROM "unshackled-word"."Elb1871HebrewMapping")
           - (SELECT count(*) FROM "unshackled-word"."MappingLegacyRows" WHERE "Language" = 'hebrew') AS "HebrewMissing";

-- V2. Treatment breakdown (compare with the numbers in the header).
SELECT 'V2 treatment' AS "Check", lr."Language", lr."Treatment", count(*) AS "Rows", count(*) FILTER (WHERE lr."PartDropped") AS "PartDropped"
FROM "unshackled-word"."MappingLegacyRows" lr
GROUP BY lr."Language", lr."Treatment"
ORDER BY lr."Language", count(*) DESC;

-- V3. Every legacy edge is preserved: its German word and its source word sit in the same active link (expected 0).
SELECT 'V3 lost edges' AS "Check", count(*) AS "Lost"
FROM (SELECT egm."Id", 'greek' lang, egm."ElbWordId", egm."StepWordId", 1 st FROM "unshackled-word"."Elb1871GreekMapping" egm WHERE egm."StepWordId" IS NOT NULL
      UNION ALL
      SELECT ehm."Id", 'hebrew', ehm."ElbWordId", ehm."StepWordId", 2 FROM "unshackled-word"."Elb1871HebrewMapping" ehm WHERE ehm."StepWordId" IS NOT NULL) le
WHERE 1=1
    AND NOT EXISTS (SELECT 1
                    FROM "unshackled-word"."MappingLinkGermanWords" gm
                        JOIN "unshackled-word"."MappingLinkSourceWords" ms ON ms."LinkId" = gm."LinkId"
                        JOIN "unshackled-word"."SourceWords" sw ON sw."Id" = ms."SourceWordId"
                    WHERE 1=1
                        AND gm."BaseWordId" = le."ElbWordId"
                        AND gm."IsActive"
                        AND sw."SourceTextId" = le.st
                        AND sw."ExternalId" = le."StepWordId");

-- V4. Shape rules (expected 0 rows).
SELECT 'V4 shape errors' AS "Check", count(*) AS "Errors" FROM "unshackled-word"."MappingLinkShapeErrors";

-- V5. Links and members by kind/role.
SELECT 'V5 links' AS "Check", ml."SourceTextId", ml."Kind", count(*) AS "Links"
     , sum((SELECT count(*) FROM "unshackled-word"."MappingLinkGermanWords" g WHERE g."LinkId" = ml."Id")) AS "GermanMembers"
     , sum((SELECT count(*) FROM "unshackled-word"."MappingLinkSourceWords" s WHERE s."LinkId" = ml."Id")) AS "SourceMembers"
FROM "unshackled-word"."MappingLinks" ml
GROUP BY ml."SourceTextId", ml."Kind"
ORDER BY 2, 3;

-- V6. Unmapped source words unchanged (expected: Greek 13,579, Hebrew 11,378 on the DB copy).
SELECT 'V6 unmapped source words' AS "Check", sw."SourceTextId", count(*) AS "Unmapped"
FROM "unshackled-word"."SourceWords" sw
WHERE 1=1
    AND sw."RetiredAt" IS NULL
    AND NOT EXISTS (SELECT 1 FROM "unshackled-word"."MappingLinkSourceWords" ms WHERE ms."SourceWordId" = sw."Id" AND ms."IsActive" AND ms."LinkLevel" = 0)
GROUP BY sw."SourceTextId";

-- V7. German words that had legacy rows but have no active member now (expected: only empty_row /
--     added_dropped... words without any other row; listed by treatment).
SELECT 'V7 German words lost' AS "Check", lr."Treatment", count(DISTINCT coalesce(egm."ElbWordId", ehm."ElbWordId")) AS "Words"
FROM "unshackled-word"."MappingLegacyRows" lr
    LEFT JOIN "unshackled-word"."Elb1871GreekMapping" egm ON lr."Language" = 'greek' AND egm."Id" = lr."LegacyId"
    LEFT JOIN "unshackled-word"."Elb1871HebrewMapping" ehm ON lr."Language" = 'hebrew' AND ehm."Id" = lr."LegacyId"
WHERE 1=1
    AND NOT EXISTS (SELECT 1 FROM "unshackled-word"."MappingLinkGermanWords" gm
                    WHERE gm."BaseWordId" = coalesce(egm."ElbWordId", ehm."ElbWordId") AND gm."IsActive")
GROUP BY lr."Treatment";

-- V8. Orphans: source registry rows whose STEP row is gone (expected 0 right after migration), links without members.
SELECT 'V8 orphans' AS "Check"
     , (SELECT count(*) FROM "unshackled-word"."SourceWords" sw
        WHERE sw."SourceTextId" = 1 AND NOT EXISTS (SELECT 1 FROM "unshackled-word"."StepGreekWords" s WHERE s."Id" = sw."ExternalId"))  AS "GreekRegistryOrphans"
     , (SELECT count(*) FROM "unshackled-word"."SourceWords" sw
        WHERE sw."SourceTextId" = 2 AND NOT EXISTS (SELECT 1 FROM "unshackled-word"."StepHebrewWords" s WHERE s."Id" = sw."ExternalId")) AS "HebrewRegistryOrphans"
     , (SELECT count(*) FROM "unshackled-word"."MappingLinks" ml
        WHERE NOT EXISTS (SELECT 1 FROM "unshackled-word"."MappingLinkGermanWords" g WHERE g."LinkId" = ml."Id")
            AND NOT EXISTS (SELECT 1 FROM "unshackled-word"."MappingLinkSourceWords" s WHERE s."LinkId" = ml."Id"))                    AS "EmptyLinks";

-- V9. Compat views vs legacy: (ElbWordId, StepWordId) pairs missing from the view (expected 0) and extra pairs
--     created by expanding n:m groups (expected > 0, the price of set <-> set; reported, not an error).
SELECT 'V9 compat greek' AS "Check"
     , (SELECT count(*) FROM (SELECT "ElbWordId", "StepWordId" FROM "unshackled-word"."Elb1871GreekMapping" WHERE "StepWordId" IS NOT NULL
                              EXCEPT SELECT "ElbWordId", "StepWordId" FROM "unshackled-word"."Elb1871GreekMappingCompat") x) AS "MissingPairs"
     , (SELECT count(*) FROM (SELECT "ElbWordId", "StepWordId" FROM "unshackled-word"."Elb1871GreekMappingCompat" WHERE "StepWordId" IS NOT NULL
                              EXCEPT SELECT "ElbWordId", "StepWordId" FROM "unshackled-word"."Elb1871GreekMapping") x) AS "ExtraPairs"
UNION ALL
SELECT 'V9 compat hebrew'
     , (SELECT count(*) FROM (SELECT "ElbWordId", "StepWordId" FROM "unshackled-word"."Elb1871HebrewMapping" WHERE "StepWordId" IS NOT NULL
                              EXCEPT SELECT "ElbWordId", "StepWordId" FROM "unshackled-word"."Elb1871HebrewMappingCompat") x)
     , (SELECT count(*) FROM (SELECT "ElbWordId", "StepWordId" FROM "unshackled-word"."Elb1871HebrewMappingCompat" WHERE "StepWordId" IS NOT NULL
                              EXCEPT SELECT "ElbWordId", "StepWordId" FROM "unshackled-word"."Elb1871HebrewMapping") x);

-- V10. Invariant spot check: no source word / German word part in two active level-0 links (the unique indexes
--      guarantee it; this query is for the record).
SELECT 'V10 duplicates' AS "Check"
     , (SELECT count(*) FROM (SELECT "SourceWordId" FROM "unshackled-word"."MappingLinkSourceWords" WHERE "IsActive" GROUP BY "SourceWordId", "LinkLevel" HAVING count(*) > 1) x) AS "SourceDup"
     , (SELECT count(*) FROM (SELECT "BaseWordId" FROM "unshackled-word"."MappingLinkGermanWords" WHERE "IsActive" AND "Role" <> 'grammar' GROUP BY "BaseWordId", "PartIndex", "LinkLevel" HAVING count(*) > 1) x) AS "GermanDup";

-- V11. Registry consistency (run after every seed / STEP re-import, expected all 0): live registry rows whose STEP id
--      names a word at another verse/position/text (V8 cannot see this: the id still exists), live rows without a
--      STEP id, and active links on retired words (to re-map by hand).
SELECT 'V11 registry' AS "Check"
     , (SELECT count(*) FROM "unshackled-word"."SourceWords" sw
            JOIN "unshackled-word"."StepGreekWords" s ON s."Id" = sw."ExternalId"
        WHERE sw."SourceTextId" = 1
            AND (s."LxxRefId", s."PositionInVerse", s."GreekNoDiacritics") IS DISTINCT FROM (sw."RefId", sw."PositionInVerse", sw."MatchText"))  AS "GreekWrongWord"
     , (SELECT count(*) FROM "unshackled-word"."SourceWords" sw
            JOIN "unshackled-word"."StepHebrewWords" s ON s."Id" = sw."ExternalId"
        WHERE sw."SourceTextId" = 2
            AND (s."LxxRefId", s."PositionInVerse", s."HebrewNoDiacritics") IS DISTINCT FROM (sw."RefId", sw."PositionInVerse", sw."MatchText")) AS "HebrewWrongWord"
     , (SELECT count(*) FROM "unshackled-word"."SourceWords" sw WHERE sw."RetiredAt" IS NULL AND sw."ExternalId" IS NULL)                     AS "LiveWithoutStepId"
     , (SELECT count(*) FROM "unshackled-word"."MappingLinkSourceWords" ms
            JOIN "unshackled-word"."SourceWords" sw ON sw."Id" = ms."SourceWordId"
        WHERE ms."IsActive" AND sw."RetiredAt" IS NOT NULL)                                                                                   AS "ActiveLinksOnRetired";
