export type BibleSectionKey =
  | "law"
  | "history"
  | "wisdom"
  | "prophets"
  | "gospels"
  | "paul"
  | "general"
  | "revelation";

export interface BibleSection {
  key: BibleSectionKey;
  /** Chart colour slot 1-8 (`--color-chart-N`). */
  slot: number;
  firstBookId: number;
  lastBookId: number;
}

/** The 8 parts of the Bible used to colour charts (book ids as in `bibleStructure`). */
export const BIBLE_SECTIONS: readonly BibleSection[] = [
  { key: "law", slot: 1, firstBookId: 1, lastBookId: 5 },
  { key: "history", slot: 2, firstBookId: 6, lastBookId: 17 },
  { key: "wisdom", slot: 3, firstBookId: 18, lastBookId: 22 },
  { key: "prophets", slot: 4, firstBookId: 23, lastBookId: 39 },
  { key: "gospels", slot: 5, firstBookId: 40, lastBookId: 44 },
  { key: "paul", slot: 6, firstBookId: 45, lastBookId: 57 },
  { key: "general", slot: 7, firstBookId: 58, lastBookId: 65 },
  { key: "revelation", slot: 8, firstBookId: 66, lastBookId: 66 },
];

export function bookSection(bookId: number): BibleSection | undefined {
  return BIBLE_SECTIONS.find((s) => bookId >= s.firstBookId && bookId <= s.lastBookId);
}
