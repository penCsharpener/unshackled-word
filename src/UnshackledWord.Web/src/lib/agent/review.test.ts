import { describe, expect, it } from "vitest";
import { lemmaProposalDiff, proposalDiff, summary } from "@/test/agentFixtures";
import {
  bulkCounts,
  filterProposals,
  ignoresReviewKey,
  liveLinks,
  nextOpenProposal,
  roleChangeParts,
  toMappingLink,
} from "./review";

const list = [
  summary(1, 1),
  summary(2, 2, { status: "merged" }),
  summary(3, 3, { flagCount: 2 }),
  summary(4, 4, { status: "discarded", flagCount: 1 }),
  summary(5, 5),
];

describe("filterProposals", () => {
  it("filters by status and by flags", () => {
    expect(filterProposals(list, "open", false).map((p) => p.id)).toEqual([1, 3, 5]);
    expect(filterProposals(list, "open", true).map((p) => p.id)).toEqual([3]);
    expect(filterProposals(list, "all", true).map((p) => p.id)).toEqual([3, 4]);
    expect(filterProposals(list, "all", false)).toHaveLength(5);
  });
});

describe("bulkCounts", () => {
  it("counts the open proposals the bulk merge takes and those it leaves", () => {
    expect(bulkCounts(list)).toEqual({ unflagged: 2, flagged: 1 });
  });
});

describe("nextOpenProposal", () => {
  it("takes the next open one in list order", () => {
    expect(nextOpenProposal(list, 1)).toBe(3);
    expect(nextOpenProposal(list, 3)).toBe(5);
  });

  it("falls back to the closest earlier open one, then to nothing", () => {
    expect(nextOpenProposal(list, 5)).toBe(3);
    expect(nextOpenProposal([summary(1, 1)], 1)).toBeNull();
    expect(nextOpenProposal([], 9)).toBeNull();
  });
});

describe("toMappingLink", () => {
  it("keeps only the link fields the server knows", () => {
    expect(
      toMappingLink({
        elbWordId: 1,
        german: "Gerstenernte",
        stepWordId: 2,
        original: "x",
        isAddedWord: false,
        partOrder: 2,
        germanWordPart: "ernte",
      }),
    ).toEqual({
      elbWordId: 1,
      stepWordId: 2,
      isAddedWord: false,
      partOrder: 2,
      germanWordPart: "ernte",
    });
    expect(
      toMappingLink({
        elbWordId: 3,
        german: "und",
        isAddedWord: true,
        parentElbWordId: 1,
        parentGerman: "a",
      }),
    ).toEqual({ elbWordId: 3, isAddedWord: true, parentElbWordId: 1 });
  });
});

describe("ignoresReviewKey", () => {
  const press = (target: Element | Document, init: KeyboardEventInit = {}) => {
    let result = false;
    const handler = (event: Event) => {
      result = ignoresReviewKey(event as KeyboardEvent);
    };
    document.addEventListener("keydown", handler);
    target.dispatchEvent(new KeyboardEvent("keydown", { key: "j", bubbles: true, ...init }));
    document.removeEventListener("keydown", handler);
    return result;
  };

  it("lets plain keys through on the page", () => {
    expect(press(document.body)).toBe(false);
  });

  it("ignores modifiers, inputs and editors", () => {
    document.body.innerHTML =
      '<input id="i" /><div data-mapping-editor><button id="b">x</button></div>';
    expect(press(document.body, { ctrlKey: true })).toBe(true);
    expect(press(document.getElementById("i") as Element)).toBe(true);
    expect(press(document.getElementById("b") as Element)).toBe(true);
    document.body.innerHTML = "";
  });
});

describe("lemma-mode helpers", () => {
  it("filters on role changes", () => {
    const withRoles = [summary(1, 1), summary(2, 2, { hasRoleChanges: true })];
    expect(filterProposals(withRoles, "all", false, true).map((p) => p.id)).toEqual([2]);
    expect(filterProposals(withRoles, "all", false)).toHaveLength(2);
  });

  it("takes the live links with roles, else the diff links", () => {
    expect(liveLinks(lemmaProposalDiff)).toBe(lemmaProposalDiff.currentLinks);
    expect(liveLinks(proposalDiff).map((l) => l.elbWordId)).toEqual([101, 104]);
  });

  it("tells what a role change does", () => {
    const [und, er] = lemmaProposalDiff.roleChanges ?? [];
    if (!und || !er) throw new Error("fixture");
    expect(roleChangeParts(und)).toEqual({
      added: false,
      role: false,
      morpheme: true,
      multiHead: false,
      partLemma: false,
    });
    expect(roleChangeParts(er).added).toBe(true);
    expect(roleChangeParts({ ...und, role: "unit", strongsNumber: undefined }).role).toBe(true);
  });
});
