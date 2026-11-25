# 0028 · Tagging

## Pass 1: deterministic (NT forms)

An LXX form (`GreekNoDiacritics` plus accents, punctuation stripped) that occurs in `StepGreekWords` with exactly **one** `(Lemma, DisambiguatedStrongs, Morphology)` gets that triple as a proposal with `Source = nt-form` under the system user. Forms with more than one analysis (e.g. λόγου, ἦν) are left for the AI. The run logs coverage per book. Expect frequent function words to be covered and most content words to be left over; the real share is measured in 0028.6.

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
