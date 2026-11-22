import { describe, expect, it } from "vitest";
import { classifyHref, dropUnsafeLinks, passageSpansOfPath } from "./bibleLink";

describe("classifyHref", () => {
  it("routes reader paths, opens http(s) outside and rejects the rest", () => {
    expect(classifyHref("/bible/45/12?v=1-2#v1")).toBe("bible");
    expect(classifyHref("/bible/43/3")).toBe("bible");
    expect(classifyHref("https://example.org/x")).toBe("external");
    expect(classifyHref("http://example.org")).toBe("external");
    for (const href of [
      "javascript:alert(1)",
      "JaVaScRiPt:alert(1)",
      "data:text/html,x",
      "//evil.example",
      "/admin",
      "mailto:a@b.c",
      "",
      null,
    ]) {
      expect(classifyHref(href)).toBe("unsafe");
    }
  });
});

describe("passageSpansOfPath", () => {
  it("turns verse ranges into ref-id spans and gives no span for a chapter", () => {
    expect(passageSpansOfPath("/bible/45/12?v=1-2#v1")).toBe("45012001-45012002");
    expect(passageSpansOfPath("/bible/45/8?v=28#v28")).toBe("45008028");
    expect(passageSpansOfPath("/bible/45/8?v=1,3-4#v1")).toBe("45008001,45008003-45008004");
    expect(passageSpansOfPath("/bible/43/3")).toBeUndefined();
  });
});

describe("dropUnsafeLinks", () => {
  it("reduces unsafe links to their text and keeps safe ones and code", () => {
    expect(dropUnsafeLinks("a [x](javascript:alert(1)) b")).toBe("a x b");
    expect(dropUnsafeLinks("[x](data:text/html,y)")).toBe("x");
    expect(dropUnsafeLinks("![i](javascript:x)")).toBe("");
    expect(dropUnsafeLinks("<javascript:alert(1)>")).toBe("javascript:alert(1)");
    expect(dropUnsafeLinks("[Röm 8,28](/bible/45/8?v=28#v28) [e](https://example.org)")).toBe(
      "[Röm 8,28](/bible/45/8?v=28#v28) [e](https://example.org)",
    );
    expect(dropUnsafeLinks("`[x](javascript:y)`")).toBe("`[x](javascript:y)`");
  });
});
