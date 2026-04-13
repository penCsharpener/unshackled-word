CREATE TABLE "unshackled-word"."StepGreekVerses"
(
    "Id"           serial4                  NOT NULL,
    "HebRefId"     INT                      NOT NULL,
    "LxxRefId"     INT                      NOT NULL,
    "VerseText"    text COLLATE "und-x-icu" NOT NULL,
    "LemmaText"    text COLLATE "und-x-icu" NOT NULL,
    "SearchVector" tsvector GENERATED ALWAYS AS (
        setweight(to_tsvector('simple', coalesce("VerseText", '')), 'A') ||
        setweight(to_tsvector('simple', coalesce("LemmaText", '')), 'B')
        ) STORED,
    CONSTRAINT "StepGreekVerses_PK" PRIMARY KEY ("Id")
);

CREATE INDEX "StepGreekVerses_HebRefId_idx" ON "unshackled-word"."StepGreekVerses" USING btree ("HebRefId");
CREATE INDEX "StepGreekVerses_LxxRefId_idx" ON "unshackled-word"."StepGreekVerses" USING btree ("LxxRefId");
CREATE INDEX "StepGreekVerses_SearchVector_idx" ON "unshackled-word"."StepGreekVerses" USING gin ("SearchVector");

CREATE TABLE "unshackled-word"."StepHebrewVerses"
(
    "Id"           serial4                  NOT NULL,
    "HebRefId"     INT                      NOT NULL,
    "LxxRefId"     INT                      NOT NULL,
    "VerseText"    text COLLATE "und-x-icu" NOT NULL,
    "LemmaText"    text COLLATE "und-x-icu" NOT NULL,
    "SearchVector" tsvector GENERATED ALWAYS AS (
        setweight(to_tsvector('simple', coalesce("VerseText", '')), 'A') ||
        setweight(to_tsvector('simple', coalesce("LemmaText", '')), 'B')
        ) STORED,
    CONSTRAINT "StepHebrewVerses_PK" PRIMARY KEY ("Id")
);

CREATE INDEX "StepHebrewVerses_HebRefId_idx" ON "unshackled-word"."StepHebrewVerses" USING btree ("HebRefId");
CREATE INDEX "StepHebrewVerses_LxxRefId_idx" ON "unshackled-word"."StepHebrewVerses" USING btree ("LxxRefId");
CREATE INDEX "StepHebrewVerses_SearchVector_idx" ON "unshackled-word"."StepHebrewVerses" USING gin ("SearchVector");

