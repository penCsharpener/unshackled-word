import { api } from "@/lib/api";

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
