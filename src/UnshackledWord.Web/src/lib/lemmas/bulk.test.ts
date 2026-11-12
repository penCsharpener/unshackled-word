import { describe, expect, it } from "vitest";
import {
  addFrom,
  type BulkQuery,
  clampPage,
  defaultSelection,
  deselectedOriginalCount,
  deselectRows,
  effectiveGroupMode,
  flattenPage,
  groupKey,
  groupState,
  isSelected,
  leftOutOriginals,
  memberKey,
  membersState,
  origKeyOfGroupKey,
  pageAfterSizeChange,
  parseBulkUrl,
  planIds,
  queryProblem,
  resolveIds,
  rowsBetween,
  selectAll,
  selectedInGroup,
  selectedTotal,
  selectNone,
  setGroup,
  staleDetailsOf,
  toggleGroup,
  toggleMembers,
  togglePage,
  toggleRow,
  withBulkUrl,
} from "./bulk";
import type { BulkGroupCount, BulkOccurrence, BulkOriginalCount } from "./bulkTypes";

const groups: BulkGroupCount[] = [
  { plainWord: "Jesu", partOfSpeech: "PROPN", count: 5 },
  { plainWord: "Jesum", partOfSpeech: "PROPN", count: 3 },
];
const keyJesu = groupKey("Jesu", "PROPN");
const keyJesum = groupKey("Jesum", "PROPN");
const ref = (id: number, key = keyJesu) => ({ id, key });

function occ(
  wordId: number,
  plainWord: string,
  patch: Partial<BulkOccurrence> = {},
): BulkOccurrence {
  return {
    wordId,
    bookId: 40,
    chapter: 1,
    verse: 1,
    positionInVerse: 1,
    lemma: plainWord,
    wordInContext: plainWord,
    plainWord,
    partOfSpeech: "PROPN",
    left: "",
    right: "",
    ...patch,
  };
}

const query: BulkQuery = {
  from: ["Jesu", "Jesum"],
  to: "Jesus",
  pos: "PROPN",
  original: ["G4680", "added"],
  scope: "book",
  testament: "nt",
  bookId: 43,
  chapter: 1,
};

describe("selection across pages", () => {
  it("starts with everything or nothing selected, counted from the group counts", () => {
    expect(selectedTotal(selectAll(), groups)).toBe(8);
    expect(selectedTotal(selectNone(), groups)).toBe(0);
  });

  it("keeps a deselected row after paging and counts it", () => {
    let sel = toggleRow(selectAll(), ref(11));
    expect(isSelected(sel, 11, keyJesu)).toBe(false);
    expect(isSelected(sel, 12, keyJesu)).toBe(true);
    expect(selectedTotal(sel, groups)).toBe(7);
    sel = toggleRow(sel, ref(11));
    expect(selectedTotal(sel, groups)).toBe(8);
    expect(sel.flips.size).toBe(0);
  });

  it("derives tri-state group checkboxes", () => {
    let sel = selectAll();
    expect(groupState(sel, groups[0] as BulkGroupCount)).toBe("checked");
    sel = toggleRow(sel, ref(11));
    expect(groupState(sel, groups[0] as BulkGroupCount)).toBe("mixed");
    expect(groupState(sel, groups[1] as BulkGroupCount)).toBe("checked");
    sel = toggleGroup(sel, groups[0] as BulkGroupCount); // mixed -> all selected
    expect(groupState(sel, groups[0] as BulkGroupCount)).toBe("checked");
    sel = toggleGroup(sel, groups[0] as BulkGroupCount); // checked -> none
    expect(groupState(sel, groups[0] as BulkGroupCount)).toBe("unchecked");
    expect(selectedTotal(sel, groups)).toBe(3);
  });

  it("a group override resets row flips of that group only", () => {
    let sel = toggleRow(toggleRow(selectAll(), ref(11)), ref(21, keyJesum));
    sel = setGroup(sel, keyJesu, false);
    expect(sel.flips.has(11)).toBe(false);
    expect(sel.flips.has(21)).toBe(true);
    expect(selectedInGroup(sel, groups[0] as BulkGroupCount)).toBe(0);
    expect(selectedInGroup(sel, groups[1] as BulkGroupCount)).toBe(2);
  });

  it("selects a page unless it is fully selected already, then clears it", () => {
    const page = [ref(1), ref(2), ref(3)];
    let sel = togglePage(selectNone(), page);
    expect(page.every((r) => isSelected(sel, r.id, r.key))).toBe(true);
    expect(selectedTotal(sel, groups)).toBe(3);
    sel = togglePage(sel, page);
    expect(selectedTotal(sel, groups)).toBe(0);
    sel = togglePage(toggleRow(selectAll(), ref(2)), page);
    expect(isSelected(sel, 2, keyJesu)).toBe(true);
  });

  it("deselects stale rows and keeps the rest", () => {
    const sel = deselectRows(selectAll(), [ref(4), ref(22, keyJesum)]);
    expect(selectedTotal(sel, groups)).toBe(6);
  });

  it("selects a range of a page", () => {
    expect(rowsBetween([10, 11, 12, 13, 14], 3, 1)).toEqual([11, 12, 13]);
    expect(rowsBetween([10, 11], 0, 9)).toEqual([10, 11]);
  });

  it("plans which ids to fetch for explicit apply ids", () => {
    const flipped = toggleRow(selectAll(), ref(11));
    const plan = planIds(flipped, groups);
    expect(plan.fetchAll).toBe(true);
    expect(resolveIds(plan, [{ ids: [10, 11, 12], origKey: "" }])).toEqual({
      wordIds: [10, 12],
      expectedOriginal: ["", ""],
    });

    const none = toggleRow(selectNone(), ref(7));
    const nonePlan = planIds(none, groups);
    expect(nonePlan.fetchAll).toBe(false);
    expect(nonePlan.fetchGroups).toEqual([]);
    expect(resolveIds(nonePlan, [])).toEqual({ wordIds: [7], expectedOriginal: [""] });

    const oneGroup = setGroup(selectNone(), keyJesum, true);
    const groupPlan = planIds(oneGroup, groups);
    expect(groupPlan.fetchGroups).toEqual([keyJesum]);
    expect(resolveIds(groupPlan, [{ ids: [21, 22], origKey: "" }]).wordIds).toEqual([21, 22]);
  });
});

describe("paging", () => {
  it("clamps pages and keeps the first row when the size changes", () => {
    expect(clampPage(9, 120, 50)).toBe(3);
    expect(clampPage(0, 120, 50)).toBe(1);
    expect(clampPage(2, 0, 50)).toBe(1);
    expect(pageAfterSizeChange(3, 50, 100)).toBe(2); // rows 100.. now on page 2
    expect(pageAfterSizeChange(1, 100, 500)).toBe(1);
  });
});

describe("URL state", () => {
  it("round-trips a review link, keeping other parameters", () => {
    const base = new URLSearchParams("foo=1");
    const url = withBulkUrl(base, { step: "review", query, page: 3, size: 250 });
    expect(url.get("foo")).toBe("1");
    const parsed = parseBulkUrl(new URLSearchParams(url.toString()));
    expect(parsed).toEqual({ step: "review", query, page: 3, size: 250, group: null });
  });

  it("falls back to defaults for invalid paging values", () => {
    const params = withBulkUrl(new URLSearchParams(), { step: "review", query });
    params.set("page", "-4");
    params.set("size", "77");
    const parsed = parseBulkUrl(params);
    expect(parsed.page).toBe(1);
    expect(parsed.size).toBe(100);
  });

  it("opens the search step when a review link has no usable search", () => {
    expect(parseBulkUrl(new URLSearchParams("dlg=review&page=2")).step).toBe("search");
    expect(parseBulkUrl(new URLSearchParams("dlg=review&from=a&to=a")).step).toBe("search");
    expect(parseBulkUrl(new URLSearchParams("dlg=review&from=a&to=b&scope=book")).step).toBe(
      "search",
    );
  });

  it("is closed without a dlg parameter and ignores unknown steps", () => {
    expect(parseBulkUrl(new URLSearchParams("")).step).toBeNull();
    expect(parseBulkUrl(new URLSearchParams("dlg=nope")).step).toBeNull();
    expect(parseBulkUrl(new URLSearchParams("dlg=search")).step).toBe("search");
  });

  it("carries the original filter and the grouping choice in the URL", () => {
    const url = withBulkUrl(new URLSearchParams(), { step: "review", query, group: "original" });
    expect(url.getAll("orig")).toEqual(["G4680", "added"]);
    expect(url.get("grp")).toBe("original");
    const parsed = parseBulkUrl(url);
    expect(parsed.query?.original).toEqual(["G4680", "added"]);
    expect(parsed.group).toBe("original");
    expect(
      parseBulkUrl(withBulkUrl(new URLSearchParams(), { step: "review", query })).group,
    ).toBeNull();
  });

  it("drops invalid original keys and unknown grouping values", () => {
    const params = withBulkUrl(new URLSearchParams(), { step: "review", query });
    params.append("orig", "bogus");
    params.set("grp", "sideways");
    const parsed = parseBulkUrl(params);
    expect(parsed.query?.original).toEqual(["G4680", "added"]);
    expect(parsed.group).toBeNull();
  });

  it("repeats from and drops scope parameters that do not apply", () => {
    const params = withBulkUrl(new URLSearchParams(), {
      step: "review",
      query: { ...query, scope: "all" },
    });
    expect(params.getAll("from")).toEqual(["Jesu", "Jesum"]);
    expect(params.has("book")).toBe(false);
  });
});

describe("query", () => {
  it("validates source and target", () => {
    expect(queryProblem({ ...query, from: [] })).toBe("noFrom");
    expect(queryProblem({ ...query, to: "  " })).toBe("noTo");
    expect(queryProblem({ ...query, to: "Jesu" })).toBe("same");
    expect(queryProblem(query)).toBeNull();
    expect(addFrom(["Jesu"], " Jesum ")).toEqual(["Jesu", "Jesum"]);
    expect(addFrom(["Jesu"], "Jesu")).toEqual(["Jesu"]);
  });
});

describe("list items", () => {
  it("regroups a reading-order page by form and inserts expanded verses", () => {
    const rows = [occ(3, "Jesum"), occ(4, "Jesu"), occ(5, "Jesum"), occ(6, "Jesu")];
    const items = flattenPage(rows, groups, new Set([4]));
    expect(items.map((item) => item.kind)).toEqual([
      "group",
      "row",
      "verse",
      "row",
      "group",
      "row",
      "row",
    ]);
    expect(items[0]).toMatchObject({ kind: "group", onPage: 2 });
    expect(items[1]).toMatchObject({ kind: "row", index: 0 });
    expect((items[1] as { row: BulkOccurrence }).row.wordId).toBe(4);
    expect((items[5] as { row: BulkOccurrence }).row.wordId).toBe(3);
  });

  it("reads stale rows with their reason from the 409 body", () => {
    expect(staleDetailsOf({ stale: [1, 2] }).get(2)).toEqual({ reason: "lemma" });
    const details = staleDetailsOf({
      stale: [5, 6],
      details: [
        { wordId: 5, reason: "original", origKey: "G5158" },
        { wordId: 6, reason: "lemma", lemma: "Weiser" },
      ],
    });
    expect(details.get(5)).toMatchObject({ reason: "original", origKey: "G5158" });
    expect(details.get(6)).toMatchObject({ reason: "lemma", lemma: "Weiser" });
    expect(staleDetailsOf(null).size).toBe(0);
  });
});

// "Weise" in the shape of the real data: sage (H2450, G4680), manner (G5158), unmapped additions.
const weiseOriginals: BulkOriginalCount[] = [
  { key: "H2450", word: "חָכָם", gloss: "weise", pos: "A", count: 32 },
  { key: "G4680", word: "σοφός", gloss: "weise", pos: "A", count: 9 },
  { key: "H4941", word: "מִשְׁפָּט", gloss: "Recht, Weise", pos: "N", count: 14, minority: true },
  { key: "G5158", word: "τρόπος", gloss: "Art", pos: "N", count: 4, minority: true },
  { key: "H2449", word: "חָכַם", gloss: "weise sein", pos: "V", count: 3, posConflict: true },
  { key: "added", count: 2 },
  { key: "none", count: 1 },
];
const g = (origKey: string, count: number, partOfSpeech = "NOUN"): BulkGroupCount => ({
  origKey,
  plainWord: "Weise",
  partOfSpeech,
  count,
});
const weiseGroups: BulkGroupCount[] = [
  g("H2450", 32),
  g("G4680", 9),
  g("H4941", 14),
  g("G5158", 4),
  g("H2449", 3),
  g("added", 2),
  g("none", 1),
];

describe("original-language guard", () => {
  it("shows the form view unless the server says ambiguous or the user chose", () => {
    expect(effectiveGroupMode(null, false)).toBe("form");
    expect(effectiveGroupMode(null, undefined)).toBe("form");
    expect(effectiveGroupMode(null, true)).toBe("original");
    expect(effectiveGroupMode("form", true)).toBe("form");
    expect(effectiveGroupMode("original", false)).toBe("original");
  });

  it("selects everything when the lemma is not ambiguous", () => {
    const sel = defaultSelection(weiseGroups, weiseOriginals, false);
    expect(selectedTotal(sel, weiseGroups)).toBe(65);
  });

  it("deselects only flagged groups and added / unmapped groups of content lemmas", () => {
    const sel = defaultSelection(weiseGroups, weiseOriginals, true);
    // kept: H2450 32 + G4680 9 (synonym groups stay selected); dropped: minority 14 + 4, pos conflict 3, added 2, none 1
    expect(selectedTotal(sel, weiseGroups)).toBe(41);
    expect(selectedInGroup(sel, g("G4680", 9))).toBe(9);
    expect(selectedInGroup(sel, g("H4941", 14))).toBe(0);
    expect(selectedInGroup(sel, g("added", 2))).toBe(0);
    expect(deselectedOriginalCount(sel, weiseGroups)).toBe(3); // H4941, G5158, H2449 (added / none are not counted)
  });

  it("keeps added / unmapped groups of function words selected", () => {
    const groups = [g("G3588", 50, "DET"), g("added", 20, "DET"), g("none", 5, "DET")];
    const sel = defaultSelection(groups, [{ key: "G3588", count: 50 }], true);
    expect(selectedTotal(sel, groups)).toBe(75);
  });

  it("never drops a second large (synonym) group", () => {
    const groups = [g("G2962", 722, "NOUN"), g("G1203", 300, "NOUN")];
    const originals: BulkOriginalCount[] = [
      { key: "G2962", count: 722 },
      { key: "G1203", count: 300 },
    ];
    expect(selectedTotal(defaultSelection(groups, originals, true), groups)).toBe(1022);
  });

  it("aggregates tri-state across originals and forms", () => {
    const sel = defaultSelection(weiseGroups, weiseOriginals, true);
    expect(membersState(sel, weiseGroups)).toBe("mixed");
    expect(membersState(sel, [g("H2450", 32), g("G4680", 9)])).toBe("checked");
    expect(membersState(sel, [g("H4941", 14)])).toBe("unchecked");
    const all = toggleMembers(sel, weiseGroups);
    expect(selectedTotal(all, weiseGroups)).toBe(65);
    expect(selectedTotal(toggleMembers(all, weiseGroups), weiseGroups)).toBe(0);
  });

  it("lists the original groups left out of an apply", () => {
    const sel = defaultSelection(weiseGroups, weiseOriginals, true);
    const left = leftOutOriginals(sel, weiseGroups, weiseOriginals);
    expect(left.map((l) => `${l.key}:${l.count}`)).toEqual([
      "H4941:14",
      "G5158:4",
      "H2449:3",
      "added:2",
      "none:1",
    ]);
    expect(left[0]?.word).toBe("מִשְׁפָּט");
  });

  it("groups a page by original, then form, with two header levels and collapsing", () => {
    const rows = [
      occ(1, "Weise", { origKey: "G5158", origKind: "aligned" }),
      occ(2, "Weise", { origKey: "H2450", origKind: "aligned" }),
      occ(3, "Weise", { origKind: "added" }),
      occ(4, "Weise", { origKey: "H2450", origKind: "aligned" }),
    ].map((row) => ({ ...row, partOfSpeech: "NOUN" }));
    const items = flattenPage(rows, weiseGroups, new Set(), {
      mode: "original",
      byOriginal: weiseOriginals,
    });
    expect(items.map((item) => item.kind)).toEqual([
      "original",
      "group",
      "row",
      "row",
      "original",
      "group",
      "row",
      "original",
      "group",
      "row",
    ]);
    const headers = items
      .filter((i) => i.kind === "original")
      .map((i) => (i as { key: string }).key);
    expect(headers).toEqual(["H2450", "G5158", "added"]); // byOriginal order, added last
    expect(items[0]).toMatchObject({ kind: "original", onPage: 2 });

    const collapsed = flattenPage(rows, weiseGroups, new Set(), {
      mode: "original",
      byOriginal: weiseOriginals,
      collapsed: new Set(["H2450"]),
    });
    expect(collapsed.map((item) => item.kind)).toEqual([
      "original",
      "original",
      "group",
      "row",
      "original",
      "group",
      "row",
    ]);
  });

  it("plans one id request per selected (original, form) group and keeps the original per id", () => {
    const sel = defaultSelection(weiseGroups, weiseOriginals, true);
    const plan = planIds(sel, weiseGroups);
    expect(plan.fetchAll).toBe(false);
    expect(plan.fetchGroups.map(origKeyOfGroupKey)).toEqual(["H2450", "G4680"]);
    const resolved = resolveIds(plan, [
      { ids: [1, 2], origKey: "H2450" },
      { ids: [3], origKey: "G4680" },
    ]);
    expect(resolved).toEqual({ wordIds: [1, 2, 3], expectedOriginal: ["H2450", "H2450", "G4680"] });
  });

  it('sends the shared original with a single request and "" for added / unmapped', () => {
    const one = [g("G2424", 928, "PROPN")];
    const plan = planIds(selectAll(), one);
    expect(plan.fetchAll).toBe(true);
    expect(resolveIds(plan, [{ ids: [5, 6], origKey: plan.allOrigKey }]).expectedOriginal).toEqual([
      "G2424",
      "G2424",
    ]);

    // an individually selected row of an unselected "added" group is expected as ""
    let sel = defaultSelection(weiseGroups, weiseOriginals, true);
    sel = toggleRow(sel, { id: 99, key: memberKey(g("added", 2)) });
    const extra = resolveIds(planIds(sel, weiseGroups), []);
    expect(extra).toEqual({ wordIds: [99], expectedOriginal: [""] });
  });
});
