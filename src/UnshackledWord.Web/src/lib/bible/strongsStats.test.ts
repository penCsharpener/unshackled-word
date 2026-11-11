import { afterEach, describe, expect, it, vi } from "vitest";
import { bibleApi } from "./api";
import { parseBookFilter, parseRenderingFilter } from "./strongsOccurrences";
import {
  germanGloss,
  isShownStrongs,
  isStatsWord,
  originalTextAttributes,
  statsTestaments,
} from "./strongsStats";
import type { GetStrongsSummaryResponse } from "./types";

afterEach(() => vi.unstubAllGlobals());

function lastUrl(fetchMock: ReturnType<typeof vi.fn>): string {
  return String(fetchMock.mock.calls.at(-1)?.[0]);
}

describe("stats API calls", () => {
  it("builds the summary, word stats and filtered concordance URLs", async () => {
    const fetchMock = vi.fn(async () => new Response("{}"));
    vi.stubGlobal("fetch", fetchMock);

    await bibleApi.strongsSummary("H430G");
    expect(lastUrl(fetchMock)).toMatch(/\/elberfelder\/strongsSummary\/strongs\/H430G$/);

    await bibleApi.wordStats("Gerechtigkeit's", "ot");
    expect(lastUrl(fetchMock)).toMatch(
      /\/elberfelder\/wordStats\?word=Gerechtigkeit%27s&testament=ot$/,
    );

    await bibleApi.strongsOccurrences("G26", 2, 50);
    expect(lastUrl(fetchMock)).toMatch(/strongs\/G26\?page=2&pageSize=50$/);

    await bibleApi.strongsOccurrences("G26", 1, 50, { book: 44, rendering: "versammlung" });
    expect(lastUrl(fetchMock)).toMatch(
      /strongs\/G26\?page=1&pageSize=50&book=44&rendering=versammlung$/,
    );
  });
});

describe("filter params", () => {
  it("accepts books 1-66 only", () => {
    expect(parseBookFilter("44")).toBe(44);
    expect(parseBookFilter("0")).toBeUndefined();
    expect(parseBookFilter("67")).toBeUndefined();
    expect(parseBookFilter("4x")).toBeUndefined();
    expect(parseBookFilter(null)).toBeUndefined();
  });

  it("lower-cases renderings and rejects empty or long ones", () => {
    expect(parseRenderingFilter(" Versammlung ")).toBe("versammlung");
    expect(parseRenderingFilter("")).toBeUndefined();
    expect(parseRenderingFilter("a".repeat(51))).toBeUndefined();
    expect(parseRenderingFilter(null)).toBeUndefined();
  });
});

describe("helpers", () => {
  it("accepts a single German word for word stats", () => {
    expect(isStatsWord("Versammlung")).toBe(true);
    expect(isStatsWord("Über-Mensch")).toBe(true);
    expect(isStatsWord("zwei Wörter")).toBe(false);
    expect(isStatsWord("")).toBe(false);
  });

  it("builds the gloss from the most used renderings", () => {
    expect(germanGloss([])).toEqual([]);
    expect(
      germanGloss([
        { word: "Versammlung", key: "versammlung", count: 77 },
        { word: "Versammlungen", key: "versammlungen", count: 40 },
        { word: "Gemeinde", key: "gemeinde", count: 30 },
        { word: "die", key: "die", count: 1 },
      ]),
    ).toEqual(["Versammlung", "Versammlungen"]);
  });

  it("charts the home testament and the other one only if it is used", () => {
    const stat = (bibleBookId: number) => ({ bibleBookId, count: 1, verseCount: 1 });
    expect(statsTestaments("greek", [stat(44)])).toEqual(["nt"]);
    expect(statsTestaments("greek", [stat(44), stat(19)])).toEqual(["ot", "nt"]);
    expect(statsTestaments("hebrew", [stat(1)])).toEqual(["ot"]);
    expect(statsTestaments("aramaic", [stat(27), stat(40)])).toEqual(["ot", "nt"]);
    expect(statsTestaments("hebrew", [])).toEqual(["ot"]);
  });

  it("recognises the shown Strong's number among its variants", () => {
    const summary = { strongs: "G3004" } as GetStrongsSummaryResponse;
    expect(isShownStrongs("G3004G", summary)).toBe(true);
    expect(isShownStrongs("G3004", summary)).toBe(true);
    expect(isShownStrongs("G30041", summary)).toBe(false);
    expect(isShownStrongs("G3005", summary)).toBe(false);
    const lettered = { strongs: "H430G" } as GetStrongsSummaryResponse;
    expect(isShownStrongs("H430G", lettered)).toBe(true);
    expect(isShownStrongs("H430H", lettered)).toBe(false);
  });

  it("maps the original language to lang and dir", () => {
    expect(originalTextAttributes("greek")).toEqual({ lang: "el", dir: "ltr" });
    expect(originalTextAttributes("hebrew")).toEqual({ lang: "he", dir: "rtl" });
    expect(originalTextAttributes("aramaic")).toEqual({ lang: "arc", dir: "rtl" });
  });
});
