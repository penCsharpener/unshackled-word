import type { BibleStructureBook, ChapterRef, PickerGroup, Testament } from "./types";

export const PSALM_SECTIONS: readonly (readonly [number, number])[] = [
  [1, 41],
  [42, 72],
  [73, 89],
  [90, 106],
  [107, 150],
];

const PSALMS_BOOK_ID = 19;

export function bookName(book: BibleStructureBook, language: string): string {
  return language.startsWith("en") ? book.nameEn : book.nameDe;
}

export function chapterCount(book: BibleStructureBook): number {
  return book.versesPerChapter.length;
}

export function chapterPath(ref: ChapterRef): string {
  return `/bible/${ref.bookId}/${ref.chapter}`;
}

/** Integers only; with `books` the book and chapter range are validated too. */
export function parseChapterParams(
  bookId?: string,
  chapter?: string,
  books?: BibleStructureBook[],
): ChapterRef | null {
  if (bookId === undefined || chapter === undefined) return null;
  if (!/^\d+$/.test(bookId) || !/^\d+$/.test(chapter)) return null;
  const ref = { bookId: Number(bookId), chapter: Number(chapter) };
  if (ref.bookId < 1 || ref.chapter < 1) return null;
  if (!books) return ref;
  const book = books.find((b) => b.id === ref.bookId);
  if (!book || ref.chapter > chapterCount(book)) return null;
  return ref;
}

/** Previous/next chapter, crossing book boundaries; null before the first / after the last. */
export function adjacentChapter(
  books: BibleStructureBook[],
  ref: ChapterRef,
  direction: -1 | 1,
): ChapterRef | null {
  const sorted = [...books].sort((a, b) => a.id - b.id);
  const index = sorted.findIndex((b) => b.id === ref.bookId);
  const book = sorted[index];
  if (!book) return null;
  const next = ref.chapter + direction;
  if (next >= 1 && next <= chapterCount(book)) return { bookId: book.id, chapter: next };
  const neighbour = sorted[index + direction];
  if (!neighbour) return null;
  return { bookId: neighbour.id, chapter: direction === 1 ? 1 : chapterCount(neighbour) };
}

function range(from: number, to: number): number[] {
  return Array.from({ length: to - from + 1 }, (_, i) => from + i);
}

export function buildPickerGroups(
  books: BibleStructureBook[],
  testament: Testament,
  language: string,
  t: (key: string, opts?: Record<string, unknown>) => string,
): PickerGroup[] {
  return books
    .filter((book) => book.testament === testament)
    .sort((a, b) => a.id - b.id)
    .flatMap((book): PickerGroup[] => {
      const name = bookName(book, language);
      if (book.id === PSALMS_BOOK_ID) {
        return PSALM_SECTIONS.map(([from, to], i) => ({
          key: `${book.id}-${i + 1}`,
          bookId: book.id,
          title: t("bible.picker.psalmsSection", { book: name, number: i + 1, from, to }),
          chapters: range(from, Math.min(to, chapterCount(book))),
        }));
      }
      return [
        {
          key: String(book.id),
          bookId: book.id,
          title: name,
          chapters: range(1, chapterCount(book)),
        },
      ];
    });
}
