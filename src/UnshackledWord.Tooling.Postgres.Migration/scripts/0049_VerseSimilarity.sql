-- Similar verses by shared Strong's (roadmap 0027.2, docs/roadmaps/0027-verse-similarity/04-model.md).
--   VerseSimilarityRuns  one row per rebuild with its parameters, counts, duration and (0027.6) evaluation. "Status": running | succeeded | failed.
--                        "StartedBy" is the user id of the maintainer, NULL = SeedDb (CLI verb or pipeline).
--   VerseSimilarities    the top-K most similar verses per source verse of the current run only, keyed by STEP "LxxRefId", within one
--                        testament. A rebuild replaces all rows in one transaction (DELETE + insert); older runs keep their row but no pairs.
-- Derived data like "StepGreekWordEditions": rebuilt from the STEP words by VerseSimilarityRebuildService, not part of the repo seed, no audit,
-- no user data.
--
-- Idempotent: safe to run again.

CREATE TABLE IF NOT EXISTS "unshackled-word"."VerseSimilarityRuns"
(
    "Id"            int4         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "StartedAt"     timestamptz  NOT NULL,
    "FinishedAt"    timestamptz  NULL,
    "Status"        varchar(20)  NOT NULL,
    "StartedBy"     int4         NULL,
    "StopDfShare"   numeric(6,4) NOT NULL,
    "TopK"          int2         NOT NULL,
    "MinScore"      numeric(6,4) NOT NULL,
    "PhraseWeight"  numeric(6,4) NOT NULL,
    "PairCount"     int4         NULL,
    "StopWordCount" int4         NULL,
    "DurationMs"    int4         NULL,
    "Evaluation"    jsonb        NULL,
    "Error"         text         NULL
);

CREATE TABLE IF NOT EXISTS "unshackled-word"."VerseSimilarities"
(
    "SourceLxxRefId" int4         NOT NULL,
    "TargetLxxRefId" int4         NOT NULL,
    "Rank"           int2         NOT NULL,
    "Score"          numeric(7,4) NOT NULL,
    "SharedCount"    int2         NOT NULL,
    "PhraseCount"    int2         NOT NULL,
    "RunId"          int4         NOT NULL REFERENCES "unshackled-word"."VerseSimilarityRuns" ("Id"),
    CONSTRAINT "VerseSimilarities_PK" PRIMARY KEY ("SourceLxxRefId", "Rank")
);

COMMENT ON TABLE "unshackled-word"."VerseSimilarityRuns" IS 'One row per verse similarity rebuild (roadmap 0027): parameters, counts, duration, evaluation. Only the latest succeeded run has pairs.';
COMMENT ON COLUMN "unshackled-word"."VerseSimilarityRuns"."Status" IS 'running | succeeded | failed. A failed run leaves the pairs of the previous run.';
COMMENT ON COLUMN "unshackled-word"."VerseSimilarityRuns"."StartedBy" IS 'User id of the maintainer who started the run; NULL = SeedDb.';
COMMENT ON COLUMN "unshackled-word"."VerseSimilarityRuns"."Evaluation" IS 'recall@30 per testament and golden pair ranks (roadmap 0027.6).';
COMMENT ON TABLE "unshackled-word"."VerseSimilarities" IS 'Top-K similar verses per source verse by shared Strong''s (roadmap 0027), keyed by STEP LxxRefId. Derived, rebuilt as a whole, not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."VerseSimilarities"."Rank" IS '1..TopK within the source verse.';
COMMENT ON COLUMN "unshackled-word"."VerseSimilarities"."PhraseCount" IS 'Matched content bigrams (stop words removed).';
