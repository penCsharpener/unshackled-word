-- Log of repo seed imports (roadmap 0010.4): one row per import run of a seed kind ('morphology' now, 'mappings' with roadmap 0010.9).
-- SeedVersion is the version hash of the seed files that were imported (see assets/seed/manifest.json); Exact/Shifted/Modified/Orphaned
-- count the seed lines per anchoring class (see Application/Features/Seed/Anchoring). Schema only: rows are written by the SeedDb runners.
--
-- Named "0009b" for the same reason as 0009a_Elb1871Morphology.sql (no new number is taken); nothing else depends on its position.

CREATE TABLE IF NOT EXISTS "unshackled-word"."SeedImports"
(
    "Id"          serial4      NOT NULL,
    "Kind"        varchar(30)  NOT NULL,
    "SeedVersion" varchar(64)  NOT NULL,
    "ExportedAt"  timestamptz  NULL,
    "ImportedAt"  timestamptz  NOT NULL DEFAULT now(),
    "Rows"        int4         NOT NULL,
    "Exact"       int4         NOT NULL,
    "Shifted"     int4         NOT NULL,
    "Modified"    int4         NOT NULL,
    "Orphaned"    int4         NOT NULL,
    CONSTRAINT "SeedImports_PK" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "SeedImports_Kind_idx" ON "unshackled-word"."SeedImports" ("Kind", "ImportedAt" DESC);

COMMENT ON TABLE "unshackled-word"."SeedImports" IS 'One row per imported repo seed (morphology, later mappings): version, counts per anchoring class, import time.';
COMMENT ON COLUMN "unshackled-word"."SeedImports"."ExportedAt" IS 'exportedAt of the seed manifest (when the maintainer exported it), not the import time.';
