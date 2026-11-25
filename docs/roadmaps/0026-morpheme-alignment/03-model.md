# 0026 · Model

## Picture

```
StepHebrewWords 55953  וְ/הָֽ/עֲמָלֵקִ֥י   (Num 14:25 #1)
  pos 1  ו   H9002  HC     and ─────────┐
  pos 2  ה   H9009  Td     the ───────┐ │      main mapping (unchanged):
  pos 3  עֲמָלֵקִי H6003 Ngmsa Amalekite ─┐ │ │      98342 Die        → 55953
                                      │ │ │      98343 Amalekiter → 55953
MorphemeAlignments (new, shadow)      │ │ │
  55953/3 → 98343 Amalekiter   ◄──────┘ │ │
  55953/2 → 98342 Die          ◄────────┘ │
  55953/1 → unrendered         ◄──────────┘
```

## Tables (migration `0050_MorphemeAlignment.sql`)

`MorphemeAlignmentRuns`: `Id`, `Source` (`projection` | `rules` | `ai` | `human`), `UserId`, `AgentRunId` NULL (FK to `AgentRuns` for `ai`), `Scope` jsonb (book/chapter range), `Stats` jsonb, `CreatedAt`, `FinishedAt` NULL.

`MorphemeAlignments`:

| Column | Type | Note |
|---|---|---|
| `Id` | bigserial PK | |
| `StepWordId` | int NOT NULL | FK `StepHebrewWords` |
| `PositionInWord` | int NOT NULL | position of the `/` piece, 1-based, as in `StepHebrewWordsNormalizedToHebrewWords` |
| `ElbWordId` | int NULL | NULL only with `Unrendered` |
| `GermanWordPart` | varchar(30) NULL | as in the main mapping |
| `Unrendered` | boolean NOT NULL DEFAULT false | CHECK: `Unrendered` = (`ElbWordId` IS NULL) |
| `Confidence` | real NULL | AI only |
| `RunId` | int NOT NULL | FK `MorphemeAlignmentRuns` |
| `CreatedAt` | timestamptz NOT NULL | |
| `SupersededAt` | timestamptz NULL | NULL = current |

Partial unique index on (`StepWordId`, `PositionInWord`, `ElbWordId`, `GermanWordPart`) `WHERE SupersededAt IS NULL` (NULLS NOT DISTINCT). A morpheme may have several current rows (one German word each, *zur Antwort geben*), one German word may render several morphemes (*zum* = ל + ה). Punctuation pieces (`\`, H9014–H9019) get no rows.

A view `MorphemeAlignmentCurrent` joins current rows with the morpheme (`StepHebrewWordsNormalized`), the surface piece (split of `HebrewNormalised`), the gloss piece and the German word, for the API and the evaluation queries.

Superseding rule: a run writes per morpheme; when a run assigns a morpheme, every current row of that morpheme from a source of **equal or lower** rank is superseded (`projection` < `rules` < `ai` < `human`). A `human` row is never superseded by a machine run; the run records it as a disagreement in `Stats` instead.

## Producers

1. **Projection** (`Application/Features/MorphemeAlignment/MorphemeProjection`, pure). Per real main-mapping link on a Hebrew word: one non-punctuation morpheme → that position; link with `StrongsNumber` set (0018 backfill) → the position of that Strong's (first one when repeated, flagged); a single German link on a multi-morpheme word → main morpheme. Everything else stays open. Free and fast; it shows what the main mapping already says.
2. **Rules in shadow mode.** Runs `LinkRoleRules.Decide` (0018) on the main-mapping state as input and writes only the rule-1 morpheme decisions into the layer, translated from Strong's to position. No change set, no write to the main tables. This reuses tested code and gives a baseline for the AI.
3. **AI morpheme pass** (run mode `morpheme`, prompt `assets/agent-prompts/morpheme-hebrew-v1.md`). Package per verse: German tokens (text, STTS, lemma), source words each with their morphemes as `[pos, segment, translit, gloss, grammar, strongs]` (from the `/` pieces), and the fixed word links of the main mapping. Answer: `[german, source, pos]` per link, `[source, pos, "none"]` for unrendered morphemes, optional confidence. Validator: every `pos` exists on that source word and is not punctuation; every (german, source) pair is a main-mapping link; nothing else is accepted. Reuses `AgentRunJobRunner`, profiles, cost tracking.
4. **Human.** Maintainer sets or corrects morphemes in the review UI (0026.8f).

## Promotion and drop paths (after the gate)

- **Promote**: for each current layer row whose main-mapping link exists and whose `StrongsNumber` is NULL or differs, set `StrongsNumber` to the morpheme's Strong's. Written as one `Elb1871MorphologyChangeSets` row (`Kind = 'morphemePromotion'`) with `MappingChangeSetRows`, so the 0018 undo works. Repeated equal Strong's in one word lose the position distinction here (the main mapping cannot hold it); the promotion report lists those words.
- **Keep separate**: the reader toggle becomes public; seed export added in a new roadmap.
- **Drop**: migration `00NN_DropMorphemeAlignment.sql`, remove endpoints, page and toggle. Nothing else depends on the layer.
