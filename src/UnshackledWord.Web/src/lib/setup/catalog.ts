import type {
  RunSetupRequest,
  SetupStep,
  SetupStepKey,
  SetupStepStatus,
  SetupTable,
} from "./types";

export type DisabledReason =
  | "done"
  | "missing"
  | "notResumable"
  | "notWired"
  | "readOnly"
  | "jobRunning";

/**
 * Status from counts, the same rule the server applies (03-seed-catalog.md). The fake uses it; the
 * page never recomputes. A `note` ("nothing to import") makes a step that ran green even with 0 rows.
 */
export function stepStatus(
  tables: SetupTable[],
  missing: SetupStepKey[],
  failed: boolean,
  note?: string,
): SetupStepStatus {
  if (failed) return "amber";
  if (note) return "green";
  const filled = tables.filter((table) => table.rows > 0).length;
  if (tables.length > 0 && filled === tables.length) return "green";
  if (filled > 0) return "amber";
  return missing.length > 0 ? "grey" : "red";
}

/** "n von m Schritten fertig". */
export function summarize(steps: SetupStep[]): { done: number; total: number } {
  return { done: steps.filter((s) => s.status === "green").length, total: steps.length };
}

/**
 * Keys a run of this scope would start, in catalog order: red (also grey, once its prerequisites
 * are planned), or amber and resumable; wired; runnable. A step counts as green for the steps behind
 * it as soon as it is planned, so a chain can start `elb1871-verses` after `elb1871-text`.
 */
export function runnableKeys(steps: SetupStep[], scope: RunSetupRequest): SetupStepKey[] {
  const planned = new Set<SetupStepKey>();
  const green = new Set(steps.filter((s) => s.status === "green").map((s) => s.key));
  for (const step of steps) {
    if (!inScope(step, scope)) continue;
    const startable = step.status === "red" || step.status === "grey" || step.resumable;
    const wanted = step.status !== "green" && startable;
    if (!wanted || !step.wired || !step.runnable) continue;
    if (step.status === "amber" && !step.resumable) continue;
    if (!step.requires.every((key) => green.has(key) || planned.has(key))) continue;
    planned.add(step.key);
  }
  return steps.filter((s) => planned.has(s.key)).map((s) => s.key);
}

function inScope(step: SetupStep, scope: RunSetupRequest): boolean {
  if ("all" in scope) return true;
  if ("group" in scope) return step.group === scope.group;
  return scope.keys.includes(step.key);
}

/** Why a row's button is disabled, or null. i18n key under `setup.disabled.*`. */
export function disabledReason(step: SetupStep, jobRunning: boolean): DisabledReason | null {
  if (!step.runnable) return "readOnly";
  if (!step.wired) return "notWired";
  if (step.status === "green") return "done";
  if (step.status === "grey") return "missing";
  if (step.status === "amber" && !step.resumable) return "notResumable";
  if (jobRunning) return "jobRunning";
  return null;
}
