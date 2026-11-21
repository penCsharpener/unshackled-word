# 0022 · Vocab model and renderings view

```
StepStrongsLexicon (STEP, read-only)        Elb1871*Mapping / RevisionMapping (head links)
        │ Lang + Number + Extra                         │
        ▼                                               ▼
VocabStrongs ── n:1 ──► VocabEntries ◄── 1:n ── VocabMeanings      StatsVocabRenderings (matview)
 (StrongsKey)           (lemma, form,           (German, rank,      (StrongsKey, GermanKey, counts)
                         audio, rank)            accepted, origin)        │ summed via VocabStrongs
        VocabCourses ── 1:n (by Lang, rank) ──┘                           └──► derive ► VocabMeanings
        VocabCards / VocabReviewLog (per user)  ── see 04-study-engine.md
```

## Migration `0037_Vocab.sql` (content side)

```sql
CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabCourses"
(
    "Id"     serial4      NOT NULL,
    "Code"   varchar(30)  NOT NULL,          -- 'nt-greek', 'ot-hebrew'
    "Track"  char(1)      NOT NULL,          -- 'G' | 'H' (H includes Aramaic)
    "Kind"   varchar(10)  NOT NULL DEFAULT 'frequency',  -- 'frequency' now; 'curated' later
    "NameDe" varchar(100) NOT NULL,
    "NameEn" varchar(100) NOT NULL,
    CONSTRAINT "VocabCourses_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "VocabCourses_Code_UQ" UNIQUE ("Code"),
    CONSTRAINT "VocabCourses_Track_CK" CHECK ("Track" IN ('G', 'H')),
    CONSTRAINT "VocabCourses_Kind_CK" CHECK ("Kind" IN ('frequency'))
);

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabEntries"
(
    "Id"                   serial4                     NOT NULL,
    "Track"                char(1)                     NOT NULL,
    "LanguageId"           int4                        NOT NULL,   -- Hebrew=0, Aramaic=1, Greek=2
    "Lemma"                varchar(50) COLLATE "und-x-icu" NOT NULL,  -- STEP OriginalWord
    "LemmaNormalized"      varchar(50) COLLATE "und-x-icu" NOT NULL,  -- search key (VocabText.Normalize)
    "DictionaryForm"       varchar(120) COLLATE "und-x-icu" NULL,   -- 'μέσος, -η, -ον'; typing target when set, see 04
    "DictionaryFormOrigin" varchar(10)                 NULL,       -- 'generated' | 'agent' | 'manual'
    "Transliteration"      varchar(50)                 NULL,
    "Pos"                  varchar(20)                 NULL,
    "IsProperName"         boolean                     NOT NULL DEFAULT false,
    "IsMorpheme"           boolean                     NOT NULL DEFAULT false,  -- H9001–H9049
    "Occurrences"          int4                        NOT NULL DEFAULT 0,
    "FrequencyRank"        int4                        NULL,       -- per track, null when morpheme/retired
    "AudioObjectKey"       varchar(200)                NULL,
    "ReviewedAt"           timestamptz                 NULL,
    "ReviewedByUserId"     int4                        NULL,
    "RetiredAt"            timestamptz                 NULL,
    "ContentVersion"       int4                        NOT NULL DEFAULT 1,   -- bumped by learning-relevant changes
    "UpdatedAt"            timestamptz                 NOT NULL DEFAULT now(),
    "UpdatedByUserId"      int4                        NULL,
    CONSTRAINT "VocabEntries_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "VocabEntries_Track_CK" CHECK ("Track" IN ('G', 'H'))
);
CREATE INDEX IF NOT EXISTS "VocabEntries_Rank_idx" ON "unshackled-word"."VocabEntries" ("Track", "FrequencyRank");

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabStrongs"
(
    "StrongsKey"   varchar(10) NOT NULL,   -- 'H1254a', 'G3056' (same form as StatsStrongsRenderings."StrongsKey")
    "Lang"         char(1)     NOT NULL,
    "VocabEntryId" int4        NOT NULL,
    "Occurrences"  int4        NOT NULL DEFAULT 0,
    CONSTRAINT "VocabStrongs_PK" PRIMARY KEY ("Lang", "StrongsKey"),   -- a key belongs to exactly one entry
    CONSTRAINT "VocabStrongs_Entry_FK" FOREIGN KEY ("VocabEntryId") REFERENCES "unshackled-word"."VocabEntries" ("Id")
);

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabMeanings"
(
    "Id"           serial4      NOT NULL,
    "VocabEntryId" int4         NOT NULL,
    "German"       varchar(80)  NOT NULL,
    "GermanKey"    varchar(80)  NULL,          -- renderings key it was derived from
    "Rank"         int4         NOT NULL,
    "Count"        int4         NULL,          -- renderings count at derivation
    "Share"        numeric(5,2) NULL,          -- percent of the entry's head links
    "IsAccepted"   boolean      NOT NULL DEFAULT true,   -- counts as a correct answer
    "Origin"       varchar(10)  NOT NULL,      -- 'derived' | 'manual'
    "IsPinned"     boolean      NOT NULL DEFAULT false,  -- survives re-derivation
    CONSTRAINT "VocabMeanings_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "VocabMeanings_Entry_German_UQ" UNIQUE ("VocabEntryId", "German")
);

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabEntryChanges"   -- append-only; what the learner sees as "geändert"
(
    "Id"              serial4     NOT NULL,
    "VocabEntryId"    int4        NOT NULL,   -- no FK: survives a deleted/merged entry
    "ContentVersion"  int4        NOT NULL,   -- the version this change produced
    "Kind"            varchar(12) NOT NULL,   -- meanings | form | lemma | keys | merge | split | retire | delete
    "Before"          jsonb       NULL,       -- e.g. {"meanings": ["Mitte", "mitten"]}
    "After"           jsonb       NULL,
    "TargetEntryId"   int4        NULL,       -- merge/split: the other entry
    "ChangedByUserId" int4        NULL,
    "ChangedAt"       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "VocabEntryChanges_PK" PRIMARY KEY ("Id")
);
CREATE INDEX IF NOT EXISTS "VocabEntryChanges_Entry_idx" ON "unshackled-word"."VocabEntryChanges" ("VocabEntryId", "ContentVersion");
```

Plus constraints in the project style for origin checks and the FKs to `Users`. The two courses are inserted with `ON CONFLICT DO NOTHING`.

## Generator (`VocabLexiconGenerator`)

- Group `StepStrongsLexicon` by (track, `OriginalWord`): one entry per group, one `VocabStrongs` row per key. H1254a/H1254b → one entry.
- `Occurrences` per key from the STEP word tables (Greek `DisambiguatedStrongs`, Hebrew main morpheme of `DisambiguatedStrongs`). The entry's occurrences are the sum. `FrequencyRank` is `row_number()` per track by occurrences desc, then Strong's number.
- `IsProperName` comes from `StepPersonLexicon`/`StepPlaceLexicon`, `IsMorpheme` from H9001–H9049.
- `DictionaryForm` is generated only where it is safe from the STEP lexicon (e.g. Greek nouns whose lexicon line holds the genitive and article). Otherwise it stays null.
- Idempotent. A second run adds new keys to the matching entry and never touches `manual`/`agent` fields, pinned meanings or `RetiredAt`. Ranks are recomputed. Rank changes do not bump `ContentVersion`.

## `StatsVocabRenderings` (migration `0038_VocabRenderings.sql`)

A materialized view over head links in the **live text**: for each verse, the Elberfelder 2027 `RevisionVerses` variant when one exists, otherwise the Elb1871 mapping. Columns: `Lang, StrongsKey, GermanKey, GermanDisplay, KeyIsForm, LinkCount, VerseCount, FromRevision` (links coming from 2027 variants). German key: Elb1871Morphology lemma for Elb1871 words and for revision words with `Change = 'same'` (via `BasePositionInVerse`), otherwise lower-case form. Unique index for `REFRESH … CONCURRENTLY`. Refreshed by the maintainer (`POST api/maintainer/vocab/renderings/refresh`, time in `DqRefreshState."VocabRenderingsRefreshedAt"`) and after a seed rebase. Not chained on `DqWordFacts`, so the 0013 `CASCADE` warning does not reach it.

## Deriving meanings

`POST api/maintainer/vocab/meanings/derive` (scope: all, a course, or a rank range; `dryRun`). Per entry, sum the renderings over its keys and keep keys with `Share ≥ Vocab:MeaningMinShare` (default 5 %) and `LinkCount ≥ 2`, at most `Vocab:MeaningMax` (6). Replace only `Origin = 'derived'` and unpinned rows. When the accepted set of an entry actually changes, bump `ContentVersion` and write one `meanings` change row. A rerun that changes nothing bumps nothing. The report lists entries whose top meaning changed, so the maintainer can look at them.
