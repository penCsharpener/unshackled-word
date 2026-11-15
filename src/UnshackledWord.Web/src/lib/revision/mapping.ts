import type { GetVerseMappingResponse } from "@/lib/bible/types";
import type { RevisionCarryReport, RevisionVerseMappingResponse } from "./types";

/** Words the maintainer should look at again. Empty once the mapping was saved by a writer. */
export interface ReviewMarkers {
  /** German words that are `changed` or `new` */
  german: Set<number>;
  /** original words without any link (restricted to the ones that lost their link when the report is known) */
  original: Set<number>;
}

export function reviewMarkers(
  mapping: RevisionVerseMappingResponse,
  carry?: RevisionCarryReport,
): ReviewMarkers {
  const markers: ReviewMarkers = { german: new Set(), original: new Set() };
  if (mapping.mappingReviewedAt) return markers;
  for (const word of mapping.germanWords) {
    if (word.change !== "same") markers.german.add(word.id);
  }
  const linked = new Set(
    mapping.links.flatMap((l) => (l.stepWordId === undefined ? [] : [l.stepWordId])),
  );
  const lost = carry ? new Set(carry.lostOriginalWordIds) : undefined;
  for (const word of mapping.originalWords) {
    if (!linked.has(word.id) && (!lost || lost.has(word.id))) markers.original.add(word.id);
  }
  return markers;
}

/** The revision mapping in the shape `MappingEditor` and `OriginalVerse` take; only the editable flag is added. */
export function toMappingData(
  mapping: RevisionVerseMappingResponse,
  editingEnabled: boolean,
): GetVerseMappingResponse {
  return {
    bibleBookId: mapping.bibleBookId,
    chapter: mapping.chapter,
    verse: mapping.verse,
    language: mapping.language,
    editingEnabled,
    germanWords: mapping.germanWords,
    originalWords: mapping.originalWords,
    links: mapping.links,
  };
}
