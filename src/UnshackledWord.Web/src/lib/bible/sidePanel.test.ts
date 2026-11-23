import { afterEach, describe, expect, it } from "vitest";
import { nextSidePanelMode, readSidePanelMode, SIDE_PANEL_KEY } from "./sidePanel";

afterEach(() => localStorage.clear());

describe("side panel mode", () => {
  it("defaults to TSK, also for unknown stored values", () => {
    expect(readSidePanelMode()).toBe("tsk");
    localStorage.setItem(SIDE_PANEL_KEY, "bogus");
    expect(readSidePanelMode()).toBe("tsk");
  });

  it("reads a stored mode", () => {
    localStorage.setItem(SIDE_PANEL_KEY, "off");
    expect(readSidePanelMode()).toBe("off");
  });

  it("falls back to TSK for the former notes mode", () => {
    localStorage.setItem(SIDE_PANEL_KEY, "notes");
    expect(readSidePanelMode()).toBe("tsk");
  });

  it("cycles TSK → original → off → TSK without a notes step", () => {
    expect(nextSidePanelMode("tsk")).toBe("original");
    expect(nextSidePanelMode("original")).toBe("off");
    expect(nextSidePanelMode("off")).toBe("tsk");
  });
});
