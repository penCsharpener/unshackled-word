# Roadmap 0027: Similar verses by shared Strong's

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What is missing, feasibility (compute and size), end state, deferred items |
| [02-decisions.md](02-decisions.md) | Maintainer decisions from the interview, plan decisions, code findings |
| [03-algorithm.md](03-algorithm.md) | Tokens, stop words, score, phrase bonus, top-K, evaluation |
| [04-model.md](04-model.md) | Tables `VerseSimilarityRuns`, `VerseSimilarities`, rebuild flow |
| [05-ui.md](05-ui.md) | Side panel mode "Ähnliche Verse", maintainer card |
| [06-tasks.md](06-tasks.md) | Task table (backend and frontend woven) |
| [07-risks.md](07-risks.md) | Risks worth naming before starting |

## Status (2026-11-25)

Planned after interview, implementation started. This roadmap goes before 0026: migration `0049`, version 0.12.0 (0026 moved to 0050/0051 and 0.13.0).

**0027.1 (token loader, scorer, dry run) done, with these deviations and findings:**

- **Score formula:** the formula in [03-algorithm.md](03-algorithm.md#score) divides a sum of idf by a sum of idf², so a verse is not 1 with itself and a rarer shared word gives a *lower* score. Implemented as a real cosine with weight w(s) = idf(s): `base = Σ idf(s)² (s ∈ A∩B) / (‖A‖·‖B‖)`, each shared bigram x→y adds `PhraseWeight · (idf(x)² + idf(y)²)` to the numerator. Everything else (df per testament, stop list, set semantics, bigrams over the stop-word-free sequence, top-K with MinScore, inverted index) is as documented. Shared bigrams are found by merging the two verses' sorted bigram sets for candidates with at least two shared tokens, not by a separate bigram index (same result).
- **Golden pairs (defaults):** Mk 14:58 → 2 Cor 5:1 rank 6 (done). Acts 7:48 → Acts 17:24 rank 1 (phrase χειροποίητος ναός κατοικέω). **Heb 4:12 → Rev 2:12 rank 11** (0.117, tied to three decimals with rank 10 Lk 21:24), Rev 1:16 rank 34, Rev 19:15 rank 55: Heb 4:12 shares only δίστομος G1366 with them (μάχαιρα G3162 ≠ ῥομφαία G4501, τομώτερος ≠ ὀξύς), against a long verse full of rare words. Base Strong's (`--base-strongs`) makes it worse (rank 15); StopDfShare 0.005 gives rank 9 (Rev 1:16 rank 13) but removes חֶרֶב, the only shared word of Ps 149:6 → Prov 5:4. Ps 149:6 → Prov 5:4 is rank 385 at defaults: Prov 5:4 has פֶּה H6310H ("mouths"), Ps 149:6 פִּיפִיָּה H6374, so only חֶרֶב is shared. Defaults left as documented; to be decided in 0027.6.
- **Stop list at 2 %** includes H3068G (YHWH), H0430G (God), H3478 (Israel), G2316 (θεός); logged per run.
- **Verb:** `verse-similarity --dry-run [--show <ref>]... [--show-count n] [--base-strongs] [--stop-df-share x] [--top-k n] [--min-score x] [--phrase-weight x]`. References in STEP form (`Heb.4.12`, English counting = `LxxRefId`), dotted with German/English names (`Mk.14.58`, `Ps.149.6`) or German citation (`Mk 14,58`, Elberfelder counting via `Elb1871Verses`). Without `--dry-run` it refuses with exit code 2.
