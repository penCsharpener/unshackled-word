import type { GetStrongsSummaryResponse, GetWordStatsResponse } from "@/lib/bible/types";

export const fixtureSummary: GetStrongsSummaryResponse = {
  strongs: "G26",
  language: "greek",
  lemma: "ἀγάπη",
  transliteration: "agapē",
  gloss: "love",
  occurrenceCount: 116,
  verseCount: 110,
  mappedCount: 113,
  unmappedCount: 3,
  byBook: [
    { bibleBookId: 40, count: 80, verseCount: 75 },
    { bibleBookId: 66, count: 36, verseCount: 35 },
  ],
  renderings: [
    { word: "Liebe", key: "liebe", count: 90 },
    { word: "Lieben", key: "lieben", count: 20 },
  ],
};

export const fixtureWordStats: GetWordStatsResponse = {
  word: "Liebe",
  testament: "nt",
  occurrenceCount: 100,
  verseCount: 95,
  unmappedCount: 2,
  byBook: [{ bibleBookId: 40, count: 100, verseCount: 95 }],
  originals: [
    {
      strongs: "G26",
      language: "greek",
      lemma: "ἀγάπη",
      transliteration: "agapē",
      gloss: "love",
      count: 90,
      totalCount: 116,
    },
    {
      strongs: "H160",
      language: "hebrew",
      lemma: "אַהֲבָה",
      gloss: "love",
      count: 10,
      totalCount: 30,
    },
  ],
};
