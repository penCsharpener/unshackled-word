// Fake backend of the setup page (roadmap 0031.1): the response shape the API will return, in memory
// plus sessionStorage, with timers instead of runners. Since 0031.6f the page talks to the real API; this is only the stand-in server of
// the page tests. Deleted in 0031.10.
import { ApiError } from "@/lib/api";
import { runnableKeys, stepStatus } from "./catalog";
import type {
  RunSetupRequest,
  SetupGroup,
  SetupJob,
  SetupSource,
  SetupStatus,
  SetupStep,
  SetupStepKey,
} from "./types";
import { SETUP_STEP_KEYS } from "./types";

interface FakeStepSpec {
  key: SetupStepKey;
  group: SetupGroup;
  source: SetupSource;
  sourceHost?: string;
  requires: SetupStepKey[];
  resumable: boolean;
  runnable: boolean;
  /** Target counts after "run". `populated` marks materialized views. */
  tables: { name: string; rows: number; populated?: boolean }[];
  note?: "nothingToImport";
}

const STEP_HOST = "raw.githubusercontent.com";

function spec(
  key: SetupStepKey,
  group: SetupGroup,
  source: SetupSource,
  requires: SetupStepKey[],
  tables: FakeStepSpec["tables"],
  extra: Partial<FakeStepSpec> = {},
): FakeStepSpec {
  return {
    key,
    group,
    source,
    requires,
    tables,
    resumable: true,
    runnable: true,
    ...(source === "download" ? { sourceHost: STEP_HOST } : {}),
    ...extra,
  };
}

/** Dev counts from 03-seed-catalog.md (2026-11-27), in catalog order. 22 entries. */
export const FAKE_CATALOG: readonly FakeStepSpec[] = [
  spec(
    "books",
    "base",
    "migration",
    [],
    [
      { name: "BibleBooks", rows: 66 },
      { name: "Languages", rows: 2 },
    ],
    { runnable: false },
  ),
  spec("stts", "base", "assets", [], [{ name: "SttsTags", rows: 54 }]),
  spec("revision-levels", "base", "assets", [], [{ name: "Revisions", rows: 3 }]),
  spec(
    "elb1871-text",
    "base",
    "assets",
    ["books"],
    [
      { name: "Elb1871Words", rows: 722743 },
      { name: "BibleVerseCountingMapping", rows: 31174 },
    ],
    { resumable: false },
  ),
  spec(
    "elb1871-verses",
    "base",
    "assets",
    ["elb1871-text"],
    [{ name: "Elb1871Verses", rows: 31169 }],
  ),
  spec(
    "step-text",
    "step",
    "download",
    ["books"],
    [
      { name: "StepGreekWords", rows: 142095 },
      { name: "StepHebrewWords", rows: 305648 },
      { name: "StepGreekWordEditions", rows: 1100328 },
    ],
    { resumable: true },
  ),
  spec(
    "step-strongs",
    "step",
    "download",
    ["step-text"],
    [{ name: "StepUnifiedStrongs", rows: 21378 }],
  ),
  spec(
    "step-morphology",
    "step",
    "download",
    ["step-text"],
    [
      { name: "StepGreekMorphology", rows: 1644 },
      { name: "StepHebrewMorphology", rows: 921 },
    ],
  ),
  spec(
    "step-lexicon",
    "step",
    "download",
    ["step-strongs"],
    [{ name: "StepStrongsLexicon", rows: 21293 }],
  ),
  spec(
    "step-relationship",
    "step",
    "download",
    ["step-text"],
    [
      { name: "StepHebrewWordsNormalized", rows: 38521 },
      { name: "StepHebrewWordsNormalizedToHebrewWords", rows: 540463 },
    ],
  ),
  spec(
    "step-strongs-text",
    "step",
    "download",
    ["step-text", "elb1871-text"],
    [{ name: "StepStrongsToText", rows: 682558 }],
  ),
  spec(
    "morphology",
    "other",
    "assets",
    ["elb1871-text"],
    [{ name: "Elb1871Morphology", rows: 721542 }],
    { resumable: false },
  ),
  spec("tsk", "other", "assets", ["books"], [{ name: "Tsk", rows: 378805 }], { resumable: false }),
  spec(
    "tsk-curation",
    "other",
    "assets",
    ["tsk", "elb1871-text"],
    [{ name: "TskCurations", rows: 5 }],
    { resumable: false, note: "nothingToImport" },
  ),
  spec("sr-gnt", "other", "download", ["books"], [{ name: "SrGntWords", rows: 137361 }], {
    sourceHost: "github.com",
    resumable: false,
  }),
  spec(
    "sbl",
    "other",
    "download",
    ["books"],
    [
      { name: "SblText", rows: 7939 },
      { name: "SblApparatus", rows: 4441 },
    ],
    { sourceHost: "github.com" },
  ),
  spec(
    "ai-mappings",
    "other",
    "assets",
    ["elb1871-text", "step-text"],
    [
      { name: "Elb1871GreekMapping", rows: 169981 },
      { name: "Elb1871HebrewMapping", rows: 555589 },
    ],
    { resumable: false },
  ),
  spec(
    "lxx-text",
    "other",
    "download",
    ["step-text"],
    [
      { name: "LxxVerses", rows: 23620 },
      { name: "LxxWords", rows: 486313 },
    ],
    { sourceHost: "github.com", resumable: false },
  ),
  spec(
    "lxx-tags",
    "other",
    "assets",
    ["lxx-text"],
    [
      { name: "LxxWordTags", rows: 265807 },
      { name: "LxxHebrewLinks", rows: 0 },
    ],
    { note: "nothingToImport" },
  ),
  spec(
    "revision-seed",
    "other",
    "assets",
    ["revision-levels", "ai-mappings"],
    [
      { name: "RevisionVerses", rows: 0 },
      { name: "RevisionWords", rows: 0 },
      { name: "RevisionMapping", rows: 0 },
    ],
    { note: "nothingToImport" },
  ),
  spec(
    "verse-similarity",
    "derived",
    "assets",
    ["step-relationship"],
    [{ name: "VerseSimilarities", rows: 917459 }],
  ),
  spec(
    "stats-views",
    "derived",
    "assets",
    ["ai-mappings", "morphology"],
    [
      { name: "StatsStrongs", rows: 14210, populated: true },
      { name: "StatsStrongsRenderings", rows: 96830, populated: true },
      { name: "StatsGermanLemma", rows: 18420, populated: true },
      { name: "StatsGermanForms", rows: 52960, populated: true },
      { name: "StatsVocabRenderings", rows: 41380, populated: true },
      { name: "DqWordFacts", rows: 722743, populated: true },
    ],
  ),
];

export interface FakeOptions {
  /** `?fake=reset`: clear the stored state before the first read. */
  reset?: boolean;
  /** `?fake=fail:<key>`: that step fails once (exercises the failed state). */
  failKey?: SetupStepKey;
  /** Injectable for tests. */
  random?: () => number;
  now?: () => Date;
}

/** Reads `?fake=` from the URL (comma list: `reset`, `fail:<key>`). */
export function fakeOptionsFromSearch(search: string): FakeOptions {
  const value = new URLSearchParams(search).get("fake") ?? "";
  const options: FakeOptions = {};
  for (const part of value.split(",")) {
    if (part === "reset") options.reset = true;
    if (part.startsWith("fail:")) {
      const key = part.slice("fail:".length);
      const known = SETUP_STEP_KEYS.find((k) => k === key);
      if (known) options.failKey = known;
    }
  }
  return options;
}

export interface SetupFake {
  status(): Promise<SetupStatus>;
  /** Rejects with ApiError(409) while a job runs, ApiError(400) naming a key that is not runnable. */
  run(request: RunSetupRequest): Promise<SetupJob>;
  /** Clears state and timers (tests, `?fake=reset`). */
  reset(): void;
}

interface StoredState {
  /** Row counts per step key, table order of the spec. */
  counts: Partial<Record<SetupStepKey, number[]>>;
  /** Steps that ran to the end (lets a step with a note count as done at 0 rows). */
  completed: SetupStepKey[];
  failed: SetupStepKey[];
  failConsumed: boolean;
  job?: SetupJob;
}

const STORAGE_KEY = "uw.setup.fake";
const LOG_LIMIT = 500;
const TICK_MS = 300;

function initialState(): StoredState {
  const books = FAKE_CATALOG.find((s) => s.key === "books");
  return {
    counts: { books: books?.tables.map((table) => table.rows) ?? [] },
    completed: ["books"],
    failed: [],
    failConsumed: false,
  };
}

function readStorage(): StoredState | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as StoredState) : null;
  } catch {
    return null;
  }
}

function writeStorage(state: StoredState | null) {
  try {
    if (state) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    else sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // storage unavailable: the fake stays in memory only
  }
}

function pad(value: number) {
  return String(value).padStart(2, "0");
}

function clock(date: Date) {
  return `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

/** Log text per step, without the time stamp: the last line (done) is added by the walker. */
function fakeLogLines(step: FakeStepSpec, rows: number[]): string[] {
  const first =
    step.source === "download"
      ? `${step.key}: lade ${step.sourceHost ?? "Quelle"} …`
      : step.source === "assets"
        ? `${step.key}: lese assets/seed/${step.key} …`
        : `${step.key}: lese Migration …`;
  const lines = [first];
  const total = rows.reduce((sum, n) => sum + n, 0);
  lines.push(`${step.key}: ${total} Zeilen gelesen`);
  step.tables.forEach((table, index) => {
    lines.push(`${step.key}: ${table.name}: ${rows[index]} eingefügt`);
  });
  return lines;
}

export function createSetupFake(options: FakeOptions = {}): SetupFake {
  const random = options.random ?? Math.random;
  const now = options.now ?? (() => new Date());
  const timers = new Set<ReturnType<typeof setTimeout>>();

  if (options.reset) writeStorage(null);
  let state: StoredState = readStorage() ?? initialState();
  if (state.job?.status === "running") {
    // timers do not survive a reload, like a job does not survive an API restart
    state = {
      ...state,
      job: {
        ...state.job,
        status: "failed",
        error: "Seite neu geladen",
        finishedAt: now().toISOString(),
      },
    };
  }
  const save = () => writeStorage(state);
  save();

  const later = (ms: number, fn: () => void) => {
    const id = setTimeout(() => {
      timers.delete(id);
      fn();
    }, ms);
    timers.add(id);
  };

  function steps(): SetupStep[] {
    const result: SetupStep[] = [];
    const green = new Set<SetupStepKey>();
    for (const s of FAKE_CATALOG) {
      const stored = state.counts[s.key];
      const tables = s.tables.map((table, index) => {
        const rows = stored?.[index] ?? 0;
        return table.populated === undefined
          ? { name: table.name, rows }
          : { name: table.name, rows, populated: rows > 0 };
      });
      const missing = s.requires.filter((key) => !green.has(key));
      const failed = state.failed.includes(s.key);
      const ranWithNote = s.note !== undefined && state.completed.includes(s.key);
      const status = stepStatus(tables, missing, failed, ranWithNote ? s.note : undefined);
      if (status === "green") green.add(s.key);
      const allZero = tables.every((table) => table.rows === 0);
      result.push({
        key: s.key,
        group: s.group,
        source: s.source,
        ...(s.sourceHost ? { sourceHost: s.sourceHost } : {}),
        status,
        requires: s.requires,
        missing: status === "grey" ? missing : [],
        resumable: s.resumable,
        wired: true,
        runnable: s.runnable,
        ...(failed ? { failed: true } : {}),
        ...(ranWithNote && allZero ? { note: s.note } : {}),
        tables,
      });
    }
    return result;
  }

  function appendLog(job: SetupJob, text: string) {
    const log = [...job.log, `${clock(now())} ${text}`];
    state = { ...state, job: { ...job, log: log.slice(-LOG_LIMIT) } };
    save();
  }

  function patchJob(patch: Partial<SetupJob>) {
    if (!state.job) return;
    state = { ...state, job: { ...state.job, ...patch } };
    save();
  }

  function walk(planned: SetupStepKey[], index: number) {
    const key = planned[index];
    if (!key) {
      patchJob({ status: "succeeded", finishedAt: now().toISOString(), currentStep: undefined });
      return;
    }
    const step = FAKE_CATALOG.find((s) => s.key === key);
    if (!step) return;
    const duration = 1500 + random() * 2500;
    const target = step.tables.map((table) => table.rows);
    const lines = fakeLogLines(step, target);
    const failing = options.failKey === key && !state.failConsumed;
    patchJob({ currentStep: key });
    if (state.job) appendLog(state.job, lines[0] ?? key);

    let elapsed = 0;
    let shown = 1;
    const finish = () => {
      state = {
        ...state,
        counts: { ...state.counts, [key]: target },
        completed: state.completed.includes(key) ? state.completed : [...state.completed, key],
        failed: state.failed.filter((k) => k !== key),
      };
      save();
      if (state.job) appendLog(state.job, `${key}: fertig (${(duration / 1000).toFixed(1)} s)`);
      walk(planned, index + 1);
    };
    const tick = () => {
      elapsed += TICK_MS;
      if (failing && elapsed >= duration / 2) {
        fail(key, step, target);
        return;
      }
      const remaining = duration - elapsed;
      if (remaining > TICK_MS) {
        if (shown < lines.length && state.job) appendLog(state.job, lines[shown++] ?? key);
        later(TICK_MS, tick);
      } else {
        later(remaining, finish);
      }
    };
    later(TICK_MS, tick);
  }

  function fail(key: SetupStepKey, step: FakeStepSpec, target: number[]) {
    const half = target.map((n, i) => (i === 0 ? Math.floor(n / 2) : 0));
    state = {
      ...state,
      counts: { ...state.counts, [key]: half },
      failed: state.failed.includes(key) ? state.failed : [...state.failed, key],
      failConsumed: true,
    };
    save();
    if (state.job)
      appendLog(state.job, `${key}: Fehler beim Schreiben von ${step.tables[0]?.name ?? key}`);
    patchJob({
      status: "failed",
      error: `Fake-Fehler in ${key}`,
      failedStep: key,
      finishedAt: now().toISOString(),
      currentStep: undefined,
    });
  }

  return {
    async status() {
      const current = steps();
      return {
        checkedAt: now().toISOString(),
        steps: current,
        ...(state.job ? { job: state.job } : {}),
      };
    },
    async run(request) {
      if (state.job?.status === "running") {
        throw new ApiError(409, "Es läuft schon ein Auftrag.");
      }
      const current = steps();
      const planned = runnableKeys(current, request);
      if ("keys" in request) {
        const rejected = request.keys.find((key) => !planned.includes(key));
        if (rejected) throw new ApiError(400, `Schritt nicht ausführbar: ${rejected}`);
      }
      if (planned.length === 0) throw new ApiError(400, "Nichts auszuführen.");
      const kind: SetupJob["kind"] =
        "all" in request
          ? "all"
          : "group" in request
            ? `group:${request.group}`
            : (planned[0] ?? "all");
      const job: SetupJob = {
        id: crypto.randomUUID(),
        operation: "setup",
        kind,
        status: "running",
        startedAt: now().toISOString(),
        log: [],
      };
      state = { ...state, job };
      save();
      walk(planned, 0);
      return job;
    },
    reset() {
      for (const id of timers) clearTimeout(id);
      timers.clear();
      state = initialState();
      writeStorage(null);
    },
  };
}

let instance: SetupFake | undefined;

/** Module singleton used by useSetup.ts; created on first use with `fakeOptionsFromSearch(location.search)`. */
export function setupFake(): SetupFake {
  instance ??= createSetupFake(fakeOptionsFromSearch(globalThis.location?.search ?? ""));
  return instance;
}

/** Tests: replace the singleton. */
export function replaceSetupFake(fake: SetupFake | undefined) {
  instance = fake;
}
