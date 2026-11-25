import { describe, expect, it } from "vitest";
import { strongsFlashKeys } from "./tskFlash";
import type { CrossReference, GetVerseCrossReferenceTextsResponse } from "./types";

type Entry = GetVerseCrossReferenceTextsResponse["references"][number];

const entry = (
  verse: number,
  strongs: (string[] | undefined)[],
  extra: Partial<CrossReference> = {},
): Entry =>
  ({
    bibleBookId: 43,
    chapter: 1,
    verse,
    ...extra,
    verses: strongs.map((s, i) => ({ verse: verse + i, text: "", ...(s && { strongs: s }) })),
  }) as Entry;

const keyOf = (r: CrossReference) => `${r.verse}`;

describe("strongsFlashKeys", () => {
  it("flashes a link whose target verse has a key of the hovered word", () => {
    const refs = [entry(1, [["G0746", "G2316"]]), entry(5, [["G0025"]])];
    expect([...strongsFlashKeys(["G2316"], refs, keyOf)]).toEqual(["1"]);
  });

  it("flashes nothing without a match", () => {
    expect(strongsFlashKeys(["G9999"], [entry(1, [["G0746"]])], keyOf).size).toBe(0);
  });

  it("matches in any verse of a range", () => {
    const refs = [entry(3, [["G0001"], ["G0025"]])];
    expect([...strongsFlashKeys(["G0025"], refs, keyOf)]).toEqual(["3"]);
  });

  it("never flashes hidden links", () => {
    const refs = [entry(1, [["G0025"]], { rating: "hide" })];
    expect(strongsFlashKeys(["G0025"], refs, keyOf).size).toBe(0);
  });

  it("copes with verses without strongs, no senses and no data", () => {
    expect(strongsFlashKeys(["G0025"], [entry(1, [undefined])], keyOf).size).toBe(0);
    expect(strongsFlashKeys([], [entry(1, [["G0025"]])], keyOf).size).toBe(0);
    expect(strongsFlashKeys(["G0025"], undefined, keyOf).size).toBe(0);
  });
});
