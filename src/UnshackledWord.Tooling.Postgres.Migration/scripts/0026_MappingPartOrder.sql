-- Compound parts in the live mapping (roadmap 0016.10): a German word split into parts ("Gersten" + "ernte") needs the order of its parts.
-- "PositionInVerse" of a mapping row is the position of the whole German word, so it cannot carry it; the AI worker returned a PartOrder
-- but dropped it on insert. A new nullable column keeps the existing ones untouched.
--
-- "PartOrder" counts 1..n over the parts of one German word ("ElbWordId"). Several rows of one word share an order when a part is linked to
-- more than one original word. NULL = the row is a whole-word link (or a part of an older import whose order is derived on read).
-- The part texts themselves stay in "GermanWordPart"; the mapping seed does not carry the order (it is derived from the position of the part
-- inside the word on import), so the seed format stays as it is.

ALTER TABLE "unshackled-word"."Elb1871GreekMapping"
    ADD COLUMN IF NOT EXISTS "PartOrder" int4 NULL;
ALTER TABLE "unshackled-word"."Elb1871HebrewMapping"
    ADD COLUMN IF NOT EXISTS "PartOrder" int4 NULL;

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Elb1871GreekMapping_PartOrder_CK') THEN
            ALTER TABLE "unshackled-word"."Elb1871GreekMapping"
                ADD CONSTRAINT "Elb1871GreekMapping_PartOrder_CK" CHECK ("PartOrder" IS NULL OR ("PartOrder" >= 1 AND "GermanWordPart" IS NOT NULL));
        END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Elb1871HebrewMapping_PartOrder_CK') THEN
            ALTER TABLE "unshackled-word"."Elb1871HebrewMapping"
                ADD CONSTRAINT "Elb1871HebrewMapping_PartOrder_CK" CHECK ("PartOrder" IS NULL OR ("PartOrder" >= 1 AND "GermanWordPart" IS NOT NULL));
        END IF;
    END
$$;

COMMENT ON COLUMN "unshackled-word"."Elb1871GreekMapping"."PartOrder" IS 'Order (1..n) of the compound part "GermanWordPart" inside the German word; NULL for a whole-word link.';
COMMENT ON COLUMN "unshackled-word"."Elb1871HebrewMapping"."PartOrder" IS 'Order (1..n) of the compound part "GermanWordPart" inside the German word; NULL for a whole-word link.';
