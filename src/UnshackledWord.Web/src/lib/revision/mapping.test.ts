import { describe, expect, it } from "vitest";
import { reviewMarkers, toMappingData } from "./mapping";
import type { RevisionVerseMappingResponse } from "./types";

const mapping: RevisionVerseMappingResponse = {
  revisionVerseId: 1,
  level: 200,
  version: 1,
  bibleBookId: 43,
  chapter: 3,
  verse: 16,
  language: "greek",
  germanWords: [
    { id: 1, positionInVerse: 1, text: "Denn", change: "same" },
    { id: 2, positionInVerse: 2, text: "einzigen", change: "changed" },
    { id: 3, positionInVerse: 3, text: "sehr", change: "new" },
  ],
  originalWords: [
    { id: 10, position: 1, original: "ὁ" },
    { id: 20, position: 2, original: "μονογενῆ" },
    { id: 30, position: 3, original: "υἱόν" },
  ],
  links: [
    { elbWordId: 1, stepWordId: 10, isAddedWord: false, origin: "carried" },
    { elbWordId: 2, stepWordId: 20, isAddedWord: false, origin: "carried" },
  ],
};

describe("reviewMarkers", () => {
  it("marks changed and new German words and unlinked original words", () => {
    const markers = reviewMarkers(mapping);
    expect([...markers.german]).toEqual([2, 3]);
    expect([...markers.original]).toEqual([30]);
  });

  it("limits original markers to the lost ones when the carry report is known", () => {
    const markers = reviewMarkers(mapping, {
      same: 1,
      changed: 1,
      new: 1,
      lostOriginalWordIds: [],
    });
    expect([...markers.original]).toEqual([]);
  });

  it("is empty once the mapping was reviewed", () => {
    const markers = reviewMarkers({ ...mapping, mappingReviewedAt: "2026-11-13T10:00:00Z" });
    expect(markers.german.size + markers.original.size).toBe(0);
  });
});

describe("toMappingData", () => {
  it("keeps the words and links and sets the editable flag", () => {
    const data = toMappingData(mapping, true);
    expect(data.editingEnabled).toBe(true);
    expect(data.links).toHaveLength(2);
    expect(data.germanWords[1]?.text).toBe("einzigen");
  });
});
