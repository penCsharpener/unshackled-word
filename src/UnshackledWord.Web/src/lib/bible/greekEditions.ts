import { useCallback, useState } from "react";
import type { GreekEdition } from "./types";

/** The eight printed main editions in catalog order; used until the catalog (`useGreekEditions`) is loaded. */
export const FALLBACK_MAIN_EDITIONS = ["NA28", "NA27", "Tyn", "SBL", "WH", "Treg", "TR", "Byz"];

/** Main edition codes from the catalog (`isMain`, catalog order); the fallback list while it is not loaded. */
export function mainEditionCodes(catalog: readonly GreekEdition[] | undefined): string[] {
  const main = catalog?.filter((e) => e.isMain).map((e) => e.code) ?? [];
  return main.length > 0 ? main : FALLBACK_MAIN_EDITIONS;
}

/**
 * The fact behind the Grundtext mark of a word that is not in all main editions (roadmap 0025.5f):
 * `only` with the present editions when it is in fewer than half of them, else `missing` with the absent ones
 * (also when no main edition has it, e.g. a word only in NIV/KJV or a manuscript: "nur in" would list nothing).
 * Undefined for a word without `editionCodes` (in all main editions).
 */
export function editionVariant(
  editionCodes: readonly string[] | undefined,
  mainCodes: readonly string[],
): { kind: "only" | "missing"; codes: string[] } | undefined {
  if (!editionCodes) return undefined;
  const present = mainCodes.filter((code) => editionCodes.includes(code));
  const missing = mainCodes.filter((code) => !editionCodes.includes(code));
  if (missing.length === 0) return undefined;
  return present.length > 0 && present.length * 2 < mainCodes.length
    ? { kind: "only", codes: present }
    : { kind: "missing", codes: missing };
}

/**
 * The edition filter of the Grundtext panel (roadmap 0025.6f): the words of a verse that stay visible under the
 * selected main edition (`null` = "Alle") and how many were hidden. Only a word with `editionCodes` that lacks the code
 * is hidden; a word without them is in all main editions. Order and spelling are untouched: this is the text of the
 * data minus foreign words, not a rebuilt edition.
 */
export function filterWordsByEdition<T extends { editionCodes?: string[] }>(
  words: readonly T[],
  edition: string | null,
): { visible: T[]; hidden: number } {
  if (!edition) return { visible: [...words], hidden: 0 };
  const visible = words.filter((w) => !w.editionCodes || w.editionCodes.includes(edition));
  return { visible, hidden: words.length - visible.length };
}

export const GREEK_EDITION_KEY = "uw.reader.greekEdition";

/** Stored edition code; null ("Alle") when nothing (or no storage) is there. */
export function readGreekEdition(): string | null {
  try {
    return localStorage.getItem(GREEK_EDITION_KEY) || null;
  } catch {
    return null;
  }
}

/** The reader's edition filter, global for all chapters and remembered in this browser. */
export function useGreekEditionFilter() {
  const [edition, setEdition] = useState<string | null>(readGreekEdition);
  const update = useCallback((next: string | null) => {
    try {
      if (next) localStorage.setItem(GREEK_EDITION_KEY, next);
      else localStorage.removeItem(GREEK_EDITION_KEY);
    } catch {
      // storage unavailable: the choice only lasts for this page view
    }
    setEdition(next);
  }, []);
  return [edition, update] as const;
}
