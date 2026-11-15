-- Revision audit log (roadmap 0014, additive part): append-only history of every text save, mapping save and withdraw of a revision verse,
-- so the live table can later hold only the final version. The old tables are untouched; the app dual-writes until 0014.3.
--
-- "Mapping" is a compact jsonb array keyed by word position (RevisionWords."Id" changes on text saves): [pos, stepWordId, isAddedWord, parentPos, germanWordPart, origin(c/m/s)].

CREATE TABLE IF NOT EXISTS "unshackled-word"."RevisionVerseAudit"
(
    "Id"                int8         GENERATED ALWAYS AS IDENTITY,
    "RevisionVerseId"   int4         NULL,
    "HebRefId"          int4         NOT NULL,
    "RevisionId"        int4         NOT NULL,
    "Version"           int4         NOT NULL,
    "Action"            varchar(10)  NOT NULL,
    "VerseText"         text         NOT NULL,
    "Note"              varchar(500) NULL,
    "BasedOnRevisionId" int4         NULL,
    "BasedOnVersion"    int4         NULL,
    "BaseTextHash"      varchar(12)  NOT NULL,
    "Mapping"           jsonb        NULL,
    "ByUserId"          int4         NULL,
    "At"                timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT "RevisionVerseAudit_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "RevisionVerseAudit_Revision_FK" FOREIGN KEY ("RevisionId") REFERENCES "unshackled-word"."Revisions" ("Id"),
    CONSTRAINT "RevisionVerseAudit_ByUser_FK" FOREIGN KEY ("ByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "RevisionVerseAudit_Action_CK" CHECK ("Action" IN ('text', 'mapping', 'withdraw'))
);

CREATE INDEX IF NOT EXISTS "RevisionVerseAudit_Verse_idx" ON "unshackled-word"."RevisionVerseAudit" ("HebRefId", "RevisionId", "Id" DESC);

COMMENT ON TABLE "unshackled-word"."RevisionVerseAudit" IS 'Append-only history of RevisionVerses: one row per text save, mapping save or withdraw, holding the state after the action. Local data, not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerseAudit"."RevisionVerseId" IS 'The live row at the time of the action; no FK because the live row may be withdrawn (or, until 0014.3, superseded).';
COMMENT ON COLUMN "unshackled-word"."RevisionVerseAudit"."Mapping" IS 'Mapping snapshot after the action: array of [position, stepWordId, isAddedWord, parentPosition, germanWordPart, origin c/m/s]; NULL for withdraw.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerseAudit"."ByUserId" IS 'NULL = repo seed import.';

-- Backfill, only into an empty table so a re-run cannot duplicate history. One chronological insert (by time, then verse row, text before mapping before
-- withdraw) so the audit "Id" order is the history order: a text row per verse row, a mapping row per reviewed verse row (same snapshot: mapping edits
-- had no history before), a withdraw row per ended verse row without a successor of the same verse and revision.
DO
$$
    BEGIN
        IF EXISTS (SELECT 1 FROM "unshackled-word"."RevisionVerseAudit") THEN
            RETURN;
        END IF;

        INSERT INTO "unshackled-word"."RevisionVerseAudit"
            ("RevisionVerseId", "HebRefId", "RevisionId", "Version", "Action", "VerseText", "Note", "BasedOnRevisionId", "BasedOnVersion", "BaseTextHash", "Mapping", "ByUserId", "At")
        WITH base AS (SELECT rv."Id"
                           , rv."HebRefId"
                           , rv."RevisionId"
                           , rv."Version"
                           , rv."VerseText"
                           , rv."Note"
                           , p."RevisionId" AS "BasedOnRevisionId"
                           , p."Version"    AS "BasedOnVersion"
                           , rv."BaseTextHash"
                           , COALESCE((SELECT jsonb_agg(jsonb_build_array(rw."PositionInVerse", rm."StepWordId", rm."IsAddedWord", pw."PositionInVerse", rm."GermanWordPart", left(rm."Origin", 1)) ORDER BY rw."PositionInVerse", rm."Id")
                                       FROM "unshackled-word"."RevisionWords" rw
                                           JOIN "unshackled-word"."RevisionMapping" rm ON rm."RevisionWordId" = rw."Id"
                                           LEFT JOIN "unshackled-word"."RevisionWords" pw ON pw."Id" = rm."ParentRevisionWordId"
                                       WHERE rw."RevisionVerseId" = rv."Id"), '[]'::jsonb) AS "Mapping"
                           , rv."CreatedByUserId"
                           , rv."CreatedAt"
                           , rv."MappingReviewedAt"
                           , rv."MappingReviewedByUserId"
                           , rv."IsActive"
                           , rv."EndedAt"
                           , rv."EndedByUserId"
                           , NOT EXISTS (SELECT 1
                                         FROM "unshackled-word"."RevisionVerses" s
                                         WHERE s."HebRefId" = rv."HebRefId"
                                             AND s."RevisionId" = rv."RevisionId"
                                             AND s."Id" > rv."Id") AS "IsLast"
                      FROM "unshackled-word"."RevisionVerses" rv
                          LEFT JOIN "unshackled-word"."RevisionVerses" p ON p."Id" = rv."BasedOnRevisionVerseId"),
             events AS (SELECT b."Id", 1 AS "Rank", 'text' AS "Action", b."Mapping", b."CreatedByUserId" AS "ByUserId", b."CreatedAt" AS "At", b FROM base b
                        UNION ALL
                        SELECT b."Id", 2, 'mapping', b."Mapping", b."MappingReviewedByUserId", b."MappingReviewedAt", b FROM base b WHERE b."MappingReviewedAt" IS NOT NULL
                        UNION ALL
                        SELECT b."Id", 3, 'withdraw', NULL, b."EndedByUserId", b."EndedAt", b FROM base b WHERE NOT b."IsActive" AND b."IsLast")
        SELECT e."Id"
             , (e.b)."HebRefId"
             , (e.b)."RevisionId"
             , (e.b)."Version"
             , e."Action"
             , (e.b)."VerseText"
             , (e.b)."Note"
             , (e.b)."BasedOnRevisionId"
             , (e.b)."BasedOnVersion"
             , (e.b)."BaseTextHash"
             , e."Mapping"
             , e."ByUserId"
             , e."At"
        FROM events e
        ORDER BY e."At"
               , e."Id"
               , e."Rank";
    END
$$;
