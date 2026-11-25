import type { CrossReference, GetVerseCrossReferenceTextsResponse } from "./types";

/**
 * Keys of the TSK links that flash because a hovered word's original word (Strong's key of one of its senses) occurs in
 * one of the link's target verses. The API leaves very frequent lemmas out of a verse's `strongs`, so those never match.
 * Hidden links never flash. `keyOf` is the key the link list uses (`crossReferenceKey`).
 */
export function strongsFlashKeys(
  senseKeys: readonly string[],
  references: GetVerseCrossReferenceTextsResponse["references"] | undefined,
  keyOf: (reference: CrossReference) => string,
): Set<string> {
  const keys = new Set<string>();
  if (senseKeys.length === 0 || !references) return keys;
  const wanted = new Set(senseKeys);
  for (const reference of references) {
    if (reference.rating === "hide") continue;
    if (reference.verses.some((verse) => verse.strongs?.some((key) => wanted.has(key)))) {
      keys.add(keyOf(reference));
    }
  }
  return keys;
}
