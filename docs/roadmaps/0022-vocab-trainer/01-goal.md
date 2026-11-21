# 0022 · Goal

## Today

- README item 4 ("vocab trainers for Greek and Hebrew") and CLAUDE.md priority 4 exist only as a line. There is no vocab table, no learner progress, no object storage, and no scheduler in the solution.
- The lexicon data is there: `StepStrongsLexicon` holds 11,013 Hebrew, 669 Aramaic and 11,035 Greek entries (7,755 / 633 / 10,832 distinct `OriginalWord`). The STEP word tables hold 142,096 Greek and 305,652 Hebrew tagged words.
- The NT has 5,412 distinct Greek lemmas: 1,139 occur 10 times or more, 1,897 only once. The 300 most frequent lemmas cover 80 % of all NT word occurrences and the top 1,000 cover 91 %. That curve is the reason to learn by frequency.
- German renderings per Strong's already exist as `StatsStrongsRenderings` (0013/0014, head links only since 0018). It reads **only Elb1871** mappings through `DqWordFacts`, not the revision, and it is keyed by Strong's, not by a learnable lemma.
- Lexio (`../lexio`) has a working trainer: an FSRS-6 engine, waves of four new words, a quiz-type ladder by word age, distractor rules, a script keyboard, browser audio recording and Garage storage. It is the reference for behaviour, not code to copy. Lexio is Svelte, this app is React. Lexio's synsets are not used here.

## End state

- A curated **vocab lexicon** (`VocabEntries`): one row per learnable lemma with a dictionary form (`μέσος, -η, -ον`), a list of German meanings and pronunciation audio. A mapping table connects one or more Strong's keys (H1254A, H1254B …) to one entry.
- Code generates the lexicon from the STEP lemmas. German meanings come from a revision-aware renderings view (Elberfelder 2027 where a variant exists, Elb1871 otherwise) with counts. The maintainer overrides meanings and forms in a vocab editor and regenerates the derived meanings when the revision has grown.
- Two system courses, **NT Greek** and **OT Hebrew/Aramaic**, each ordered by frequency. Any signed-in user studies them. Progress is one FSRS-6 card per user and entry, scheduled by the FsrsSharp v6 NuGet package.
- Quiz types: introduction, lemma → German (MC), German → lemma (MC), typing the lemma with an on-screen Greek/Hebrew keyboard, listening, verse context.
- Garage (S3-compatible) runs in the compose stack and stores the audio files.
- The course page shows how much of the Testament's text the learner's known words cover.

## Out of scope (deferred)

- Images and the image → lemma quiz for concrete nouns.
- Hand-picked courses (a root and its derivatives) and any course other than the two frequency courses.
- One card per meaning (Lexio senses), paradigm drills, inflected-form quizzes.
- Suggestions by non-maintainers, the reviewer hierarchy, an audit log for vocab edits.
- FSRS extras: grading by response time, a retention setting per learner, weight optimization (the review log is collected now, so this can come later).
- Importing progress from Lexio: there is none to import.
