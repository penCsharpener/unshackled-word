import { afterEach, describe, expect, it, vi } from "vitest";
import type { WordToken } from "./types";
import { fetchWordDetail, mappingStatus, normalizeStrongs } from "./wordDetail";
import { mockProvenance, mockWordDetail } from "./wordDetail.mock";

describe("normalizeStrongs", () => {
  it.each([
    ["G0444", "G444"],
    ["G3056", "G3056"],
    ["{H7225G}", "H7225G"],
    ["H9003/{H7225G}", "H7225G"],
    ["h0430g_a", "H430G"],
    ["H0430G", "H430G"],
    ["", undefined],
    ["abc", undefined],
  ])("%j → %j", (input, expected) => {
    expect(normalizeStrongs(input)).toBe(expected);
  });
});

describe("mockWordDetail", () => {
  it("returns the exact form reading", async () => {
    const word: WordToken = { id: 2, text: "war", senses: [{ key: "G1510", form: "ἦν" }] };
    const detail = await mockWordDetail(word, 0);
    expect(detail.readings).toHaveLength(1);
    expect(detail.readings[0]?.original).toBe("ἦν");
    expect(detail.readings[0]?.morphology?.code).toBe("V-IAI-3S");
  });

  it("uses the token form when it differs", async () => {
    const word: WordToken = { id: 11, text: "ist", senses: [{ key: "G1510", form: "ἐστιν" }] };
    const reading = (await mockWordDetail(word, 0)).readings[0];
    expect(reading?.original).toBe("ἐστιν");
    expect(reading?.lemma).toBe("εἰμί");
    expect(reading?.morphology).toBeUndefined();
  });

  it("orders two readings by position", async () => {
    const word: WordToken = {
      id: 20,
      text: "ehe",
      senses: [
        { key: "G2228", form: "ἢ" },
        { key: "G4250", form: "πρὶν" },
      ],
    };
    const readings = (await mockWordDetail(word, 0)).readings;
    expect(readings.map((r) => r.original)).toEqual(["πρὶν", "ἢ"]);
  });

  it("returns morphemes for Hebrew", async () => {
    const word: WordToken = {
      id: 30,
      text: "Anfang",
      senses: [{ key: "H7225G", form: "בְּרֵאשִׁית" }],
    };
    const reading = (await mockWordDetail(word, 0)).readings[0];
    expect(reading?.morphemes).toHaveLength(2);
    expect(reading?.morphemes?.[0]?.role).toBe("prefix");
  });

  it("returns no readings without senses", async () => {
    const detail = await mockWordDetail({ id: 1, text: "Es", senses: [] }, 0);
    expect(detail.readings).toEqual([]);
    expect(detail.wordInContext).toBe("Es");
  });

  it("falls back for unknown Strong's numbers", async () => {
    const word: WordToken = { id: 4, text: "x", senses: [{ key: "G9999", form: "" }] };
    const reading = (await mockWordDetail(word, 0)).readings[0];
    expect(reading?.strongs).toBe("G9999");
    expect(reading?.stepWordId).toBe(-1);
  });
});

describe("mock mapping data", () => {
  const verse: WordToken[] = [
    { id: 40, text: "Und", senses: [{ key: "H776G", form: "וְהָאָרֶץ" }] },
    { id: 41, text: "die", senses: [{ key: "H776G", form: "וְהָאָרֶץ" }] },
    { id: 42, text: "Erde", senses: [{ key: "H776G", form: "וְהָאָרֶץ" }] },
    { id: 43, text: "war", senses: [{ key: "H1961", form: "הָיְתָה" }] },
    { id: 44, text: "der", senses: [] },
    { id: 45, text: "Tiefe", senses: [{ key: "H8415", form: "תְהוֹם" }] },
    { id: 46, text: "zu", senses: [] },
  ];
  const detailOf = (id: number) =>
    mockWordDetail(verse.find((w) => w.id === id) as WordToken, 0, verse);

  it("finds words sharing an original word and marks the shared reading", async () => {
    const detail = await detailOf(42);
    expect(detail.positionInVerse).toBe(3);
    expect(detail.coMappedWords?.map((w) => [w.wordInContext, w.relation])).toEqual([
      ["Und", "sameOriginal"],
      ["die", "sameOriginal"],
    ]);
    expect(detail.readings[0]?.sharedWithWordIds).toEqual([40, 41]);
    expect(detail.mapping).toBeDefined();
  });

  it("treats an article before a mapped word as added, with its parent", async () => {
    const added = await detailOf(44);
    expect(added.isAddedWord).toBe(true);
    expect(added.coMappedWords).toEqual([
      { wordId: 45, wordInContext: "Tiefe", positionInVerse: 6, relation: "parent" },
    ]);
    expect(added.mapping).toBeDefined();
    const parent = await detailOf(45);
    expect(parent.coMappedWords?.map((w) => w.relation)).toEqual(["addedWord"]);
  });

  it("leaves other words without senses unmapped", async () => {
    const detail = await detailOf(46);
    expect(detail.isAddedWord).toBeUndefined();
    expect(detail.mapping).toBeUndefined();
    expect(detail.coMappedWords).toBeUndefined();
  });

  it("hints at many original words", async () => {
    const word: WordToken = {
      id: 50,
      text: "Der",
      senses: [
        { key: "G1510", form: "ἦν" },
        { key: "G3056", form: "λόγος" },
        { key: "G4250", form: "πρὶν" },
      ],
    };
    expect((await mockWordDetail(word, 0)).hints).toEqual(["manyOriginals"]);
  });

  it("derives the mapping status", () => {
    expect(mappingStatus(mockProvenance(3))).toBe("manual");
    expect(mappingStatus(mockProvenance(4))).toBe("confirmed");
    expect(mappingStatus(mockProvenance(5))).toBe("aiSuggested");
    expect(mockProvenance(5).model).toBeDefined();
  });
});

describe("fetchWordDetail", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("calls the API for source api", async () => {
    const fetchMock = vi.fn(
      async (_input: RequestInfo | URL) =>
        new Response(JSON.stringify({ wordId: 2, wordInContext: "war", readings: [] })),
    );
    vi.stubGlobal("fetch", fetchMock);
    await fetchWordDetail({ id: 2, text: "war", senses: [] }, undefined, "api");
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain("/elberfelder/wordDetail/wordId/2");
  });
});
