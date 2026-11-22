import type { AgentRun } from "@/lib/agent/types";
import { api } from "@/lib/api";

// Dictionary-form agent runs (roadmap 0022.13): `api/maintainer/vocab/form-runs`, Admin/Maintainer only.
// Mirrors `VocabFormRunContracts.cs`; nulls are omitted by the API. Status and cost of a started run
// come from the agent run endpoints (`api/maintainer/agent-runs/{id}`).

const BASE = "/maintainer/vocab/form-runs";

/** Range of a form run: the entries of a course in a frequency-rank range whose form is missing or generated. */
export interface VocabFormRunRange {
  /** Course code, `nt-greek` or `ot-hebrew`. */
  course: string;
  /** First frequency rank (the API default is 1). */
  fromRank?: number;
  /** Last frequency rank; without it the run goes to the end of the course, the limit still applies. */
  toRank?: number;
  /** At most this many entries (the API default is 200). */
  limit?: number;
  /** Profile name of `AgentMapping:Profiles`. */
  profile: string;
  model?: string;
}

/** Dry run of a form run (`VocabFormRunEstimateResponse`): calls no model and writes nothing. */
export interface VocabFormRunEstimate {
  course: string;
  profile: string;
  model: string;
  promptVersion: string;
  /** Entries the run would ask (form missing or generated, not retired). */
  entries: number;
  batches: number;
  fromRank: number;
  toRank: number;
  estimatedInputTokens: number;
  estimatedOutputTokens: number;
  estimatedCostUsd: number;
  warnings: string[];
}

/** `POST api/maintainer/vocab/form-runs/estimate`; 404 when no entry in the range needs a form. */
export function estimateVocabFormRun(body: VocabFormRunRange, signal?: AbortSignal) {
  return api.post<VocabFormRunEstimate>(`${BASE}/estimate`, body, signal);
}

/** `POST api/maintainer/vocab/form-runs`: starts the run in the background (202 with the run to poll). */
export function startVocabFormRun(body: VocabFormRunRange) {
  return api.post<AgentRun>(BASE, body);
}
