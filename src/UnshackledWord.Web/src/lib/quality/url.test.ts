import { describe, expect, it } from "vitest";
import {
  clearTrailPatch,
  formatFacets,
  formatGermanQ,
  formatTrail,
  jumpToCrumb,
  MAX_TRAIL,
  normalizeOriginalQ,
  parseFacets,
  parseGermanQ,
  parseQualityUrl,
  parseTrail,
  pushFilter,
  type TrailEntry,
  visibleCrumbs,
  withQualityUrl,
} from "./url";

const params = (query: string) => new URLSearchParams(query);

describe("parseQualityUrl", () => {
  it("returns defaults for an empty URL", () => {
    expect(parseQualityUrl(params(""))).toEqual({
      testament: undefined,
      bookId: undefined,
      check: undefined,
      facets: {},
      side: "o",
      q: "",
      trail: [],
      metric: "german",
      functionWords: false,
      page: 1,
      size: 50,
    });
  });

  it("reads all params", () => {
    const state = parseQualityUrl(
      params(
        "t=nt&book=43&check=M3&f=class%3DT%2Cpos%3DNOUN&x=d&q=gott~NOUN&m=source&fw=1&page=3&size=100",
      ),
    );
    expect(state).toMatchObject({
      testament: "nt",
      bookId: 43,
      check: "M3",
      facets: { class: "T", pos: "NOUN" },
      side: "d",
      q: "gott~NOUN",
      metric: "source",
      functionWords: true,
      page: 3,
      size: 100,
    });
  });

  it("ignores invalid values", () => {
    const state = parseQualityUrl(params("t=xx&book=-1&check=drop&size=7&page=0&x=z&m=foo"));
    expect(state.testament).toBeUndefined();
    expect(state.bookId).toBeUndefined();
    expect(state.check).toBeUndefined();
    expect(state.size).toBe(50);
    expect(state.page).toBe(1);
    expect(state.side).toBe("o");
    expect(state.metric).toBe("german");
  });
});

describe("facets", () => {
  it("round-trips and skips malformed parts", () => {
    expect(parseFacets("class=T,pos=NOUN,bad,=x,k=")).toEqual({ class: "T", pos: "NOUN" });
    expect(formatFacets({ class: "T", pos: "NOUN" })).toBe("class=T,pos=NOUN");
    expect(formatFacets({})).toBe("");
  });
});

describe("withQualityUrl", () => {
  it("sets and clears params, defaults are omitted", () => {
    const next = withQualityUrl(params("t=ot&page=4"), {
      testament: "nt",
      size: 50,
      side: "o",
      metric: "german",
    });
    expect(next.get("t")).toBe("nt");
    expect(next.has("size")).toBe(false);
    expect(next.has("x")).toBe(false);
    expect(next.has("m")).toBe(false);
  });

  it("resets the page when a data facet changes but not for explorer changes", () => {
    expect(withQualityUrl(params("page=4"), { bookId: 1 }).has("page")).toBe(false);
    expect(withQualityUrl(params("page=4"), { q: "G1" }).get("page")).toBe("4");
    expect(withQualityUrl(params("page=4"), { page: 5 }).get("page")).toBe("5");
  });

  it("does not mutate its input", () => {
    const prev = params("t=ot");
    withQualityUrl(prev, { testament: undefined });
    expect(prev.get("t")).toBe("ot");
  });
});

describe("trail", () => {
  const o = (q: string): TrailEntry => ({ side: "o", q });
  const d = (q: string): TrailEntry => ({ side: "d", q });

  it("parses and formats", () => {
    const trail = parseTrail("o:G2962,d:herr~NOUN,o:H3068,junk,x:1");
    expect(trail).toEqual([o("G2962"), d("herr~NOUN"), o("H3068")]);
    expect(formatTrail(trail)).toBe("o:G2962,d:herr~NOUN,o:H3068");
  });

  it("pushFilter appends, skips a repeated last entry and keeps at most 8", () => {
    expect(pushFilter([o("G1")], o("G1")).trail).toEqual([o("G1")]);
    let trail: TrailEntry[] = [];
    for (let i = 1; i <= 10; i++) trail = pushFilter(trail, o(`G${i}`)).trail;
    expect(trail).toHaveLength(MAX_TRAIL);
    expect(trail[0]).toEqual(o("G3"));
    expect(trail[MAX_TRAIL - 1]).toEqual(o("G10"));
  });

  it("pushFilter returns the new current filter", () => {
    expect(pushFilter([o("G1")], d("herr~NOUN"))).toEqual({
      side: "d",
      q: "herr~NOUN",
      trail: [o("G1"), d("herr~NOUN")],
    });
  });

  it("jumpToCrumb truncates the trail after the crumb", () => {
    const trail = [o("G1"), d("a~NOUN"), o("H2")];
    expect(jumpToCrumb(trail, 1)).toEqual({
      side: "d",
      q: "a~NOUN",
      trail: [o("G1"), d("a~NOUN")],
    });
    expect(jumpToCrumb(trail, 5)).toBeNull();
  });

  it("clearTrailPatch empties trail and filter", () => {
    const next = withQualityUrl(params("trail=o:G1&q=G1"), clearTrailPatch());
    expect(next.has("trail")).toBe(false);
    expect(next.has("q")).toBe(false);
  });

  it("visibleCrumbs folds all but the last ones", () => {
    const trail = [1, 2, 3, 4, 5];
    expect(visibleCrumbs(trail, 3, false)).toEqual({
      hidden: 2,
      shown: [
        { item: 3, index: 2 },
        { item: 4, index: 3 },
        { item: 5, index: 4 },
      ],
    });
    expect(visibleCrumbs(trail, 3, true).hidden).toBe(0);
    expect(visibleCrumbs([1, 2], 3, false).hidden).toBe(0);
  });
});

describe("filter keys", () => {
  it("parses and formats German filters", () => {
    expect(parseGermanQ("gott~NOUN")).toEqual({ lemma: "gott", pos: "NOUN" });
    expect(parseGermanQ("gott")).toEqual({ lemma: "gott" });
    expect(parseGermanQ("gott~")).toEqual({ lemma: "gott" });
    expect(formatGermanQ("gott", "NOUN")).toBe("gott~NOUN");
    expect(formatGermanQ("gott")).toBe("gott");
  });

  it("normalizes Strong's keys", () => {
    expect(normalizeOriginalQ("g2316")).toBe("G2316");
    expect(normalizeOriginalQ("H430")).toBe("H0430");
    expect(normalizeOriginalQ("H0430G")).toBe("H0430");
    expect(normalizeOriginalQ("Gott")).toBeNull();
  });
});
