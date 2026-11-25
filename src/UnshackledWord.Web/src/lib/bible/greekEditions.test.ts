import { afterEach, describe, expect, it } from "vitest";
import { filterWordsByEdition, GREEK_EDITION_KEY, readGreekEdition } from "./greekEditions";

const all = ["NA28", "NA27", "Tyn", "SBL", "WH", "Treg", "TR", "Byz"];
const words = [
  { id: 1, original: "a" },
  { id: 2, original: "b", editionCodes: ["TR", "Byz"] },
  { id: 3, original: "c", editionCodes: all.filter((c) => c !== "Byz") },
  { id: 4, original: "d", editionCodes: ["TR", "Byz"] },
];

afterEach(() => localStorage.clear());

describe("filterWordsByEdition", () => {
  it("keeps everything without a selection", () => {
    expect(filterWordsByEdition(words, null)).toEqual({ visible: words, hidden: 0 });
  });

  it("hides words that only TR and Byz have under NA28", () => {
    const result = filterWordsByEdition(words, "NA28");
    expect(result.visible.map((w) => w.id)).toEqual([1, 3]);
    expect(result.hidden).toBe(2);
  });

  it("keeps them under TR", () => {
    const result = filterWordsByEdition(words, "TR");
    expect(result.visible.map((w) => w.id)).toEqual([1, 2, 3, 4]);
    expect(result.hidden).toBe(0);
  });

  it("always keeps a word without editionCodes and does not touch Hebrew input", () => {
    const hebrew = [
      { original: "x", editionCodes: undefined },
      { original: "y" } as { editionCodes?: string[] },
    ];
    expect(filterWordsByEdition(hebrew, "NA28")).toEqual({ visible: hebrew, hidden: 0 });
  });

  it("does not change the order", () => {
    expect(filterWordsByEdition(words, "Byz").visible.map((w) => w.id)).toEqual([1, 2, 4]);
  });
});

describe("stored edition", () => {
  it("defaults to null and reads a stored code", () => {
    expect(readGreekEdition()).toBeNull();
    localStorage.setItem(GREEK_EDITION_KEY, "TR");
    expect(readGreekEdition()).toBe("TR");
  });
});
