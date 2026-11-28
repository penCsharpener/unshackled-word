import { describe, expect, it } from "vitest";
import { WORD_TIP_WIDTH, wordTipPosition } from "./WordTipCard";

const viewport = { width: 1000, height: 800 };

describe("wordTipPosition", () => {
  it("puts the card under the anchor at its left edge", () => {
    expect(wordTipPosition({ left: 100, top: 100, bottom: 130 }, viewport)).toEqual({
      x: 100,
      y: 138,
      above: false,
    });
  });

  it("keeps the card inside the viewport on the right and the left", () => {
    expect(wordTipPosition({ left: 950, top: 100, bottom: 130 }, viewport).x).toBe(
      1000 - WORD_TIP_WIDTH - 8,
    );
    expect(wordTipPosition({ left: -20, top: 100, bottom: 130 }, viewport).x).toBe(8);
  });

  it("flips above the anchor near the bottom", () => {
    expect(wordTipPosition({ left: 100, top: 700, bottom: 730 }, viewport)).toEqual({
      x: 100,
      y: 692,
      above: true,
    });
  });
});
