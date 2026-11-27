import type { AgentRun } from "@/lib/agent/types";
import { api } from "@/lib/api";
import type { VocabFormRunEstimate, VocabFormRunRange } from "./formRunApi";

// Root agent runs (roadmap 0030.4): `api/maintainer/vocab/root-runs`, Admin/Maintainer only.
// Mirrors `VocabRootRunContracts.cs`; nulls are omitted by the API. Status and cost of a started run
// come from the agent run endpoints (`api/maintainer/agent-runs/{id}`).

const BASE = "/maintainer/vocab/root-runs";

/** Range of a root run; like a form run plus the option "Vorbefüllung prüfen". */
export interface VocabRootRunRange extends VocabFormRunRange {
  /** Ask the entries with a prefill row too and replace their row (listed in the report). Manual rows are never touched. */
  checkPrefill?: boolean;
}

/** Dry run of a root run (`VocabRootRunEstimateResponse`); same shape as the form run's. */
export type VocabRootRunEstimate = VocabFormRunEstimate;

export type VocabRootRunItemKind = "rejected" | "flagged" | "replaced";

/** One entry of the run report: the entries the maintainer should look at. */
export interface VocabRootRunItem {
  entryId: number;
  lemma: string;
  strongsKey: string;
  kind: VocabRootRunItemKind;
  reason: string;
  /** The model's answer as short text, e.g. `σύν + ἐπί + G3140`. */
  answer?: string;
}

export interface VocabRootRunReport {
  runId: number;
  rejected: number;
  flagged: number;
  replaced: number;
  items: VocabRootRunItem[];
}

/** `POST api/maintainer/vocab/root-runs/estimate`; 404 when no entry in the range needs a root. */
export function estimateVocabRootRun(body: VocabRootRunRange, signal?: AbortSignal) {
  return api.post<VocabRootRunEstimate>(`${BASE}/estimate`, body, signal);
}

/** `POST api/maintainer/vocab/root-runs`: starts the run in the background (202 with the run to poll). */
export function startVocabRootRun(body: VocabRootRunRange) {
  return api.post<AgentRun>(BASE, body);
}

/** `GET api/maintainer/vocab/root-runs/{runId}/report`: rejected, flagged and replaced entries of the run. */
export function getVocabRootRunReport(runId: number, signal?: AbortSignal) {
  return api.get<VocabRootRunReport>(`${BASE}/${runId}/report`, signal);
}
