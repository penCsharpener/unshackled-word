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

/** For the later word-level layer (`chapterWords`). Nulls are omitted by the API, hence optional. */
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
