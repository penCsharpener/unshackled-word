// Response shapes of /api/dataQuality/* (docs/drafts/data-quality-design.md, "API contract").
// camelCase JSON, nulls omitted. Keep every contract type in this file so backend adjustments stay local.

export type Severity = "error" | "warning" | "info";
export type UnmappedSide = "german" | "greek" | "hebrew";
export type CoverageMetric = "german" | "source";
export type TestamentFacet = "ot" | "nt";

export interface CheckSummary {
  code: string;
  tier: 1 | 2 | 3;
  count: number;
  errorCount: number;
  previousCount?: number;
  /** U1: `{ key: "content", count }` (probable gaps); M4: `{ key: "nt", count }` (findings in the NT) */
  sub?: { key: string; count: number };
}

export interface OverviewResponse {
  refreshedAt?: string | null;
  refreshing: boolean;
  lastEditAt?: string;
  totals: { germanWords: number; sourceWords: number };
  checks: CheckSummary[];
}

export interface UnmappedClass {
  /** UPOS, "?" (no PoS) or "none" (no mapping row) for german; original class, "notNaTr", "HTo", "ketiv" otherwise */
  key: string;
  label: string;
  count: number;
  legit: boolean;
}

export interface UnmappedTop {
  /** `lemma~PoS` (german) or base Strong's (greek / hebrew); doubles as the explorer `q` */
  key: string;
  label: string;
  original?: string;
  transliteration?: string;
  gloss?: string;
  pos?: string;
  count: number;
}

export interface UnmappedResponse {
  side: UnmappedSide;
  total: number;
  refreshedAt?: string | null;
  byClass: UnmappedClass[];
  byBook: { bibleBookId: number; count: number }[];
  top: UnmappedTop[];
}

export interface CoverageChapter {
  chapter: number;
  total: number;
  unaligned: number;
}

export interface CoverageResponse {
  metric: CoverageMetric;
  refreshedAt?: string | null;
  books: { bibleBookId: number; chapters: CoverageChapter[] }[];
}

/** One finding row (check rows and explorer verse hits share the shape). */
export interface Finding {
  check: string;
  severity: Severity;
  bibleBookId: number;
  chapter: number;
  verse: number;
  elbWordId?: number;
  stepWordId?: number;
  lang?: "G" | "H";
  german?: string;
  lemma?: string;
  pos?: string;
  original?: string;
  strongs?: string;
  gloss?: string;
  /** short reason, e.g. "Wortart VERB <-> P" */
  detail: string;
  groupKey?: string;
  /** About 5 words of the verse on either side of `german`, as one string (see `splitContext`). */
  context?: string;
}

export interface CheckGroup {
  key: string;
  label: string;
  /**
   * Meaning depends on the check: M1 occurrences of the lemma/PoS, M2 aligned words of the Strong's,
   * L2/L3/L4 findings of the key, M4 findings per testament, C1 unaligned words of the chapter.
   */
  count: number;
  share?: number;
  /** "minority", "posConflict", ... and "error" (C1 chapters over the error share) */
  flags?: string[];
  /** G / H when the group is language specific (M1, M2): pass as `lang` together with `group`. */
  lang?: "G" | "H";
}

export interface CheckMatrix {
  rows: string[];
  columns: string[];
  cells: { row: string; column: string; count: number; expected: boolean }[];
}

export interface CheckResponse {
  code: string;
  /** Items of the page's filter; for aggregated checks without `group` the number of groups. */
  total: number;
  page: number;
  pageSize: number;
  refreshedAt?: string | null;
  groups?: CheckGroup[];
  matrix?: CheckMatrix;
  items: Finding[];
}

export interface OriginalCandidate {
  strongs: string;
  lang: "G" | "H";
  lemma: string;
  transliteration: string;
  gloss: string;
  occurrences: number;
  matchedBy: "strongs" | "script" | "translit" | "skeleton" | "gloss";
}
export interface OriginalSearchResponse {
  q: string;
  folded: string;
  candidates: OriginalCandidate[];
}

export interface Rendering {
  lemma: string;
  pos: string;
  count: number;
  share: number;
  flags: string[];
}
export interface OriginalDetailResponse {
  strongs: string;
  lang: "G" | "H";
  lemma: string;
  transliteration: string;
  gloss: string;
  origClass: string;
  occurrences: number;
  aligned: number;
  unmapped: number;
  refreshedAt?: string | null;
  renderings: Rendering[];
}

export interface GermanCandidate {
  lemma: string;
  pos: string;
  count: number;
  matchedBy: "lemma" | "form";
}
export interface GermanSearchResponse {
  q: string;
  candidates: GermanCandidate[];
}

export interface GermanSource {
  /** "G2316", "H0430", "added", "none" */
  key: string;
  lang?: "G" | "H";
  lemma?: string;
  transliteration?: string;
  gloss?: string;
  origClass?: string;
  count: number;
  share: number;
  flags: string[];
}
export interface GermanForm {
  form: string;
  count: number;
  otherLemmas: { lemma: string; pos: string; count: number }[];
}
export interface GermanDetailResponse {
  lemma: string;
  pos?: string;
  count: number;
  refreshedAt?: string | null;
  sources: GermanSource[];
  forms: GermanForm[];
}

export interface HitsResponse {
  total: number;
  page: number;
  pageSize: number;
  items: Finding[];
}

export interface RefreshResponse {
  refreshing: boolean;
}

/** Facets every GET accepts. */
export interface QualityFacets {
  testament?: TestamentFacet;
  bookId?: number;
}
