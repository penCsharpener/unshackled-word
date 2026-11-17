# 0016 · Validation

Runs per verse in `AgentProposalValidator`. **Hard** rules reject the verse (nothing stored, message back to the model). **Soft** rules store the proposal with a flag the reviewer sees.

## Package binding (hard)

- `packageId` exists, is not expired, and the verse word-id hash still matches the live verse (text not changed since the package was built).
- Every local id exists in the package; ids are mapped back only through the package table, never parsed.
- `fixed` items are not changed or removed.

## Mappings (hard)

Reuses `SaveVerseMappingValidator` after id mapping, plus:

- German id and source id belong to the **same verse** (`g` of `v3` with `s` of `v3` only).
- Added word: parent is a German word of the same verse, not itself, and not itself an added word without a link (no parent chains longer than one).
- A German word is not both linked and added.
- No duplicate link.
- Every `open` German token is covered (`links`, `added` or `unmapped` with reason).
- `parts`: same German id, at least two parts, orders 1..n without gaps (rows with the same order share the text, so one part may link to several source words), part text max 30 chars, concatenated text equals the German word (letters and digits only, case-insensitive), an added word has no parts. Implemented for live saves in `SaveVerseMappingValidator` (0016.10); the agent validator reuses it.

## Mappings (soft)

- Source word left without any German link (except articles/particles listed in the instructions).
- Crossing links far apart (`g1→s14` in a 15-word verse) — often a model slip.
- Link contradicts confirmed lemma ↔ Strongs statistics (German lemma never seen with that Strongs, while another candidate in the verse is common).
- More than 3 German words on one source word.

## Lemmas (hard)

- Not empty, no whitespace except inside multi-word lemmas listed in the instructions, no punctuation, max 40 chars.
- STTS (if given) fits the part of speech (`LemmaSttsGuard`).
- Nouns (`NN`, `NE`) start with a capital letter; other parts of speech do not, unless the lemma is a name.
- Separable particle: the particle is a prefix of the verb lemma.

## Lemmas (soft)

- Lemma not in the lexicon and not a known modern headword (new word → reviewer looks).
- Lemma and form share no prefix of 2+ letters and the pair is not in the irregular-verb table (`ging`/`gehen` is fine, `ging`/`laufen` is flagged).
- Verb lemma does not end in `-en`, `-eln`, `-ern`, `-n`.
- Lemma differs from a human-edited lemma for the same form elsewhere.

## Message format

`v3 g7: source s12 belongs to v4` — local ids, one line each, so a model can fix exactly that.
