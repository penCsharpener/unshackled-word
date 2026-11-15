import type {
  VerseMappingGermanWord,
  VerseMappingLink,
  VerseMappingOriginalWord,
} from "@/lib/bible/types";

/** Preset of the revision slider (`GET api/revision/levels`). Levels may repeat: Elb1871 (`isBase`) shares its level with a revision that has variants. */
export interface RevisionLevel {
  level: number;
  code: string;
  name: string;
  isBase: boolean;
  isDefault: boolean;
}

export interface RevisionVariant {
  revisionVerseId: number;
  level: number;
  version: number;
  text: string;
  createdAt: string;
  createdBy?: string;
  mappingReviewedAt?: string;
}

export interface RevisionVerseResponse {
  baseText: string;
  variants: RevisionVariant[];
}

export interface SaveRevisionRequest {
  level: number;
  text: string;
  /** Active version the editor was loaded with; omitted when the level has no variant yet. */
  expectedVersion?: number;
  note?: string;
  /** Level whose active variant the mapping is carried from; 0 (default) = Elberfelder 1871. */
  basedOnLevel?: number;
}

/** What the carry-over of the mapping did when a text was saved. */
export interface RevisionCarryReport {
  same: number;
  changed: number;
  new: number;
  /** STEP word ids that were linked in the parent text and have no link in the new text. */
  lostOriginalWordIds: number[];
}

export interface SaveRevisionResponse {
  revisionVerseId: number;
  level: number;
  version: number;
  carry: RevisionCarryReport;
}

export type RevisionWordChange = "same" | "changed" | "new";

/**
 * `GET api/revision/verseMapping/{id}`: `id` / `elbWordId` of words and links are RevisionWords ids. The verse id is stable across text
 * saves, the word ids are not: every text save rewrites the words, so a mapping loaded before a text save must be reloaded.
 */
export interface RevisionVerseMappingResponse {
  revisionVerseId: number;
  level: number;
  version: number;
  bibleBookId: number;
  chapter: number;
  verse: number;
  language: "greek" | "hebrew";
  /** set once a writer saved the mapping; markers disappear then */
  mappingReviewedAt?: string;
  germanWords: (VerseMappingGermanWord & { change: RevisionWordChange })[];
  originalWords: VerseMappingOriginalWord[];
  links: (VerseMappingLink & { origin: "carried" | "manual" | "seed" })[];
}

export interface RevisionVerseRef {
  bookId: number;
  chapter: number;
  verse: number;
}

/** One audit entry of a verse at one level (`GET api/revision/verse/…/history?level=`), newest first. */
export interface RevisionHistoryVersion {
  auditId: number;
  version: number;
  /** `text` = a text save, `mapping` = a mapping save of the same `version`, `withdraw` = the revision of this level was removed. */
  action: "text" | "mapping" | "withdraw";
  text: string;
  note?: string;
  /** Newest text entry of a verse that is not withdrawn. */
  isActive: boolean;
  at: string;
  by?: string;
  /** `at` of the next text or withdraw entry. */
  endedAt?: string;
}

export interface RevisionHistoryResponse {
  level: number;
  versions: RevisionHistoryVersion[];
}

/** Preset with both names for the maintainer's level management (`GET api/revision/levels/manage`). */
export interface RevisionLevelDetail {
  level: number;
  code: string;
  nameDe: string;
  nameEn: string;
  descriptionDe?: string;
  descriptionEn?: string;
  isBase: boolean;
  isDefault: boolean;
  /** Stored variant versions at this level; the level value is fixed once this is above 0. */
  variantCount: number;
}

/** Body of `POST api/revision/levels` and `PUT api/revision/levels/{currentLevel}` (`level` = the new value). */
export interface SaveRevisionLevelRequest {
  level: number;
  code: string;
  nameDe: string;
  nameEn: string;
  descriptionDe?: string;
  descriptionEn?: string;
  isDefault: boolean;
}

/** One hit of `POST api/revision/bulkReplace/search` (roadmap 0013); nulls are omitted by the API. */
export interface BulkReplaceRow {
  hebRefId: number;
  /** 1-based number among the hits of the same verse */
  occurrence: number;
  positionInVerse: number;
  found: string;
  contextBefore: string;
  contextAfter: string;
  strongs: string[];
  lemma?: string;
  /** the article or determiner right before the hit */
  articleBefore?: string;
  /** active version of the target revision, absent when it has no variant of the verse */
  targetVersion?: number;
  targetDiffers: boolean;
}

export interface BulkReplaceFacet {
  /** absent = hits without a link to an original word */
  strongs?: string;
  count: number;
}

export interface BulkReplaceSearchRequest {
  word: string;
  sameLemma: boolean;
  sourceRevision: string;
  targetRevision: string;
}

export interface BulkReplaceSearchResponse {
  lemmas: string[];
  total: number;
  capped: boolean;
  facets: BulkReplaceFacet[];
  rows: BulkReplaceRow[];
}
