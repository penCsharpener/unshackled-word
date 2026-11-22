import { describe, expect, it } from "vitest";
import { remapPhysicalKey, scriptRows } from "./scriptLayouts";

describe("remapPhysicalKey", () => {
  it("maps Latin keys to Greek letters", () => {
    expect(remapPhysicalKey("greek", "m")).toBe("μ");
    expect(remapPhysicalKey("greek", "q")).toBe("θ");
    expect(remapPhysicalKey("greek", "j")).toBe("ς");
    expect(remapPhysicalKey("greek", "W")).toBe("Ω");
  });

  it("maps Latin keys to Hebrew letters, final forms included", () => {
    expect(remapPhysicalKey("hebrew", "a")).toBe("א");
    expect(remapPhysicalKey("hebrew", "e")).toBe("ך");
    expect(remapPhysicalKey("hebrew", ";")).toBe("ץ");
    expect(remapPhysicalKey("hebrew", "A")).toBe("א");
  });

  it("leaves unmapped and non-ASCII keys alone", () => {
    expect(remapPhysicalKey("greek", "v")).toBeUndefined();
    expect(remapPhysicalKey("greek", "μ")).toBeUndefined();
    expect(remapPhysicalKey("greek", "Enter")).toBeUndefined();
    expect(remapPhysicalKey("greek", "1")).toBeUndefined();
  });
});

describe("scriptRows", () => {
  it("draws all 24 Greek letters plus the final sigma and a backspace key", () => {
    const keys = scriptRows("greek").flat();
    expect(keys).toHaveLength(25 + 1);
    expect(keys.at(-1)).not.toBe("⌫");
    expect(scriptRows("greek")[0]?.at(-1)).toBe("⌫");
  });

  it("draws the 22 Hebrew letters and the five final forms", () => {
    const keys = scriptRows("hebrew")
      .flat()
      .filter((key) => key !== "⌫");
    expect(keys).toHaveLength(27);
    expect(keys).toContain("ץ");
  });
});
