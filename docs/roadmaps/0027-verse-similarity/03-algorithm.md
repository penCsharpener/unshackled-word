# 0027 · Algorithm

## Tokens

A verse is the sequence of its Strong's numbers in word order (`PositionInVerse`, for Hebrew then `PositionInWord`):

- **Greek:** `DisambiguatedStrongs` of every word that is in at least one main edition.
- **Hebrew/Aramaic:** root pieces (`IsRoot`) from `StepHebrewWordsNormalized`; prefix/suffix pieces are dropped.
- Disambiguated numbers are used as they are (`H0430G` ≠ `H0430H`). That keeps homographs apart and proper names separate from common nouns. Risk in [07-risks.md](07-risks.md).

Each testament is its own corpus (own document frequency, own pairs).

## Stop words

`df(s)` = number of verses in the testament that contain Strong's `s`. A token is a stop word when `df(s) > StopDfShare × verses` (default 0.02: about 460 OT, 160 NT verses) or it is a Hebrew prefix number (H9001–H9xxx). Removed tokens are not used for scoring and do not break runs (see below). The cut list per run is logged with its size.

## Score

Each verse is a **set** of content tokens (repeats ignored), weighted `idf(s) = ln(N / df(s))`.

```
base(a,b)   = Σ idf(s) for s in A∩B  /  sqrt(Σ idf² of A · Σ idf² of B)        cosine on idf-weighted sets
phrase(a,b) = PhraseWeight × Σ (idf(x)+idf(y)) for each content bigram x→y that occurs in both,
              in the same order, in the content-token sequences (stop words removed)
score       = base + phrase / sqrt(Σ idf² of A · Σ idf² of B)
```

- Cosine keeps long verses (genealogies, lists) from matching everything.
- Bigrams over the stop-word-free sequence mean an article or particle between two content words does not break "ῥομφαία δίστομος ὀξεῖα". A trigram counts as two bigrams, so longer runs grow naturally.
- Default `PhraseWeight` 0.5; tuned in 0027.6.

## Candidate generation and top-K

For verse `a`, walk the posting lists of its content tokens, accumulate shared idf per candidate `b ≠ a`, then add the bigram bonus only for candidates with at least two shared tokens (bigram index: bigram → verses). Keep the `TopK` (default 30) with `score ≥ MinScore` (default 0.1). Pairs are stored per direction (rank of b for a can differ from rank of a for b).

## Evaluation

After every run, against TSK links within the same testament:

- **recall@30:** share of TSK links (a→b) where b is in a's top 30.
- **golden pairs:** Heb 4:12 ↔ Rev 1:16 / 2:12; Mk 14:58 ↔ 2 Cor 5:1; Acts 7:48 ↔ Acts 17:24; Ps 149:6 ↔ Prov 5:4. Each gets a rank (or "missing").

Both are stored on the run row. They are signals, not goals: a high-scoring pair that TSK lacks may be exactly the new connection this roadmap is for.
