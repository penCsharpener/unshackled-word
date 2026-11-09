export type Testament = "ot" | "nt";

export interface BibleStructureBook {
  id: number;
  testament: Testament;
  nameDe: string;
  nameEn: string;
  abbreviationDe: string;
  abbreviationEn: string;
  /** index 0 = chapter 1; Heb (= Elb1871) versification */
  versesPerChapter: number[];
}
export interface GetBibleStructureResponse {
  books: BibleStructureBook[];
}

export interface ChapterVerse {
  verse: number;
  text: string;
}
export interface GetChapterTextResponse {
  bibleBookId: number;
  chapter: number;
  verses: ChapterVerse[];
}

/** One row of `chapterWords`. Nulls are omitted by the API, hence optional. */
export interface ChapterWord {
  id: number;
  verse: number;
  positionInVerse: number;
  wordInContext: string;
  plainWord?: string;
  lemma?: string;
  original?: string;
  originalLemma?: string;
  strongs?: string;
  verseCode?: string;
  grammaticalKey?: string;
}
export interface GetWordsOfChapterResponse {
  bibleBookId: number;
  chapter: number;
  words: ChapterWord[];
}

export interface ChapterRef {
  bookId: number;
  chapter: number;
}
export interface PickerGroup {
  key: string;
  bookId: number;
  title: string;
  chapters: number[];
}

/** One original-language reading of an Elberfelder word: shared `key` (Strong's, else lemma) plus its surface form. */
export interface WordSense {
  key: string;
  form: string;
}
/** An Elberfelder word with all its original-language readings (the API returns one row per reading). */
export interface WordToken {
  id: number;
  text: string;
  senses: WordSense[];
}
export interface VerseWords {
  verse: number;
  words: WordToken[];
}
/** `same`: shares a key and the same original form; `related`: shares a key with a different form. */
export type HighlightKind = "same" | "related";

export type OriginalLanguage = "greek" | "hebrew" | "aramaic";

export type PartOfSpeechKey =
  | "adjective"
  | "adverb"
  | "article"
  | "conjunction"
  | "indeclinable"
  | "interjection"
  | "noun"
  | "nounProper"
  | "nounProperPerson"
  | "nounProperPlace"
  | "particle"
  | "preposition"
  | "pronoun"
  | "suffix"
  | "verb";
export type TenseKey =
  | "present"
  | "imperfect"
  | "future"
  | "aorist"
  | "perfect"
  | "pluperfect"
  | "futurePerfect";
export type VoiceKey = "active" | "middle" | "passive" | "middleOrPassive";
export type MoodKey =
  | "indicative"
  | "subjunctive"
  | "optative"
  | "imperative"
  | "infinitive"
  | "participle";
export type PersonKey = "first" | "second" | "third";
export type CaseKey = "nominative" | "genitive" | "dative" | "accusative" | "vocative";
export type NumberKey = "singular" | "dual" | "plural";
export type GenderKey = "masculine" | "feminine" | "neuter" | "common";
export type DegreeKey = "comparative" | "superlative";
export type HebrewStemKey =
  | "qal"
  | "niphal"
  | "piel"
  | "pual"
  | "hiphil"
  | "hophal"
  | "hithpael"
  | "other";
export type HebrewFormKey =
  | "perfect"
  | "imperfect"
  | "sequentialPerfect"
  | "sequentialImperfect"
  | "cohortative"
  | "jussive"
  | "imperative"
  | "infinitiveAbsolute"
  | "infinitiveConstruct"
  | "participleActive"
  | "participlePassive";
export type HebrewStateKey = "absolute" | "construct" | "determined";

/** A morphology code plus its decoded parts. Parts are stable keys, translated by i18n (`bible.grammar.*`). */
export interface Morphology {
  code: string; // "N-NSM", "V-IAI-3S", "HR/Ncfsa", "HNcmpa"
  partOfSpeech?: PartOfSpeechKey;
  tense?: TenseKey; // Greek
  voice?: VoiceKey; // Greek
  mood?: MoodKey; // Greek
  person?: PersonKey;
  case?: CaseKey; // Greek
  number?: NumberKey;
  gender?: GenderKey;
  degree?: DegreeKey; // Greek
  stem?: HebrewStemKey; // Hebrew/Aramaic verbs
  hebrewForm?: HebrewFormKey; // Hebrew/Aramaic verbs
  state?: HebrewStateKey; // Hebrew/Aramaic nominals
  /** English STEP description, only when the decoder could not map the code. */
  description?: string;
}

/** One part of a Hebrew/Aramaic word (prefix, root, suffix), in word order. */
export interface Morpheme {
  role: "prefix" | "root" | "suffix";
  original: string;
  strongs?: string; // "H9003"
  gloss?: string; // English
  morphology?: Morphology;
}

export interface LexiconSummary {
  strongs: string;
  lemma: string;
  transliteration?: string;
  gloss?: string; // English
  definition?: string; // English, markup stripped, max ~600 chars
  definitionTruncated?: boolean;
}

/** One original-language word behind the German word. */
export interface Rendering {
  word: string;
  count: number;
}

/** A person or place of the STEP lists behind a proper noun, matched by Strong's number. */
export interface NamedEntity {
  kind: "person" | "place";
  name: string;
  brief: string; // English, one line
}

export interface OriginalReading {
  stepWordId: number;
  language: OriginalLanguage;
  positionInVerse: number; // position in the original verse; readings are sorted by it
  germanWordPart?: string; // part of the German word this reading covers
  original: string; // with diacritics
  transliteration?: string;
  lemma?: string;
  strongs: string; // STEP disambiguated root, e.g. "G3056", "H7225G"
  gloss?: string; // dictionary gloss, English
  contextGloss?: string; // meaning in this verse, English
  morphology?: Morphology; // whole word (Hebrew: root part)
  morphemes?: Morpheme[]; // Hebrew/Aramaic only
  lexicon?: LexiconSummary;
  /** uses of this root Strong's number in its testament (NT for Greek, OT for Hebrew/Aramaic) */
  occurrenceCount?: number;
  /** most used Elberfelder renderings of this Strong's number, most used first (top 5) */
  renderings?: Rendering[];
  /** other German words of the verse mapped to this same original word (ids from `coMappedWords`) */
  sharedWithWordIds?: number[];
  /** set when the Strong's number belongs to a STEP person or place */
  entity?: NamedEntity;
}

/** Who wrote the current version of the word's mapping rows. */
export type MappingSource = "ai" | "human";
/** Provenance of the word's mapping (Elb1871GreekMapping / Elb1871HebrewMapping); omitted when unmapped. */
export interface MappingProvenance {
  source: MappingSource;
  createdAt: string; // ISO 8601, current version
  model?: string; // AI model id, AI only, e.g. "gemini-3.1-flash-lite-preview"
  confirmedAt?: string; // ISO 8601, a human confirmed the AI mapping
}
/** Derived for the badge: AI unconfirmed, AI confirmed by a human, or written by a human. */
export type MappingStatus = "aiSuggested" | "confirmed" | "manual";

/**
 * `sameOriginal`: shares at least one original word ("Und die Erde" → וְהָאָרֶץ);
 * `addedWord`: added by the translator for this word (ParentGermanWordId = this word);
 * `parent`: the word this added word belongs to.
 */
export type CoMappedRelation = "sameOriginal" | "addedWord" | "parent";
export interface CoMappedWord {
  wordId: number;
  wordInContext: string;
  positionInVerse: number;
  relation: CoMappedRelation;
}
/** Server-derived uncertainty hints (there is no confidence value in the data). */
export type MappingHintKey = "manyOriginals" | "unclearWordPart" | "textVariants";

/** TSK target in Elberfelder numbering; `endVerse` makes it a range inside the same chapter. */
export interface CrossReference {
  bibleBookId: number;
  chapter: number;
  verse: number;
  endVerse?: number;
}

export interface GetWordDetailResponse {
  wordId: number;
  wordInContext: string;
  positionInVerse: number;
  germanLemma?: string;
  isAddedWord?: boolean; // omitted when false
  readings: OriginalReading[]; // [] = not linked (or added word)
  mapping?: MappingProvenance;
  coMappedWords?: CoMappedWord[]; // same verse, sorted by positionInVerse
  hints?: MappingHintKey[];
  /** TSK cross references of the word's verse (verse level, same list for every word of the verse), TSK order, max 20. */
  crossReferences?: CrossReference[];
  /** omitted when false: the verse has more cross references than listed */
  crossReferencesTruncated?: boolean;
}

export interface StrongsOccurrence {
  bibleBookId: number;
  chapter: number;
  verse: number;
  original?: string;
  before: StrongsOccurrenceToken[];
  keyword: StrongsOccurrenceToken[];
  /** Punctuation that trailed the keyword; drawn outside the keyword box. */
  keywordSuffix?: string;
  after: StrongsOccurrenceToken[];
}
export interface StrongsOccurrenceToken {
  text: string;
  matched: boolean;
}
export interface GetStrongsOccurrencesResponse {
  strongs: string;
  language: "greek" | "hebrew";
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  items: StrongsOccurrence[];
}

export interface VerseMappingGermanWord {
  id: number;
  positionInVerse: number;
  text: string;
}
export interface VerseMappingOriginalWord {
  id: number;
  position: number;
  original: string;
  transliteration?: string;
  strongs?: string;
  gloss?: string; // English
  morphologyCode?: string;
}
/**
 * One mapping row of a verse. A normal link has `stepWordId`; an added word has `isAddedWord`
 * and `parentElbWordId` (the German word it belongs to) instead.
 */
export interface VerseMappingLink {
  elbWordId: number;
  stepWordId?: number;
  isAddedWord: boolean;
  parentElbWordId?: number;
}
export interface GetVerseMappingResponse {
  bibleBookId: number;
  chapter: number;
  verse: number;
  language: "greek" | "hebrew";
  /** false: the editor is read-only (no Save) */
  editingEnabled: boolean;
  germanWords: VerseMappingGermanWord[];
  originalWords: VerseMappingOriginalWord[];
  links: VerseMappingLink[];
}
export interface PutVerseMappingRequest {
  links: VerseMappingLink[];
}
