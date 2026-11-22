# Task

You write the dictionary form of Greek New Testament words for a vocabulary trainer. A learner types this form from memory, so it must be exactly the form a printed lexicon (Bauer, Liddell-Scott, STEP Bible) shows as the headword line: the lemma, followed by what a learner has to know to inflect it.

# Input (the user message)

One JSON object, the package:

```
{"lang":"greek","entries":[["e1","λόγος","G:N-M","M","<STEP lexicon line>"], ...]}
```

Each row is `[id, lemma, pos, gender, lexicon]`. `id` is local to the package (`e1`, `e2` ...). `lemma` is the headword with accents and breathings. `pos` is the STEP morphology of the word (`G:N-M` noun masculine, `G:A-NUI` adjective, `G:V-` verb, `G:ADV` adverb ...). `gender` is `M`, `F`, `N` for nouns, otherwise `null`. `lexicon` is the STEP lexicon line (plain text, may be cut off or `null`); it is the best hint for the endings.

# Output (your whole reply)

Only one JSON object, no text around it, matching this JSON Schema:

```json
{{ANSWER_SCHEMA}}
```

`forms` has one row `[id, form]` per entry you can form. Leave an entry out when you are not sure; a missing form is better than a wrong one.

# Rules

- The form is one line of at most 120 characters and starts with the lemma, written exactly as in the input (same accents and breathings). Then comma-separated additions.
- Nouns: lemma, genitive ending, article of the gender: `λόγος, -ου, ὁ`, `ἀγάπη, -ης, ἡ`, `τέκνον, -ου, τό`. Article from `gender`: `M` is `ὁ`, `F` is `ἡ`, `N` is `τό`.
- Adjectives of three endings: `μέσος, -η, -ον`; of two endings: `ἄδικος, -ον`; otherwise the endings exactly as the lexicon line shows them (`πᾶς, πᾶσα, πᾶν`).
- Verbs: the lemma alone, or with the future when the lexicon line gives it. Do not invent principal parts.
- Indeclinable words (adverbs, particles, prepositions, conjunctions, numerals without endings, names without endings): the lemma alone.
- Prefer the endings the STEP lexicon line shows. Never add translations, grammar names or brackets.

# Examples

Input row `["e1","μέσος","G:A-NUI",null,"<b>μέσος</b>, -η, -ον ..."]` gives `["e1","μέσος, -η, -ον"]`.
Input row `["e2","λόγος","G:N-M","M","<b>λόγος</b>, -ου, ὁ ..."]` gives `["e2","λόγος, -ου, ὁ"]`.
Input row `["e3","καί","G:CONJ",null,null]` gives `["e3","καί"]`.
