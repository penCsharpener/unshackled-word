import { api } from "@/lib/api";
import type { VocabLang } from "./api";

/** Kinds of a lexicon change (roadmap 0022.8b). Unknown kinds from newer servers are shown as they come. */
export type VocabChangeKind =
  | "meanings"
  | "form"
  | "lemma"
  | "keys"
  | "merge"
  | "split"
  | "retire"
  | "delete";

/** One change row of a flagged card. `before`/`after` are the stored JSON of the change. */
export interface VocabChangeRow {
  kind: VocabChangeKind | (string & {});
  before?: Record<string, unknown> | null;
  after?: Record<string, unknown> | null;
  changedAt: string;
  targetEntryId?: number | null;
  targetLemma?: string | null;
  targetDictionaryForm?: string | null;
}

/** A card of the learner that was revised (`changed`) or removed (`removed`). Mirrors `VocabChangedEntryResponse`. */
export interface VocabChangedEntry {
  entryId: number;
  lemma: string;
  dictionaryForm?: string | null;
  transliteration?: string | null;
  lang: VocabLang;
  status: "changed" | "removed";
  changes: VocabChangeRow[];
}

/** Mirrors `GET api/vocab/changes`. */
export interface VocabChangesResponse {
  count: number;
  items: VocabChangedEntry[];
}

/** Mirrors `POST api/vocab/changes/{entryId}`; `remaining` counts the flagged cards left. */
export interface VocabChangeActionResponse {
  action: "keep" | "reset";
  entryId: number;
  remaining: number;
}

/** Mirrors `POST api/vocab/changes/keep-all`; removed cards stay in the list. */
export interface VocabKeepAllResponse {
  kept: number;
  remaining: number;
}

export function getVocabChanges(signal?: AbortSignal) {
  return api.get<VocabChangesResponse>("/vocab/changes", signal);
}

/** "Weiterlernen" (keep) or "Zurücksetzen" / "Entfernen" (reset) of one flagged card. */
export function postVocabChangeAction(entryId: number, action: "keep" | "reset") {
  return api.post<VocabChangeActionResponse>(`/vocab/changes/${entryId}`, { action });
}

/** "Alle weiterlernen": keeps every changed card, removed cards stay. */
export function keepAllVocabChanges() {
  return api.post<VocabKeepAllResponse>("/vocab/changes/keep-all", {});
}
