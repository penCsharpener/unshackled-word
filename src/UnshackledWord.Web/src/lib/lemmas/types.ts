/** Morphology fields of a word that are edited as dropdowns (JSON property names of the API). */
export const MORPH_FIELDS = [
  "degree",
  "verbForm",
  "tense",
  "person",
  "number",
  "mood",
  "case",
  "gender",
] as const;
export type MorphField = (typeof MORPH_FIELDS)[number];

export type EditableField = "lemma" | "partOfSpeech" | "stts" | MorphField;
export const EDITABLE_FIELDS: readonly EditableField[] = [
  "lemma",
  "partOfSpeech",
  "stts",
  ...MORPH_FIELDS,
];

/** The editable values of one word; `null` = not set. */
export type WordValues = Record<EditableField, string | null>;

export interface ValueMapEntry {
  code: string;
  labelDe: string;
  labelEn: string;
}
export interface ValueMapField {
  field: string;
  values: ValueMapEntry[];
}
export interface RuleEntry {
  partOfSpeech: string;
  fields: string[];
  byVerbForm?: { verbForm: string; fields: string[] }[];
}
export interface SttsTagEntry {
  tag: string;
  upos: string;
  allowedUpos: string[];
}
export interface SttsProposalEntry {
  partOfSpeech: string;
  verbForm?: string;
  mood?: string;
  stts: string;
}
export interface ValueMapsResponse {
  fields: ValueMapField[];
  rules?: RuleEntry[];
  /** STTS tags of the table with the UD tags they accept */
  sttsTags?: SttsTagEntry[];
  /** most specific rule first */
  sttsProposals?: SttsProposalEntry[];
}

/** One row of GET /lemmas/chapter. Nulls are omitted by the API, hence optional. */
export interface LemmaWord {
  wordId: number;
  verse: number;
  positionInVerse: number;
  wordInContext: string;
  plainWord?: string;
  lemma?: string;
  partOfSpeech?: string;
  stts?: string;
  degree?: string;
  verbForm?: string;
  tense?: string;
  person?: string;
  number?: string;
  mood?: string;
  case?: string;
  gender?: string;
  hasMorphology: boolean;
  /** set when stts does not fit the part of speech: the tag that would */
  proposedStts?: string;
}
export interface LemmaChapterResponse {
  bookId: number;
  chapter: number;
  words: LemmaWord[];
}

/** Body of PUT /lemmas/chapter: full replacement of the editable fields per word. */
export interface LemmaWordChange {
  wordId: number;
  lemma: string;
  partOfSpeech: string;
  /** only sent when changed; omitted = keep the stored tag */
  stts?: string;
  degree?: string;
  verbForm?: string;
  tense?: string;
  person?: string;
  number?: string;
  mood?: string;
  case?: string;
  gender?: string;
}
export interface UpdateLemmaChapterResponse {
  updated: number;
  words: LemmaWord[];
}
