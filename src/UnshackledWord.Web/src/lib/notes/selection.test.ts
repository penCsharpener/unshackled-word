import { describe, expect, it } from "vitest";
import type { VerseWords } from "@/lib/bible/types";
import type { Note } from "./api";
import { isWordSelected, noteMarkerWord, selectionTarget } from "./selection";

const base: VerseWords = {
  verse: 28,
  words: ["denen", "die", "Gott", "lieben"].map((text, i) => ({
    id: 100 + i,
    text,
    senses: [],
    position: i + 1,
    basePosition: i + 1,
  })),
};
const variant: VerseWords = {
  verse: 28,
  words: [
    { id: 0, revisionWordId: 1, text: "allen", senses: [], position: 1 },
    { id: 102, revisionWordId: 2, text: "Gott", senses: [], position: 2, basePosition: 3 },
    { id: 103, revisionWordId: 3, text: "lieben", senses: [], position: 3, basePosition: 4 },
  ],
  revised: { verse: 28, level: 300, revisionVerseId: 9 },
};
const wordsNote = (extra: Partial<Note> = {}): Note => ({
  id: "n",
  anchor: {
    kind: "words",
    bookId: 45,
    chapter: 8,
    startHebRefId: 45008028,
    startPosition: 3,
    endPosition: 4,
  },
  anchorState: "ok",
  bodyMarkdown: "",
  bodyLinked: "",
  visibility: "private",
  publicState: "none",
  isOwn: true,
  authorDisplayName: "Ada",
  createdAt: "2026-11-22T10:00:00Z",
  ...extra,
});

describe("note selection", () => {
  it("turns words of one verse into Elb1871 positions in either drag direction", () => {
    expect(
      selectionTarget({ anchor: { verse: 28, index: 3 }, focus: { verse: 28, index: 2 } }, [base]),
    ).toEqual({ kind: "words", verse: 28, startPosition: 3, endPosition: 4, quote: "Gott lieben" });
    expect(
      isWordSelected(
        { anchor: { verse: 28, index: 3 }, focus: { verse: 28, index: 2 } },
        { verse: 28, index: 2 },
      ),
    ).toBe(true);
    expect(
      isWordSelected(
        { anchor: { verse: 28, index: 3 }, focus: { verse: 28, index: 2 } },
        { verse: 28, index: 1 },
      ),
    ).toBe(false);
  });

  it("uses base positions of variant words and falls back to the verse for a variant-only word", () => {
    expect(
      selectionTarget({ anchor: { verse: 28, index: 1 }, focus: { verse: 28, index: 2 } }, [
        variant,
      ]),
    ).toMatchObject({ kind: "words", startPosition: 3, endPosition: 4 });
    expect(
      selectionTarget({ anchor: { verse: 28, index: 0 }, focus: { verse: 28, index: 1 } }, [
        variant,
      ]),
    ).toEqual({ kind: "verse", verse: 28 });
  });

  it("offers a verse range across verses", () => {
    expect(
      selectionTarget({ anchor: { verse: 30, index: 0 }, focus: { verse: 28, index: 2 } }, [base]),
    ).toEqual({ kind: "verses", verse: 28, endVerse: 30 });
  });

  it("puts the marker on the last anchored word, through mappedWords on a variant", () => {
    expect(noteMarkerWord(wordsNote(), base)).toBe("b103");
    expect(
      noteMarkerWord(
        wordsNote({
          mappedWords: [
            { revisionWordId: 2, positionInVerse: 2, basePositionInVerse: 3 },
            { revisionWordId: 3, positionInVerse: 3, basePositionInVerse: 4 },
          ],
        }),
        variant,
      ),
    ).toBe("r3");
  });

  it("keeps drifted, unmapped and verse notes at verse level", () => {
    expect(noteMarkerWord(wordsNote({ anchorState: "drifted" }), base)).toBeUndefined();
    expect(
      noteMarkerWord(wordsNote({ anchorState: "noBaseWords", mappedWords: [] }), variant),
    ).toBeUndefined();
    expect(noteMarkerWord(wordsNote(), variant)).toBeUndefined();
    expect(
      noteMarkerWord(
        wordsNote({ anchor: { kind: "verse", bookId: 45, chapter: 8, startHebRefId: 45008028 } }),
        base,
      ),
    ).toBeUndefined();
  });
});
