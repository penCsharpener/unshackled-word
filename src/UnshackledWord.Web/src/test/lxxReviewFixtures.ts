import { vi } from "vitest";
import type {
  LxxReviewChapterResponse,
  LxxReviewUnit,
  LxxReviewWord,
} from "@/lib/maintainer/lxxReview";

/** Rahlfs Ruth 1 as `/edit/lxx` sees it: three open units with AI proposals (one with a low word) and a confirmed one. */

const ai = (lemma: string, strongs: string, morphology: string, hebrew: number[], low = false) => ({
  lemma,
  strongs,
  morphology,
  confidence: low ? ("low" as const) : ("high" as const),
  prefilled: false,
  hebrewWordIds: hebrew,
});

const word = (id: number, position: number, greek: string, extra: Partial<LxxReviewWord> = {}) => ({
  id,
  position,
  greek,
  hebrewWordIds: [],
  linksConfirmed: false,
  ...extra,
});

const base = {
  chapter: 1,
  subverse: 0,
  mapKind: "same",
  isAddition: false,
  open: true,
  hasConfirmed: false,
  german: [],
};

export const unitA: LxxReviewUnit = {
  ...base,
  lxxVerseId: 101,
  verse: 1,
  ref: "Rut 1:1",
  words: [
    word(1001, 1, "καὶ", {
      tag: {
        lemma: "καί",
        strongs: "G2532",
        morphology: "CONJ",
        status: "proposed",
        source: "nt-form",
      },
      hebrewWordIds: [501],
      ai: ai("καί", "G2532", "CONJ", [501]),
    }),
    word(1002, 2, "ἐγένετο", { ai: ai("γίνομαι", "G1096", "V-2ADI-3S", [502], true) }),
  ],
  hebrew: [
    {
      id: 501,
      number: 1,
      text: "וַיְהִי",
      strongs: "H1961",
      gloss: "to be",
      german: "Und es geschah",
      germanWordIds: [71, 72, 73],
      note: "1) to be, become",
    },
    { id: 502, number: 2, text: "בִּימֵי" },
  ],
  german: [
    { id: 71, text: "Und" },
    { id: 72, text: "es" },
    { id: 73, text: "geschah" },
    { id: 74, text: "in" },
    { id: 75, text: "den" },
    { id: 76, text: "Tagen," },
  ],
  proposal: {
    id: 11,
    runId: 3,
    createdAt: "2026-11-25T10:00:00Z",
    note: "Hinweis der KI",
    flags: [{ code: "no-strongs", message: "Wort 2: unsicher", position: 2 }],
  },
};

export const unitB: LxxReviewUnit = {
  ...base,
  lxxVerseId: 102,
  verse: 2,
  ref: "Rut 1:2",
  words: [word(1003, 1, "ὄνομα", { ai: ai("ὄνομα", "G3686", "N-NSN", [503]) })],
  hebrew: [{ id: 503, number: 1, text: "שֵׁם" }],
  proposal: { id: 12, runId: 3, createdAt: "2026-11-25T10:00:00Z", flags: [] },
};

export const unitC: LxxReviewUnit = {
  ...base,
  lxxVerseId: 103,
  verse: 3,
  ref: "Rut 1:3",
  words: [word(1004, 1, "ἀνήρ", { ai: ai("ἀνήρ", "G0435", "N-NSM", [504], true) })],
  hebrew: [{ id: 504, number: 1, text: "אִישׁ" }],
  proposal: { id: 13, runId: 3, createdAt: "2026-11-25T10:00:00Z", flags: [] },
};

export const unitD: LxxReviewUnit = {
  ...base,
  lxxVerseId: 104,
  verse: 5,
  ref: "Rut 1:5",
  open: false,
  hasConfirmed: true,
  words: [
    word(1005, 1, "τέκνον", {
      tag: {
        lemma: "τέκνον",
        strongs: "G5043",
        morphology: "N-NSN",
        status: "confirmed",
        source: "human",
      },
    }),
  ],
  hebrew: [{ id: 505, number: 1, text: "בֵּן" }],
};

export const ruthChapter = (units: LxxReviewUnit[]): LxxReviewChapterResponse => ({
  bibleBookId: 8,
  chapter: 1,
  units,
});

export const booksResponse = {
  books: [
    {
      bibleBookId: 8,
      openUnits: 3,
      aiUnits: 3,
      lowWords: 2,
      chapters: [{ chapter: 1, openUnits: 3, aiUnits: 3, lowWords: 2 }],
    },
  ],
};

/** The page's layout switches at md; jsdom has no `matchMedia`, so the tests ask for the desktop table. */
export function stubDesktop() {
  vi.stubGlobal("matchMedia", (query: string) => ({
    matches: true,
    media: query,
    addEventListener: () => {},
    removeEventListener: () => {},
  }));
}
