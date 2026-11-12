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

export type EditableField = "lemma" | "partOfSpeech" | MorphField;
export const EDITABLE_FIELDS: readonly EditableField[] = ["lemma", "partOfSpeech", ...MORPH_FIELDS];

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
export interface ValueMapsResponse {
  fields: ValueMapField[];
  rules?: RuleEntry[];
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
  degree?: string;
  verbForm?: string;
  tense?: string;
  person?: string;
  number?: string;
  mood?: string;
  case?: string;
  gender?: string;
  hasMorphology: boolean;
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
