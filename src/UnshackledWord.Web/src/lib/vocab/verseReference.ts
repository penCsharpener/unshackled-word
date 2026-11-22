import { bookAbbreviation } from "@/lib/bible/books";
import type { BibleStructureBook } from "@/lib/bible/types";
import type { VocabSampleVerse } from "./api";

type VerseRef = Pick<VocabSampleVerse, "bibleBookId" | "chapter" | "verse">;

/** "Joh 1,1" in the UI language; the book id until the Bible structure has loaded. */
export function sampleVerseReference(
  verse: VerseRef,
  books: readonly BibleStructureBook[] | undefined,
  language: string,
): string {
  const book = books?.find((item) => item.id === verse.bibleBookId);
  const name = book ? bookAbbreviation(book, language) : String(verse.bibleBookId);
  return `${name} ${verse.chapter},${verse.verse}`;
}

/** Reader link of a sample verse. */
export function sampleVerseHref(verse: VerseRef): string {
  return `/bible/${verse.bibleBookId}/${verse.chapter}#v${verse.verse}`;
}
