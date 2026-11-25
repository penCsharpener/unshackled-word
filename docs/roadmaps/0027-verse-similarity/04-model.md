# 0027 · Model

Migration `0049_VerseSimilarity.sql`.

```sql
CREATE TABLE IF NOT EXISTS "unshackled-word"."VerseSimilarityRuns"
(
    "Id"            int4 GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "StartedAt"     timestamptz  NOT NULL,
    "FinishedAt"    timestamptz  NULL,
    "Status"        varchar(20)  NOT NULL,          -- running | succeeded | failed
    "StartedBy"     int4         NULL,              -- user id; NULL = SeedDb
    "StopDfShare"   numeric(6,4) NOT NULL,
    "TopK"          int2         NOT NULL,
    "MinScore"      numeric(6,4) NOT NULL,
    "PhraseWeight"  numeric(6,4) NOT NULL,
    "PairCount"     int4         NULL,
    "StopWordCount" int4         NULL,
    "DurationMs"    int4         NULL,
    "Evaluation"    jsonb        NULL,              -- recall@30 per testament, golden pair ranks
    "Error"         text         NULL
);

CREATE TABLE IF NOT EXISTS "unshackled-word"."VerseSimilarities"
(
    "SourceLxxRefId" int4         NOT NULL,
    "TargetLxxRefId" int4         NOT NULL,
    "Rank"           int2         NOT NULL,         -- 1..TopK within the source
    "Score"          numeric(7,4) NOT NULL,
    "SharedCount"    int2         NOT NULL,
    "PhraseCount"    int2         NOT NULL,         -- matched bigrams
    "RunId"          int4         NOT NULL REFERENCES "unshackled-word"."VerseSimilarityRuns" ("Id"),
    CONSTRAINT "VerseSimilarities_PK" PRIMARY KEY ("SourceLxxRefId", "Rank")
);
```

- The primary key serves the only read pattern (all hits of the verses of one chapter, ordered).
- Only the current run's rows exist; older runs keep their row in `VerseSimilarityRuns` (history of parameters and evaluation) but no pairs.
- No audit, no user data, not in the seed.

## Rebuild flow

1. Insert a run row (`running`).
2. Load tokens (two queries, one per testament), compute in memory.
3. One transaction: `DELETE FROM VerseSimilarities`, bulk insert, update the run row (`succeeded`, counts, duration).
4. Compute the evaluation, update `Evaluation`.
5. On exception: run row `failed` with `Error`; the old pairs stay.

Only one run at a time (advisory lock); a second start returns 409.

**Triggers:** SeedDb after the STEP import when `VerseSimilarities` is empty or STEP words were inserted in this run (same rule as the Greek editions refresh), and the maintainer job.
