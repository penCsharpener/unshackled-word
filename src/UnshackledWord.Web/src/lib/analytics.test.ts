import { afterEach, describe, expect, it, vi } from "vitest";
import {
  levelBucket,
  resultsBucket,
  setStaffSession,
  track,
  trackRevisionLevel,
  uwBeforeSend,
} from "./analytics";

afterEach(() => {
  delete window.umami;
  setStaffSession(false);
  window.history.pushState({}, "", "/");
  vi.useRealTimers();
});

describe("track", () => {
  it("is a no-op without window.umami", () => {
    expect(() => track("login", {})).not.toThrow();
  });

  it("forwards to window.umami", () => {
    const spy = vi.fn();
    window.umami = { track: spy };
    track("chapter-open", { testament: "nt" });
    expect(spy).toHaveBeenCalledWith("chapter-open", { testament: "nt" });
  });

  it("debounces the revision level to the resting value", () => {
    vi.useFakeTimers();
    const spy = vi.fn();
    window.umami = { track: spy };
    for (const level of [100, 300, 600]) trackRevisionLevel(level);
    vi.advanceTimersByTime(5000);
    expect(spy).toHaveBeenCalledTimes(1);
    expect(spy).toHaveBeenCalledWith("revision-level", { level: ">500" });
  });
});

describe("buckets", () => {
  it("buckets result counts", () => {
    expect([0, 1, 10, 11].map(resultsBucket)).toEqual(["0", "1-10", "1-10", ">10"]);
  });
  it("buckets levels", () => {
    expect([1, 99, 100, 199, 200, 201, 500, 501].map(levelBucket)).toEqual([
      "1-99",
      "1-99",
      "100-199",
      "100-199",
      "200",
      "201-500",
      "201-500",
      ">500",
    ]);
  });
});

describe("uwBeforeSend", () => {
  it("is attached to window", () => {
    expect(window.uwBeforeSend).toBe(uwBeforeSend);
  });
  it("passes a normal reader route", () => {
    const payload = { url: "/bible/45/8" };
    expect(uwBeforeSend("event", payload)).toBe(payload);
  });
  it.each(["/wartung", "/wartung/x", "/admin/users", "/edit/lemmas", "/edit/proposals/1/2?x=1"])(
    "drops %s",
    (url) => {
      expect(uwBeforeSend("pageview", { url })).toBe(false);
    },
  );
  it("drops everything for a maintainer session", () => {
    setStaffSession(true);
    expect(uwBeforeSend("pageview", { url: "/bible/45/8" })).toBe(false);
  });
  it("remembers the maintainer session for the next page load", () => {
    setStaffSession(true);
    expect(window.localStorage.getItem("uw.analytics.staff")).toBe("1");
    setStaffSession(false);
    expect(window.localStorage.getItem("uw.analytics.staff")).toBeNull();
  });
  it("drops by current location when the payload has no url", () => {
    window.history.pushState({}, "", "/admin/log");
    expect(uwBeforeSend("event", {})).toBe(false);
  });
});
