import type { SeedJob } from "@/lib/maintainer/seedJobs";

export type SetupGroup = "base" | "step" | "other" | "derived";
export type SetupSource = "migration" | "assets" | "download";
/** Derived by the server from row counts (03-seed-catalog.md, status rule). */
export type SetupStepStatus = "green" | "amber" | "red" | "grey";

export interface SetupTable {
  name: string;
  rows: number;
  /** Materialized views (stats-views): populated yes/no before a count makes sense. */
  populated?: boolean;
}

export const SETUP_STEP_KEYS = [
  "books",
  "stts",
  "revision-levels",
  "elb1871-text",
  "elb1871-verses",
  "step-text",
  "step-strongs",
  "step-morphology",
  "step-lexicon",
  "step-relationship",
  "step-strongs-text",
  "morphology",
  "tsk",
  "tsk-curation",
  "sr-gnt",
  "sbl",
  "ai-mappings",
  "lxx-text",
  "lxx-tags",
  "revision-seed",
  "verse-similarity",
  "stats-views",
] as const;
export type SetupStepKey = (typeof SETUP_STEP_KEYS)[number];
export const SETUP_GROUPS: readonly SetupGroup[] = ["base", "step", "other", "derived"];

export interface SetupStep {
  key: SetupStepKey;
  group: SetupGroup;
  source: SetupSource;
  /** Host for the "needs internet" hint, download steps only. */
  sourceHost?: string;
  status: SetupStepStatus;
  requires: SetupStepKey[];
  /** Requires that are not green; non-empty exactly when status is grey. */
  missing: SetupStepKey[];
  resumable: boolean;
  /** false until the runner has moved into UnshackledWord.Seeding (0031.5-0031.8). */
  wired: boolean;
  /** false for read-only rows (books from migration 0001). */
  runnable: boolean;
  /** A job for this step failed in this API process (makes it amber). */
  failed?: boolean;
  /** Valid green with 0 rows ("nichts zu importieren"): revision-seed, lxx-tags links. */
  note?: "nothingToImport";
  tables: SetupTable[];
}

/** SeedJob of the setup operation; `kind` is the step key(s), `group:<g>` or `all`. */
export type SetupJob = Omit<SeedJob, "operation" | "kind"> & {
  operation: "setup";
  kind: SetupStepKey | `group:${SetupGroup}` | "all";
  /** Step currently running inside a group/all job (filled from `SetupStatus.currentStep`). */
  currentStep?: SetupStepKey;
  /** Step that failed and stopped the chain (filled from the step's `failed` flag when the job failed). */
  failedStep?: SetupStepKey;
};

export interface SetupStatus {
  checkedAt: string;
  steps: SetupStep[];
  /** Same shape as api/maintainer/seed/status; operation "setup", kind = step key | `group:<g>` | "all". */
  job?: SetupJob;
  /** Step the running job works on right now (server: `currentStep` of `GET /admin/setup`). */
  currentStep?: SetupStepKey;
}

export type RunSetupRequest = { keys: SetupStepKey[] } | { group: SetupGroup } | { all: true };
