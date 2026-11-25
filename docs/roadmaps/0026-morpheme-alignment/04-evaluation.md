# 0026 · Evaluation

## Population

Mapped Hebrew/Aramaic words with more than one non-punctuation morpheme: 143,159 on the dev DB. The interesting part is the 105,230 with more than one German link; a word with one German link is easy and inflates every score, so every metric is reported for both groups.

## Metrics (page "Wartung > Morpheme", 0026.7f)

| Metric | Definition | Tells |
|---|---|---|
| Coverage | share of morphemes of the population with a current row (assigned or `unrendered`), per source | how much each producer decides at all |
| Agreement | for morphemes decided by two sources, share with the same German word set | where producers are interchangeable |
| Accuracy | against the reference set: share of morphemes whose German word set equals the reference | which producer is right |
| Main-mapping consistency | layer rows whose German word is linked to the source word in the main mapping but with a different non-NULL `StrongsNumber` | conflicts a promotion would overwrite |
| Unrendered rate per Strong's | `unrendered` / decided, for H9001, H9002, H9009 … | plausibility: ו is often not rendered, ה as *der/die/das* usually is |
| Cost | tokens and cost per verse and per decided morpheme, from `AgentRuns` | whether the AI pass is affordable for the whole OT (about 23,000 verses) |

All metrics per book and per morpheme Strong's, with drill-down to sample verses (reader links), like the 0018 link-roles report.

## Reference set

50 verses hand-checked per morpheme, chosen to cover: waw-consecutive narrative (Gen 1, Ruth 1), article + preposition (בַּ, לַ = prefix + article fused, Strong's H9003 + H9009 on one piece or two), pronoun suffixes (Ruth 1:8, Ps 23), construct chains, Aramaic (Dan 2:4–10). Stored as `assets/agent-reference/morpheme-reference-v1.json` (no personal data), frozen like 0018.10: the AI prompt's own example verses are excluded.

## Decision gate (0026.9 decides, maintainer signs off)

| Outcome | Signal |
|---|---|
| Promote into main mapping | accuracy of the best producer ≥ 97 % on multi-link words, main-mapping conflicts reviewed, reader segment view judged useful |
| Keep separate | accuracy good, but the position distinction (repeated morphemes) or `unrendered` matters to readers and cannot live in `StrongsNumber` |
| Drop | rules-in-shadow already reach the bar: run the existing 0018 backfill instead, the layer adds nothing |

The thresholds are proposals; the maintainer sets them before the reference set is built, so the numbers cannot move the bar.
