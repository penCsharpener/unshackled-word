import { describe, expect, it } from "vitest";
import { parseVerseSelection } from "./verseSelection";

const sorted = (value: string | null | undefined) =>
  [...parseVerseSelection(value)].sort((a, b) => a - b);

describe("parseVerseSelection", () => {
  it("expands ranges and single verses", () => {
    expect(sorted("1-5,7,9-10")).toEqual([1, 2, 3, 4, 5, 7, 9, 10]);
    expect(sorted("7")).toEqual([7]);
  });

  it("ignores a range that runs backwards", () => {
    expect(sorted("5-3")).toEqual([]);
  });

  it("ignores text that is not a verse list", () => {
    expect(sorted("x")).toEqual([]);
    expect(sorted("")).toEqual([]);
    expect(sorted(null)).toEqual([]);
    expect(sorted(undefined)).toEqual([]);
  });

  it("keeps the valid parts of a partly invalid list", () => {
    expect(sorted("x,2,5-3,4")).toEqual([2, 4]);
  });

  it("ignores verse 0 and numbers above 999", () => {
    expect(sorted("0")).toEqual([]);
    expect(sorted("0-2")).toEqual([]);
    expect(sorted("1000")).toEqual([]);
    expect(sorted("1-1000")).toEqual([]);
  });

  it("accepts spaces around the dash and the commas", () => {
    expect(sorted(" 1 - 2 , 4 ")).toEqual([1, 2, 4]);
  });

  it("does not repeat a verse named twice", () => {
    expect(sorted("1-3,2-4,3")).toEqual([1, 2, 3, 4]);
  });
});
