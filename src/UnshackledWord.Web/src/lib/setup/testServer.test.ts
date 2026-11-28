import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { de } from "@/i18n/de";
import { en } from "@/i18n/en";
import { ApiError } from "@/lib/api";
import { runnableKeys, stepStatus, summarize } from "./catalog";
import { createSetupFake, FAKE_CATALOG } from "./testServer";
import type { SetupStep, SetupStepKey, SetupTable } from "./types";
import { SETUP_STEP_KEYS } from "./types";

beforeEach(() => {
  sessionStorage.clear();
  vi.useFakeTimers();
});
afterEach(() => vi.useRealTimers());

const find = (steps: SetupStep[], key: SetupStepKey) => {
  const step = steps.find((s) => s.key === key);
  if (!step) throw new Error(key);
  return step;
};

describe("setup test server", () => {
  it("starts with 22 steps and only the books green", async () => {
    const fake = createSetupFake({ random: () => 0 });
    const { steps } = await fake.status();
    expect(steps).toHaveLength(22);
    expect(FAKE_CATALOG.map((s) => s.key)).toEqual([...SETUP_STEP_KEYS]);
    expect(steps.filter((s) => s.status === "green").map((s) => s.key)).toEqual(["books"]);
    expect(find(steps, "elb1871-verses")).toMatchObject({
      status: "grey",
      missing: ["elb1871-text"],
    });
    expect(find(steps, "stts").status).toBe("red");
    expect(summarize(steps)).toEqual({ done: 1, total: 22 });
  });

  it("plans all 21 runnable steps in catalog order, grey ones behind planned ones included", async () => {
    const fake = createSetupFake({ random: () => 0 });
    const { steps } = await fake.status();
    const keys = runnableKeys(steps, { all: true });
    expect(keys).toHaveLength(21);
    expect(keys).toEqual(SETUP_STEP_KEYS.filter((k) => k !== "books"));
    expect(runnableKeys(steps, { group: "base" })).toEqual([
      "stts",
      "revision-levels",
      "elb1871-text",
      "elb1871-verses",
    ]);
    expect(runnableKeys(steps, { keys: ["elb1871-verses"] })).toEqual([]);
  });

  it("runs one step: log every 300 ms, counts and status afterwards", async () => {
    const fake = createSetupFake({ random: () => 0 });
    const job = await fake.run({ keys: ["elb1871-text"] });
    expect(job).toMatchObject({ operation: "setup", kind: "elb1871-text", status: "running" });
    await vi.advanceTimersByTimeAsync(600);
    const mid = await fake.status();
    expect(mid.job?.status).toBe("running");
    expect(mid.job?.currentStep).toBe("elb1871-text");
    expect(mid.job?.log.length).toBe(3);
    await vi.advanceTimersByTimeAsync(1000);
    const after = await fake.status();
    expect(after.job?.status).toBe("succeeded");
    const text = find(after.steps, "elb1871-text");
    expect(text.status).toBe("green");
    expect(text.tables.map((t) => t.rows)).toEqual([722743, 31174]);
    expect(find(after.steps, "elb1871-verses").status).toBe("red");
  });

  it("rejects a second run with 409 and a step that cannot run with 400", async () => {
    const fake = createSetupFake({ random: () => 0 });
    await expect(fake.run({ keys: ["elb1871-verses"] })).rejects.toMatchObject({ status: 400 });
    await fake.run({ keys: ["stts"] });
    const error = await fake.run({ keys: ["tsk"] }).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(409);
  });

  it("runs everything to 22 of 22 with revision-seed green by its note", async () => {
    const fake = createSetupFake({ random: () => 0 });
    await fake.run({ all: true });
    await vi.advanceTimersByTimeAsync(21 * 1500 + 1000);
    const { steps, job } = await fake.status();
    expect(job?.status).toBe("succeeded");
    expect(summarize(steps)).toEqual({ done: 22, total: 22 });
    expect(find(steps, "revision-seed")).toMatchObject({
      status: "green",
      note: "nothingToImport",
    });
    expect(find(steps, "lxx-tags").status).toBe("green");
  });

  it("fails the configured step once, stops the chain and succeeds on the second run", async () => {
    const fake = createSetupFake({ random: () => 0, failKey: "sbl" });
    await fake.run({ all: true });
    await vi.advanceTimersByTimeAsync(21 * 1500 + 1000);
    const { steps, job } = await fake.status();
    expect(job).toMatchObject({ status: "failed", failedStep: "sbl", error: "Fake-Fehler in sbl" });
    const sbl = find(steps, "sbl");
    expect(sbl).toMatchObject({ status: "amber", failed: true });
    expect(sbl.tables[0]?.rows).toBe(Math.floor(7939 / 2));
    expect(find(steps, "lxx-text").status).not.toBe("green");
    expect(find(steps, "verse-similarity").status).not.toBe("green");

    await fake.run({ keys: ["sbl"] });
    await vi.advanceTimersByTimeAsync(2000);
    const again = await fake.status();
    expect(again.job?.status).toBe("succeeded");
    expect(find(again.steps, "sbl")).toMatchObject({ status: "green" });
    expect(find(again.steps, "sbl").failed).toBeUndefined();
  });

  it("reset clears the state and the stored copy", async () => {
    const fake = createSetupFake({ random: () => 0 });
    await fake.run({ keys: ["stts"] });
    await vi.advanceTimersByTimeAsync(2000);
    fake.reset();
    const { steps, job } = await fake.status();
    expect(job).toBeUndefined();
    expect(summarize(steps).done).toBe(1);
    const again = createSetupFake({ random: () => 0, reset: true });
    expect(summarize((await again.status()).steps).done).toBe(1);
  });

  it("marks a job that was running at reload as failed", async () => {
    const first = createSetupFake({ random: () => 0 });
    await first.run({ keys: ["stts"] });
    const second = createSetupFake({ random: () => 0 });
    const { job } = await second.status();
    expect(job).toMatchObject({ status: "failed", error: "Seite neu geladen" });
  });
});

describe("stepStatus", () => {
  const t = (...rows: number[]): SetupTable[] => rows.map((r, i) => ({ name: `T${i}`, rows: r }));

  it.each([
    ["all rows -> green", t(5, 2), [], false, undefined, "green"],
    ["some rows -> amber", t(5, 0), [], false, undefined, "amber"],
    ["no rows -> red", t(0, 0), [], false, undefined, "red"],
    ["no rows, prerequisite missing -> grey", t(0), ["books"], false, undefined, "grey"],
    ["failed -> amber", t(0), [], true, undefined, "amber"],
    ["noted zero rows -> green", t(0, 0), [], false, "nothingToImport", "green"],
  ] as const)("%s", (_name, tables, missing, failed, note, expected) => {
    expect(stepStatus([...tables], [...missing] as SetupStepKey[], failed, note)).toBe(expected);
  });
});

describe("setup i18n", () => {
  it("has the same keys in de and en", () => {
    const keys = (value: unknown, prefix = ""): string[] =>
      typeof value === "object" && value !== null
        ? Object.entries(value).flatMap(([k, v]) => keys(v, `${prefix}${k}.`))
        : [prefix];
    expect(keys(de.setup)).toEqual(keys(en.setup));
    expect(Object.keys(de.setup.step).sort()).toEqual([...SETUP_STEP_KEYS].sort());
  });
});
