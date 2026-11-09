import type {
  CoMappedWord,
  CrossReference,
  GetWordDetailResponse,
  MappingHintKey,
  MappingProvenance,
  NamedEntity,
  OriginalReading,
  WordToken,
} from "./types";
import { normalizeStrongs } from "./wordDetail";

export const MOCK_DELAY_MS = 120;

type MockReading = Omit<OriginalReading, "germanWordPart" | "sharedWithWordIds">;

/** Hand-written mock persons/places keyed by normalized Strong's, no STEP text. */
const MOCK_ENTITIES: Record<string, NamedEntity> = {
  G2424: { kind: "person", name: "Sample person", brief: "Hand-written sample description." },
  H3389: { kind: "place", name: "Sample place", brief: "Hand-written sample description." },
};

/** Mock readings keyed by normalized Strong's. Hand-written, no STEP text copied. */
export const MOCK_READINGS: Record<string, MockReading> = {
  G3056: {
    stepWordId: 140005,
    language: "greek",
    positionInVerse: 5,
    original: "λόγος",
    transliteration: "logos",
    lemma: "λόγος",
    strongs: "G3056",
    gloss: "word",
    contextGloss: "Word",
    morphology: {
      code: "N-NSM",
      partOfSpeech: "noun",
      case: "nominative",
      number: "singular",
      gender: "masculine",
    },
    lexicon: {
      strongs: "G3056",
      lemma: "λόγος",
      transliteration: "logos",
      gloss: "word",
      definition: "Sample text: the real lexicon definition is loaded live from the STEP data.",
      definitionTruncated: true,
    },
    occurrenceCount: 137,
    renderings: [
      { word: "Wort", count: 112 },
      { word: "Rede", count: 14 },
      { word: "Lehre", count: 6 },
    ],
  },
  G1510: {
    stepWordId: 140003,
    language: "greek",
    positionInVerse: 3,
    original: "ἦν",
    transliteration: "ēn",
    lemma: "εἰμί",
    strongs: "G1510",
    gloss: "to be",
    contextGloss: "was",
    morphology: {
      code: "V-IAI-3S",
      partOfSpeech: "verb",
      tense: "imperfect",
      voice: "active",
      mood: "indicative",
      person: "third",
      number: "singular",
    },
    lexicon: { strongs: "G1510", lemma: "εἰμί", gloss: "to be" },
  },
  G4250: {
    stepWordId: 140014,
    language: "greek",
    positionInVerse: 14,
    original: "πρὶν",
    transliteration: "prin",
    lemma: "πρίν",
    strongs: "G4250",
    gloss: "before",
    contextGloss: "before",
    morphology: { code: "ADV", partOfSpeech: "adverb" },
  },
  G2228: {
    stepWordId: 140015,
    language: "greek",
    positionInVerse: 15,
    original: "ἢ",
    transliteration: "ē",
    lemma: "ἤ",
    strongs: "G2228",
    gloss: "or, than",
    contextGloss: "–",
    morphology: { code: "PRT", partOfSpeech: "particle" },
  },
  H7225G: {
    stepWordId: 100001,
    language: "hebrew",
    positionInVerse: 1,
    original: "בְּרֵאשִׁית",
    transliteration: "be·re·shit",
    lemma: "רֵאשִׁית",
    strongs: "H7225G",
    gloss: "beginning",
    contextGloss: "in the beginning",
    morphology: {
      code: "HR/Ncfsa",
      partOfSpeech: "noun",
      gender: "feminine",
      number: "singular",
      state: "absolute",
    },
    morphemes: [
      {
        role: "prefix",
        original: "בְּ",
        strongs: "H9003",
        gloss: "in",
        morphology: { code: "HR", partOfSpeech: "preposition" },
      },
      {
        role: "root",
        original: "רֵאשִׁית",
        strongs: "H7225G",
        gloss: "beginning",
        morphology: {
          code: "HNcfsa",
          partOfSpeech: "noun",
          gender: "feminine",
          number: "singular",
          state: "absolute",
        },
      },
    ],
    occurrenceCount: 51,
    renderings: [
      { word: "Anfang", count: 40 },
      { word: "Erstling", count: 7 },
    ],
  },
  H430G: {
    stepWordId: 100003,
    language: "hebrew",
    positionInVerse: 3,
    original: "אֱלֹהִים",
    transliteration: "e·lo·him",
    lemma: "אֱלֹהִים",
    strongs: "H0430G",
    gloss: "God",
    contextGloss: "God",
    morphology: {
      code: "HNcmpa",
      partOfSpeech: "noun",
      gender: "masculine",
      number: "plural",
      state: "absolute",
    },
    morphemes: [
      {
        role: "root",
        original: "אֱלֹהִים",
        strongs: "H0430G",
        gloss: "God",
        morphology: {
          code: "HNcmpa",
          partOfSpeech: "noun",
          gender: "masculine",
          number: "plural",
          state: "absolute",
        },
      },
    ],
  },
};

/** Same rule as `words.ts` `normalizeForm`: letters and marks only, NFC, lower case. */
const normalizeOriginal = (value: string): string =>
  value
    .replace(/[^\p{L}\p{M}]/gu, "")
    .normalize("NFC")
    .toLowerCase();

/** Articles that the mock treats as "added by the translator" when the next word is mapped. */
const MOCK_ADDED_WORDS = new Set([
  "der",
  "die",
  "das",
  "den",
  "dem",
  "des",
  "ein",
  "eine",
  "einen",
  "einem",
  "einer",
  "eines",
]);
/** Public-domain TSK style sample (verse level), the same for every word because the mock has no verse reference. */
const MOCK_CROSS_REFERENCES: CrossReference[] = [
  { bibleBookId: 1, chapter: 1, verse: 1 },
  { bibleBookId: 20, chapter: 8, verse: 22, endVerse: 24 },
  { bibleBookId: 43, chapter: 1, verse: 1, endVerse: 3 },
];

const MOCK_MODEL = "gemini-3.1-flash-lite-preview";
const MOCK_CREATED_AT = "2026-03-14T10:00:00Z";
const MOCK_CONFIRMED_AT = "2026-10-02T08:30:00Z";

/** Deterministic by word id: every third word manual, every third confirmed, the rest AI suggestions. */
export function mockProvenance(wordId: number): MappingProvenance {
  if (wordId % 3 === 0) return { source: "human", createdAt: MOCK_CONFIRMED_AT };
  if (wordId % 3 === 1) {
    return {
      source: "ai",
      model: MOCK_MODEL,
      createdAt: MOCK_CREATED_AT,
      confirmedAt: MOCK_CONFIRMED_AT,
    };
  }
  return { source: "ai", model: MOCK_MODEL, createdAt: MOCK_CREATED_AT };
}

const isMockAdded = (word: WordToken, next: WordToken | undefined) =>
  word.senses.length === 0 &&
  next !== undefined &&
  next.senses.length > 0 &&
  MOCK_ADDED_WORDS.has(word.text.toLowerCase());

const sharesOriginal = (a: WordToken, b: WordToken) =>
  a.senses.some((s) => s.form !== "" && b.senses.some((o) => o.key === s.key && o.form === s.form));

/**
 * Mock of the word detail. `verseWords` (the word's verse, in order) stands in for the mapping table:
 * words sharing a sense with the same form share an original word, an article without senses right
 * before a mapped word is an added word belonging to it.
 */
export function mockWordDetail(
  word: WordToken,
  delayMs = MOCK_DELAY_MS,
  verseWords: WordToken[] = [],
): Promise<GetWordDetailResponse> {
  const index = verseWords.findIndex((w) => w.id === word.id);
  const positionOf = (i: number) => i + 1;
  const position = index >= 0 ? positionOf(index) : 1;
  const coMapped: CoMappedWord[] = [];
  const toCoMapped = (i: number, relation: CoMappedWord["relation"]): CoMappedWord => ({
    wordId: verseWords[i]?.id ?? 0,
    wordInContext: verseWords[i]?.text ?? "",
    positionInVerse: positionOf(i),
    relation,
  });

  const isAdded = index >= 0 && isMockAdded(word, verseWords[index + 1]);
  if (isAdded) coMapped.push(toCoMapped(index + 1, "parent"));
  verseWords.forEach((other, i) => {
    if (i === index || isAdded) return;
    if (i === index - 1 && isMockAdded(other, word)) coMapped.push(toCoMapped(i, "addedWord"));
    else if (sharesOriginal(word, other)) coMapped.push(toCoMapped(i, "sameOriginal"));
  });
  coMapped.sort((a, b) => a.positionInVerse - b.positionInVerse);

  const readings: OriginalReading[] = [];
  const used = new Set<string>();
  word.senses.forEach((sense, senseIndex) => {
    const key = normalizeStrongs(sense.key);
    if (!key || used.has(key)) return;
    used.add(key);
    const entry = MOCK_READINGS[key];
    let reading: OriginalReading;
    if (entry) {
      reading = { ...entry };
      if (sense.form !== "" && normalizeOriginal(entry.original) !== sense.form) {
        reading.original = sense.form;
        delete reading.transliteration;
        delete reading.contextGloss;
        delete reading.morphology;
        delete reading.morphemes;
      }
    } else {
      reading = {
        stepWordId: -1,
        language: key.startsWith("G") ? "greek" : "hebrew",
        positionInVerse: senseIndex + 1,
        original: sense.form || key,
        strongs: key,
      };
    }
    const entity = MOCK_ENTITIES[key];
    if (entity) reading.entity = entity;
    const sharedWith = coMapped
      .filter((c) => {
        const other = verseWords.find((w) => w.id === c.wordId);
        return (
          c.relation === "sameOriginal" &&
          other?.senses.some((o) => o.form !== "" && o.key === sense.key && o.form === sense.form)
        );
      })
      .map((c) => c.wordId);
    if (sharedWith.length > 0) reading.sharedWithWordIds = sharedWith;
    readings.push(reading);
  });
  readings.sort((a, b) => a.positionInVerse - b.positionInVerse);

  const detail: GetWordDetailResponse = {
    wordId: word.id,
    wordInContext: word.text,
    positionInVerse: position,
    readings,
  };
  if (isAdded) detail.isAddedWord = true;
  if (readings.length > 0 || isAdded) detail.mapping = mockProvenance(word.id);
  if (coMapped.length > 0) detail.coMappedWords = coMapped;
  const hints: MappingHintKey[] = readings.length >= 3 ? ["manyOriginals"] : [];
  if (hints.length > 0) detail.hints = hints;
  if (readings.length > 0) detail.crossReferences = MOCK_CROSS_REFERENCES;

  return new Promise((resolve) => {
    setTimeout(() => resolve(detail), delayMs);
  });
}
