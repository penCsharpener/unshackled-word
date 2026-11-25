# 0027 · Algorithm

## Tokens

A verse is the sequence of its Strong's numbers in word order (`PositionInVerse`, for Hebrew then `PositionInWord`):

- **Greek:** `DisambiguatedStrongs` of every word that is in at least one main edition.
- **Hebrew/Aramaic:** root pieces (`IsRoot`) from `StepHebrewWordsNormalized`; prefix/suffix pieces are dropped.
- Disambiguated numbers are used as they are (`H0430G` ≠ `H0430H`). That keeps homographs apart and proper names separate from common nouns. Risk in [07-risks.md](07-risks.md).

Each testament is its own corpus (own document frequency, own pairs).

## Stop words

`df(s)` = number of verses in the testament that contain Strong's `s`. A token is a stop word when `df(s) > StopDfShare × verses` (default 0.03 since 0027.6, was 0.02: about 700 OT, 240 NT verses; 52 OT + 65 NT words by df plus 13 Hebrew prefixes) or it is a Hebrew prefix number (H9001–H9xxx). Removed tokens are not used for scoring and do not break runs (see below). The cut list per run is logged with its size.

## Score

Each verse is a **set** of content tokens (repeats ignored), weighted `idf(s) = ln(N / df(s))`.

```
norm(A)     = sqrt(Σ idf² of A)
base(a,b)   = Σ idf(s)² for s in A∩B  /  (norm(A) · norm(B))         cosine on idf-weighted sets (a verse scores 1 with itself)
phrase(a,b) = PhraseWeight × Σ (idf(x)² + idf(y)²) for each content bigram x→y that occurs in both,
              in the same order, in the content-token sequences (stop words removed)
score       = base + phrase / (norm(A) · norm(B))
```

(Corrected in 0027.1: the first draft divided a sum of idf by idf², which is not a cosine.)

- Cosine keeps long verses (genealogies, lists) from matching everything.
- Bigrams over the stop-word-free sequence mean an article or particle between two content words does not break "ῥομφαία δίστομος ὀξεῖα". A trigram counts as two bigrams, so longer runs grow naturally.
- Default `PhraseWeight` 0.5; confirmed in 0027.6 (0.25 and 1.0 change recall by ≤ 0.3 points).

## Candidate generation and top-K

For verse `a`, walk the posting lists of its content tokens, accumulate shared idf per candidate `b ≠ a`, then add the bigram bonus only for candidates with at least two shared tokens (bigram index: bigram → verses). Keep the `TopK` (default 30) with `score ≥ MinScore` (default 0.1, confirmed in 0027.6). Pairs are stored per direction (rank of b for a can differ from rank of a for b).

## Evaluation

After every run (`VerseSimilarityEvaluator`, stored as JSON in `VerseSimilarityRuns.Evaluation`; the dry run prints the same), against TSK links within the same testament:

- **TSK links:** the raw table `Tsk` (the unchanged `assets/tskxref.txt` import), distinct by source and target range. The curation layer (`TskCurations`, 0024) is ignored: added links do not count, hidden ones still do. Source and target are compared in STEP numbering (`Tsk.LxxRefId` / `RelatedStartLxxRefId`…`RelatedEndLxxRefId` against the stored `LxxRefId` pairs; both are English counting), so no versification mapping is involved and the two read-side mappings (TSK via `BibleVerseCountingMapping`, similarity via `Elb1871Verses`) cannot disagree. Links across testaments and links whose target contains the source are skipped. A range target counts when any of its verses is a hit (best rank).
- **recall@30 and recall@10:** share of those TSK links (a→b) where b is among a's first 30 (10) stored hits, per testament, with the number of links considered. Dev DB: 198,510 OT and 91,680 NT links.
- **golden pairs:** Heb 4:12 ↔ Rev 1:16 / 2:12; Mk 14:58 ↔ 2 Cor 5:1; Acts 7:48 ↔ Acts 17:24; Ps 149:6 ↔ Isa 41:15 (פִּיפִיָּה). Each gets a rank in both directions (or "missing"). Ps 149:6 ↔ Prov 5:4 was dropped in 0027.1: Prov 5:4 has פֶּה H6310, not פִּיפִיָּה H6374, so the two share only חֶרֶב and cannot rank high on Strong's.
- A failure of the evaluation never fails the run: the JSON then holds only `error`.

Both are stored on the run row. They are signals, not goals: a high-scoring pair that TSK lacks may be exactly the new connection this roadmap is for. The tuning that set the defaults is in [02-decisions.md](02-decisions.md).
