# 0028 · Model

Migrations `0050_Lxx.sql` (text) and `0051_LxxTags.sql` (tags, links, audit). Raw SQL follows the SQL-formatting skill. `COMMENT ON` for every table.

```sql
CREATE TABLE "unshackled-word"."LxxVerses" (
    "Id"            int4 GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "BibleBookId"   int4        NOT NULL,
    "TextForm"      varchar(4)  NOT NULL DEFAULT '',    -- '' | 'og' | 'th' (Daniel)
    "Chapter"       int4        NOT NULL,
    "Verse"         int4        NOT NULL,               -- Psalm title = verse 1 (Greek counting)
    "Subverse"      int4        NOT NULL DEFAULT 0,
    "RahlfsRefId"   int8        NOT NULL,               -- book·10⁸ + chapter·10⁵ + verse·10² + subverse
    "SweteRef"      varchar(30) NOT NULL,
    "LxxRefId"      int4        NULL,                   -- STEP / English counting
    "HebRefId"      int4        NULL,                   -- Elberfelder counting
    "AfterHebRefId" int4        NULL,                   -- additions: shown after this verse
    "MapKind"       varchar(10) NOT NULL,               -- one | merged | split | addition | absent
    UNIQUE ("RahlfsRefId", "TextForm"));

CREATE TABLE "unshackled-word"."LxxWords" (
    "Id"                int4 GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "LxxVerseId"        int4         NOT NULL REFERENCES "unshackled-word"."LxxVerses",
    "Position"          int4         NOT NULL,
    "Greek"             varchar(100) NOT NULL,           -- with punctuation as in Swete file 01
    "GreekNoDiacritics" varchar(100) NOT NULL,
    "SrcHash"           varchar(8)   NOT NULL,           -- SourceKey hash, the seed anchor
    UNIQUE ("LxxVerseId", "Position"));

CREATE TABLE "unshackled-word"."LxxWordTags" (              -- the current tag per word
    "LxxWordId"   int4         PRIMARY KEY REFERENCES "unshackled-word"."LxxWords",
    "Lemma"       varchar(100) NOT NULL,
    "Strongs"     varchar(20)  NOT NULL,                  -- TBESG, e.g. G0746, G6123
    "Morphology"  varchar(30)  NOT NULL,                  -- Robinson style, e.g. N-DSF
    "Status"      varchar(12)  NOT NULL,                  -- proposed | confirmed
    "Source"      varchar(12)  NOT NULL,                  -- nt-form | ai | human
    "UserId"      int4         NOT NULL,                  -- AI user, system user or maintainer
    "UpdatedAt"   timestamptz  NOT NULL DEFAULT now());

CREATE TABLE "unshackled-word"."LxxHebrewLinks" (           -- LXX word renders Hebrew word(s)
    "LxxWordId"       int4 NOT NULL REFERENCES "unshackled-word"."LxxWords",
    "StepHebrewWordId" int4 NOT NULL,
    "Status"  varchar(12) NOT NULL, "UserId" int4 NOT NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY ("LxxWordId", "StepHebrewWordId"));

CREATE TABLE "unshackled-word"."LxxTagAudit" (               -- append-only, like RevisionVerseAudit
    "Id" int8 GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "LxxVerseId" int4 NOT NULL, "Action" varchar(20) NOT NULL,   -- confirm | edit | reject | import
    "Before" jsonb NULL, "After" jsonb NOT NULL, "UserId" int4 NOT NULL, "At" timestamptz NOT NULL DEFAULT now());
```

- `AgentVerseProposals_Language_CK` gains `'lxx'` (in `0051`). A proposal row is keyed by `BibleBookId`/`Chapter`/`Verse` in Rahlfs numbering, plus `Flags.textForm` and `Flags.subverse`.
- **Text tables** are filled by SeedDb only when `LxxVerses` is empty, like the STEP import gate. A re-import never touches tags: they anchor on `(RahlfsRefId, TextForm, Position, SrcHash)`.
- **Seed files** (`assets/seed/lxx/`, CC0, no Greek text, no user names):
  - `swete-to-rahlfs.csv`: `SweteRef;RahlfsRef;TextForm;Note`
  - `tags.csv`: `RahlfsRef;TextForm;Position;SrcHash;Lemma;Strongs;Morphology` (confirmed tags only)
  - `links.csv`: `RahlfsRef;TextForm;Position;SrcHash;HebLxxRefId;HebPosition;HebSrcHash` (Hebrew side as `SourceKey`)
- **Hebrew link anchor:** `StepHebrewWordId` is a serial that a STEP re-import renumbers. The seed therefore stores the Hebrew `SourceKey`, and import resolves it with `SourceWordResolver`, as the mapping seed does.
