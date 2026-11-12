import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router";
import {
  type BulkQuery,
  type BulkStep,
  type BulkUrlState,
  clearBulkUrl,
  type GroupMode,
  type PageSize,
  parseBulkUrl,
  withBulkUrl,
} from "./bulk";

export interface BulkUrlTarget {
  step: BulkStep;
  query?: BulkQuery;
  page?: number;
  size?: PageSize;
  group?: GroupMode | null;
}

/** Dialog, step, search and paging of the bulk lemma replace live in the URL's search params. */
export function useBulkUrl() {
  const [params, setParams] = useSearchParams();
  const url: BulkUrlState = useMemo(() => parseBulkUrl(params), [params]);
  const go = useCallback(
    (target: BulkUrlTarget, replace = false) =>
      setParams((prev) => withBulkUrl(prev, { group: parseBulkUrl(prev).group, ...target }), {
        replace,
      }),
    [setParams],
  );
  const close = useCallback(
    (replace = false) => setParams((prev) => clearBulkUrl(prev), { replace }),
    [setParams],
  );
  return { url, go, close };
}
