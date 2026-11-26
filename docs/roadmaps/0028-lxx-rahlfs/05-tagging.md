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

A package is one chapter (or 40 verses at most) of a book. Per verse it contains:
- the Swete words with their positions, and any pass-1 proposal marked as "prefilled, check";
- the Hebrew verse that TVTMS maps to it: STEP words with position, `DisambiguatedStrongs`, gloss and morphemes (none for additions);
- TBESG candidates per word: lemmas whose form list or stem matches, at most 5.

The answer lists, per LXX word: `lemma`, `strongs`, `morph`, `heb` (Hebrew positions it renders, may be empty) and `confidence` (`high`/`low`), plus a verse `note`.

The validator (`AgentProposalValidator`, new `lxx` branch) checks:
- the Strong's is in TBESG and its lemma equals the answer's lemma;
- the morphology code parses under the Robinson grammar used by STEP (a shared `GreekMorphologyCode` parser);
- each Hebrew position exists in the mapped verse;
- every word is answered exactly once.

A failing word drops the word, not the verse, and the drop is recorded in `Flags`. The prompt lives in `assets/agent-prompts/lxx-tag-v1.md`, and profiles use `AgentMapping:Profiles` as they are.

## Review and confirm

- `/edit/lxx` lists verses that have open proposals, filtered by book and confidence.
- Confirming a verse writes `LxxWordTags` (`confirmed`, `Source` `ai` or `human` when edited) and `LxxHebrewLinks`, appends one `LxxTagAudit` row, and sets the proposal to `merged` or `edited-merged`.
- "Ganzes Kapitel bestätigen" confirms only the words of `high` confidence that the maintainer has seen on screen; `low` words stay open.
- A newer run on a verse supersedes its open proposals but never overwrites confirmed tags. "KI neu für diesen Bereich" (the CLAUDE.md requirement) runs only on verses without confirmed tags unless the maintainer ticks "auch bestätigte".
