import type {
  BibleStructureBook,
  GetBibleStructureResponse,
  GetChapterTextResponse,
  GetWordsOfChapterResponse,
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

const word = (
  id: number,
  verse: number,
  positionInVerse: number,
  wordInContext: string,
  strongs?: string,
  original?: string,
) => ({ id, verse, positionInVerse, wordInContext, strongs, original });

export const fixtureChapterWords: GetWordsOfChapterResponse = {
  bibleBookId: 43,
  chapter: 3,
  words: [
    word(1, 1, 1, "Es"),
    word(2, 1, 2, "war", "G1510", "ἦν"),
    word(3, 1, 3, "ein"),
    word(4, 1, 4, "Mensch", "G0444", "ἄνθρωπος"),
    word(5, 2, 1, "Dieser"),
    word(6, 2, 2, "kam", "G2064", "ἦλθεν"),
    word(7, 2, 3, "zu"),
    word(8, 2, 4, "Jesu", "G2424", "Ἰησοῦν"),
    word(9, 3, 1, "Jesus", "G2424", "Ἰησοῦς"),
    word(10, 3, 2, "war", "G1510", "ἦν,"),
    word(11, 3, 3, "ist", "G1510", "ἐστιν"),
  ],
};
