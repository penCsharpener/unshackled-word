import { describe, expect, it } from "vitest";
import { unitA, unitB, unitC, unitD } from "@/test/lxxReviewFixtures";
import {
  buildConfirmRequest,
  initialUnitDraft,
  initialWordDraft,
  isUnitDirty,
  type LxxReviewUnit,
  type LxxReviewWord,
  lexiconQueryReady,
  reconcileDraft,
  safeSelection,
  safeUnits,
  selectionRange,
  type UnitDraft,
  validateWordDraft,
  type WordDraft,
} from "./lxxReview";

const wordOf = (d: UnitDraft, id: number): WordDraft => d.words.get(id) as WordDraft;
const draftOf = (unit: LxxReviewUnit, patch: (d: UnitDraft) => void = () => {}) => {
  const draft = initialUnitDraft(unit);
  patch(draft);
  return draft;
};

describe("validateWordDraft", () => {
  const ok = { lemma: "καί", strongs: "G2532", morphology: "CONJ", hebrewWordIds: [] };

  it("accepts none or all of lemma, Strong's and code", () => {
    expect(validateWordDraft({ ...ok, lemma: "", strongs: "", morphology: "" }, undefined)).toEqual(
      [],
    );
    expect(validateWordDraft(ok, true)).toEqual([]);
    expect(validateWordDraft({ ...ok, morphology: "" }, undefined)).toEqual(["partial"]);
  });

  it("allows five-digit Strong's numbers and rejects other shapes", () => {
    expect(validateWordDraft({ ...ok, strongs: "G20969" }, true)).toEqual([]);
    expect(validateWordDraft({ ...ok, strongs: "G746" }, true)).toEqual(["strongs"]);
    expect(validateWordDraft({ ...ok, strongs: "H0746" }, true)).toEqual(["strongs"]);
  });

  it("reports the live check's verdict only when it is known", () => {
    expect(validateWordDraft(ok, false)).toEqual(["morphology"]);
    expect(validateWordDraft(ok, undefined)).toEqual([]);
  });
});

describe("lexiconQueryReady", () => {
  it("needs one digit after G or two characters", () => {
    expect(lexiconQueryReady("G7")).toBe(true);
    expect(lexiconQueryReady("g0746")).toBe(true);
    expect(lexiconQueryReady("α")).toBe(false);
    expect(lexiconQueryReady("ἀρ")).toBe(true);
  });
});

describe("buildConfirmRequest", () => {
  it("sends every word, trims, upper-cases the code and keeps links-only words without tag fields", () => {
    const draft = draftOf(unitA, (d) => {
      d.words.set(1001, {
        lemma: " καί ",
        strongs: "g2532",
        morphology: "conj ",
        hebrewWordIds: [502, 501],
      });
      d.words.set(1002, { lemma: "", strongs: "", morphology: "", hebrewWordIds: [] });
    });
    expect(buildConfirmRequest(unitA, draft)).toEqual({
      lxxVerseId: 101,
      proposalId: 11,
      words: [
        {
          lxxWordId: 1001,
          lemma: "καί",
          strongs: "G2532",
          morphology: "CONJ",
          hebrewWordIds: [501, 502],
        },
        { lxxWordId: 1002, hebrewWordIds: [] },
      ],
    });
  });

  it("drops Hebrew ids that are not in the unit", () => {
    const draft = draftOf(unitB, (d) => {
      d.words.set(1003, { ...wordOf(d, 1003), hebrewWordIds: [503, 999] });
    });
    expect(buildConfirmRequest(unitB, draft).words[0]?.hebrewWordIds).toEqual([503]);
  });
});

describe("initialWordDraft", () => {
  it("prefers the AI answer, then the stored tag, then empty with the stored links", () => {
    expect(initialWordDraft(unitA.words[1] as LxxReviewWord).strongs).toBe("G1096");
    expect(initialWordDraft(unitD.words[0] as LxxReviewWord)).toMatchObject({
      strongs: "G5043",
      hebrewWordIds: [],
    });
    expect(
      initialWordDraft({
        id: 1,
        position: 1,
        greek: "x",
        hebrewWordIds: [7],
        linksConfirmed: false,
      }),
    ).toEqual({
      lemma: "",
      strongs: "",
      morphology: "",
      hebrewWordIds: [7],
    });
  });
});

describe("safeSelection", () => {
  const units = [unitA, unitB, unitC, unitD];
  const visible = new Set([101, 102, 103, 104]);

  it("lists only AI-high unconfirmed words of visible units with a proposal and counts the low ones", () => {
    const drafts = new Map(units.map((u) => [u.lxxVerseId, initialUnitDraft(u)]));
    const selection = safeSelection(units, drafts, visible);
    expect(selection.units).toEqual([
      { lxxVerseId: 101, proposalId: 11, lxxWordIds: [1001] },
      { lxxVerseId: 102, proposalId: 12, lxxWordIds: [1003] },
    ]);
    expect(selection.counts).toEqual({ words: 2, units: 2, low: 2, dirty: 0 });
  });

  it("leaves out dirty and conflicting units and units that are not visible", () => {
    const dirty = draftOf(unitA, (d) => {
      d.words.set(1001, { ...wordOf(d, 1001), hebrewWordIds: [] });
    });
    const conflict = { ...initialUnitDraft(unitB), saveState: "conflict" as const };
    const drafts = new Map([
      [101, dirty],
      [102, conflict],
    ]);
    expect(isUnitDirty(unitA, dirty)).toBe(true);
    const selection = safeSelection([unitA, unitB, unitC], drafts, new Set([101, 102]));
    expect(selection.units).toEqual([]);
    expect(selection.counts.dirty).toBe(2);
    expect(safeUnits([unitA, unitB], drafts, new Set([103]))).toEqual([]);
  });
});

describe("selectionRange", () => {
  const units = [unitA, unitB, unitC, unitD];

  it("returns null without a selection", () => {
    expect(selectionRange(8, units, new Set())).toBeNull();
  });

  it("makes one block for neighbours and one per gap, never including an unselected verse", () => {
    const range = selectionRange(8, units, new Set([101, 102, 104]));
    expect(range?.blocks).toEqual([
      { fromChapter: 1, fromVerse: 1, toChapter: 1, toVerse: 2 },
      { fromChapter: 1, fromVerse: 5, toChapter: 1, toVerse: 5 },
    ]);
    expect(range?.initial).toEqual({
      book: 8,
      fromChapter: 1,
      fromVerse: 1,
      toChapter: 1,
      toVerse: 2,
    });
    expect(range?.confirmedUnits).toBe(1);
    expect(selectionRange(8, units, new Set([101, 103]))?.blocks.map((b) => b.fromVerse)).toEqual([
      1, 3,
    ]);
  });

  it("makes one block of neighbouring selected verses", () => {
    // 1,2,3 selected, 5 not: one block 1-3
    expect(selectionRange(8, units, new Set([101, 102, 103]))?.blocks).toEqual([
      { fromChapter: 1, fromVerse: 1, toChapter: 1, toVerse: 3 },
    ]);
  });
});

describe("reconcileDraft", () => {
  const changed: LxxReviewUnit = {
    ...unitA,
    proposal: { ...(unitA.proposal as NonNullable<LxxReviewUnit["proposal"]>), id: 99 },
  };

  it("keeps an unchanged unit's draft and resets an untouched one when the proposal changes", () => {
    const draft = initialUnitDraft(unitA);
    expect(reconcileDraft(draft, unitA, unitA)).toBe(draft);
    expect(reconcileDraft(draft, unitA, changed).proposalId).toBe(99);
  });

  it("turns a draft with unsaved edits into a conflict and keeps the edits", () => {
    const edited = draftOf(unitA, (d) => {
      d.words.set(1001, { ...wordOf(d, 1001), lemma: "x" });
    });
    const result = reconcileDraft(edited, unitA, changed);
    expect(result.saveState).toBe("conflict");
    expect(result.words.get(1001)?.lemma).toBe("x");
  });

  it("follows the server after the unit's own save", () => {
    const saved = { ...initialUnitDraft(unitA), saveState: "saved" as const };
    const result = reconcileDraft(saved, unitA, changed);
    expect(result.saveState).toBe("saved");
    expect(result.proposalId).toBe(99);
  });
});
