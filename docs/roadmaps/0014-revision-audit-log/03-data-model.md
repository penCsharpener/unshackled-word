# 0014 · Data model

Decisions behind this: [02-decisions.md](02-decisions.md).

## RevisionVerses (live, one row per verse and revision)

```sql
"Id"                      serial4      -- stable across text saves
"HebRefId"                int4
"RevisionId"              int4         -- FK Revisions
"Version"                 int4         -- current text version, >= 1
"VerseText"               text
"BasedOnRevisionId"       int4 NULL    -- FK Revisions; NULL = started from Elb1871   (replaces BasedOnRevisionVerseId)
"BasedOnVersion"          int4 NULL    -- version of the parent when this text was written
"BaseTextHash"            varchar(12)
"Note"                    varchar(500) NULL
"CreatedByUserId"/"CreatedAt"          -- first creation
"UpdatedByUserId"/"UpdatedAt"          -- last text save (new)
"MappingReviewedByUserId"/"MappingReviewedAt"
"SearchVector"            generated
CONSTRAINT "RevisionVerses_Verse_UQ" UNIQUE ("HebRefId", "RevisionId")
```

Dropped: `IsActive`, `EndedAt`, `EndedByUserId`, `RevisionVerses_Lifecycle_CK`, `RevisionVerses_Version_UQ`,
`RevisionVerses_Active_UQ`. Partial indexes lose `WHERE "IsActive"` (`Search_idx`, `Unreviewed_idx`).

`RevisionWords_Verse_FK` and `RevisionMapping_Word_FK` / `_Parent_FK` get `ON DELETE CASCADE`, so a withdraw or a
word rewrite is one `DELETE`.

## RevisionVerseAudit (append-only)

```sql
CREATE TABLE "unshackled-word"."RevisionVerseAudit"
(
    "Id"                int8 GENERATED ALWAYS AS IDENTITY,
    "RevisionVerseId"   int4         NULL,  -- no FK: the live row may be withdrawn
    "HebRefId"          int4         NOT NULL,
    "RevisionId"        int4         NOT NULL,  -- FK Revisions
    "Version"           int4         NOT NULL,
    "Action"            varchar(10)  NOT NULL,  -- 'text' | 'mapping' | 'withdraw'
    "VerseText"         text         NOT NULL,
    "Note"              varchar(500) NULL,
    "BasedOnRevisionId" int4         NULL,
    "BasedOnVersion"    int4         NULL,
    "BaseTextHash"      varchar(12)  NOT NULL,
    "Mapping"           jsonb        NULL,      -- see below; NULL for 'withdraw'
    "ByUserId"          int4         NULL,      -- NULL = seed import
    "At"                timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT "RevisionVerseAudit_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "RevisionVerseAudit_Action_CK" CHECK ("Action" IN ('text', 'mapping', 'withdraw'))
);
CREATE INDEX "RevisionVerseAudit_Verse_idx" ON "unshackled-word"."RevisionVerseAudit" ("HebRefId", "RevisionId", "Id" DESC);
```

`Mapping` is an array of compact rows keyed by word position, Lang implied by the book:

```json
[[1, 3056, 0, null, null, "m"], [2, null, 1, 1, "s", "c"]]
//  pos, stepWordId, isAddedWord, parentPos, germanWordPart, origin (c/m/s)
```

The latest audit row of a live verse always equals the live row; a test asserts this after every write path.

## History contract

`GET …/history?level=` reads only the audit table. Entries change shape slightly: `auditId` instead of
`revisionVerseId`, `action`, `at`, `by`; `isActive` becomes "is the newest text entry and the verse is not withdrawn";
`endedAt` is the `at` of the next text/withdraw entry. `VersionHistory` shows mapping entries as a small "Zuordnung
gespeichert" line under their version.
