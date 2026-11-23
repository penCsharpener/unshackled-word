import { describe, expect, it } from "vitest";
import {
  checkGroup,
  filterForFinding,
  heatLevel,
  jumpTargets,
  SHARE_SCALE,
  splitContext,
  tileSeverity,
  visibleChecks,
} from "./checks";
import type { Finding } from "./types";

const finding: Finding = {
  check: "M3",
  severity: "warning",
  bibleBookId: 43,
  chapter: 3,
  verse: 16,
  elbWordId: 100,
  stepWordId: 7,
  lemma: "Gott",
  pos: "NOUN",
  strongs: "G2316",
  detail: "x",
};

describe("checks", () => {
  it("derives tile severity", () => {
    expect(tileSeverity({ count: 0, errorCount: 0 })).toBe("ok");
    expect(tileSeverity({ count: 3, errorCount: 1 })).toBe("error");
    expect(tileSeverity({ count: 3, errorCount: 0 })).toBe("warning");
  });

  it("groups checks and hides tier 3", () => {
    expect(checkGroup("L2")).toBe("lemma");
    expect(checkGroup("U1")).toBe("mapping");
    const shown = visibleChecks([
      { code: "M2", tier: 1, count: 1, errorCount: 0 },
      { code: "A1", tier: 3, count: 1, errorCount: 0 },
      { code: "L1", tier: 1, count: 1, errorCount: 0 },
    ]);
    expect(shown.map((c) => c.code)).toEqual(["L1", "M2"]);
  });

  it("builds jump targets from the data a finding has", () => {
    const hrefs = Object.fromEntries(jumpTargets(finding).map((j) => [j.kind, j.href]));
    expect(hrefs.mapping).toBe("/bible/43/3?map=16&w=100&s=7#v16");
    expect(hrefs.lemma).toBe("/edit/lemmas/43/3?word=100");
    expect(hrefs.bulk).toBe("/edit/lemmas/43/3?dlg=search&from=Gott&orig=G2316");
    expect(hrefs.strongs).toBe("/bible/strongs/G2316?rendering=Gott");
    expect(
      jumpTargets({ ...finding, elbWordId: undefined, lemma: undefined, strongs: undefined }),
    ).toEqual([]);
  });

  it("groups T checks as TSK curation and links source and target verse", () => {
    expect(checkGroup("T1")).toBe("tsk");
    const hrefs = Object.fromEntries(
      jumpTargets({
        ...finding,
        elbWordId: undefined,
        lemma: undefined,
        strongs: undefined,
        targetBibleBookId: 1,
        targetChapter: 2,
        targetVerse: 7,
      }).map((j) => [j.kind, j.href]),
    );
    expect(hrefs.verse).toBe("/bible/43/3#v16");
    expect(hrefs.target).toBe("/bible/1/2#v7");
  });

  it("filters by Strong's first, then by lemma", () => {
    expect(filterForFinding(finding)).toEqual({ side: "o", q: "G2316" });
    expect(filterForFinding({ ...finding, strongs: undefined })).toEqual({
      side: "d",
      q: "Gott~NOUN",
    });
    expect(filterForFinding({ ...finding, strongs: undefined, lemma: undefined })).toBeNull();
  });

  it("maps shares to heat levels 0-5", () => {
    expect(heatLevel(0, SHARE_SCALE)).toBe(0);
    expect(heatLevel(0.001, SHARE_SCALE)).toBe(1);
    expect(heatLevel(0.02, SHARE_SCALE)).toBe(2);
    expect(heatLevel(0.07, SHARE_SCALE)).toBe(3);
    expect(heatLevel(0.15, SHARE_SCALE)).toBe(4);
    expect(heatLevel(0.2, SHARE_SCALE)).toBe(5);
  });
});

describe("splitContext", () => {
  it("splits at the occurrence closest to the middle", () => {
    expect(splitContext("und siehe, es war sehr gut. Und siehe da", "siehe")).toEqual({
      left: "und ",
      right: ", es war sehr gut. Und siehe da",
    });
  });

  it("puts everything left when the word is empty or missing", () => {
    expect(splitContext("Tage, da sie", "")).toEqual({ left: "Tage, da sie", right: "" });
    expect(splitContext("Tage, da sie", "xyz")).toEqual({ left: "Tage, da sie", right: "" });
    expect(splitContext(undefined, "x")).toBeNull();
  });
});
