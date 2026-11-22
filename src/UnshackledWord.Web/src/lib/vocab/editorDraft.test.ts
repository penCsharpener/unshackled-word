import { afterEach, describe, expect, it, vi } from "vitest";
import type { MeaningRow } from "@/components/MeaningListEditor";
import { json, stubApi } from "@/test/authHelpers";
import { buildSavePlan, type EditorDraft, saveVocabEntryPlan } from "./editorDraft";

const mitte: MeaningRow = {
  id: 101,
  german: "Mitte",
  count: 40,
  share: 69,
  isAccepted: true,
  origin: "derived",
  isPinned: false,
};

const wort: MeaningRow = {
  id: 102,
  german: "Wort",
  count: 1,
  share: 1.7,
  isAccepted: true,
  origin: "derived",
  isPinned: false,
};

const inmitten: MeaningRow = {
  german: "inmitten",
  count: null,
  share: null,
  isAccepted: true,
  origin: "manual",
  isPinned: false,
};

const base: EditorDraft = {
  dictionaryForm: "μέσος",
  transliteration: "mesos",
  reviewed: false,
  meanings: [mitte, wort],
};

/** The draft with other rows; `base` itself stays untouched. */
function withMeanings(meanings: MeaningRow[], over: Partial<EditorDraft> = {}): EditorDraft {
  return { ...base, ...over, meanings };
}

afterEach(() => vi.unstubAllGlobals());

describe("buildSavePlan text override", () => {
  it("sends a changed override, and a blank one when it is removed", () => {
    const withOverride: EditorDraft = {
      ...base,
      meanings: [{ ...mitte, germanOverride: "Mitte, Zentrum" }, wort],
    };
    const set = buildSavePlan(base, withOverride);
    expect(set.changed).toEqual([{ id: 101, body: { germanOverride: "Mitte, Zentrum" } }]);
    expect(set.learningRelevant).toBe(true);
    const cleared = buildSavePlan(withOverride, base);
    expect(cleared.changed).toEqual([{ id: 101, body: { germanOverride: "" } }]);
    expect(buildSavePlan(withOverride, withOverride).count).toBe(0);
  });
});

describe("buildSavePlan", () => {
  it("has nothing to save for an unchanged draft", () => {
    const plan = buildSavePlan(base, withMeanings([mitte, wort]));
    expect(plan.count).toBe(0);
    expect(plan.entry).toBeNull();
    expect(plan.learningRelevant).toBe(false);
  });

  it("sends all three entry fields when the form changes", () => {
    const plan = buildSavePlan(base, { ...base, dictionaryForm: "μέσος, -η, -ον" });
    expect(plan.entry).toEqual({
      dictionaryForm: "μέσος, -η, -ον",
      transliteration: "mesos",
      reviewed: false,
    });
    expect(plan.count).toBe(1);
    expect(plan.learningRelevant).toBe(true);
  });

  it("does not count a reviewed flag as learning-relevant", () => {
    const plan = buildSavePlan(base, { ...base, reviewed: true });
    expect(plan.entry).toEqual({
      dictionaryForm: "μέσος",
      transliteration: "mesos",
      reviewed: true,
    });
    expect(plan.count).toBe(1);
    expect(plan.learningRelevant).toBe(false);
  });

  it("sends only the changed pin of a saved meaning", () => {
    const plan = buildSavePlan(base, withMeanings([{ ...mitte, isPinned: true }, wort]));
    expect(plan.changed).toEqual([{ id: 101, body: { isPinned: true } }]);
    expect(plan.count).toBe(1);
  });

  it("sends an unaccepted meaning as isAccepted false", () => {
    const plan = buildSavePlan(base, withMeanings([mitte, { ...wort, isAccepted: false }]));
    expect(plan.changed).toEqual([{ id: 102, body: { isAccepted: false } }]);
  });

  it("lists dropped meanings for deletion and new ones for creation", () => {
    const plan = buildSavePlan(base, withMeanings([mitte, inmitten]));
    expect(plan.removed).toEqual([102]);
    expect(plan.added.map((row) => row.german)).toEqual(["inmitten"]);
    expect(plan.count).toBe(2);
  });

  it("flags a reordered list and keeps the draft order in the refs", () => {
    const plan = buildSavePlan(base, withMeanings([wort, mitte]));
    expect(plan.orderChanged).toBe(true);
    expect(plan.refs).toEqual([{ id: 102 }, { id: 101 }]);
    expect(plan.count).toBe(1);
  });
});

describe("saveVocabEntryPlan", () => {
  it("creates a new meaning first and then puts the wanted order", async () => {
    const fetchMock = stubApi({
      "POST /maintainer/vocab/entries/56/meanings": () =>
        json({ entryId: 56, contentVersion: 4, bumped: true, learners: 0, meaningId: 201 }),
      "PUT /maintainer/vocab/entries/56/meanings/order": () =>
        json({ entryId: 56, contentVersion: 5, bumped: true, learners: 0 }),
    });
    await saveVocabEntryPlan(56, buildSavePlan(base, withMeanings([inmitten, mitte, wort])));

    const order = fetchMock.mock.calls.find(
      ([url, init]) => init?.method === "PUT" && String(url).endsWith("/order"),
    );
    expect(JSON.parse(String(order?.[1]?.body))).toEqual({ meaningIds: [201, 101, 102] });
  });
});
