import { afterEach, describe, expect, it } from "vitest";
import {
  effectiveSidePanelMode,
  nextSidePanelMode,
  readSidePanelMode,
  SIDE_PANEL_KEY,
  sidePanelModes,
} from "./sidePanel";

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

  it("reads the similar verses mode", () => {
    localStorage.setItem(SIDE_PANEL_KEY, "similar");
    expect(readSidePanelMode()).toBe("similar");
  });

  it("cycles TSK → original → LXX → similar → off → TSK without a notes step", () => {
    expect(nextSidePanelMode("tsk")).toBe("original");
    expect(nextSidePanelMode("original")).toBe("lxx");
    expect(nextSidePanelMode("lxx")).toBe("similar");
    expect(nextSidePanelMode("similar")).toBe("off");
    expect(nextSidePanelMode("off")).toBe("tsk");
  });

  it("reads the LXX mode", () => {
    localStorage.setItem(SIDE_PANEL_KEY, "lxx");
    expect(readSidePanelMode()).toBe("lxx");
  });

  it("skips the LXX in New Testament chapters", () => {
    expect(sidePanelModes(false)).toEqual(["tsk", "original", "similar", "off"]);
    expect(nextSidePanelMode("original", false)).toBe("similar");
    // a stored LXX choice shows the Grundtext there and cycles on from it
    expect(effectiveSidePanelMode("lxx", false)).toBe("original");
    expect(effectiveSidePanelMode("lxx", true)).toBe("lxx");
    expect(effectiveSidePanelMode("similar", false)).toBe("similar");
  });
});
