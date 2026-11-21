import { api } from "@/lib/api";
import type { SeedJobState, SeedStepReport } from "@/lib/maintainer/seedJobs";

/** Course track: `G` = Neues Testament (Greek), `H` = Altes Testament (Hebrew and Aramaic). */
export type VocabTrack = "G" | "H";

/** Language of a lemma: `grc` for Greek, `he` for Hebrew and Aramaic. */
export type VocabLang = "grc" | "he";

/** Rows per list page; the API takes at most 100 per request. */
export const VOCAB_PAGE_SIZE = 50;

/** Mirrors one course of `GET api/vocab/courses`; nulls are omitted by the API. */
export interface VocabCourse {
  code: string;
  track: VocabTrack;
  nameDe: string;
  nameEn: string;
  totalEntries: number;
  totalOccurrences: number;
}

export interface VocabCoursesResponse {
  courses: VocabCourse[];
}

/** Mirrors one item of `GET api/vocab/courses/{code}/entries`; nulls are omitted by the API. */
export interface VocabEntryItem {
  id: number;
  rank: number;
  lemma: string;
  transliteration: string;
  /** Study form (Lernform), absent when the entry has none. */
  dictionaryForm?: string | null;
  meanings: string[];
  occurrences: number;
  hasAudio: boolean;
  reviewed: boolean;
  strongsKeys: string[];
}

export interface VocabEntriesResponse {
  code: string;
  total: number;
  skip: number;
  take: number;
  items: VocabEntryItem[];
}

export interface VocabEntriesQuery {
  skip: number;
  take: number;
  q?: string;
}

/** Mirrors `GET api/vocab/entries/{id}` (`VocabEntryDetailResponse`); nulls are omitted by the API. */
export interface VocabEntryDetail {
  id: number;
  track: VocabTrack;
  rank?: number | null;
  lemma: string;
  transliteration?: string | null;
  dictionaryForm?: string | null;
  pos?: string | null;
  occurrences: number;
  isProperName: boolean;
  reviewed: boolean;
  hasAudio: boolean;
}

/** One Strong's key of an entry with its occurrences. */
export interface VocabEntryKey {
  strongsKey: string;
  occurrences: number;
}

/** Accepted meaning in rank order; empty until the meanings are derived (roadmap 0022.3). */
export interface VocabMeaning {
  german: string;
  rank: number;
}

/** One German rendering summed over the entry's Strong's keys. */
export interface VocabRendering {
  germanKey: string;
  germanDisplay: string;
  linkCount: number;
  /** Part of `linkCount` from Elberfelder 2027 variants. */
  fromRevisionCount: number;
  /** Share of all head links of the entry, in percent (one decimal). */
  share: number;
  posTags: string[];
  keyIsForm: boolean;
}

export interface VocabSampleWord {
  position: number;
  text: string;
  transliteration?: string | null;
  isHit: boolean;
}

/** A sample verse; the reader route is `/bible/{bibleBookId}/{chapter}#v{verse}`. */
export interface VocabSampleVerse {
  bibleBookId: number;
  chapter: number;
  verse: number;
  fromRevision: boolean;
  humanMade: boolean;
  original: VocabSampleWord[];
  german: VocabSampleWord[];
}

export interface VocabEntryResponse {
  entry: VocabEntryDetail;
  strongsKeys: VocabEntryKey[];
  meanings: VocabMeaning[];
  renderings: VocabRendering[];
  renderingsTotal: number;
  totalLinks: number;
  /** Last refresh of the renderings; absent = never refreshed, `renderings` is then empty. */
  refreshedAt?: string | null;
  sampleVerses: VocabSampleVerse[];
}

/** A vocab background job on the shared seed job runner (`SeedJobResponse` with vocab operation names). */
export interface VocabJob {
  id: string;
  /** `vocabRenderings` or `vocabGenerate`. */
  operation: string;
  kind: string;
  status: SeedJobState;
  startedAt: string;
  finishedAt?: string | null;
  log: string[];
  error?: string | null;
  result?: { steps: SeedStepReport[] } | null;
}

/** Mirrors `GET api/maintainer/vocab/renderings/status`. */
export interface VocabRenderingsStatus {
  job?: VocabJob | null;
  refreshedAt?: string | null;
}

/** Mirrors `GET api/maintainer/vocab/status` (lexicon generation). */
export interface VocabGenerateStatus {
  job?: VocabJob | null;
}

/** Language of a track's lemmas: Greek for `G`, Hebrew for `H`. */
export function vocabLang(track: VocabTrack): VocabLang {
  return track === "H" ? "he" : "grc";
}

export function vocabEntriesQueryString(query: VocabEntriesQuery): string {
  const params = new URLSearchParams();
  params.set("skip", String(query.skip));
  params.set("take", String(query.take));
  const q = query.q?.trim();
  if (q) params.set("q", q);
  return params.toString();
}

/** Public course list (`GET api/vocab/courses`). */
export function getCourses(signal?: AbortSignal) {
  return api.get<VocabCoursesResponse>("/vocab/courses", signal);
}

/** One page of a course's entries (`GET api/vocab/courses/{code}/entries`), optionally filtered by `q`. */
export function getCourseEntries(code: string, query: VocabEntriesQuery, signal?: AbortSignal) {
  return api.get<VocabEntriesResponse>(
    `/vocab/courses/${encodeURIComponent(code)}/entries?${vocabEntriesQueryString(query)}`,
    signal,
  );
}

/** One entry with its keys, renderings and sample verses (`GET api/vocab/entries/{id}`). */
export function getVocabEntry(id: number, signal?: AbortSignal) {
  return api.get<VocabEntryResponse>(`/vocab/entries/${id}`, signal);
}

/** Last renderings refresh and its job (`GET api/maintainer/vocab/renderings/status`). */
export function getRenderingsStatus(signal?: AbortSignal) {
  return api.get<VocabRenderingsStatus>("/maintainer/vocab/renderings/status", signal);
}

/** Starts the renderings refresh job (`POST api/maintainer/vocab/renderings/refresh`, 202 with the job). */
export function startRenderingsRefresh() {
  return api.post<VocabJob>("/maintainer/vocab/renderings/refresh", undefined);
}

/** Last lexicon generation and its job (`GET api/maintainer/vocab/status`). */
export function getVocabGenerateStatus(signal?: AbortSignal) {
  return api.get<VocabGenerateStatus>("/maintainer/vocab/status", signal);
}

/** Starts the lexicon generation job (`POST api/maintainer/vocab/generate`, 202 with the job). */
export function startVocabGenerate() {
  return api.post<VocabJob>("/maintainer/vocab/generate", undefined);
}

/** Body of `POST api/maintainer/vocab/meanings/derive`; without `course` the scope is all entries. */
export interface DeriveVocabMeaningsRequest {
  /** Course code (`nt-greek`, `ot-hebrew`); absent = all entries. */
  course?: string;
  rankFrom?: number;
  rankTo?: number;
  /** Computes and reports only, writes nothing. */
  dryRun: boolean;
}

/** Mirrors `GET api/maintainer/vocab/meanings/status`. */
export interface VocabMeaningsStatus {
  job?: VocabJob | null;
}

/** Last meaning derivation and its job (`GET api/maintainer/vocab/meanings/status`). */
export function getMeaningsStatus(signal?: AbortSignal) {
  return api.get<VocabMeaningsStatus>("/maintainer/vocab/meanings/status", signal);
}

/** Starts the meaning derivation job (`POST api/maintainer/vocab/meanings/derive`, 202 with the job; 409 while another job runs). */
export function startMeaningsDerive(request: DeriveVocabMeaningsRequest) {
  return api.post<VocabJob>("/maintainer/vocab/meanings/derive", request);
}
