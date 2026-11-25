# 0026 · Decisions

## Fixed (from the request)

- **The existing mapping stays untouched.** No row of this plan writes `Elb1871HebrewMapping`, `Elb1871GreekMapping`, `RevisionMapping`, `AgentVerseProposals` or the mapping seed. The only way data reaches the main mapping is the promotion row (0026.9), which runs only after the decision gate and goes through the existing 0018 change-set mechanism with undo.
- **It is an evaluation.** The layer must be cheap to drop: dropping it is `DROP TABLE` of its own tables plus removing its endpoints and the reader toggle.

## Plan decisions (proposed, need confirmation)

1. **Anchor = STEP word + position in word, German side = Elb1871 word.** Key `(StepHebrewWordId, PositionInWord)` from `StepHebrewWordsNormalizedToHebrewWords`, not the Strong's number. The main mapping keys morphemes by Strong's and merges repeats (`SourceMorphemes.ParseHebrew`); a position key keeps two equal morphemes apart and maps 1:1 to the `/` pieces. The German side uses `ElbWordId` plus optional `GermanWordPart`, as the main mapping does, so a later promotion is a lookup, not a translation.
2. **Word links are input, never output.** The layer only assigns morphemes to German words that are already linked to that source word in the main mapping. A German word the main mapping does not link to the source word cannot get a morpheme. This keeps the two layers comparable and means the AI pass cannot invent links.
3. **Append-only with "current" flag**, like `RevisionVerseAudit`: a new assignment for a morpheme supersedes the old one (`SupersededAt`), nothing is updated in place. Every row carries `Source` (`projection`, `rules`, `ai`, `human`), `UserId` (AI runs under the AI user id, CLAUDE.md "Editing model") and `RunId`.
4. **"Not rendered" is a value.** ו in Num 14:25 #1 has no German word in Elb1871 (*Die Amalekiter …*). A morpheme can be marked `unrendered` explicitly, so coverage tells "nobody decided yet" from "decided: no German word".
5. **AI pass is a new run mode `morpheme`** in the existing agent-run machinery (0016/0018: profiles, cost tracking, background job), with its own prompt `morpheme-hebrew-v1`, answers written to the layer, not to `AgentVerseProposals`.
6. **Maintainer-only until the gate.** Endpoints and the reader toggle require `Admin`/`Maintainer`; anonymous readers see nothing new.
7. **No new dependency.** Proposed new frontend component: `MorphemeSegments` (a Hebrew word as hoverable segments). To be confirmed in chat before 0026.4f, per the frontend conventions; check `Components.md` for an existing word-chip component to extend first.

## Open questions for the maintainer

| Question | Why it changes the plan |
|---|---|
| Has the 0018 link-role backfill been applied on the live DB since 2026-11-17? | If yes, the projection producer already has real morpheme data and the "rules" producer is mostly redundant; if no, the shadow rules run is the cheapest first result. |
| Should the separate layer survive a "promote" decision, or be dropped after promotion? | Keeping it means two sources of truth for morphemes; dropping it means the reader segment view must then read the main mapping's `StrongsNumber`. |
| Is a paid AI run acceptable for the evaluation, and on which range (e.g. Ruth + Num 13–14, about 150 verses)? | Decides whether 0026.6 runs at all and sets the cost ceiling. |
| Who builds the reference set (50 verses hand-checked per morpheme)? | Without it accuracy cannot be measured, only agreement ([04-evaluation.md](04-evaluation.md)). |
| Should prompt improvements for the main mapping (richer `morphemes` in `hebrew-v3`) be part of this roadmap or a follow-up? | Listed as optional row 0026.10; it is the "improve the AI mapping" half of the request but changes what future main-mapping proposals look like. |
