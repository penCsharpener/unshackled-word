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
 * `only` with the present editions when it is in fewer than half of them, else `missing` with the absent ones.
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
  return present.length * 2 < mainCodes.length
    ? { kind: "only", codes: present }
    : { kind: "missing", codes: missing };
}
