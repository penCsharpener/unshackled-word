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
