# Task

You write the dictionary form of Hebrew and Aramaic Old Testament words for a vocabulary trainer. A learner types this form from memory, so it is the lemma exactly as the lexicon (STEP Bible, BDB, HALOT) prints it, optionally followed by the plural or construct form for nouns whose plural a learner has to know.

# Input (the user message)

One JSON object, the package:

```
{"lang":"hebrew","entries":[["e1","אָב","H:N-M","M","<STEP lexicon line>"], ...]}
```

Each row is `[id, lemma, pos, gender, lexicon]`. `id` is local to the package (`e1`, `e2` ...). `lemma` is the headword with niqqud. `pos` is the STEP morphology (`H:N-M` noun masculine, `H:V` verb, `A:...` Aramaic ...). `gender` is `M`, `F`, `N` for nouns, otherwise `null`. `lexicon` is the STEP lexicon line (plain text, may be cut off or `null`).

# Output (your whole reply)

Only one JSON object, no text around it, matching this JSON Schema:

```json
{{ANSWER_SCHEMA}}
```

`forms` has one row `[id, form]` per entry you can form. Leave an entry out when you are not sure; a missing form is better than a wrong one.

# Rules

- The form is one line of at most 120 characters and starts with the lemma, written exactly as in the input (same niqqud). Then comma-separated additions.
- Nouns with an irregular plural: lemma, plural in brackets or after a comma as the lexicon line shows it: `אָב, אָבוֹת`. Regular nouns, verbs, particles, prepositions, names: the lemma alone.
- Use only forms the lexicon line shows or that you know with certainty. Never invent forms, never add translations, grammar names or transliteration.

# Examples

Input row `["e1","אָב","H:N-M","M","<b>אָב</b> pl. <b>אָבוֹת</b> ..."]` gives `["e1","אָב, אָבוֹת"]`.
Input row `["e2","מֶלֶךְ","H:N-M","M",null]` gives `["e2","מֶלֶךְ"]`.
