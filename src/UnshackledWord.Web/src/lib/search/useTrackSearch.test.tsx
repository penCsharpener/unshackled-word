import { renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { useTrackSearch } from "./useTrackSearch";

afterEach(() => {
  delete window.umami;
});

describe("useTrackSearch", () => {
  it("sends kind and bucket once, never the query", () => {
    const spy = vi.fn();
    window.umami = { track: spy };
    const { rerender } = renderHook(({ n }) => useTrackSearch("verses", "Gnade", n), {
      initialProps: { n: undefined as number | undefined },
    });
    expect(spy).not.toHaveBeenCalled();
    rerender({ n: 25 });
    rerender({ n: 26 });
    expect(spy).toHaveBeenCalledTimes(1);
    expect(spy).toHaveBeenCalledWith("search", { kind: "verses", results: ">10" });
    expect(JSON.stringify(spy.mock.calls)).not.toContain("Gnade");
  });
});
