# 0024 · Data model

```
tskxref.txt ──seed──▶ Tsk (immutable, 1 row per entry×target, now with Sort + Heb keys)
                          │  key = (SourceHebRefId, TargetStartHebRefId, TargetEndHebRefId)
                          ▼
                     TskCurations (current state per key; Origin tsk|added)
                          ▲                       ▲
     maintainer PUT ──────┘                       └── accept ── TskCurationProposals (user suggestions)
                          │
                          └──▶ TskCurationAudit (append-only, every applied change)
                          │
                          └──▶ assets/seed/tsk/curations.csv (export/import, no personal data)
```

## Stable key on `Tsk` (migration `0044_TskKeys.sql`)

```sql
ALTER TABLE "unshackled-word"."Tsk"
    ADD COLUMN IF NOT EXISTS "Sort"                int4 NULL,
    ADD COLUMN IF NOT EXISTS "SourceHebRefId"      int4 NULL,
    ADD COLUMN IF NOT EXISTS "TargetStartHebRefId" int4 NULL,
    ADD COLUMN IF NOT EXISTS "TargetEndHebRefId"   int4 NULL;   -- NULL = single verse
CREATE INDEX IF NOT EXISTS "Tsk_Source_idx" ON "unshackled-word"."Tsk" ("SourceHebRefId", "Sort", "Id");
```

The backfill reproduces today's read path in SQL: source via `BibleVerseCountingMapping` (`INNER JOIN`, unmapped sources stay NULL and are not shown, as today), targets via `MIN`/`MAX(HebRefId)` with `LxxRefId` fallback, then the `CrossReferenceMapper` normalisation. `Sort` stays NULL for existing rows (the order is `Id`, as today); `TskStrategy` writes `Sort` and the Heb keys on new seeds. `TskRepository` reads the columns, ordered by `SourceHebRefId, Sort NULLS LAST, Id`.

## `TskCurations` (migration `0045_TskCurations.sql`)

```sql
CREATE TABLE IF NOT EXISTS "unshackled-word"."TskCurations"
(
    "Id"                  int4 GENERATED ALWAYS AS IDENTITY,
    "SourceHebRefId"      int4         NOT NULL,
    "TargetStartHebRefId" int4         NOT NULL,
    "TargetEndHebRefId"   int4         NULL,
    "Origin"              varchar(5)   NOT NULL,  -- tsk | added
    "Rating"              varchar(9)   NULL,      -- highlight | hide | NULL (neutral)
    "Comment"             varchar(500) NULL,
    "StartPosition"       int4         NULL,      -- Elb1871 PositionInVerse in the source verse
    "EndPosition"         int4         NULL,
    "QuotedText"          varchar(500) NULL,      -- Elb1871 words at link time
    "UpdatedAt"           timestamptz  NOT NULL,
    "UpdatedByUserId"     int4         NULL,      -- NULL after a seed import
    CONSTRAINT "TskCurations_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "TskCurations_Origin_CK" CHECK ("Origin" IN ('tsk', 'added')),
    CONSTRAINT "TskCurations_Rating_CK" CHECK ("Rating" IS NULL OR "Rating" IN ('highlight', 'hide')),
    CONSTRAINT "TskCurations_Word_CK" CHECK (
        ("StartPosition" IS NULL AND "EndPosition" IS NULL AND "QuotedText" IS NULL)
     OR ("StartPosition" BETWEEN 1 AND "EndPosition" AND "QuotedText" IS NOT NULL)),
    CONSTRAINT "TskCurations_Range_CK" CHECK ("TargetEndHebRefId" IS NULL OR "TargetEndHebRefId" > "TargetStartHebRefId")
);
CREATE UNIQUE INDEX IF NOT EXISTS "TskCurations_Key_UX" ON "unshackled-word"."TskCurations"
    ("SourceHebRefId", "TargetStartHebRefId", COALESCE("TargetEndHebRefId", 0));
```

- Only non-default links have a row. A row that becomes neutral, uncommented and unlinked (and `Origin = tsk`) is deleted, so the table and the export stay a true diff.
- `Origin = added` rows are links TSK lacks; deleting one removes the link. An added link that later turns out to exist in `Tsk` (same key) is shown once.
- Read side: the chapter list is `Tsk` rows ∪ `added` rows, left-joined to `TskCurations` on the key, hidden rows dropped for readers, ordered highlighted first, then TSK order, added links last.

## `TskCurationProposals` and `TskCurationAudit` (same migration)

- `TskCurationProposals`: `Id`, key columns, the full desired state (`Origin`, `Rating`, `Comment`, `StartPosition`, `EndPosition`, `QuotedText`, plus `Delete bool` for withdrawing an added link), `UserId`, `CreatedAt`, `State` (`pending` | `accepted` | `rejected` | `superseded`), `DecidedByUserId`, `DecidedAt`, `Reason varchar(500)`. One pending proposal per user and key; a new one supersedes the old. Accepting checks the current state has not changed since the proposal was made (`BaseUpdatedAt`); otherwise the review page shows the three-way state.
- `TskCurationAudit`: append-only `Id`, key, `Before jsonb`, `After jsonb`, `UserId`, `At`, `Source` (`edit` | `proposal` | `import`). Like `RevisionVerseAudit`, not part of the seed.

## Word links and drift

Same rule as notes ([0023 model](../0023-notes/03-model-and-anchoring.md)): positions are Elb1871 positions of the **source** verse; `QuotedText` is rebuilt server-side from `Elb1871Words.WordInContext`. On read, a link whose words no longer match (`drifted`) or whose positions are gone is returned without positions and shown at verse level with the quote as hint. On revision levels the marker sits on the variant words whose `BasePositionInVerse` falls in the range. Linking several refs to one word is one request with a list of keys.
