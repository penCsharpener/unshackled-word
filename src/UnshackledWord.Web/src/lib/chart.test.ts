import { describe, expect, it } from "vitest";
import { donutSegments, formatNumber, formatPercent } from "./chart";

describe("formatPercent", () => {
  it("formats with one decimal in German", () => {
    expect(formatPercent(23, 114, "de").replace(/\s/g, " ")).toBe("20,2 %");
  });

  it("returns 0 for an empty total", () => {
    expect(formatPercent(0, 0, "en")).toBe("0%");
  });
});

describe("formatNumber", () => {
  it("uses the language's grouping", () => {
    expect(formatNumber(1234, "de")).toBe("1.234");
    expect(formatNumber(1234, "en")).toBe("1,234");
  });
});

describe("donutSegments", () => {
  it("starts each arc where the previous share ends and cuts a gap", () => {
    expect(donutSegments([75, 25], 1)).toEqual([
      { start: 0, length: 74 },
      { start: 75, length: 24 },
    ]);
  });

  it("uses no gap for a single full slice", () => {
    expect(donutSegments([5], 1)).toEqual([{ start: 0, length: 100 }]);
  });

  it("keeps tiny slices visible", () => {
    const lengths = donutSegments([1000, 1], 1).map((s) => s.length);
    expect(Math.min(...lengths)).toBeGreaterThanOrEqual(0.4);
  });

  it("returns empty arcs for a zero total", () => {
    expect(donutSegments([0, 0], 1)).toEqual([
      { start: 0, length: 0 },
      { start: 0, length: 0 },
    ]);
  });
});
