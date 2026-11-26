# 0028 · Tagging

## Pass 1: deterministic (NT forms)

An LXX form (`GreekNoDiacritics` plus accents, punctuation stripped) that occurs in `StepGreekWords` with exactly **one** `(Lemma, DisambiguatedStrongs, Morphology)` gets that triple as a proposal with `Source = nt-form` under the system user. Forms with more than one analysis (e.g. ὁ, θεός, αὐτῶν; measured in 0028.6) are left for the AI. The run logs coverage per book. Expect frequent function words to be covered and most content words to be left over; the real share is measured in 0028.6.

**Normalization (0028.6).** Both sides are running text (the NT `Greek` has graves like `τοὺς`, capitals and attached punctuation, `¶`), so both go through `LxxFormNormalizer.Normalize` and are compared like with like: NFD, keep letters and combining marks only (this drops punctuation, `¶`, digits, Swete's `[ ]` and `⸂ ⸃ ⸆`, and the elision mark `᾽ ’`), grave to acute, lower case, NFC. Breathings, iota subscript and diaeresis stay. An elided form stays its own form (`ἀπ` is not `ἀπό`). Words that are all capitals without any accent or breathing (`ΕΝ ΑΡΧΗ`, Swete's book openings) are skipped and counted, because their accented form is unknowable; `Ἐν` with a breathing is ordinary text.

**Rules of the run.**
- The NT side is `StepGreekWords` grouped by (Greek, Lemma, DisambiguatedStrongs, Morphology), then by normalized form. One distinct triple means a proposal; more than one means the form is ambiguous. A single odd NT occurrence therefore makes a form ambiguous (see the 0028.6 status note).
- The Strong's must match the table CHECK (`G` + 4 digits + optional capital letter) and the morphology must pass `GreekMorphologyCode`; otherwise the word is skipped and counted as invalid.
- Written rows: `Status = proposed`, `Source = nt-form`, `UserId` NULL (system; there is no system user in `Users`). Rows with another source or status (`confirmed`, `ai`, `human`) are never touched. The run makes the `nt-form`/`proposed` rows equal to the computed set: new ones are inserted, changed ones updated, ones that no longer qualify deleted. A second run changes 0 rows.
- Verb `lxx-prefill` (SeedDb) and `POST api/maintainer/lxx/prefill` (job on `SeedJobRunner`) run the same `LxxNtFormTagger`; `GET api/maintainer/lxx/status` gives the card counts.

## Pass 2: AI mode `lxx-tag-v1`

Run mode `lxx-tag`, prompt `assets/agent-prompts/lxx-tag-v1.md` (as built in 0028.7). A run is an ordinary agent run (`api/maintainer/agent-runs` or `Tooling.AgentWorker`) over an OT book and a Rahlfs chapter/verse range; every subverse and both Daniel text forms of the range are included.

**Package** (`LxxAgentPackageBuilder`): the units of the range in Swete's reading order, never across a chapter, at most 40 units per package (or the profile's smaller `MaxVersesPerPackage`, halved after a cut-off answer); the subverses of one verse stay in one package. Per unit (`v1` …):
- `ref` (`Rt 1:1`, `Hi 36:28.1`, `Dan 3:24 (th)`), `lxx` rows `[position, form, prefill?, candidates?]`. `prefill` is the stored tag (`[lemma, strongs, morph]`, pass 1 or any other proposal: "check it"; `[…, "confirmed"]` for a confirmed one), its lemma rewritten to the TBESG spelling. Words without a tag get up to 5 TBESG candidates `[strongs, lemma, gloss]`.
- `hebRef` and `heb` (the STEP Hebrew words of `HebRefId`: `[n, text, strongs, gloss, morph, morphemes?]`, n = 1.. in reading order, morphemes only for words with a prefix or suffix); both are missing for additions. `map` = `split`/`merged` when the unit is not one-to-one.
- The empty mapping lists (`de`, `src`, `fixed`, `hints`) are not serialized for LXX verses (`AgentPackageJson`).

**Candidates** (`LxxCandidateIndex`, in memory, no per-word query): (1) the Strong's numbers the same normalized form has in the NT, most frequent first; (2) the same without accents/breathings (Swete's capitals `ΑΡΧΗ`); (3) NT forms sharing the longest prefix of at least 4 letters without accents (`ἐλάβοσαν` → `ἔλαβον` λαμβάνω, `κατῴκησαν` → `κατῴκησεν` κατοικέω); (4) TBESG lemmas sharing the longest prefix of at least 3 letters with the form or the form minus an augment ε, by NT frequency. Only Strong's numbers TBESG has are offered.

**Answer** (`AgentAnswerSchema.LxxJson`): per verse `words` rows `[position, lemma, strongs, morph, heb, confidence]`, every word exactly once, `heb` the Hebrew numbers it renders (may be empty), `confidence` `high`/`low`; optional `note`. A word TBESG has no entry for (many names) is answered with an empty lemma and Strong's.

**Validator** (`AgentProposalValidator.ValidateLxx`, codes in `LxxFlagCodes`). Per word:
- the Strong's (normalized: `G746` → `G0746`, four or five digits) is in TBESG and the lemma matches one of its TBESG lemmas (normalized form; STEP's list lemmas like `ἔπω, ἐρῶ, εἶπον` match by part); the stored lemma is TBESG's;
- the code passes `GreekMorphologyCode`;
- every Hebrew number exists in the unit's Hebrew verse (none for additions);
- the word is answered exactly once.

A failing word is dropped and recorded (`strongs-unknown`, `lemma-mismatch`, `morph-invalid`, `heb-unknown`, `word-duplicate`, `word-unknown`, `row-shape`, `no-strongs`; a missing word is `word-missing`); an odd confidence is kept as `low` (`confidence`). The rest of the verse is stored, flagged. A verse is rejected (sent back to the model) only when it is answered twice, unreadable, or no word passes.

**Storage** (`LxxProposalSubmitter`): `AgentVerseProposals` with `Language = 'lxx'`, Rahlfs book/chapter/verse, `Lemmas` = word tags `{lxxWordId, position, lemma, strongs, morph, confidence, prefilled}`, `Links` = `{lxxWordId, position, stepHebrewWordId, hebNumber}`, `Flags` an object `{textForm, subverse, lxxVerseId, hebRefId?, flags: [{code, message, position}]}` (the open-proposal index reads textForm/subverse). A new proposal supersedes only the open one of the same unit (book, chapter, verse, language, text form, subverse). The stale check at submit compares the LXX and Hebrew word ids per unit. **AI runs never write `LxxWordTags`**: the proposals wait for the review page (0028.7f). The mapping review and scoring refuse LXX proposals/runs.

## Review and confirm

Backend built in 0028.7f (the page `/edit/lxx` follows, see [06-ui.md](06-ui.md)). Two proposal sources: `proposed` rows of `LxxWordTags` (pass 1, `nt-form`) and open AI proposals (`AgentVerseProposals`, language `lxx`, one per unit). Chapters are Rahlfs chapters; a unit is one `LxxVerses` row. All endpoints are Admin/Maintainer, under `api/maintainer/lxx/`:

- `GET review/books`: books with open work (units with a proposed tag or an open AI proposal), chapters ascending, with AI unit and `low` word counts.
- `GET review/{bibleBookId}/{chapter}`: every unit in Swete order with words, current tag (any status, lemma in TBESG spelling), stored links, the unit's Hebrew words (numbered 1.. as in the package), the open AI proposal (id, run, flags, per-word answer with confidence). 404 for a chapter without LXX verses.
- `POST review/confirm` `{lxxVerseId, proposalId?, words:[{lxxWordId, lemma?, strongs?, morphology?, hebrewWordIds}]}`: confirms one unit as shown. 400 when a rule fails (Strong's not in TBESG, lemma not that entry's, code fails `GreekMorphologyCode`, Hebrew word not of the unit's verse or a link in an addition, lemma/Strong's/code not all three), 404 unknown unit, 409 when the open proposal is not `proposalId`.
- `POST review/discard` `{lxxVerseId, proposalId}`: AI proposal `discarded`, audit `reject`; tags and nt-form proposals stay (pass 1 would write them again anyway). 409 when not open.
- `POST review/confirm-chapter-safe` `{bibleBookId, chapter, units:[{lxxVerseId, proposalId, lxxWordIds}]}`: "Kapitel bestätigen (sichere)". Confirms only the listed words (what the maintainer saw) whose AI answer is `high` and that are not confirmed; `low`, words without an AI answer (an nt-form prefill alone is not safe) and already confirmed words are skipped and counted; units whose proposal changed are returned as `staleUnits`. The proposal becomes `merged` only when every word of it is confirmed as proposed, otherwise it stays open.
- `GET lexicon?q=&limit=` (TBESG by Strong's number or lemma start, accents optional) and `GET morphology?code=` (live parse check) for the edit cells.

One transaction per call locks the units, re-reads the state, closes the proposals first (a proposal that is no longer open aborts everything, `Stale`/409), upserts confirmed `LxxWordTags` (user = reviewer), replaces the words' `LxxHebrewLinks` (`confirmed`) and appends one `LxxTagAudit` row per unit with before/after snapshots. Rules of the write:

- Source of a confirmed tag: `ai` when it equals the open AI answer for the word, else the current tag's source when it equals the current tag (`nt-form` for an accepted prefill), else `human`. "Equal" is Strong's, code and TBESG lemma. The stored lemma is always TBESG's.
- Audit action `confirm`, or `edit` when a confirmed tag or link changes; nothing changed writes no row. Proposal `merged` only when tags and links equal the AI answer exactly, else `edited-merged`.
- A word with all three tag fields empty keeps its stored tag and only gets its links (words without a TBESG entry, e.g. Μωάβ).
- A newer run on a verse supersedes its open proposals but never overwrites confirmed tags.

"KI neu für diesen Bereich" (the CLAUDE.md requirement): `LxxAgentPackageBuilder` skips units whose words all have confirmed tags unless `AgentBuildRequest.IncludeConfirmed` is set (the "auch bestätigte" tick); a unit with some confirmed words is sent with those words marked `"confirmed"` in the prefill, so the model copies them. The builder option exists; passing it through the run start request (`api/maintainer/agent-runs`) belongs to the frontend step.
