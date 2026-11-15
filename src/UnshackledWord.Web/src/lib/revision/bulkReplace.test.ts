import { beforeEach, describe, expect, it } from "vitest";
import {
  applySuggestion,
  BULK_REPLACE_KEY,
  buildQueue,
  openCount,
  plainForm,
  readStored,
  splitRef,
  writeStored,
} from "./bulkReplace";
import type { BulkReplaceRow } from "./types";

const row = (over: Partial<BulkReplaceRow>): BulkReplaceRow => ({
  hebRefId: 1002022,
  occurrence: 1,
  positionInVerse: 1,
  found: "Weib",
  contextBefore: "",
  contextAfter: "",
  strongs: [],
  targetDiffers: false,
  ...over,
});

describe("bulkReplace helpers", () => {
  it("splits a HebRefId into book, chapter and verse", () => {
    expect(splitRef(43003016)).toEqual({ bookId: 43, chapter: 3, verse: 16 });
  });

  it("compares tokens without punctuation and case", () => {
    expect(plainForm("(Weibe),")).toBe("weibe");
  });

  it("replaces the hit, keeps punctuation and carries the initial capital", () => {
    expect(applySuggestion("Und das Weib, sprach: weib", ["Weib"], [1], "Frau")).toBe(
      "Und das Frau, sprach: weib",
    );
    expect(applySuggestion("weib ging", ["Weib"], [1], "Frau")).toBe("Frau ging");
    expect(applySuggestion("(Weibes) Sohn", ["Weibes"], [1], "der Frau")).toBe("(Der Frau) Sohn");
  });

  it("replaces only the checked occurrences", () => {
    expect(applySuggestion("Weib und Weib", ["Weib"], [2], "Frau")).toBe("Weib und Frau");
  });

  it("changes nothing with an empty suggestion", () => {
    expect(applySuggestion("Weib", ["Weib"], [1], "  ")).toBe("Weib");
  });

  it("merges two hits of one verse into one queue entry", () => {
    const queue = buildQueue([
      row({ occurrence: 1, strongs: ["H802"] }),
      row({ occurrence: 2, found: "Weibe", strongs: ["H802", "H2181"], articleBefore: "ein" }),
      row({ hebRefId: 1001001 }),
    ]);
    expect(queue.map((q) => q.hebRefId)).toEqual([1001001, 1002022]);
    expect(queue[1]).toMatchObject({
      forms: ["Weib", "Weibe"],
      occurrences: [1, 2],
      strongs: ["H802", "H2181"],
      articleBefore: "ein",
      status: "pending",
    });
  });

  it("counts verses still open", () => {
    const base = buildQueue([row({})])[0];
    if (!base) throw new Error("no entry");
    expect(
      openCount([
        base,
        { ...base, status: "saved" },
        { ...base, status: "reviewed" },
        { ...base, status: "skipped" },
      ]),
    ).toBe(2);
  });
});

describe("bulkReplace storage", () => {
  beforeEach(() => localStorage.clear());

  it("round-trips the queue and survives broken storage content", () => {
    const stored = {
      filter: { word: "Weib", sameLemma: false, source: "a", target: "b", suggestion: "Frau" },
      queue: buildQueue([row({})]),
    };
    writeStored(stored);
    expect(readStored()).toEqual(stored);
    localStorage.setItem(BULK_REPLACE_KEY, "{broken");
    expect(readStored()).toBeUndefined();
    writeStored(undefined);
    expect(localStorage.getItem(BULK_REPLACE_KEY)).toBeNull();
  });
});
