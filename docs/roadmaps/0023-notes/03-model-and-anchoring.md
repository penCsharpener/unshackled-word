# 0023 · Data model and anchoring

## Table (migration `0042_Notes.sql`)

One table for all four anchors; a check constraint enforces which columns each kind fills.

```sql
CREATE TABLE IF NOT EXISTS "unshackled-word"."Notes"
(
    "Id"                   uuid                     NOT NULL DEFAULT gen_random_uuid(),
    "UserId"               int4                     NOT NULL,
    "Kind"                 varchar(10)              NOT NULL,  -- chapter | verses | verse | words
    "BibleBookId"          int4                     NOT NULL,
    "Chapter"              int4                     NOT NULL,
    "StartHebRefId"        int4                     NULL,      -- null for chapter
    "EndHebRefId"          int4                     NULL,      -- = Start for verse/words
    "StartPosition"        int4                     NULL,      -- Elb1871 PositionInVerse, words only
    "EndPosition"          int4                     NULL,
    "QuotedText"           varchar(500)             NULL,      -- Elb1871 words at save time, words only
    "BodyMarkdown"         text                     NOT NULL,
    "Visibility"           varchar(10)              NOT NULL DEFAULT 'private',  -- private | internal | public
    "PublicState"          varchar(10)              NOT NULL DEFAULT 'none',     -- none | pending | approved | rejected
    "ApprovedBodyMarkdown" text                     NULL,
    "ApprovedAt"           timestamptz              NULL,
    "ApprovedByUserId"     int4                     NULL,
    "ReviewNote"           varchar(500)             NULL,      -- rejection or hide reason
    "HiddenAt"             timestamptz              NULL,
    "HiddenByUserId"       int4                     NULL,
    "CreatedAt"            timestamptz              NOT NULL DEFAULT now(),
    "UpdatedAt"            timestamptz              NULL,
    CONSTRAINT "Notes_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "Notes_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "Notes_Kind_CK" CHECK ("Kind" IN ('chapter', 'verses', 'verse', 'words')),
    CONSTRAINT "Notes_Anchor_CK" CHECK (
        ("Kind" = 'chapter' AND "StartHebRefId" IS NULL AND "EndHebRefId" IS NULL AND "StartPosition" IS NULL)
     OR ("Kind" = 'verses'  AND "StartHebRefId" < "EndHebRefId" AND "StartHebRefId" / 1000 = "EndHebRefId" / 1000 AND "StartPosition" IS NULL)
     OR ("Kind" = 'verse'   AND "StartHebRefId" = "EndHebRefId" AND "StartPosition" IS NULL)
     OR ("Kind" = 'words'   AND "StartHebRefId" = "EndHebRefId" AND "StartPosition" BETWEEN 1 AND "EndPosition" AND "QuotedText" IS NOT NULL)),
    CONSTRAINT "Notes_Visibility_CK" CHECK ("Visibility" IN ('private', 'internal', 'public')),
    CONSTRAINT "Notes_PublicState_CK" CHECK ("PublicState" IN ('none', 'pending', 'approved', 'rejected')
        AND ("Visibility" = 'public') = ("PublicState" <> 'none')),
    CONSTRAINT "Notes_Body_CK" CHECK (char_length("BodyMarkdown") BETWEEN 1 AND 20000)
);
CREATE INDEX IF NOT EXISTS "Notes_Chapter_idx" ON "unshackled-word"."Notes" ("BibleBookId", "Chapter");
CREATE INDEX IF NOT EXISTS "Notes_User_idx"    ON "unshackled-word"."Notes" ("UserId", "UpdatedAt" DESC);
CREATE INDEX IF NOT EXISTS "Notes_Pending_idx" ON "unshackled-word"."Notes" ("UpdatedAt") WHERE "PublicState" = 'pending';
```

As implemented (0023.1), `Notes_Anchor_CK` also spells out `IS NOT NULL` for the refs/positions of each kind (a CHECK that evaluates to NULL passes, so `NULL = NULL` would let a verse note without a verse in) and requires `EndPosition`/`QuotedText` to be NULL outside words notes; `Notes_AnchorChapter_CK` ties `StartHebRefId` to `BibleBookId`/`Chapter`; `ApprovedByUserId`/`HiddenByUserId` reference `Users`.

`HebRefId` is `book * 1,000,000 + chapter * 1,000 + verse` (as everywhere in the schema), so "same chapter" is `/ 1000`. No `ON DELETE CASCADE` on `Users`: users are deactivated, not deleted (0020). Exact formatting follows the SQL-formatting skill.

## Who sees what (one SQL predicate, `NoteVisibilityFilter`)

| Viewer | Sees |
|---|---|
| anonymous | `Visibility = 'public' AND ApprovedBodyMarkdown IS NOT NULL AND HiddenAt IS NULL`, body = `ApprovedBodyMarkdown` |
| logged-in | own notes (all) + others' `internal`/`public` with `HiddenAt IS NULL`, body = `BodyMarkdown` |
| maintainer | as logged-in + review queue (`pending`) and hidden notes in the review page |

The response carries `isOwn`, `authorDisplayName`, `visibility`, `publicState`, `isHidden`. Deactivated authors' notes stay visible (the content is CC0 like everything else) — see risks.

## Word anchors across text changes

A words note stores Elb1871 positions **and** `QuotedText`. On read the backend joins `Elb1871Words` on `(HebRefId, PositionInVerse)` and compares the joined `WordInContext` string with `QuotedText` (whitespace and punctuation normalised):

- equal → `anchorState: "ok"`, the icon sits on the words;
- different or positions missing (word split, bulk replace, rebase) → `anchorState: "drifted"`, the note is shown at verse level with a hint „Die markierten Wörter haben sich geändert: „…"" and an owner action "Neu verankern".

## Revision levels

The reader may show a variant (`RevisionWords`). A word anchor maps through `BasePositionInVerse`: every variant word whose base position lies inside `[StartPosition, EndPosition]` gets the marker. If no variant word maps into the range, the marker falls back to verse level (`anchorState: "noBaseWords"`, no hint for readers). A note can only be **created** on words that have a base position; selecting a new variant-only word offers a verse note instead.
