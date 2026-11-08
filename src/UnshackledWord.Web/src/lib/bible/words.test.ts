import { describe, expect, it } from "vitest";
import { fixtureChapterWords } from "@/test/fixtures/bibleStructure";
import { buildHighlights, groupWordsByVerse } from "./words";

describe("groupWordsByVerse", () => {
  const verses = groupWordsByVerse(fixtureChapterWords.words);

  it("groups by verse in order", () => {
    expect(verses.map((v) => v.verse)).toEqual([1, 2, 3]);
    expect(verses[0]?.words.map((w) => w.text)).toEqual(["Es", "war", "ein", "Mensch"]);
  });

  it("merges rows with the same word id into several senses", () => {
    const merged = groupWordsByVerse([
      {
        id: 1,
        verse: 1,
        positionInVerse: 1,
        wordInContext: "Gott",
        strongs: "H0430G",
        original: "אֱלֹהִ֑ים",
      },
      {
        id: 1,
        verse: 1,
        positionInVerse: 1,
        wordInContext: "Gott",
        strongs: "H0430G",
        original: "אֱלֹהִ֑ים",
      },
      { id: 1, verse: 1, positionInVerse: 1, wordInContext: "Gott", strongs: "H0001" },
    ]);
    expect(merged[0]?.words).toHaveLength(1);
    expect(merged[0]?.words[0]?.senses.map((s) => s.key)).toEqual(["H0430G", "H0001"]);
  });

  it("falls back to the lemma when there is no Strong's number", () => {
    const [verse] = groupWordsByVerse([
      {
        id: 1,
        verse: 1,
        positionInVerse: 1,
        wordInContext: "Wort",
        lemma: "λόγος",
        original: "λόγος,",
      },
    ]);
    expect(verse?.words[0]?.senses).toEqual([{ key: "λόγος", form: "λόγος" }]);
  });
});

describe("buildHighlights", () => {
  const verses = groupWordsByVerse(fixtureChapterWords.words);
  const find = (id: number) => verses.flatMap((v) => v.words).find((w) => w.id === id);

  it("marks equal forms as same and other forms as related", () => {
    const map = buildHighlights(find(2), verses);
    expect(map.get(2)).toBe("same");
    expect(map.get(10)).toBe("same");
    expect(map.get(11)).toBe("related");
    expect(map.has(4)).toBe(false);
  });

  it("returns nothing for words without senses or no hover", () => {
    expect(buildHighlights(find(1), verses).size).toBe(0);
    expect(buildHighlights(undefined, verses).size).toBe(0);
  });
});
