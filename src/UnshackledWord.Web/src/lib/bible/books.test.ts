import { describe, expect, it } from "vitest";
import type { BibleStructureBook } from "@/lib/bible/types";
import { fixtureBooks } from "@/test/fixtures/bibleStructure";
import {
  adjacentChapter,
  bookName,
  buildPickerGroups,
  chapterCount,
  chapterPath,
  parseChapterParams,
} from "./books";

const t = (key: string, opts?: Record<string, unknown>) =>
  `${key}:${Object.values(opts ?? {}).join(",")}`;
function byId(id: number): BibleStructureBook {
  const found = fixtureBooks.find((b) => b.id === id);
  if (!found) throw new Error(`fixture book ${id} missing`);
  return found;
}
const genesis = byId(1);
const exodus = byId(2);
const malachi = byId(39);
const matthew = byId(40);

describe("books helpers", () => {
  it("picks the name by language", () => {
    expect(bookName(genesis, "en")).toBe("Genesis");
    expect(bookName(genesis, "en-US")).toBe("Genesis");
    expect(bookName(genesis, "de")).toBe("1. Mose");
  });

  it("counts chapters and builds paths", () => {
    expect(chapterCount(genesis)).toBe(50);
    expect(chapterPath({ bookId: 43, chapter: 3 })).toBe("/bible/43/3");
  });

  it("parses params", () => {
    expect(parseChapterParams("1", "50", fixtureBooks)).toEqual({ bookId: 1, chapter: 50 });
    expect(parseChapterParams("1", "51", fixtureBooks)).toBeNull();
    expect(parseChapterParams("1", "0", fixtureBooks)).toBeNull();
    expect(parseChapterParams("abc", "1", fixtureBooks)).toBeNull();
    expect(parseChapterParams("1", "1.5", fixtureBooks)).toBeNull();
    expect(parseChapterParams("3", "1", fixtureBooks)).toBeNull();
    expect(parseChapterParams("3", "1")).toEqual({ bookId: 3, chapter: 1 });
    expect(parseChapterParams(undefined, "1")).toBeNull();
  });

  it("finds adjacent chapters across book boundaries", () => {
    expect(adjacentChapter(fixtureBooks, { bookId: 1, chapter: 2 }, 1)).toEqual({
      bookId: 1,
      chapter: 3,
    });
    expect(adjacentChapter(fixtureBooks, { bookId: 1, chapter: 50 }, 1)).toEqual({
      bookId: exodus.id,
      chapter: 1,
    });
    expect(adjacentChapter(fixtureBooks, { bookId: 2, chapter: 1 }, -1)).toEqual({
      bookId: 1,
      chapter: 50,
    });
    expect(adjacentChapter(fixtureBooks, { bookId: malachi.id, chapter: 4 }, 1)).toEqual({
      bookId: matthew.id,
      chapter: 1,
    });
    expect(adjacentChapter(fixtureBooks, { bookId: 1, chapter: 1 }, -1)).toBeNull();
    expect(adjacentChapter(fixtureBooks, { bookId: 66, chapter: 22 }, 1)).toBeNull();
    expect(adjacentChapter(fixtureBooks, { bookId: 99, chapter: 1 }, 1)).toBeNull();
  });

  it("builds picker groups and splits the Psalms into five", () => {
    const ot = buildPickerGroups(fixtureBooks, "ot", "de", t);
    expect(ot.map((g) => g.key)).toEqual(["1", "2", "19-1", "19-2", "19-3", "19-4", "19-5", "39"]);
    expect(ot[0]?.title).toBe("1. Mose");
    expect(ot[0]?.chapters).toHaveLength(50);
    expect(ot[3]?.chapters[0]).toBe(42);
    expect(ot[3]?.chapters.at(-1)).toBe(72);
    expect(ot[2]?.title).toBe("bible.picker.psalmsSection:Psalmen,1,1,41");
    expect(ot[7]?.chapters).toEqual([1, 2, 3, 4]);
    expect(buildPickerGroups(fixtureBooks, "nt", "en", t).map((g) => g.title)).toEqual([
      "Matthew",
      "Revelation",
    ]);
  });
});
