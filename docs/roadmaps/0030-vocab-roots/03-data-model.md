# 0030 · Data model

```
VocabRoots (one row per root/family)          VocabStrongsRoots (one row per word key)
  Id, Track, Root, RootNormalized,  ◄── n:1 ──  Lang, StrongsKey (PK, = VocabStrongs key)
  Homograph, StrongsKey NULL                    VocabRootId, Prefixes, PrefixKeys[], Origin
        │ StrongsKey                                    │ StrongsKey
        ▼                                               ▼
  VocabStrongs ─► VocabEntries (root's own entry)   VocabStrongs ─► VocabEntries (the word)
```

An entry's root is the `VocabStrongsRoots` row of its main key. A family is every entry whose main key points at the same `VocabRootId`. A wide family also takes in every root below it through `ParentRootId` (a recursive CTE, at most 4 levels). The track must match, and the service layer rejects cycles because a CHECK cannot see them. A root with a Strong's key is also a word: its own `VocabStrongsRoots` row (root = itself) and its `ParentRootId` are separate facts, and `ParentRootId` is the one that widens the family.

## Migration `0056_VocabRoots.sql`

```sql
CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabRoots"
(
    "Id"             serial4                         NOT NULL,
    "Track"          char(1)                         NOT NULL,
    "Root"           varchar(50)  COLLATE "und-x-icu" NOT NULL,  -- 'μαρτυρέω', 'כתב'
    "RootNormalized" varchar(50)  COLLATE "und-x-icu" NOT NULL,  -- VocabText.Normalize
    "Homograph"      int2                            NOT NULL DEFAULT 1,
    "StrongsKey"     varchar(10)                     NULL,       -- 'G3140', 'H3789'; NULL = Hebrew root without verb
    "ParentRootId"   int4                            NULL,       -- deeper family: μαρτυρία → μαρτυρέω; same track, no cycles
    "UpdatedAt"      timestamptz                     NOT NULL DEFAULT now(),
    "UpdatedByUserId" int4                           NULL,
    CONSTRAINT "VocabRoots_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "VocabRoots_Track_CK" CHECK ("Track" IN ('G', 'H')),
    CONSTRAINT "VocabRoots_Root_UQ" UNIQUE ("Track", "RootNormalized", "Homograph"),
    CONSTRAINT "VocabRoots_Greek_Key_CK" CHECK ("Track" = 'H' OR "StrongsKey" IS NOT NULL),
    CONSTRAINT "VocabRoots_Parent_FK" FOREIGN KEY ("ParentRootId") REFERENCES "unshackled-word"."VocabRoots" ("Id"),
    CONSTRAINT "VocabRoots_Parent_CK" CHECK ("ParentRootId" IS NULL OR "ParentRootId" <> "Id")
);
CREATE INDEX IF NOT EXISTS "VocabRoots_Parent_idx" ON "unshackled-word"."VocabRoots" ("ParentRootId");
CREATE UNIQUE INDEX IF NOT EXISTS "VocabRoots_StrongsKey_UQ"
    ON "unshackled-word"."VocabRoots" ("StrongsKey") WHERE "StrongsKey" IS NOT NULL;

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabStrongsRoots"
(
    "Lang"        char(1)      NOT NULL,
    "StrongsKey"  varchar(10)  NOT NULL,          -- the word's key, same form as VocabStrongs
    "VocabRootId" int4         NOT NULL,
    "Prefixes"    varchar(60)  COLLATE "und-x-icu" NULL,   -- 'σύν + ἐπί', NULL = simple word / Hebrew
    "PrefixKeys"  varchar(10)[] NULL,             -- {G4862,G1909}, same order as Prefixes
    "Origin"      varchar(10)  NOT NULL,          -- 'prefill' | 'agent' | 'manual'
    "AgentRunId"  int4         NULL,
    "UpdatedAt"   timestamptz  NOT NULL DEFAULT now(),
    "UpdatedByUserId" int4     NULL,
    CONSTRAINT "VocabStrongsRoots_PK" PRIMARY KEY ("Lang", "StrongsKey"),
    CONSTRAINT "VocabStrongsRoots_Root_FK" FOREIGN KEY ("VocabRootId") REFERENCES "unshackled-word"."VocabRoots" ("Id"),
    CONSTRAINT "VocabStrongsRoots_Origin_CK" CHECK ("Origin" IN ('prefill', 'agent', 'manual'))
);
CREATE INDEX IF NOT EXISTS "VocabStrongsRoots_Root_idx" ON "unshackled-word"."VocabStrongsRoots" ("VocabRootId");
```

Also in the migration: add `'vocab-root'` to `AgentRuns_Mode_CK` (keep every mode of the latest definition) and comment the column.

There is deliberately no foreign key from `VocabStrongsRoots` to `VocabStrongs`. A key can drop out of the vocab (retired entry) and come back on the next rebuild, and its root must survive that.

## Reading an entry's root

One SQL view `"VocabEntryRoots"` (entry id → root id, root, root key, prefixes, origin), defined as: the main key per entry (`DISTINCT ON ("VocabEntryId") ORDER BY "Occurrences" DESC, "StrongsKey"`) joined to `VocabStrongsRoots` and `VocabRoots`. Every list, filter, card and export reads this view, so "linked to the right words after a rebuild" holds by construction. Nothing is copied into `VocabEntries`.

## Rebuild

`VocabLexiconGenerator` is not changed. After a rebuild, the vocab jobs section shows the number of active entries whose main key has no `VocabStrongsRoots` row ("ohne Wurzel: 37"). The prefill (0030.2) and the AI run fill them. Retired keys keep their rows.

## Seed

- `assets/seed/vocab/roots.csv`: `Track,Root,Homograph,StrongsKey,ParentRoot,ParentHomograph` (parent by natural key; the import runs in two passes, rows first and then parents)
- `assets/seed/vocab/word-roots.csv`: `Lang,StrongsKey,RootTrack,Root,RootHomograph,Prefixes,PrefixKeys,Origin` (`PrefixKeys` joined with `;`; no user ids and no run ids).

The import matches roots by `(Track, RootNormalized, Homograph)` and word rows by `(Lang, StrongsKey)`. It never overwrites a local `manual` row with a non-manual one.
