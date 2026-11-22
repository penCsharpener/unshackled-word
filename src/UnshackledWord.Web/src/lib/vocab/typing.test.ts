import { describe, expect, it } from "vitest";
import {
  emptyAnswers,
  isComplete,
  popLetter,
  pushLetter,
  removeLetter,
  setLetter,
  toTypingLayout,
} from "./typing";

const layout = toTypingLayout([
  { kind: "group", length: 5 },
  { kind: "separator", text: ", -" },
  { kind: "group", length: 1 },
  { kind: "separator", text: ", -" },
  { kind: "group", length: 2 },
]);

describe("typing helpers", () => {
  it("reads the API layout", () => {
    expect(layout).toEqual([
      { group: 5 },
      { sep: ", -" },
      { group: 1 },
      { sep: ", -" },
      { group: 2 },
    ]);
    expect(emptyAnswers(layout)).toEqual(["", "", ""]);
  });

  it("fills the first free box and jumps to the next group", () => {
    let value = emptyAnswers(layout);
    for (const ch of "μεσοςη") value = pushLetter(layout, value, ch);
    expect(value).toEqual(["μεσος", "η", ""]);
    expect(isComplete(layout, value)).toBe(false);
    for (const ch of "ον") value = pushLetter(layout, value, ch);
    expect(isComplete(layout, value)).toBe(true);
    expect(pushLetter(layout, value, "x")).toBe(value);
  });

  it("pops the last letter across groups", () => {
    expect(popLetter(layout, ["μεσος", "η", ""])).toEqual(["μεσος", "", ""]);
    expect(popLetter(layout, ["μεσος", "", ""])).toEqual(["μεσο", "", ""]);
    expect(popLetter(layout, ["", "", ""])).toEqual(["", "", ""]);
  });

  it("sets and removes a letter at a position", () => {
    expect(setLetter(layout, ["ab", "", ""], 0, 0, "x")).toEqual(["xb", "", ""]);
    expect(setLetter(layout, ["ab", "", ""], 0, 4, "x")).toEqual(["ab", "", ""]);
    expect(removeLetter(["abc", "", ""], 0, 1)).toEqual(["ac", "", ""]);
  });
});
