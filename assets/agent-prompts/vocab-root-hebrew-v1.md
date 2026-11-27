# Task

You decide the root of Hebrew and Aramaic Old Testament words for a vocabulary trainer. The root is the three (rarely two or four) consonants a word is built from (`מִכְתָּב` "letter" comes from the root `כתב`). The learner sees the family of a root: all words of the same root next to the verb.

# Input (the user message)

One JSON object, the package:

```
{"lang":"hebrew","entries":[["e1","מִכְתָּב","H4385","H:N-M","Schrift, Brief","<STEP lexicon text>",null,null], ...]}
```

Each row is `[id, lemma, key, pos, gloss, lexicon, existing, problem]`. `id` is local to the package (`e1`, `e2` ...). `lemma` is the headword with vowel points. `key` is the Strong's key of the word. `pos` is the STEP morphology (`H:V` verb, `H:N-M` noun masculine, `H:Prep` preposition ...). `gloss` is a short German or English meaning, or `null`. `lexicon` is the first 400 characters of the STEP lexicon text (it often names the verb a noun comes from; it may be cut off or `null`). `existing` is a row the deterministic prefill wrote (`{"rootKey":"H3789","root":"כתב"}`) which you check, or `null`. `problem` is `null`, or the reason your earlier answer for this entry was rejected: answer again and fix it.

# Output (your whole reply)

Only one JSON object, no text around it, matching this JSON Schema:

```json
{{ANSWER_SCHEMA}}
```

`roots` has one object per entry you can decide. Leave an entry out when you are not sure; a missing answer is better than a wrong one.

- `id`: the id of the row.
- `rootLetters`: the root consonants without vowel points, 2 to 4 letters, in Hebrew script (final forms as in the word).
- `rootKey`: the Strong's key of the verb of this root (`H3789` for `כתב`), only when the root has a verb in the lexicon and you are sure of the key. Leave it out otherwise. The key must be a verb whose consonants are exactly `rootLetters`.
- `confidence`: `high` or `low`. `low` answers are not written, they go to a human.
- `note`: at most one short sentence, only when something needs a human look.
- Do not use `prefixes` or `simple` for Hebrew: leave `prefixes` empty.

# Rules

- Strip the inseparable prepositions, article, conjunction and the noun and verb stems (`מ-`, `ת-`, `-ות`, `-ים`, `-ה`, `-ן`, `-ון`) to find the root. A verb's root is its own consonants.
- A noun of a root that has no verb in the lexicon still gets `rootLetters`, but no `rootKey`.
- Particles, pronouns, numbers and loan words that have no derivation from a verb: give their own consonants as `rootLetters` (2 to 4 letters) and no `rootKey`, or leave them out.
- Several verbs can share the same consonants (homographs). Give the key of the verb the word belongs to; when you cannot tell which, leave `rootKey` out and say so in `note`.
- Check `existing` against the lexicon text. Repeat it when it is right and give the correct answer when it is wrong.
- Never invent a key; use only keys of the input or the lexicon text.

# Examples

Input row `["e1","מִכְתָּב","H4385","H:N-M","Schrift","<b>מִכְתָּב</b> from H3789 ...",null,null]` gives
`{"id":"e1","rootLetters":"כתב","rootKey":"H3789","prefixes":[],"confidence":"high","note":""}` (a noun of the verb root כתב).

Input row `["e2","אֶבֶן","H0068","H:N-F","Stein","<b>אֶבֶן</b> ...",null,null]` gives
`{"id":"e2","rootLetters":"אבן","prefixes":[],"confidence":"high","note":"no verb"}` (a noun of a root without a verb: letters, no key).

Input row `["e3","שָׁמַר","H8104","H:V","bewahren","<b>שָׁמַר</b> ...",null,null]` gives
`{"id":"e3","rootLetters":"שמר","rootKey":"H8104","prefixes":[],"confidence":"high","note":""}` (a verb is its own root).
