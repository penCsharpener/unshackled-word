-- Indexes for the word detail pop-up: occurrence counts and Elberfelder renderings per Strong's number.
-- Hebrew: the root Strong's number without the instance suffix ("H0853_A" -> "H0853") is the lookup key.
CREATE INDEX IF NOT EXISTS "IX_StepGreekWords_DisambiguatedStrongs"
    ON "unshackled-word"."StepGreekWords" ("DisambiguatedStrongs");

CREATE INDEX IF NOT EXISTS "IX_StepHebrewWords_RootStrongs"
    ON "unshackled-word"."StepHebrewWords" ((split_part("RootDisambiguatedStrongsInstance", '_', 1)));

CREATE INDEX IF NOT EXISTS "IdxElbGreekStepWordId"
    ON "unshackled-word"."Elb1871GreekMapping" ("StepWordId");

CREATE INDEX IF NOT EXISTS "IdxElbHebrewStepWordId"
    ON "unshackled-word"."Elb1871HebrewMapping" ("StepWordId");
