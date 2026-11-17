import { describe, expect, it } from "vitest";
import {
  decodeRefId,
  formatCost,
  formatRunRange,
  formatTokens,
  skippedVersesFromNote,
} from "./format";
import { mcpConfigSnippet } from "./mcp";
import { apiTokenState, canResumeRun, isRunActive, runTone } from "./types";

describe("run range", () => {
  it("decodes BBCCCVVV", () => {
    expect(decodeRefId(41001008)).toEqual({ book: 41, chapter: 1, verse: 8 });
    expect(decodeRefId(19119176)).toEqual({ book: 19, chapter: 119, verse: 176 });
  });

  it("writes one verse, a verse range and a range over chapters", () => {
    expect(formatRunRange({ fromRefId: 41001001, toRefId: 41001001 }, "Markus")).toBe("Markus 1,1");
    expect(formatRunRange({ fromRefId: 41001001, toRefId: 41001008 }, "Markus")).toBe(
      "Markus 1,1–8",
    );
    expect(formatRunRange({ fromRefId: 41001040, toRefId: 41002005 }, "Markus")).toBe(
      "Markus 1,40 – 2,5",
    );
  });
});

describe("numbers", () => {
  it("formats tokens and cost for the language", () => {
    expect(formatTokens(1234567, "de")).toBe("1.234.567");
    expect(formatTokens(1234567, "en")).toBe("1,234,567");
    expect(formatCost(0.4321, "en")).toBe("$0.43");
    expect(formatCost(0.0012, "en")).toBe("$0.0012");
    expect(formatCost(0, "en")).toBe("$0.00");
  });
});

describe("status helpers", () => {
  it("knows active, resumable and tone", () => {
    expect(["queued", "running", "waiting-for-provider"].every(isRunActive)).toBe(true);
    expect(["completed", "failed", "cancelled", "budget-exceeded"].some(isRunActive)).toBe(false);
    expect(["failed", "cancelled", "budget-exceeded"].every(canResumeRun)).toBe(true);
    expect(canResumeRun("completed") || canResumeRun("running")).toBe(false);
    expect(runTone("completed")).toBe("ok");
    expect(runTone("budget-exceeded")).toBe("warning");
    expect(runTone("failed")).toBe("error");
    expect(runTone("running")).toBe("info");
    expect(runTone("cancelled")).toBe("neutral");
  });

  it("a revoked token stays revoked even when it also expired", () => {
    expect(apiTokenState({ active: true })).toBe("active");
    expect(apiTokenState({ active: false })).toBe("expired");
    expect(apiTokenState({ active: false, revokedAt: "2026-11-01T00:00:00Z" })).toBe("revoked");
  });
});

describe("mcpConfigSnippet", () => {
  it("is valid JSON with the bearer token and the url", () => {
    const parsed = JSON.parse(mcpConfigSnippet("uwt_x", "https://example.org/mcp"));
    expect(parsed.mcpServers["unshackled-word"]).toEqual({
      type: "http",
      url: "https://example.org/mcp",
      headers: { Authorization: "Bearer uwt_x" },
    });
  });
});

describe("skippedVersesFromNote", () => {
  it("reads the count of a lemma run's note and is 0 otherwise", () => {
    expect(skippedVersesFromNote("2 package(s) completed. 12 verse(s) without work skipped.")).toBe(
      12,
    );
    expect(skippedVersesFromNote("2 package(s) completed.")).toBe(0);
    expect(skippedVersesFromNote(undefined)).toBe(0);
  });
});
