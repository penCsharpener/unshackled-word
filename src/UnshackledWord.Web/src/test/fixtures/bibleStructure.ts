import type {
  BibleStructureBook,
  GetBibleStructureResponse,
  GetChapterTextResponse,
} from "@/lib/bible/types";

function book(
  id: number,
  testament: "ot" | "nt",
  nameDe: string,
  nameEn: string,
  abbreviationDe: string,
  abbreviationEn: string,
  chapters: number,
): BibleStructureBook {
  return {
    id,
    testament,
    nameDe,
    nameEn,
    abbreviationDe,
    abbreviationEn,
    versesPerChapter: Array<number>(chapters).fill(20),
  };
}

export const fixtureBooks: BibleStructureBook[] = [
  book(1, "ot", "1. Mose", "Genesis", "1Mo", "Gen", 50),
  book(2, "ot", "2. Mose", "Exodus", "2Mo", "Exod", 40),
  book(19, "ot", "Psalmen", "Psalms", "Ps", "Ps", 150),
  book(39, "ot", "Maleachi", "Malachi", "Mal", "Mal", 4),
  book(40, "nt", "Matthäus", "Matthew", "Mt", "Matt", 28),
  book(66, "nt", "Offenbarung", "Revelation", "Off", "Rev", 22),
];

export const fixtureStructure: GetBibleStructureResponse = { books: fixtureBooks };

export const fixtureChapterText: GetChapterTextResponse = {
  bibleBookId: 43,
  chapter: 3,
  verses: [
    { verse: 1, text: "Es war aber ein Mensch aus den Pharisäern." },
    { verse: 2, text: "Dieser kam zu Jesu bei der Nacht." },
    { verse: 3, text: "Jesus antwortete und sprach zu ihm." },
  ],
};
