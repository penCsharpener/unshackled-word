-- Vocab trainer, learner side (roadmap 0022.7, docs/roadmaps/0022-vocab-trainer/04-study-engine.md).
--   VocabCards         one FSRS card per user and vocab entry (shared by every course over that entry)
--   VocabSessions      a study session of up to 20 words; "Plan" holds the engine's state (entry ids, remaining reps, open wave)
--   VocabReviewLog     append-only, one row per answer; input for a later FSRS optimizer
--   VocabUserSettings  per-user switches (quiz types turned off)
-- Learner progress is local data and never part of the repo seed.
-- "VocabEntryId" has no FK in "VocabCards" and "VocabReviewLog" on purpose: the lexicon may change under active cards. A card whose
-- entry was retired or deleted stays and is flagged ("entfernt") until the learner removes it; a merge moves the cards to the target
-- in the same transaction. The review log keeps rows of merged, deleted and reset cards.
--
-- Idempotent: safe to run again.

-- ---- VocabCards ---------------------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabCards"
(
    "UserId"             int4        NOT NULL,
    "VocabEntryId"       int4        NOT NULL,
    "State"              varchar(10) NOT NULL,
    "Stability"          float8      NOT NULL,
    "Difficulty"         float8      NOT NULL,
    "Due"                timestamptz NOT NULL,
    "Reps"               int4        NOT NULL DEFAULT 0,
    "Lapses"             int4        NOT NULL DEFAULT 0,
    "LastReview"         timestamptz NULL,
    "CreatedAt"          timestamptz NOT NULL DEFAULT now(),
    "SeenContentVersion" int4        NOT NULL,
    CONSTRAINT "VocabCards_PK" PRIMARY KEY ("UserId", "VocabEntryId"),
    CONSTRAINT "VocabCards_User_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "VocabCards_State_CK" CHECK ("State" IN ('new', 'learning', 'review', 'relearning')),
    CONSTRAINT "VocabCards_Fsrs_CK" CHECK ("Stability" >= 0 AND "Difficulty" >= 0),
    CONSTRAINT "VocabCards_Counts_CK" CHECK ("Reps" >= 0 AND "Lapses" >= 0 AND "Lapses" <= "Reps"),
    CONSTRAINT "VocabCards_SeenContentVersion_CK" CHECK ("SeenContentVersion" >= 1)
);

CREATE INDEX IF NOT EXISTS "VocabCards_Due_idx" ON "unshackled-word"."VocabCards" ("UserId", "Due");
-- maintainer changes (merge moves, "who has this entry") and the flagged-card check by entry
CREATE INDEX IF NOT EXISTS "VocabCards_Entry_idx" ON "unshackled-word"."VocabCards" ("VocabEntryId");

COMMENT ON TABLE "unshackled-word"."VocabCards" IS 'One FSRS card per user and vocab entry. Local data, not part of the repo seed. VocabEntryId has no FK: a card outlives a retired or deleted entry.';
COMMENT ON COLUMN "unshackled-word"."VocabCards"."State" IS 'FSRS state: new, learning, review, relearning.';
COMMENT ON COLUMN "unshackled-word"."VocabCards"."CreatedAt" IS 'Word age for the quiz ladder (day 0 MC, day 1 reverse MC/listening/verse context, day 2 typing).';
COMMENT ON COLUMN "unshackled-word"."VocabCards"."SeenContentVersion" IS 'Entry ContentVersion the learner last accepted; lower than VocabEntries.ContentVersion = flagged as "geändert".';

-- ---- VocabSessions ------------------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabSessions"
(
    "Id"         serial4     NOT NULL,
    "UserId"     int4        NOT NULL,
    "CourseId"   int4        NOT NULL,
    "StartedAt"  timestamptz NOT NULL DEFAULT now(),
    "FinishedAt" timestamptz NULL,
    "Plan"       jsonb       NOT NULL,
    CONSTRAINT "VocabSessions_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "VocabSessions_User_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "VocabSessions_Course_FK" FOREIGN KEY ("CourseId") REFERENCES "unshackled-word"."VocabCourses" ("Id"),
    CONSTRAINT "VocabSessions_Plan_CK" CHECK (jsonb_typeof("Plan") = 'object'),
    CONSTRAINT "VocabSessions_Finished_CK" CHECK ("FinishedAt" IS NULL OR "FinishedAt" >= "StartedAt")
);

-- "start or resume the open session": at most one open session per user and course
CREATE UNIQUE INDEX IF NOT EXISTS "VocabSessions_Open_UQ" ON "unshackled-word"."VocabSessions" ("UserId", "CourseId") WHERE "FinishedAt" IS NULL;
CREATE INDEX IF NOT EXISTS "VocabSessions_User_idx" ON "unshackled-word"."VocabSessions" ("UserId", "StartedAt" DESC);

COMMENT ON TABLE "unshackled-word"."VocabSessions" IS 'Study sessions. Local data, not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."VocabSessions"."Plan" IS 'Engine state: entry ids, remaining reps per entry, open wave.';
COMMENT ON COLUMN "unshackled-word"."VocabSessions"."FinishedAt" IS 'NULL = open; resumed by the next POST api/vocab/sessions for the same course.';

-- ---- VocabReviewLog -----------------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabReviewLog"
(
    "Id"               int8        GENERATED ALWAYS AS IDENTITY,
    "UserId"           int4        NOT NULL,
    "VocabEntryId"     int4        NOT NULL,
    "SessionId"        int4        NULL,
    "QuizType"         varchar(30) NOT NULL,
    "IsCorrect"        boolean     NOT NULL,
    "Given"            varchar(120) NULL,
    "ResponseMs"       int4        NULL,
    "Rating"           int2        NULL,
    "StateBefore"      varchar(10) NULL,
    "StabilityBefore"  float8      NULL,
    "DifficultyBefore" float8      NULL,
    "StabilityAfter"   float8      NULL,
    "DifficultyAfter"  float8      NULL,
    "ReviewedAt"       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "VocabReviewLog_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "VocabReviewLog_User_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "VocabReviewLog_Session_FK" FOREIGN KEY ("SessionId") REFERENCES "unshackled-word"."VocabSessions" ("Id") ON DELETE SET NULL,
    CONSTRAINT "VocabReviewLog_QuizType_CK" CHECK ("QuizType" IN ('introduction', 'multiple_choice', 'reverse_multiple_choice', 'listening', 'verse_context', 'typing')),
    -- FSRS ratings: 1 Again, 2 Hard, 3 Good, 4 Easy (the engine writes 1 and 3); NULL for an answer that did not rate the card
    CONSTRAINT "VocabReviewLog_Rating_CK" CHECK ("Rating" IS NULL OR "Rating" BETWEEN 1 AND 4),
    CONSTRAINT "VocabReviewLog_StateBefore_CK" CHECK ("StateBefore" IS NULL OR "StateBefore" IN ('new', 'learning', 'review', 'relearning')),
    CONSTRAINT "VocabReviewLog_ResponseMs_CK" CHECK ("ResponseMs" IS NULL OR "ResponseMs" >= 0)
);

CREATE INDEX IF NOT EXISTS "VocabReviewLog_User_idx" ON "unshackled-word"."VocabReviewLog" ("UserId", "ReviewedAt");
CREATE INDEX IF NOT EXISTS "VocabReviewLog_Session_idx" ON "unshackled-word"."VocabReviewLog" ("SessionId") WHERE "SessionId" IS NOT NULL;

COMMENT ON TABLE "unshackled-word"."VocabReviewLog" IS 'Append-only, one row per answer; input for a later FSRS optimizer. VocabEntryId has no FK: the log outlives merged, deleted and reset cards.';
COMMENT ON COLUMN "unshackled-word"."VocabReviewLog"."Given" IS 'What the learner answered (option text or typed letters).';
COMMENT ON COLUMN "unshackled-word"."VocabReviewLog"."Rating" IS 'FSRS rating applied: 1 Again (wrong), 3 Good (correct); NULL when the answer did not change the card.';

-- ---- VocabUserSettings --------------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabUserSettings"
(
    "UserId"            int4   NOT NULL,
    "DisabledQuizTypes" text[] NOT NULL DEFAULT '{}',
    CONSTRAINT "VocabUserSettings_PK" PRIMARY KEY ("UserId"),
    CONSTRAINT "VocabUserSettings_User_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    -- typing always works (fallback of the quiz ladder), so it cannot be turned off
    CONSTRAINT "VocabUserSettings_DisabledQuizTypes_CK"
        CHECK ("DisabledQuizTypes" <@ ARRAY ['multiple_choice', 'reverse_multiple_choice', 'listening', 'verse_context']::text[])
);

COMMENT ON TABLE "unshackled-word"."VocabUserSettings" IS 'Per-user vocab settings. No row = defaults.';
COMMENT ON COLUMN "unshackled-word"."VocabUserSettings"."DisabledQuizTypes" IS 'Quiz types the learner turned off; typing (always possible) and introduction cannot be turned off.';
