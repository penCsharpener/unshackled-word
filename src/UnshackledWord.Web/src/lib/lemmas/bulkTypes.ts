import type { LemmaWord } from "./types";

// Response shapes of /api/lemmas/lemma-counts, /bulk/* and /verse (nulls are omitted by the API).

export interface LemmaCountItem {
  lemma: string;
  count: number;
  caseVariants?: { lemma: string; count: number }[];
}
export interface LemmaCountsResponse {
  items: LemmaCountItem[];
}

export interface BulkOccurrence {
  wordId: number;
  bookId: number;
  chapter: number;
  verse: number;
  positionInVerse: number;
  lemma: string;
  wordInContext: string;
  plainWord?: string;
  partOfSpeech?: string;
  case?: string;
  number?: string;
  gender?: string;
  /** up to 5 words before / after the match, same verse */
  left: string;
  right: string;
  /** original-language word behind the German word: aligned | added | none | multi */
  origKind?: string;
  /** group key: G4680 / H2450 */
  origKey?: string;
  /** shown Strong's number, e.g. G4680 / H2450G */
  origStrongs?: string;
  /** surface form in the verse */
  origWord?: string;
  /** original word class: N V A T R P … */
  origPos?: string;
  posConflict?: boolean;
}
/** One original-language group over all matches (key: G4680 | H2450 | added | none). */
export interface BulkOriginalCount {
  key: string;
  kind?: string;
  lang?: string;
  /** lexicon word (σοφός / חָכָם), falls back to the surface form */
  word?: string;
  gloss?: string;
  pos?: string;
  count: number;
  /** share of the aligned matches of its language, 0..1 */
  share?: number;
  minority?: boolean;
  posConflict?: boolean;
  unmapped?: boolean;
  added?: boolean;
}
export interface BulkGroupCount {
  /** set when the groups were requested per original (groupBy=original) */
  origKey?: string;
  plainWord?: string;
  partOfSpeech?: string;
  count: number;
}
export interface BulkOccurrencesResponse {
  from: string[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  byBook: { bookId: number; count: number }[];
  byLemma: { lemma: string; count: number }[];
  groups: BulkGroupCount[];
  byOriginal?: BulkOriginalCount[];
  /** several original words behind the lemma: drives the hint and the default grouping */
  ambiguous?: boolean;
  occurrences: BulkOccurrence[];
}
export interface BulkWordIdsResponse {
  total: number;
  truncated: boolean;
  wordIds: number[];
}
export interface LemmaVerseResponse {
  bookId: number;
  chapter: number;
  verse: number;
  words: LemmaWord[];
}
/** Why a row was refused by the apply: its lemma changed, or its original-language mapping did. */
export interface BulkStaleDetail {
  wordId: number;
  reason: "lemma" | "original" | "stts";
  lemma?: string;
  origKey?: string;
}
export interface BulkApplyResponse {
  changeSetId: number;
  updated: number;
}
export interface BulkRevertResponse {
  changeSetId: number;
  restored: number;
  skipped: number;
}
