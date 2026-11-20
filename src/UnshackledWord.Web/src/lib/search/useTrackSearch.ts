import { useEffect, useRef } from "react";
import { resultsBucket, track } from "@/lib/analytics";

/**
 * Counts one search per `key` once its result count is known (`undefined` = not there yet). The key only
 * de-duplicates inside the page and is never sent; the event carries the kind and a result bucket.
 */
export function useTrackSearch(
  kind: "verses" | "originals" | "references",
  key: string,
  count: number | undefined,
): void {
  const last = useRef("");
  useEffect(() => {
    if (key === "" || count === undefined || last.current === key) return;
    last.current = key;
    track("search", { kind, results: resultsBucket(count) });
  }, [kind, key, count]);
}
