# 0022 · Study engine

Behaviour follows Lexio `docs/04-study-engine.md` (session shape, waves, open wave, spacing, strength). This file lists only what is different here or needs a decision. The engine owns three choices: the next entry, the quiz type, and what an answer did to the card. The frontend never judges.

## Migration `0039_VocabProgress.sql` (learner side)

```sql
CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabCards"
(
    "UserId"       int4        NOT NULL,
    "VocabEntryId" int4        NOT NULL,
    "State"        varchar(10) NOT NULL,     -- new | learning | review | relearning
    "Stability"    float8      NOT NULL,
    "Difficulty"   float8      NOT NULL,
    "Due"          timestamptz NOT NULL,
    "Reps"         int4        NOT NULL DEFAULT 0,
    "Lapses"       int4        NOT NULL DEFAULT 0,
    "LastReview"   timestamptz NULL,
    "CreatedAt"    timestamptz NOT NULL DEFAULT now(),   -- word age for the quiz ladder
    "SeenContentVersion" int4  NOT NULL,                 -- entry version the learner last accepted
    CONSTRAINT "VocabCards_PK" PRIMARY KEY ("UserId", "VocabEntryId")
);
CREATE INDEX IF NOT EXISTS "VocabCards_Due_idx" ON "unshackled-word"."VocabCards" ("UserId", "Due");

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabReviewLog"   -- append-only, input for a later optimizer
(
    "Id" bigserial, "UserId" int4, "VocabEntryId" int4, "SessionId" int4, "QuizType" varchar(20),
    "IsCorrect" boolean, "Given" varchar(120), "ResponseMs" int4, "Rating" int2,
    "StateBefore" varchar(10), "StabilityBefore" float8, "DifficultyBefore" float8,
    "StabilityAfter" float8, "DifficultyAfter" float8, "ReviewedAt" timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabSessions"
(
    "Id" serial4, "UserId" int4, "CourseId" int4, "StartedAt" timestamptz DEFAULT now(),
    "FinishedAt" timestamptz NULL, "Plan" jsonb NOT NULL   -- entry ids, remaining reps, open wave
);

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabUserSettings"
(
    "UserId" int4 PRIMARY KEY, "DisabledQuizTypes" text[] NOT NULL DEFAULT '{}'
);
```

(The final script gets the full PK/FK/CHECK naming. Shapes are shortened here.)

Cards are keyed by entry, not by course. A later curated course over the same lemmas reuses the memory, the same way progress is shared across courses in Lexio.

## Scheduler

`VocabScheduler` in `Application/Features/Vocab` wraps the FsrsSharp v6 package: retention 0.9, learning and relearning steps `[10m]`, default weights, `wrong → Again`, `correct → Good`. It is the only class that references the package. Strength 0–5 from stability on Lexio's ladder (1, 3, 7, 21, 60 days).

## Picking

- **New entries:** the lowest `FrequencyRank` in the course without a card, skipping `IsMorpheme`, `RetiredAt`, and entries without an accepted meaning. The learner walks down the list.
- **Reviews:** cards that are due, most at risk first (lowest retrievability). Lexio's allowance: reviews take the 20-word target first, new words get what is left, clamped to 4–10.

## Quiz types

| Type | Prompt → answer | Needs | From age |
|---|---|---|---|
| `introduction` | dictionary form, meanings with counts, audio, one verse → "verstanden" | — | — |
| `multiple_choice` | lemma (+ form) → pick German meaning | 3 distractors | day 0 |
| `reverse_multiple_choice` | German meaning → pick lemma | 3 distractors | day 1 |
| `listening` | audio → pick lemma | `AudioObjectKey` | day 1 |
| `verse_context` | original verse with the word highlighted, German verse with the rendering blanked → pick German meaning | a verse where a key of the entry has a head link | day 1 |
| `typing` | German meaning → type the dictionary form, or the lemma when no form is set (script keyboard, separators pre-filled) | — | day 2 |

The fallback rule is as in Lexio: if a type is not possible, take the next one. If the word's age leaves nothing, ignore the age. `typing` always works.

**Verse choice:** prefer verses whose link is human-made (`CreatedByUserId` not the AI user) and whose rendering is the prompt meaning. Prefer short verses. Text: Elberfelder 2027 variant, otherwise Elb1871 (the same rule as the view).

## What counts as correct

- MC and verse context: any `IsAccepted` meaning of the entry.
- Reverse MC and listening: the entry itself.
- Typing: the target is `DictionaryForm ?? Lemma`. `VocabText.Segment` splits it into letter groups (runs of Unicode letters with their combining marks) and fixed separators (everything else: comma, hyphen, space). `μέσος, -η, -ον` → `[μέσος] ", -" [η] ", -" [ον]`; `λόγος, -ου, ὁ` → `[λόγος] ", -" [ου] ", " [ὁ]`. `next` sends the layout (group lengths and separators, never the letters). The learner fills only the groups, and `answers` gets one string per group. Correct when every group matches: `VocabText.Normalize(given) == VocabText.Normalize(expected)`. `Normalize` = trim, NFD, drop combining marks (Greek tonos, breathings, iota subscript; Hebrew U+0591–U+05C7), `ς` → `σ`, lower-case Greek. Hebrew final letters are not folded. Unit tests: ἀρχή/αρχη, Ἰησοῦς/ιησους, בְּרֵאשִׁית/בראשית, μέσος/η/ον, a wrong ending (`ος` for `η`) fails, and a form without separators gives one group.

## Distractors (`VocabDistractorPicker`)

The absolute rule replaces Lexio's synset rule: **never an option that is also correct.**
- MC: a German distractor must not be an accepted meaning of the prompt entry (case-folded).
- Reverse MC/listening: a lemma distractor must not have the prompt meaning among its accepted meanings (λέγω and εἶπον both accept "sagen").

Tiers, additive: (1) the same open wave, (2) the same course with the same POS and a nearby rank (±200), (3) the same course with the same POS, (4) the same course with any POS. Only the top-ranked meaning of a distractor entry is shown as a German option.

## Lexicon changes under active cards

The lexicon changes while people learn from it (decision in [02-decisions.md](02-decisions.md)). The learner decides what happens to their card. The editor never decides for them.

| Change | Effect on cards |
|---|---|
| accepted meanings, form, lemma, keys added/removed | `ContentVersion + 1`. Cards stay and are flagged. |
| split (keys moved to a new entry) | The source entry is flagged. The new entry has no cards and comes as new in frequency order. |
| merge A into B | Cards on A move to B. A learner who had both keeps the card with the higher stability, and the other one is dropped (its review log stays). B is flagged for everyone who had A or B. A is deleted after a `merge` change row. |
| retire / delete | Cards stay with no entry to study. They are flagged with "entfernt", and the only choice is to remove them. Delete is allowed only for entries without keys. |

- A card is **flagged** when `SeenContentVersion < VocabEntries.ContentVersion`, or when its entry is gone. Nothing is copied per learner. The before/after comes from the `VocabEntryChanges` rows newer than the card's version.
- **Keep going** sets `SeenContentVersion` to the current version. **Reset** deletes the card. The review log keeps its rows, and the word comes back as new at its rank.
- Until the learner decides, a flagged card is scheduled and quizzed against the **current** content. A retired card is not quizzed.
- Each change by the maintainer is one transaction: entry update, version bump, change row, card moves.
- `GET api/vocab/changes` (user): flagged cards with their changes. `POST api/vocab/changes/{entryId}` `{action: keep | reset}`. There is also a "keep all" for the whole list.

## API (`Endpoints/Vocab`)

| Route | Auth | Purpose |
|---|---|---|
| `GET api/vocab/courses` | public | both courses, total entries; for a signed-in user also known/due/coverage |
| `GET api/vocab/courses/{code}/entries?skip&take&q` | public | frequency list |
| `GET api/vocab/entries/{id}` | public | entry, meanings, keys, renderings, audio URL, sample verses |
| `POST api/vocab/sessions` `{course}` | user | start or resume the open session |
| `GET api/vocab/sessions/{id}/next` | user | next card (type, prompt, options; never the answer) |
| `POST api/vocab/sessions/{id}/answers` | user | verdict, correct answers, strength before/after, verse |
| `GET`/`PUT api/vocab/settings` | user | disabled quiz types |
| `api/maintainer/vocab/*` | Admin/Maintainer | generate, refresh renderings, derive, edit, audio, agent forms |

**Coverage** = the sum of `Occurrences` of entries whose card has strength ≥ 3, divided by the track's total occurrences (morphemes excluded from both).
