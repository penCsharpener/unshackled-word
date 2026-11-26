import { afterEach, describe, expect, it } from "vitest";
import {
  hasLxx,
  LXX_FORM_KEY,
  lxxBlocks,
  lxxOriginalWords,
  lxxReference,
  readLxxForm,
  unitsOfForm,
} from "./lxx";
import type { BibleStructureBook, LxxUnit } from "./types";

afterEach(() => localStorage.clear());

const books = [
  { id: 24, nameDe: "Jeremia", nameEn: "Jeremiah", abbreviationDe: "Jer", abbreviationEn: "Jer" },
  {
    id: 11,
    nameDe: "1. Könige",
    nameEn: "1 Kings",
    abbreviationDe: "1Kön",
    abbreviationEn: "1Kgs",
  },
] as BibleStructureBook[];

const unit = (lxxVerseId: number, extra: Partial<LxxUnit> = {}): LxxUnit => ({
  lxxVerseId,
  bibleBookId: 24,
  chapter: 32,
  verse: 1,
  subverse: 0,
  ref: "Jer.32:1",
  textForm: "",
  mapKind: "one",
  isAddition: false,
  differsFromHebrew: true,
  words: [],
  ...extra,
});

describe("lxx helpers", () => {
  it("offers the LXX for Old Testament books only", () => {
    expect(hasLxx(1)).toBe(true);
    expect(hasLxx(39)).toBe(true);
    expect(hasLxx(40)).toBe(false);
  });

  it("formats the Rahlfs reference per language", () => {
    expect(lxxReference(unit(1), books, "de")).toBe("Jer 32,1");
    expect(lxxReference(unit(1), books, "en")).toBe("Jer 32:1");
    expect(
      lxxReference(unit(1, { bibleBookId: 11, chapter: 2, verse: 35, subverse: 1 }), books, "de"),
    ).toBe("1Kön 2,35.1");
    expect(lxxReference(unit(1), undefined, "de")).toBe("24 32,1");
  });

  it("groups consecutive additions and keeps the reading order", () => {
    const a1 = unit(1, { isAddition: true, subverse: 1 });
    const a2 = unit(2, { isAddition: true, subverse: 2 });
    const main = unit(3);
    const a3 = unit(4, { isAddition: true });
    expect(lxxBlocks([a1, a2, main, a3])).toEqual([
      { kind: "addition", units: [a1, a2] },
      { kind: "unit", unit: main },
      { kind: "addition", units: [a3] },
    ]);
  });

  it("keeps units of the chosen Daniel form and those without a form", () => {
    const og = unit(1, { textForm: "og" });
    const th = unit(2, { textForm: "th" });
    const none = unit(3);
    expect(unitsOfForm([og, th, none], "th")).toEqual([th, none]);
    expect(unitsOfForm([og, th, none], "og")).toEqual([og, none]);
  });

  it("numbers the words on across units", () => {
    const words = lxxOriginalWords([
      unit(1, {
        words: [
          { id: 11, position: 2, greek: "β" },
          { id: 10, position: 1, greek: "α" },
        ],
      }),
      unit(2, { words: [{ id: 20, position: 1, greek: "γ" }] }),
    ]);
    expect(words.map((w) => [w.position, w.original])).toEqual([
      [1, "α"],
      [2, "β"],
      [3, "γ"],
    ]);
  });

  it("defaults the Daniel form to Theodotion", () => {
    expect(readLxxForm()).toBe("th");
    localStorage.setItem(LXX_FORM_KEY, "bogus");
    expect(readLxxForm()).toBe("th");
    localStorage.setItem(LXX_FORM_KEY, "og");
    expect(readLxxForm()).toBe("og");
  });
});
