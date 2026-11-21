# 0022 · Decisions

## Given by the maintainer (2026-11-21)

- **Lexio is the reference, with these differences:** no synsets. The learner works through the Strong's lexicon, most frequent words first. The scheduler is the FsrsSharp v6 port, used as a NuGet package and not copied into the repo.
- **The learner learns lemmas, not Strong's numbers.** A separate vocab table holds the lemmas. A mapping table connects Strong's keys to vocab entries, so H1254A and H1254B, which share בָּרָא, become one entry with several German meanings.
- **Dictionary form column:** the card shows a learning form such as `μέσος, -η, -ον` or `λόγος, -ου, ὁ` next to the plain lemma, so that declension patterns are learned too. Code fills it where possible, then an agent run (gender, training data), then the maintainer. A vocab editor is required.
- **German meanings:** code derives them from the renderings view and the maintainer overrides them. When the Elberfelder 2027 revision has grown, the derived meanings are regenerated on demand.
- **Renderings view:** a materialized view maps Strong's to German renderings in Elberfelder 2027 with Elb1871 as fallback, with counts, so the learner sees which translations are common.
- **Courses, not learning paths.** For now there are two system courses with the whole vocabulary in frequency order: NT (Greek) and OT (Hebrew + Aramaic). Hand-picked courses come later.
- **Proper names are included** in frequency order.
- **Any signed-in user** studies. Browsing is public, studying needs login. No new role.
- **One FSRS card per lemma.** All accepted meanings count as correct.
- **Quiz types in v1:** lemma → German MC, German → lemma MC, typing, listening, verse context. The image quiz for concrete nouns is deferred.
- **Typing ignores marks:** Greek accents and breathings, Hebrew niqqud and cantillation.
- **Audio** is recorded or uploaded by the maintainer and stored in Garage. Images are deferred.
- **No old progress.** Learners start fresh.

## Accepted by the maintainer (2026-11-21)

- **Hebrew morpheme Strong's (H9001–H9049) are not vocab entries.** "and", "the" and "his" would take the top ranks of the OT course and are prefixes or suffixes, not lemmas. They get `IsMorpheme = true` and stay out of the course order. They can still be listed.
- **Frequency is counted in the original text** (STEP word rows per Strong's key, summed per entry), not in German mapping counts. It is independent of mapping quality and stable while the revision changes.
- **Derived meanings are studied right away**, marked "ungeprüft" until the maintainer sets `ReviewedAt`. Waiting for 5,000+ reviews would block the trainer for months. See [09-risks.md](09-risks.md).
- **The session engine follows Lexio's 04-study-engine:** up to 20 words, waves of at most 4 new words, three quizzes per word, a single `10m` learning step, target retention 0.9, `wrong → Again` and `correct → Good`, a strength bar 0–5 from stability. Quiz type by age: day 0 MC, from day 1 reverse MC, listening and verse context, from day 2 typing.
- **The vocab lexicon is seed data** (`assets/seed/vocab`), exported and imported like the revision. Learner progress is never part of the seed.
- **Version 0.8.0** for this roadmap.

## Changed by the maintainer (2026-11-21)

- **Typing target is the dictionary form, falling back to the lemma.** The learner types `μέσος, -η, -ον` when that form is set, otherwise `μέσος`. Only the letters are typed. Commas, hyphens and spaces are pre-filled on the card, so the input is three letter groups: `μέσος`, `η`, `ον`. Judging: every group must match after normalization (NFD, drop combining marks, `ς` → `σ`, Greek lower-case; Hebrew final letters are not folded). Replaces the plan's "type only the plain lemma".
- **The lexicon is a work in progress and may change under active cards.** Merge, split, retire and delete are allowed even when learners have cards. A change that affects learning (accepted meanings, typing form, lemma, Strong's keys, merge, split, retire) bumps the entry's `ContentVersion` and is recorded in `VocabEntryChanges`. Cards whose `SeenContentVersion` is lower are shown to the learner as "geändert", with before/after. Per card the learner chooses **keep going** (acknowledge) or **reset** (the card is removed, so the word comes back as new; the review log stays). Until they decide, the card is scheduled normally. Replaces the plan's "never delete, refuse merges of entries with cards". Mechanics in [04-study-engine.md](04-study-engine.md#lexicon-changes-under-active-cards).

## Findings the plan rests on

- `RevisionMapping` has `Role` and `StrongsNumber` (0030), but `RevisionWords` has no German lemma. Words with `Change = 'same'` can borrow the Elb1871Morphology lemma through `BasePositionInVerse`. `changed`/`new` words only have their form (`KeyIsForm`, as in 0013).
- `StatsStrongsRenderings` is created `WITH NO DATA` and filled by `POST api/dataQuality/refresh` (`DqRefreshState`). The new view follows that pattern and must survive the documented `DROP … CASCADE` of `DqWordFacts`.
- Lexio sources to port: `docs/04-study-engine.md` (engine), roadmaps 0016 (FSRS-6), 0020 and 0029 (recording, mp3 via `@breezystack/lamejs`), 0048 (Garage `v2.4.1`, `ObjectStorage:Region`, presigning through the proxy), 0049 (script keyboard, `simple-keyboard`). Each new package needs approval in chat first (CLAUDE.md).
