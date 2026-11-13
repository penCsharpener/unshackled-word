import type { OriginalsLookupResponse, SearchVersesResponse } from "@/lib/bible/types";

const words = (text: string, matched: string[]) =>
  text.split(" ").map((word) => ({ text: word, matched: matched.includes(word) }));

/** Contract fixture of `GET api/elberfelder/search/verses` (camelCase, nulls omitted). */
export const fixtureVerseSearch: SearchVersesResponse = {
  query: "ging",
  total: 3,
  page: 1,
  pageSize: 20,
  tokens: [{ text: "ging", lexemes: ["ging"], lemmas: ["gehen"] }],
  bookCounts: [
    { bookId: 40, count: 2 },
    { bookId: 66, count: 1 },
  ],
  hits: [
    {
      bibleBookId: 40,
      chapter: 5,
      verse: 1,
      tokens: words("Als er aber die Volksmenge sah, ging er auf den Berg.", ["ging"]),
    },
    {
      bibleBookId: 40,
      chapter: 14,
      verse: 25,
      tokens: words("Jesus aber ging hin zu ihnen.", ["ging"]),
    },
    {
      bibleBookId: 66,
      chapter: 10,
      verse: 9,
      tokens: words("Und ich ging hin zu dem Engel.", ["ging"]),
    },
  ],
};

/** Contract fixture of `GET api/elberfelder/search/originals`. */
export const fixtureOriginals: OriginalsLookupResponse = {
  statsRefreshedAt: "2026-11-12T10:30:00Z",
  tokens: [
    {
      token: "ging",
      lemmas: [
        {
          lemma: "gehen",
          word: "gehen",
          pos: "VERB",
          occurrences: 1500,
          lemmaOccurrences: 1500,
          highFrequency: false,
          originals: [
            {
              strongs: "H1980",
              language: "hebrew",
              lemma: "הָלַךְ",
              gloss: "to go",
              count: 900,
              share: 0.6,
              totalCount: 1500,
            },
            {
              strongs: "G4198",
              language: "greek",
              lemma: "πορεύομαι",
              gloss: "to travel",
              count: 600,
              share: 0.4,
              totalCount: 1500,
            },
          ],
        },
      ],
    },
    {
      token: "und",
      lemmas: [
        {
          lemma: "und",
          word: "und",
          occurrences: 30000,
          lemmaOccurrences: 30000,
          highFrequency: true,
          originals: [],
        },
      ],
    },
    { token: "xyz", lemmas: [] },
  ],
};

/** The same "und" with the originals revealed (`includeFrequent=true`). */
export const fixtureOriginalsFrequent: OriginalsLookupResponse = {
  statsRefreshedAt: "2026-11-12T10:30:00Z",
  tokens: [
    {
      token: "und",
      lemmas: [
        {
          lemma: "und",
          word: "und",
          occurrences: 30000,
          lemmaOccurrences: 30000,
          highFrequency: true,
          originals: [
            {
              strongs: "G2532",
              language: "greek",
              lemma: "καί",
              gloss: "and",
              count: 9000,
              share: 1,
              totalCount: 9000,
            },
          ],
        },
      ],
    },
  ],
};
