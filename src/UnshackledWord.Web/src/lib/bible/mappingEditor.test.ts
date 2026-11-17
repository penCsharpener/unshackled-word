import { describe, expect, it } from "vitest";
import {
  changeCount,
  clearWord,
  commit,
  compareStates,
  computeGroups,
  connect,
  createHistory,
  diffStates,
  disconnect,
  edgesOf,
  emptyState,
  germanNodes,
  lettersOf,
  linksToState,
  needsRoleFields,
  nodeKey,
  normalizePart,
  parseNode,
  partIssues,
  partners,
  partsFromCuts,
  partsToClear,
  readableServerError,
  roleIssues,
  setAdded,
  setMorpheme,
  setMultiHead,
  setRole,
  splitWord,
  stateToLinks,
  toggleCut,
  unadd,
  undo,
  unsplit,
  wordStatus,
} from "./mappingEditor";
import type { VerseMappingLink, VerseMappingOriginalWord } from "./types";

const links: VerseMappingLink[] = [
  { elbWordId: 1, isAddedWord: true, parentElbWordId: 2 },
  { elbWordId: 2, stepWordId: 10, isAddedWord: false },
  { elbWordId: 3, stepWordId: 10, isAddedWord: false },
];

describe("links <-> state", () => {
  it("reads edges and added words", () => {
    const state = linksToState(links);
    expect([...state.edges]).toEqual(["2|10", "3|10"]);
    expect([...state.added]).toEqual([[1, 2]]);
  });

  it("writes edges first, then added words, in reading order", () => {
    const state = linksToState([...links].reverse());
    expect(stateToLinks(state, [1, 2, 3], [10])).toEqual([
      { elbWordId: 2, stepWordId: 10, isAddedWord: false },
      { elbWordId: 3, stepWordId: 10, isAddedWord: false },
      { elbWordId: 1, isAddedWord: true, parentElbWordId: 2 },
    ]);
  });

  it("round-trips", () => {
    expect(stateToLinks(linksToState(links), [1, 2, 3], [10])).toEqual([
      links[1],
      links[2],
      links[0],
    ]);
  });
});

describe("operations", () => {
  it("connects and returns the same state for a duplicate", () => {
    const s = connect(emptyState, 1, 10);
    expect(partners(s, "g", 1)).toEqual([10]);
    expect(partners(s, "o", 10)).toEqual([1]);
    expect(connect(s, 1, 10)).toBe(s);
  });

  it("connecting an added word makes it a normal word again", () => {
    const s = connect(setAdded(emptyState, 1, 2), 1, 10);
    expect(s.added.has(1)).toBe(false);
  });

  it("disconnects", () => {
    const s = connect(emptyState, 1, 10);
    expect(disconnect(s, 1, 10).edges.size).toBe(0);
    expect(disconnect(s, 1, 11)).toBe(s);
  });

  it("setAdded drops the word's own connections and re-parents its children", () => {
    let s = connect(emptyState, 1, 10);
    s = setAdded(s, 3, 1);
    s = setAdded(s, 1, 2);
    expect(s.edges.size).toBe(0);
    expect(s.added.get(1)).toBe(2);
    expect(s.added.get(3)).toBe(2);
  });

  it("setAdded resolves an added parent, ignores itself and cycles", () => {
    let s = setAdded(emptyState, 1, 2);
    expect(setAdded(s, 1, 1)).toBe(s);
    s = setAdded(s, 3, 1);
    expect(s.added.get(3)).toBe(2);
    expect(setAdded(s, 2, 1)).toBe(s);
  });

  it("unadd", () => {
    const s = setAdded(emptyState, 1, 2);
    expect(unadd(s, 1).added.size).toBe(0);
    expect(unadd(s, 2)).toBe(s);
  });

  it("clearWord removes all connections of either side", () => {
    let s = connect(connect(connect(emptyState, 1, 10), 1, 11), 2, 10);
    s = setAdded(s, 3, 1);
    expect(clearWord(s, "g", 1).edges.size).toBe(1);
    expect(clearWord(s, "o", 10).edges.size).toBe(1);
    expect(clearWord(s, "g", 3).added.size).toBe(0);
    expect(clearWord(s, "g", 99)).toBe(s);
  });
});

describe("history", () => {
  it("records edits, skips no-ops and undoes step by step", () => {
    let h = createHistory(emptyState);
    h = commit(h, connect(h.present, 1, 10));
    h = commit(h, h.present);
    h = commit(h, connect(h.present, 2, 11));
    expect(h.past).toHaveLength(2);
    h = undo(h);
    expect(h.present.edges.size).toBe(1);
    h = undo(undo(h));
    expect(h.present).toBe(emptyState);
    expect(undo(h)).toBe(h);
  });
});

describe("computeGroups", () => {
  const state = linksToState(links);
  const groups = computeGroups([1, 2, 3, 4], [10, 11], state);

  it("numbers connected groups in reading order, one group for shared originals and added words", () => {
    expect(groups.number(nodeKey("g", 1))).toBe(1);
    expect(groups.number(nodeKey("g", 2))).toBe(1);
    expect(groups.number(nodeKey("g", 3))).toBe(1);
    expect(groups.number(nodeKey("o", 10))).toBe(1);
  });

  it("leaves unconnected words without a number", () => {
    expect(groups.number(nodeKey("g", 4))).toBeUndefined();
    expect(groups.number(nodeKey("o", 11))).toBeUndefined();
  });

  it("lists the members of a group", () => {
    expect(groups.members(nodeKey("o", 10)).sort()).toEqual(["g1", "g2", "g3", "o10"]);
  });

  it("numbers a second group after the first", () => {
    const g = computeGroups([1, 2], [10, 11], connect(connect(emptyState, 1, 10), 2, 11));
    expect(g.number(nodeKey("o", 11))).toBe(2);
  });
});

describe("diff and status", () => {
  const base = linksToState(links);

  it("is empty without edits", () => {
    expect(changeCount(diffStates(base, base))).toBe(0);
  });

  it("lists connected, disconnected and added changes", () => {
    const work = setAdded(connect(disconnect(base, 3, 10), 4, 11), 3, 2);
    const diff = diffStates(base, work);
    expect(diff.connected).toEqual([{ germanId: 4, originalId: 11 }]);
    expect(diff.disconnected).toEqual([{ germanId: 3, originalId: 10 }]);
    expect(diff.addedWords).toEqual([{ germanId: 3, parentId: 2 }]);
    expect(diff.removedAddedWords).toEqual([]);
    expect(changeCount(diff)).toBe(3);
  });

  it("reports unchanged, edited and none", () => {
    const work = connect(base, 4, 11);
    expect(wordStatus(base, work, "g", 2)).toBe("unchanged");
    expect(wordStatus(base, work, "g", 4)).toBe("edited");
    expect(wordStatus(base, work, "o", 12)).toBe("none");
    expect(wordStatus(base, setAdded(base, 3, 2), "g", 3)).toBe("edited");
  });
});

describe("compound parts", () => {
  // Gerstenernte (id 2) = Gersten + ernte; Gersten -> 10, ernte -> 11 and 12
  const partLinks: VerseMappingLink[] = [
    { elbWordId: 1, isAddedWord: true, parentElbWordId: 3 },
    { elbWordId: 2, stepWordId: 10, isAddedWord: false, partOrder: 1, germanWordPart: "Gersten" },
    { elbWordId: 2, stepWordId: 11, isAddedWord: false, partOrder: 2, germanWordPart: "ernte" },
    { elbWordId: 2, stepWordId: 12, isAddedWord: false, partOrder: 2, germanWordPart: "ernte" },
    { elbWordId: 3, stepWordId: 13, isAddedWord: false },
  ];
  const texts = new Map([
    [1, "und"],
    [2, "Gerstenernte"],
    [3, "kehrte"],
  ]);

  it("parses and builds node keys with a part", () => {
    expect(nodeKey("g", 2, 1)).toBe("g2.1");
    expect(parseNode("g2.1")).toEqual({ side: "g", id: 2, part: 1 });
    expect(parseNode("o5")).toEqual({ side: "o", id: 5 });
    expect(parseNode("g12")).toEqual({ side: "g", id: 12 });
  });

  it("reads parts into splits and part edges", () => {
    const state = linksToState(partLinks);
    expect([...state.splits]).toEqual([[2, ["Gersten", "ernte"]]]);
    expect([...state.edges].sort()).toEqual(["2.1|10", "2.2|11", "2.2|12", "3|13"]);
    expect(germanNodes(state, 2)).toEqual(["g2.1", "g2.2"]);
    expect(germanNodes(state, 3)).toEqual(["g3"]);
  });

  it("round-trips the links, parts first by order, then added words", () => {
    const state = linksToState(partLinks);
    expect(stateToLinks(state, [1, 2, 3], [10, 11, 12, 13])).toEqual([
      partLinks[1],
      partLinks[2],
      partLinks[3],
      partLinks[4],
      partLinks[0],
    ]);
  });

  it("ranks the part orders and ignores a lone part", () => {
    const ranked = linksToState([
      { elbWordId: 2, stepWordId: 10, isAddedWord: false, partOrder: 3, germanWordPart: "Gersten" },
      { elbWordId: 2, stepWordId: 11, isAddedWord: false, partOrder: 7, germanWordPart: "ernte" },
    ]);
    expect([...ranked.edges].sort()).toEqual(["2.1|10", "2.2|11"]);
    const lone = linksToState([
      { elbWordId: 2, stepWordId: 10, isAddedWord: false, partOrder: 1, germanWordPart: "Gersten" },
    ]);
    expect(lone.splits.size).toBe(0);
    expect([...lone.edges]).toEqual(["2|10"]);
  });

  it("connects, disconnects and clears a single part", () => {
    const state = linksToState(partLinks);
    expect(connect(state, 2, 13)).toBe(state); // a split word has no whole-word edge
    expect(connect(state, 2, 13, 3)).toBe(state); // no third part
    const linked = connect(state, 2, 13, 1);
    expect(partners(linked, "g", 2, 1)).toEqual([10, 13]);
    expect(partners(linked, "g", 2, 2)).toEqual([11, 12]);
    expect(partners(linked, "o", 13).sort()).toEqual([2, 3]);
    expect(disconnect(linked, 2, 13, 1)).toEqual(state);
    const cleared = clearWord(state, "g", 2, 2);
    expect(partners(cleared, "g", 2, 2)).toEqual([]);
    expect(partners(cleared, "g", 2, 1)).toEqual([10]);
    expect(edgesOf(clearWord(state, "g", 2), "g", 2)).toEqual([]);
    expect(clearWord(state, "g", 2).splits.get(2)).toEqual(["Gersten", "ernte"]);
  });

  it("splitWord drops the whole-word links; unsplit joins the part links again", () => {
    const whole = linksToState([{ elbWordId: 2, stepWordId: 10, isAddedWord: false }]);
    const split = splitWord(whole, 2, ["Gersten", "ernte"]);
    expect(split.splits.get(2)).toEqual(["Gersten", "ernte"]);
    expect(split.edges.size).toBe(0);
    expect(splitWord(split, 2, ["Gersten", "ernte"])).toBe(split);
    expect(splitWord(whole, 2, ["Gersten"])).toBe(whole);
    expect(splitWord(whole, 2, ["", "Gersten"])).toBe(whole);

    const state = linksToState(partLinks);
    const joined = unsplit(state, 2);
    expect(joined.splits.size).toBe(0);
    expect([...joined.edges].sort()).toEqual(["2|10", "2|11", "2|12", "3|13"]);
    expect(unsplit(joined, 2)).toBe(joined);
  });

  it("an added word cannot be split, and making a split word added joins it", () => {
    const added = linksToState([{ elbWordId: 1, isAddedWord: true, parentElbWordId: 2 }]);
    expect(splitWord(added, 1, ["a", "b"])).toBe(added);
    const state = linksToState(partLinks);
    const next = setAdded(state, 2, 3);
    expect(next.splits.size).toBe(0);
    expect(edgesOf(next, "g", 2)).toEqual([]);
  });

  it("groups parts on their own; an added word joins the first part", () => {
    const state = linksToState([
      ...partLinks.slice(1),
      { elbWordId: 1, isAddedWord: true, parentElbWordId: 2 },
    ]);
    const groups = computeGroups([1, 2, 3], [10, 11, 12, 13], state);
    expect(groups.number("g1")).toBe(1);
    expect(groups.number("g2.1")).toBe(1);
    expect(groups.number("g2.2")).toBe(2);
    expect(groups.number("g3")).toBe(3);
    expect(groups.members("g2.2")).toEqual(["g2.2", "o11", "o12"]);
    expect(groups.members("g2.1")).toEqual(["g1", "g2.1", "o10"]);
    expect(groups.number("g2")).toBeUndefined();
  });

  it("diffs edges of parts and split changes", () => {
    const base = linksToState(partLinks);
    expect(changeCount(diffStates(base, base))).toBe(0);
    const work = connect(base, 2, 13, 1);
    expect(diffStates(base, work).connected).toEqual([{ germanId: 2, originalId: 13, part: 1 }]);
    const joined = unsplit(base, 2);
    const diff = diffStates(base, joined);
    expect(diff.unsplitWords).toEqual([{ germanId: 2, parts: ["Gersten", "ernte"] }]);
    expect(diff.splitWords).toEqual([]);
    // same originals before and after: only the split line is listed, not six edge lines
    expect(diff.disconnected).toEqual([]);
    expect(diff.connected).toEqual([]);
    expect(changeCount(diff)).toBe(1);
    // a fresh split drops the whole-word links, which is worth listing
    const whole = linksToState([{ elbWordId: 2, stepWordId: 10, isAddedWord: false }]);
    const fresh = diffStates(whole, splitWord(whole, 2, ["Gersten", "ernte"]));
    expect(fresh.disconnected).toEqual([{ germanId: 2, originalId: 10 }]);
    expect(fresh.splitWords).toHaveLength(1);
    const resplit = splitWord(base, 2, ["Gerste", "nernte"]);
    expect(diffStates(base, resplit).splitWords).toEqual([
      { germanId: 2, parts: ["Gerste", "nernte"] },
    ]);
    expect(diffStates(base, resplit).unsplitWords).toEqual([
      { germanId: 2, parts: ["Gersten", "ernte"] },
    ]);
  });

  it("reports the status per part", () => {
    const base = linksToState(partLinks);
    const work = connect(base, 2, 13, 2);
    expect(wordStatus(base, work, "g", 2, 1)).toBe("unchanged"); // only part 2 got a link
    expect(wordStatus(base, work, "g", 2, 2)).toBe("edited");
    expect(wordStatus(base, base, "g", 2, 1)).toBe("unchanged");
    expect(wordStatus(base, work, "o", 13)).toBe("edited");
    expect(wordStatus(base, clearWord(base, "g", 2, 1), "g", 2, 1)).toBe("edited");
    expect(wordStatus(clearWord(base, "g", 2, 1), clearWord(base, "g", 2, 1), "g", 2, 1)).toBe(
      "none",
    );
  });

  it("asks for clearParts only when stored parts would be dropped", () => {
    const base = linksToState(partLinks);
    expect(partsToClear(partLinks, base)).toEqual([]);
    expect(partsToClear(partLinks, connect(base, 2, 13, 1))).toEqual([]);
    expect(partsToClear(partLinks, unsplit(base, 2))).toEqual([2]);
    expect(partsToClear(partLinks, setAdded(base, 2, 3))).toEqual([2]);
    expect(partsToClear([], base)).toEqual([]);
  });

  it("counts a lone stored part, which the editor shows as a whole word", () => {
    const lone: VerseMappingLink[] = [
      { elbWordId: 2, stepWordId: 10, isAddedWord: false, partOrder: 1, germanWordPart: "zehn" },
    ];
    const state = linksToState(lone);
    expect(state.splits.size).toBe(0);
    expect(partsToClear(lone, state)).toEqual([2]);
  });

  it("mirrors the server's part rules", () => {
    const base = linksToState(partLinks);
    expect(partIssues(base, texts)).toEqual([]);
    // every part needs a link
    expect(partIssues(clearWord(base, "g", 2, 2), texts)).toEqual([
      { code: "unlinked", germanId: 2, part: 2 },
    ]);
    // a fresh split has no links yet
    const fresh = splitWord(linksToState([]), 2, ["Gersten", "ernte"]);
    expect(partIssues(fresh, texts).map((i) => i.code)).toEqual(["unlinked", "unlinked"]);
    // the parts spell the word (case and hyphens ignored)
    const misspelled = splitWord(connect(connect(emptyState, 2, 10), 2, 11), 2, [
      "Gerste",
      "ernte",
    ]);
    expect(
      partIssues(connect(connect(misspelled, 2, 10, 1), 2, 11, 2), texts).map((i) => i.code),
    ).toEqual(["notSpelled"]);
    expect(normalizePart("Gersten-Ernte.")).toBe("gerstenernte");
    // at most 30 characters per part
    const long = "a".repeat(31);
    const longState = connect(connect(splitWord(emptyState, 2, [long, "b"]), 2, 10, 1), 2, 11, 2);
    expect(partIssues(longState, new Map([[2, `${long}b`]])).map((i) => i.code)).toEqual([
      "tooLong",
    ]);
    // a single part is not a split; a split word cannot be an added word
    const lone = { ...base, splits: new Map([[2, ["Gerstenernte"]]]) };
    expect(partIssues(lone, texts).map((i) => i.code)).toContain("tooFewParts");
    const both = { ...base, added: new Map([[2, 3]]) };
    expect(partIssues(both, texts).map((i) => i.code)).toContain("added");
  });
});

describe("cuts", () => {
  it("cuts a word into parts after the given letter counts", () => {
    expect(partsFromCuts("Weizenernte", [6])).toEqual(["Weizen", "ernte"]);
    expect(partsFromCuts("Weizenernte", [8, 2, 6])).toEqual(["We", "izen", "er", "nte"]);
    expect(partsFromCuts("Weizenernte", [])).toEqual(["Weizenernte"]);
  });

  it("ignores cuts outside the word and repeats", () => {
    expect(partsFromCuts("Gott", [0, 4, 9, 2, 2])).toEqual(["Go", "tt"]);
  });

  it("counts letters, not UTF-16 units", () => {
    expect(lettersOf("Öl")).toEqual(["Ö", "l"]);
    expect(partsFromCuts("Ölzweig", [2])).toEqual(["Öl", "zweig"]);
  });

  it("toggles a cut and keeps them sorted", () => {
    expect(toggleCut([2, 6], 4)).toEqual([2, 4, 6]);
    expect(toggleCut([2, 4, 6], 4)).toEqual([2, 6]);
  });
});

describe("compareStates", () => {
  const live = linksToState([
    { elbWordId: 1, stepWordId: 10, isAddedWord: false },
    { elbWordId: 2, stepWordId: 11, isAddedWord: false },
    { elbWordId: 4, isAddedWord: true, parentElbWordId: 1 },
  ]);
  const proposed = linksToState([
    { elbWordId: 1, stepWordId: 10, isAddedWord: false },
    { elbWordId: 2, stepWordId: 12, isAddedWord: false },
    { elbWordId: 3, isAddedWord: true, parentElbWordId: 2 },
  ]);

  it("marks links as new, removed or unchanged and keeps all of them", () => {
    const { display, edgeKind, addedKind } = compareStates(live, proposed);
    expect([...display.edges].sort()).toEqual(["1|10", "2|11", "2|12"]);
    expect(edgeKind.get("1|10")).toBe("unchanged");
    expect(edgeKind.get("2|12")).toBe("added");
    expect(edgeKind.get("2|11")).toBe("removed");
    expect(addedKind.get(3)).toBe("added");
    expect(addedKind.get(4)).toBe("removed");
    expect(display.added.get(3)).toBe(2);
    expect(display.added.get(4)).toBe(1);
  });

  it("shows the proposal's parts and drops live links that have no tile there", () => {
    const withParts = linksToState([
      { elbWordId: 5, stepWordId: 10, isAddedWord: false, partOrder: 1, germanWordPart: "Gersten" },
      { elbWordId: 5, stepWordId: 11, isAddedWord: false, partOrder: 2, germanWordPart: "ernte" },
    ]);
    const whole = linksToState([{ elbWordId: 5, stepWordId: 10, isAddedWord: false }]);
    const { display, edgeKind } = compareStates(whole, withParts);
    expect(display.splits.get(5)).toEqual(["Gersten", "ernte"]);
    expect([...display.edges].sort()).toEqual(["5.1|10", "5.2|11"]);
    expect(edgeKind.get("5.1|10")).toBe("added");
    expect([...compareStates(withParts, withParts).display.edges]).toHaveLength(2);
  });

  it("keeps a live added word out when the proposal links the word", () => {
    const linked = linksToState([{ elbWordId: 4, stepWordId: 12, isAddedWord: false }]);
    const { display } = compareStates(live, linked);
    expect(display.added.has(4)).toBe(false);
  });
});

describe("link roles (0018)", () => {
  const greek = new Map<number, VerseMappingOriginalWord>([
    [10, { id: 10, position: 1, original: "ἐβάπτισα" }],
  ]);
  const hebrew = new Map<number, VerseMappingOriginalWord>([
    [
      10,
      {
        id: 10,
        position: 1,
        original: "וַיֹּאמֶר",
        morphemes: [
          { strongs: "H9001", isMain: false },
          { strongs: "H0559", isMain: true },
        ],
      },
    ],
  ]);
  const two = linksToState([
    { elbWordId: 1, stepWordId: 10, isAddedWord: false, role: "head" },
    { elbWordId: 2, stepWordId: 10, isAddedWord: false, role: "head" },
  ]);

  it("reads roles from links and writes them only when asked", () => {
    const s = linksToState([
      { elbWordId: 1, stepWordId: 10, isAddedWord: false, role: "unit" },
      { elbWordId: 2, stepWordId: 10, isAddedWord: false, role: "head", strongsNumber: "H9001" },
    ]);
    expect(stateToLinks(s, [1, 2], [10])).toEqual([
      { elbWordId: 1, stepWordId: 10, isAddedWord: false },
      { elbWordId: 2, stepWordId: 10, isAddedWord: false },
    ]);
    expect(stateToLinks(s, [1, 2], [10], { roles: true })).toEqual([
      { elbWordId: 1, stepWordId: 10, isAddedWord: false, role: "unit" },
      { elbWordId: 2, stepWordId: 10, isAddedWord: false, role: "head", strongsNumber: "H9001" },
    ]);
  });

  it("needs role fields only with role data loaded or set", () => {
    expect(needsRoleFields(two, connect(two, 3, 10))).toBe(false);
    const unit = setRole(two, { germanId: 1, originalId: 10 }, "unit");
    expect(needsRoleFields(two, unit)).toBe(true);
    expect(needsRoleFields(unit, setRole(unit, { germanId: 1, originalId: 10 }, "head"))).toBe(
      true,
    );
  });

  it("counts a role change as a change and forgets the role of a removed link", () => {
    const unit = setRole(two, { germanId: 1, originalId: 10 }, "unit");
    expect(changeCount(diffStates(two, unit))).toBe(1);
    const again = connect(disconnect(unit, 1, 10), 1, 10);
    expect(again.roles.size).toBe(0);
  });

  it("finds several heads and units without a head per morpheme", () => {
    expect(roleIssues(two, greek)).toEqual([
      { code: "multiHead", originalId: 10, morpheme: "", germanIds: [1, 2] },
    ]);
    const split = setMorpheme(two, { germanId: 1, originalId: 10 }, "H9001");
    expect(roleIssues(split, hebrew)).toEqual([]);
    const lonely = setRole(split, { germanId: 1, originalId: 10 }, "unit");
    expect(roleIssues(lonely, hebrew)).toEqual([
      { code: "unitWithoutHead", originalId: 10, morpheme: "H9001", germanIds: [1] },
    ]);
    const accepted = setMultiHead(two, 10, "", true, greek);
    expect(roleIssues(accepted, greek)).toEqual([]);
    expect(
      stateToLinks(accepted, [1, 2], [10], { roles: true }).every((l) => l.allowMultiHead),
    ).toBe(true);
  });

  it("replaces word ids in server messages", () => {
    expect(
      readableServerError(
        "Link 2 (word 1): x. Original word 10 morpheme H9001: 2 head links (words 1, 2)",
        (id) => ({ 1: "habe", 2: "getauft" })[id],
        (id) => (id === 10 ? "ἐβάπτισα" : undefined),
      ),
    ).toBe(
      "Link 2 (word habe): x. Original word ἐβάπτισα morpheme H9001: 2 head links (words habe, getauft)",
    );
  });
});
