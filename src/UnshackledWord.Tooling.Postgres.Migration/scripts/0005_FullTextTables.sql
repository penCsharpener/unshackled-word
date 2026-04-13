CREATE TABLE "unshackled-word"."StepBibleVerses"
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
    CONSTRAINT "StepBibleVerses_PK" PRIMARY KEY ("Id")
);

CREATE INDEX "StepBibleVerses_HebRefId_idx" ON "unshackled-word"."StepBibleVerses" USING btree ("HebRefId");
CREATE INDEX "StepBibleVerses_LxxRefId_idx" ON "unshackled-word"."StepBibleVerses" USING btree ("LxxRefId");
CREATE INDEX "StepBibleVerses_SearchVector_idx" ON "unshackled-word"."StepBibleVerses" USING gin ("SearchVector");

