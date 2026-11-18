import { describe, expect, it } from "vitest";
import {
  firstReferencePath,
  passageSpansParam,
  replaceTypedBook,
  viewOfParse,
} from "./referenceParse";

describe("viewOfParse", () => {
  it("treats plain words and unknown books as no reference", () => {
    expect(viewOfParse({ references: [], bookCandidates: [] }).skipsFullText).toBe(false);
    const unknown = viewOfParse({
      references: [],
      error: { code: "unknownBook", position: 0, args: {} },
      bookCandidates: [],
    });
    expect(unknown.error).toBeNull();
    expect(unknown.skipsFullText).toBe(false);
  });

  it("skips full text for a chapter or an error, but not for a whole book", () => {
    const book = { bookId: 18, bookOnly: true, display: "Hiob", groups: [] };
    expect(viewOfParse({ references: [book], bookCandidates: [] }).skipsFullText).toBe(false);
    expect(viewOfParse({ references: [book], bookCandidates: [] }).books).toHaveLength(1);
    const chapter = {
      bookId: 18,
      bookOnly: false,
      display: "Hiob 1",
      path: "/bible/18/1",
      groups: [],
    };
    expect(viewOfParse({ references: [chapter], bookCandidates: [] }).skipsFullText).toBe(true);
    expect(
      viewOfParse({
        references: [],
        error: { code: "chapterOutOfRange", position: 5, args: {} },
        bookCandidates: [],
      }).skipsFullText,
    ).toBe(true);
  });
});

describe("firstReferencePath", () => {
  it("takes the first reference with a path", () => {
    expect(
      firstReferencePath({
        references: [
          { bookOnly: true, display: "Hiob", groups: [] },
          { bookOnly: false, display: "Hiob 1", path: "/bible/18/1", groups: [] },
        ],
        bookCandidates: [],
      }),
    ).toBe("/bible/18/1");
    expect(firstReferencePath(undefined)).toBeUndefined();
  });
});

describe("replaceTypedBook", () => {
  it("replaces only the leading book word", () => {
    expect(replaceTypedBook("Phili 1", "Philipper")).toBe("Philipper 1");
    expect(replaceTypedBook("1. Kor 13,4", "1. Korinther")).toBe("1. Korinther 13,4");
    expect(replaceTypedBook("12", "X")).toBe("12");
  });
});

describe("passageSpansParam", () => {
  const ref = (spans: { sc: number; sv: number; ec: number; ev: number }[]) => ({
    bookId: 66,
    bookOnly: false,
    display: "x",
    groups: [],
    spans: spans.map((s) => ({
      startChapter: s.sc,
      startVerse: s.sv,
      endChapter: s.ec,
      endVerse: s.ev,
    })),
  });

  it("builds ref-id pairs and single ids", () => {
    expect(
      passageSpansParam(
        ref([
          { sc: 2, sv: 1, ec: 2, ev: 5 },
          { sc: 2, sv: 7, ec: 2, ev: 7 },
        ]),
      ),
    ).toBe("66002001-66002005,66002007");
  });

  it("skips whole chapters and references without spans", () => {
    expect(passageSpansParam(ref([{ sc: 2, sv: 0, ec: 2, ev: 999 }]))).toBeUndefined();
    expect(passageSpansParam({ bookId: 66, bookOnly: false, display: "x", groups: [] })).toBe(
      undefined,
    );
  });
});
