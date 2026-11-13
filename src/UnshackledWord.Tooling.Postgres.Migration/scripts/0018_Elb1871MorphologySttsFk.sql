-- Elb1871Morphology.Stts must be a tag of SttsTags (roadmap 0010.7). Schema only.
-- The constraint is added NOT VALID: rows that exist when this script runs (SttsTags is still empty then, the seed runs
-- after all migrations) are not scanned, new rows and changed Stts values are checked from now on.
-- It is validated here only when SttsTags is filled and no row carries an unknown tag (the same rows data quality check L6
-- lists); otherwise a notice names the number of rows, seed SttsTags and run
--   ALTER TABLE "unshackled-word"."Elb1871Morphology" VALIDATE CONSTRAINT "FK_Elb1871Morphology_SttsTags";
-- Idempotent.

DO
$$
    DECLARE
        bad bigint;
        tags bigint;
    BEGIN
        IF to_regclass('"unshackled-word"."Elb1871Morphology"') IS NULL THEN
            RAISE NOTICE 'Elb1871Morphology does not exist; STTS foreign key skipped.';
            RETURN;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM pg_constraint
                       WHERE conname = 'FK_Elb1871Morphology_SttsTags'
                           AND conrelid = '"unshackled-word"."Elb1871Morphology"'::regclass) THEN
            ALTER TABLE "unshackled-word"."Elb1871Morphology"
                ADD CONSTRAINT "FK_Elb1871Morphology_SttsTags" FOREIGN KEY ("Stts") REFERENCES "unshackled-word"."SttsTags" ("Tag") NOT VALID;
        END IF;

        SELECT count(*) INTO tags FROM "unshackled-word"."SttsTags";
        SELECT count(*) INTO bad
        FROM "unshackled-word"."Elb1871Morphology" m
        WHERE m."Stts" IS NOT NULL
            AND NOT EXISTS (SELECT 1 FROM "unshackled-word"."SttsTags" s WHERE s."Tag" = m."Stts");

        IF tags = 0 OR bad > 0 THEN
            RAISE NOTICE 'STTS foreign key added NOT VALID (SttsTags rows: %, morphology rows with unknown STTS: %). Seed SttsTags, fix the rows listed by data quality check L6, then validate the constraint.', tags, bad;
        ELSE
            ALTER TABLE "unshackled-word"."Elb1871Morphology" VALIDATE CONSTRAINT "FK_Elb1871Morphology_SttsTags";
        END IF;
    END
$$;
