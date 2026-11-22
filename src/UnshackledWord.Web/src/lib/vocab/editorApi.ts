import { api } from "@/lib/api";
import type { VocabRendering, VocabSampleVerse, VocabTrack } from "./api";

// Vocab editor (roadmap 0022.4): `api/maintainer/vocab/entries`, Admin/Maintainer only.
// Mirrors `VocabEditorContracts.cs`; nulls are omitted by the API.

const BASE = "/maintainer/vocab/entries";

/** React Query keys of the editor; everything under `all` is refreshed after a write. */
export const vocabEditorKeys = {
  all: ["vocab", "editor"] as const,
  list: (query: VocabEditorListQuery) => ["vocab", "editor", "list", query] as const,
  search: (course: string | undefined, term: string) =>
    ["vocab", "editor", "search", course ?? null, term] as const,
  entry: (id: number) => ["vocab", "editor", "entry", id] as const,
};

/** Where the study form comes from: `generated` (lexicon), `agent` (KI proposal) or `manual`. */
export type VocabFormOrigin = "generated" | "agent" | "manual";

/** Filters of the editor list; each flag maps to the query parameter of the same name. */
export interface VocabEditorListQuery {
  /** Course code (`nt-greek`, `ot-hebrew`); absent = both tracks. */
  course?: string;
  q?: string;
  unreviewed?: boolean;
  noAudio?: boolean;
  formMissing?: boolean;
  formFromAgent?: boolean;
  multiKey?: boolean;
  retired?: boolean;
  rankFrom?: number;
  rankTo?: number;
  skip: number;
  take: number;
}

const FLAGS = [
  "unreviewed",
  "noAudio",
  "formMissing",
  "formFromAgent",
  "multiKey",
  "retired",
] as const;

/** Query string of `GET api/maintainer/vocab/entries`; false flags are left out. */
export function vocabEditorListQueryString(query: VocabEditorListQuery): string {
  const params = new URLSearchParams();
  if (query.course) params.set("course", query.course);
  const q = query.q?.trim();
  if (q) params.set("q", q);
  for (const flag of FLAGS) {
    if (query[flag]) params.set(flag, "true");
  }
  if (query.rankFrom != null) params.set("rankFrom", String(query.rankFrom));
  if (query.rankTo != null) params.set("rankTo", String(query.rankTo));
  params.set("skip", String(query.skip));
  params.set("take", String(query.take));
  return params.toString();
}

/** One row of the editor list (`VocabEditorListItemResponse`). */
export interface VocabEditorListItem {
  id: number;
  track: VocabTrack;
  rank?: number | null;
  lemma: string;
  transliteration?: string | null;
  dictionaryForm?: string | null;
  dictionaryFormOrigin?: VocabFormOrigin | null;
  occurrences: number;
  hasAudio: boolean;
  /** Presigned URL, expires after 1 h. */
  audioUrl?: string | null;
  reviewed: boolean;
  retired: boolean;
  contentVersion: number;
  /** Users who hold a card on the entry. */
  learners: number;
  /** Accepted meanings in rank order. */
  meanings: { german: string; rank: number }[];
  strongsKeys: string[];
}

export interface VocabEditorListResponse {
  total: number;
  skip: number;
  take: number;
  items: VocabEditorListItem[];
}

/** The entry fields of the editor (`VocabEditorEntryResponse`). */
export interface VocabEditorEntry {
  id: number;
  track: VocabTrack;
  rank?: number | null;
  lemma: string;
  transliteration?: string | null;
  dictionaryForm?: string | null;
  dictionaryFormOrigin?: VocabFormOrigin | null;
  pos?: string | null;
  occurrences: number;
  isProperName: boolean;
  isMorpheme: boolean;
  hasAudio: boolean;
  /** Presigned URL, expires after 1 h. */
  audioUrl?: string | null;
  reviewedAt?: string | null;
  retiredAt?: string | null;
  contentVersion: number;
  updatedAt: string;
}

/** A meaning of the editor, accepted or not (`VocabEditorMeaningResponse`). */
export interface VocabEditorMeaning {
  id: number;
  german: string;
  rank: number;
  isAccepted: boolean;
  /** `derived` or `manual`; only manual meanings can be deleted. */
  origin: "derived" | "manual";
  isPinned: boolean;
  count?: number | null;
  /** Percent of the head links at derivation time. */
  share?: number | null;
}

/** A Strong's key of the entry (`VocabEditorKeyResponse`). */
export interface VocabEditorKey {
  strongsKey: string;
  occurrences: number;
  lemma?: string | null;
  transliteration?: string | null;
}

/** `GET api/maintainer/vocab/entries/{id}`: everything the editor page needs. */
export interface VocabEditorEntryResponse {
  entry: VocabEditorEntry;
  meanings: VocabEditorMeaning[];
  strongsKeys: VocabEditorKey[];
  /** Users with a card on the entry. */
  learners: number;
  renderings: VocabRendering[];
  renderingsTotal: number;
  totalLinks: number;
  refreshedAt?: string | null;
  sampleVerses: VocabSampleVerse[];
}

/** What a write returned (`VocabEditResponse`); the editor reloads the entry afterwards. */
export interface VocabEditResponse {
  entryId: number;
  contentVersion: number;
  /** The edit was learning-relevant: version bumped and a change row written. */
  bumped: boolean;
  targetEntryId?: number | null;
  targetContentVersion?: number | null;
  /** Id of a created meaning. */
  meaningId?: number | null;
  learners: number;
  occurrences?: number | null;
  targetOccurrences?: number | null;
}

/** Body of `PUT api/maintainer/vocab/entries/{id}`; all three fields are sent every time. */
export interface UpdateVocabEntryBody {
  dictionaryForm: string | null;
  transliteration: string | null;
  reviewed: boolean;
}

export interface UpdateVocabMeaningBody {
  isAccepted?: boolean;
  isPinned?: boolean;
}

/** One page of the editor list (`GET api/maintainer/vocab/entries`). */
export function getVocabEditorList(query: VocabEditorListQuery, signal?: AbortSignal) {
  return api.get<VocabEditorListResponse>(`${BASE}?${vocabEditorListQueryString(query)}`, signal);
}

/** One entry for the editor (`GET api/maintainer/vocab/entries/{id}`). */
export function getVocabEditorEntry(id: number, signal?: AbortSignal) {
  return api.get<VocabEditorEntryResponse>(`${BASE}/${id}`, signal);
}

/** Saves the entry's form, transliteration and reviewed flag (`PUT`). */
export function updateVocabEntry(id: number, body: UpdateVocabEntryBody) {
  return api.put<VocabEditResponse>(`${BASE}/${id}`, body);
}

/** Accepts the stored agent form as written: its origin becomes `manual`, text and version stay (`POST .../accept-form`, 409 without a form). */
export function acceptVocabForm(id: number) {
  return api.post<VocabEditResponse>(`${BASE}/${id}/accept-form`, undefined);
}

/** Adds a manual, accepted meaning at the end (`POST .../meanings`). */
export function addVocabMeaning(id: number, german: string) {
  return api.post<VocabEditResponse>(`${BASE}/${id}/meanings`, { german });
}

/** Changes accept or pin of one meaning; only the sent fields change (`PATCH`). */
export function updateVocabMeaning(id: number, meaningId: number, body: UpdateVocabMeaningBody) {
  return api.patch<VocabEditResponse>(`${BASE}/${id}/meanings/${meaningId}`, body);
}

/** Deletes a manual meaning (`DELETE`). */
export function deleteVocabMeaning(id: number, meaningId: number) {
  return api.delete<VocabEditResponse>(`${BASE}/${id}/meanings/${meaningId}`);
}

/** Puts every meaning of the entry in the wanted order (`PUT .../meanings/order`). */
export function reorderVocabMeanings(id: number, meaningIds: number[]) {
  return api.put<VocabEditResponse>(`${BASE}/${id}/meanings/order`, { meaningIds });
}

/** Moves a Strong's key to another entry; without a target it gets a new entry (`POST .../keys/{key}/move`). */
export function moveVocabKey(id: number, key: string, targetEntryId?: number) {
  return api.post<VocabEditResponse>(`${BASE}/${id}/keys/${encodeURIComponent(key)}/move`, {
    targetEntryId,
  });
}

/** Merges entry `id` into `targetId`; entry `id` is deleted (`POST .../merge/{targetId}`). */
export function mergeVocabEntry(id: number, targetId: number) {
  return api.post<VocabEditResponse>(`${BASE}/${id}/merge/${targetId}`, undefined);
}

/** Retires an entry: it leaves the lists, its cards stay (`POST .../retire`). */
export function retireVocabEntry(id: number) {
  return api.post<VocabEditResponse>(`${BASE}/${id}/retire`, undefined);
}

/** Deletes an entry; the API refuses while it still has keys (`DELETE`). */
export function deleteVocabEntry(id: number) {
  return api.delete<VocabEditResponse>(`${BASE}/${id}`);
}

/** Uploads an MP3 (`PUT .../audio`, multipart field `file`, MP3 only, at most 1 MB; 503 when storage is not configured). */
export function uploadVocabAudio(id: number, file: Blob, fileName = "audio.mp3") {
  const form = new FormData();
  form.append("file", file, fileName);
  return api.putForm<VocabEditResponse>(`${BASE}/${id}/audio`, form);
}

/** Removes the entry's audio (`DELETE .../audio`). */
export function deleteVocabAudio(id: number) {
  return api.delete<VocabEditResponse>(`${BASE}/${id}/audio`);
}
