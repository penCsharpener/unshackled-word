import { describe, expect, it } from "vitest";
import { wordDiff } from "./wordDiff";

describe("wordDiff", () => {
  it("marks identical texts as unchanged", () => {
    expect(wordDiff("a b c", "a  b c")).toEqual([{ kind: "same", text: "a b c" }]);
  });

  it("marks inserted words", () => {
    expect(wordDiff("a c", "a b c")).toEqual([
      { kind: "same", text: "a" },
      { kind: "added", text: "b" },
      { kind: "same", text: "c" },
    ]);
  });

  it("marks deleted words", () => {
    expect(wordDiff("a b c", "a c")).toEqual([
      { kind: "same", text: "a" },
      { kind: "removed", text: "b" },
      { kind: "same", text: "c" },
    ]);
  });

  it("marks a replacement as removed plus added", () => {
    const parts = wordDiff("a b c", "a x c");
    expect(parts.filter((p) => p.kind === "removed")).toEqual([{ kind: "removed", text: "b" }]);
    expect(parts.filter((p) => p.kind === "added")).toEqual([{ kind: "added", text: "x" }]);
    expect(parts[0]).toEqual({ kind: "same", text: "a" });
    expect(parts[parts.length - 1]).toEqual({ kind: "same", text: "c" });
  });

  it("handles empty sides", () => {
    expect(wordDiff("", "a b")).toEqual([{ kind: "added", text: "a b" }]);
    expect(wordDiff("a b", "")).toEqual([{ kind: "removed", text: "a b" }]);
  });
});
