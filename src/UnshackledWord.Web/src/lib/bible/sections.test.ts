import { describe, expect, it } from "vitest";
import { BIBLE_SECTIONS, bookSection } from "./sections";

describe("bookSection", () => {
  it("maps book ids to their section", () => {
    expect(bookSection(1)?.key).toBe("law");
    expect(bookSection(19)?.key).toBe("wisdom");
    expect(bookSection(44)?.key).toBe("gospels");
    expect(bookSection(45)?.key).toBe("paul");
    expect(bookSection(66)?.key).toBe("revelation");
  });

  it("returns undefined outside 1-66", () => {
    expect(bookSection(0)).toBeUndefined();
    expect(bookSection(67)).toBeUndefined();
  });

  it("covers every book exactly once with slots 1-8", () => {
    for (let id = 1; id <= 66; id++) {
      expect(BIBLE_SECTIONS.filter((s) => id >= s.firstBookId && id <= s.lastBookId)).toHaveLength(
        1,
      );
    }
    expect(BIBLE_SECTIONS.map((s) => s.slot)).toEqual([1, 2, 3, 4, 5, 6, 7, 8]);
  });
});
