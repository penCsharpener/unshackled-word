import { describe, expect, it } from "vitest";
import { sentenceDiff, sentences } from "./textDiff";

describe("sentences", () => {
  it("splits after sentence marks and at line breaks", () => {
    expect(sentences("Erster Satz. Zweiter? „Dritter!“ Vierter\nFünfter")).toEqual([
      "Erster Satz.",
      "Zweiter?",
      "„Dritter!“",
      "Vierter",
      "Fünfter",
    ]);
  });
});

describe("sentenceDiff", () => {
  it("keeps unchanged sentences whole and word-diffs the changed one", () => {
    const segments = sentenceDiff(
      "Gott ist treu. Er hält, was er verspricht. Amen.",
      "Gott ist treu. Er hält immer, was er sagt. Amen.",
    );
    expect(segments.map((s) => s.kind)).toEqual(["same", "changed", "same"]);
    expect(segments[0]).toEqual({ kind: "same", text: "Gott ist treu." });
    expect(segments[2]).toEqual({ kind: "same", text: "Amen." });
    const changed = segments[1];
    if (changed?.kind !== "changed") throw new Error("expected a changed segment");
    const of = (kind: string) => changed.parts.filter((p) => p.kind === kind).map((p) => p.text);
    expect(of("same")).toEqual(["Er", "was er"]);
    expect(of("added")).toEqual(["hält immer,", "sagt."]);
    expect(of("removed")).toEqual(["hält,", "verspricht."]);
  });

  it("marks an added sentence as fully added", () => {
    expect(sentenceDiff("Eins.", "Eins. Zwei drei.")).toEqual([
      { kind: "same", text: "Eins." },
      { kind: "changed", parts: [{ kind: "added", text: "Zwei drei." }] },
    ]);
  });

  it("has no changed segment for equal texts with other whitespace", () => {
    expect(sentenceDiff("Eins.  Zwei.", "Eins.\nZwei.").every((s) => s.kind === "same")).toBe(true);
  });
});
