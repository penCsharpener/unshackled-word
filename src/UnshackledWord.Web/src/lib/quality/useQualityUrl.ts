import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router";
import {
  jumpToCrumb,
  parseQualityUrl,
  pushFilter,
  type QualityPatch,
  type QualityUrlState,
  type TrailEntry,
  withQualityUrl,
} from "./url";

/** Reads / writes the data-quality page state (filters, trail, selection) in the URL search params. */
export function useQualityUrl() {
  const [params, setParams] = useSearchParams();
  const state: QualityUrlState = useMemo(() => parseQualityUrl(params), [params]);

  const update = useCallback(
    (patch: QualityPatch, replace = false) =>
      setParams((prev) => withQualityUrl(prev, patch), { replace, preventScrollReset: true }),
    [setParams],
  );

  /** A new explorer filter: pushes a history entry and extends the trail. */
  const openFilter = useCallback(
    (entry: TrailEntry) =>
      setParams((prev) => withQualityUrl(prev, pushFilter(parseQualityUrl(prev).trail, entry)), {
        preventScrollReset: true,
      }),
    [setParams],
  );

  const goToCrumb = useCallback(
    (index: number) =>
      setParams(
        (prev) => {
          const target = jumpToCrumb(parseQualityUrl(prev).trail, index);
          return target ? withQualityUrl(prev, target) : prev;
        },
        { preventScrollReset: true },
      ),
    [setParams],
  );

  return { state, update, openFilter, goToCrumb };
}
