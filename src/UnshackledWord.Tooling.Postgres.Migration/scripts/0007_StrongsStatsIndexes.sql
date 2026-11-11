-- Strong's stats and German word stats: German word key (lemma, falling back to the plain form) per book.
-- The expression must stay identical to the one used in StrongsStatsRepository, or the index is not used.
CREATE INDEX IF NOT EXISTS "IX_Elb1871Words_GermanKey_Book"
    ON "unshackled-word"."Elb1871Words" ((LOWER(COALESCE(NULLIF("Lemma", ''), "PlainWord"))), "BibleBookId");
