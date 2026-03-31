CREATE TABLE "unshackled-word"."BibleVerseCountingMapping"
(
    "Id"       serial4 NOT NULL,
    "HebRefId" INT     NOT NULL,
    "LxxRefId" INT     NOT NULL,
    CONSTRAINT "BibleVerseCountingMapping_PK" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "BibleVerseCountingMapping_Heb_Lxx_UQ" ON "unshackled-word"."BibleVerseCountingMapping" ("HebRefId", "LxxRefId");
CREATE INDEX "BibleVerseCountingMapping_Lxx_Heb_IDX" ON "unshackled-word"."BibleVerseCountingMapping" ("LxxRefId", "HebRefId");
