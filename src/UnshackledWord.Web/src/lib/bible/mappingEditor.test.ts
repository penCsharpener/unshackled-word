import { describe, expect, it } from "vitest";
import {
  changeCount,
  clearWord,
  commit,
  computeGroups,
  connect,
  createHistory,
  diffStates,
  disconnect,
  emptyState,
  linksToState,
  nodeKey,
  partners,
  setAdded,
  stateToLinks,
  unadd,
  undo,
  wordStatus,
} from "./mappingEditor";
import type { VerseMappingLink } from "./types";

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
