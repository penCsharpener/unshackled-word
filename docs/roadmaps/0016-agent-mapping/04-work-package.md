# 0016 · Work package

## Range size

A package covers consecutive verses of one book and one language, cut by a token budget (default ~6,000 input tokens of variable part), never splitting a verse. Typical result: one NT chapter or half a long OT chapter. Larger ranges are split into several packages that share the cached prefix ([07-providers-and-caching.md](07-providers-and-caching.md)).

## Deterministic pass (before the model)

Each German token gets a status: `fixed` (algorithm decided, model must not change it), `hint` (algorithm guess, model may change), or `open`.

| Rule | Status | Source |
|---|---|---|
| Punctuation-only tokens are not mapped | removed from package | `PlainWord` empty |
| Human-confirmed links (merged by a maintainer, not from seed) | `fixed` unless the run is "redo" | proposal history |
| Proper names: German name ↔ source word whose Strongs is a name with a known German form (lexicon) and exactly one candidate in the verse | `fixed` | Strongs lexicon + confirmed pairs |
| Exactly one source word in the verse whose Strongs has been confirmed for this German lemma ≥ K times, and no competitor | `hint` | statistics over confirmed links (lemma ↔ Strongs) |
| German article directly before a noun with no source article in the verse | `hint` added word, parent = noun | POS + morphology |
| Lemma from human edit for this exact word id | `fixed` lemma | `Elb1871MorphologyEdits` |
| Lemma from lexicon for unambiguous form | `hint` lemma | [05-lemmatisation.md](05-lemmatisation.md) |

The model sees `fixed` items as context only. If a whole verse is `fixed`, it is not sent.

## Package format (model input)

Ids are local to the package: verses `v1…`, German `g1…`, source words `s1…`. Short ids cut tokens and stop the model from copying ids between verses by accident (numbers restart meaning per verse only through the `v` prefix, the validator checks membership).

```json
{"packageId":"p_7f3a","lang":"greek","verses":[
 {"v":"v1","ref":"Mk 1:1",
  "de":[["g1","Anfang","Anfang","NN","fixed-lemma"],["g2","des","der","ART"],["g3","Evangeliums","Evangelium","NN"]],
  "src":[["s1","Ἀρχὴ","ἀρχή","G0746","beginning","N-NSF"],["s2","τοῦ","ὁ","G3588","the","T-GSN"]],
  "fixed":[["g1","s1"]],
  "hints":[["g3","s3"]]}]}
```

Arrays instead of objects keep the per-word cost low; the column order is defined once in the cached instructions.

## Answer format (model output)

```json
{"packageId":"p_7f3a","verses":[
 {"v":"v1","links":[["g2","s2"],["g3","s3"]],"added":[],
  "lemmas":[["g3","Evangelium"]],"parts":[],"note":null}]}
```

- `links`: `[german, source]`, many-to-many allowed.
- `added`: `[german, parentGerman]`.
- `lemmas`: only where the model changes or confirms a `hint`/`open` lemma; optional `[g, lemma, stts]`.
- `parts`: compound splits `[german, source, order, text]`, e.g. `["g12","s4",1,"Gersten"],["g12","s3",2,"ernte"]`. A German word with parts has no plain link in `links`.
- `note`: optional free text per verse for the reviewer (max 200 chars; longer is cut and flagged).
- `unmapped`: `[german, reason]` for open tokens with no counterpart; stored as flags.
- `lemmas` may carry a 4th element, the part of speech (`[g, lemma, stts, upos]`), so a tag change (e.g. `NE` → `NN`) can pass the STTS fit rule.

## Terms (as implemented in 0016.3)

- **Confirmed link**: a mapping row whose `CreatedByUserId` is a user without role `ai`. Seed rows have no author and are not confirmed.
- **Modes**: `redo` (default) offers confirmed links and existing seed/AI links as hints (decided 2026-11-16; never `fixed`, the model may change them; to be implemented after 0016.6); `fill-open` keeps every existing link as `fixed` and asks only for words without any link.
- **Whole verse fixed** means all links and all lemmas fixed; such verses are counted but not sent.
- In `fixed` and `hints`, a pair whose second element is a `g…` id is an added word with that parent.
- Local `g`/`s` ids are unique within a package; `v` restarts per package.

Every `open` German token must appear in `links` or `added`, or in `unmapped` with a reason; silence is a rejection ([06-validation.md](06-validation.md)).
