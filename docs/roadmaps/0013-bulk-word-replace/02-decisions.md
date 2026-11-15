# 0013 · Decisions and findings

## Decided with the maintainer
- Search form: German word, optional Strong's narrowing (facets with counts), a suggested replacement, a checkbox per row.
- Matching: whole word, case-insensitive; optional "also forms of the same lemma" (Elb1871 lemma).
- Two dropdowns: **source** revision (what is searched; effective text = own variant else Elb1871; default Elberfelder 2027) and
  **target** revision (where versions are saved; default Elberfelder 2027). Repeated edits of one verse accumulate in the
  target's single active variant.
- **No direct apply (2026-11-15).** A word swap cannot fix article agreement (`ein Weib` => `eine Frau`). The button is
  "Prüfen": the user walks through the checked verses one at a time, edits the whole verse text (step 2), then confirms the
  mapping in the mapping editor (step 3).
- Each verse is submitted on its own after step 2 (text save) and finished after step 3 (mapping confirmed). No batch commit.
- The review queue is kept in the browser (`localStorage`), no server-side draft.
- Many carried-over "prüfen" marks after `ein Weib` => `eine Frau` (two changed words) are accepted.
- Roles as for saving a revision verse (Admin, Maintainer).

## Consequences
- The server only gains the search endpoint; saving and mapping reuse `POST revision/verse`, `GET`/`PUT revision/verseMapping`.
- The text version is saved when going from step 2 to 3 (the mapping editor needs a stored variant). A verse abandoned in step 3
  stays saved with `MappingReviewedAt` empty and appears in the revision overview's unreviewed list.
- Audit: every text save is a new full version (author, time, note, carry parent), so the text history is complete. Mapping edits
  rewrite the active variant's rows in place and are not versioned (only `MappingReviewedAt`/`By` and row `Origin`/`CreatedBy`).

## Findings in the code
1. Revisions are addressed by level in the save contracts (`Level = @Level AND NOT IsBase`), valid because levels are unique among
   non-base revisions. The search takes the revision `code` and resolves it.
2. The reader's `RevisionSql.EffectiveVerses` falls back along the slider; bulk replace uses "own variant else Elb1871".
3. Lemma/Strong's of revised words are indirect (`BasePositionInVerse`, `RevisionMapping` + STEP tables).
