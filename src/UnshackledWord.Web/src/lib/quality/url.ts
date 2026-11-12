import type { CoverageMetric, TestamentFacet } from "./types";

// All page state of the data-quality page lives in the URL search params.
//   t       testament facet: ot | nt (absent = both)
//   book    book facet (bibleBookId)
//   check   selected check of the findings list (U1, M3 ...)
//   f       findings facets of the selected check, e.g. "class=T,pos=NOUN"
//   x       explorer side: o (original) | d (German)
//   q       explorer filter: G2316 / H0430 (original) or gott~NOUN (German lemma ~ PoS)
//   trail   filter history, comma list of side-prefixed q values ("o:G2962,d:herr~NOUN")
//   m       heatmap metric: german | source
//   fw      "1" = unmapped panels include function words
//   page, size  findings paging

export const PAGE_SIZES = [50, 100, 250] as const;
export type PageSize = (typeof PAGE_SIZES)[number];
export const DEFAULT_PAGE_SIZE: PageSize = 50;
export const MAX_TRAIL = 8;

export type ExplorerSide = "o" | "d";

export interface TrailEntry {
  side: ExplorerSide;
  q: string;
}

export interface QualityUrlState {
  testament?: TestamentFacet;
  bookId?: number;
  check?: string;
  facets: Record<string, string>;
  side: ExplorerSide;
  q: string;
  trail: TrailEntry[];
  metric: CoverageMetric;
  functionWords: boolean;
  page: number;
  size: PageSize;
}

const positiveInt = (value: string | null): number | undefined =>
  value && /^\d+$/.test(value) && Number(value) > 0 ? Number(value) : undefined;

export function parseFacets(value: string | null): Record<string, string> {
  const result: Record<string, string> = {};
  if (!value) return result;
  for (const part of value.split(",")) {
    const eq = part.indexOf("=");
    if (eq <= 0) continue;
    const key = part.slice(0, eq).trim();
    const val = part.slice(eq + 1).trim();
    if (/^[A-Za-z]+$/.test(key) && val) result[key] = val;
  }
  return result;
}

export function formatFacets(facets: Record<string, string>): string {
  return Object.entries(facets)
    .filter(([, value]) => value !== "")
    .map(([key, value]) => `${key}=${value.replaceAll(",", " ")}`)
    .join(",");
}

export function parseTrail(value: string | null): TrailEntry[] {
  if (!value) return [];
  const entries: TrailEntry[] = [];
  for (const part of value.split(",")) {
    const match = /^([od]):(.+)$/.exec(part.trim());
    if (match) entries.push({ side: match[1] as ExplorerSide, q: match[2] as string });
  }
  return entries.slice(-MAX_TRAIL);
}

export function formatTrail(trail: readonly TrailEntry[]): string {
  return trail.map((entry) => `${entry.side}:${entry.q.replaceAll(",", " ")}`).join(",");
}

export function parseQualityUrl(params: URLSearchParams): QualityUrlState {
  const t = params.get("t");
  const x = params.get("x");
  const m = params.get("m");
  const size = positiveInt(params.get("size"));
  return {
    testament: t === "ot" || t === "nt" ? t : undefined,
    bookId: positiveInt(params.get("book")),
    check: /^[A-Z]\d{1,2}$/.test(params.get("check") ?? "")
      ? (params.get("check") ?? undefined)
      : undefined,
    facets: parseFacets(params.get("f")),
    side: x === "d" ? "d" : "o",
    q: params.get("q")?.trim() ?? "",
    trail: parseTrail(params.get("trail")),
    metric: m === "source" ? "source" : "german",
    functionWords: params.get("fw") === "1",
    page: positiveInt(params.get("page")) ?? 1,
    size: PAGE_SIZES.find((candidate) => candidate === size) ?? DEFAULT_PAGE_SIZE,
  };
}

/** Everything a caller may change; omitted keys keep their value. `undefined` values clear a key. */
export type QualityPatch = Partial<{
  [K in keyof QualityUrlState]: QualityUrlState[K] | undefined;
}>;

/** Facets that change the data set: findings go back to page 1 and a stale selection is dropped. */
const RESETS_PAGE: (keyof QualityPatch)[] = ["testament", "bookId", "check", "facets", "size"];

/** Applies `patch` to the current params and returns the new params (the input is not mutated). */
export function withQualityUrl(prev: URLSearchParams, patch: QualityPatch): URLSearchParams {
  const next = new URLSearchParams(prev);
  const set = (key: string, value: string | undefined) => {
    if (value === undefined || value === "") next.delete(key);
    else next.set(key, value);
  };
  if ("testament" in patch) set("t", patch.testament);
  if ("bookId" in patch) set("book", patch.bookId === undefined ? undefined : String(patch.bookId));
  if ("check" in patch) set("check", patch.check);
  if ("facets" in patch) set("f", formatFacets(patch.facets ?? {}));
  if ("side" in patch) set("x", patch.side === "d" ? "d" : undefined);
  if ("q" in patch) set("q", patch.q);
  if ("trail" in patch) set("trail", formatTrail(patch.trail ?? []));
  if ("metric" in patch) set("m", patch.metric === "source" ? "source" : undefined);
  if ("functionWords" in patch) set("fw", patch.functionWords ? "1" : undefined);
  if ("size" in patch)
    set("size", patch.size && patch.size !== DEFAULT_PAGE_SIZE ? String(patch.size) : undefined);
  if ("page" in patch) set("page", patch.page && patch.page > 1 ? String(patch.page) : undefined);
  else if (RESETS_PAGE.some((key) => key in patch)) next.delete("page");
  return next;
}

/** Drops all facets (testament, book) but keeps the explorer and the selected check. */
export function resetFacetsPatch(): QualityPatch {
  return { testament: undefined, bookId: undefined, facets: {}, page: 1 };
}

// ---- explorer filters and trail ------------------------------------------------------------

export interface GermanFilter {
  lemma: string;
  pos?: string;
}

export function parseGermanQ(q: string): GermanFilter {
  const at = q.lastIndexOf("~");
  if (at < 0) return { lemma: q };
  const pos = q.slice(at + 1);
  return pos ? { lemma: q.slice(0, at), pos } : { lemma: q.slice(0, at) };
}

export function formatGermanQ(lemma: string, pos?: string): string {
  return pos ? `${lemma}~${pos}` : lemma;
}

/** Original filter: "G2424" / "H0430" (a trailing letter variant is cut off); null when invalid. */
export function normalizeOriginalQ(q: string): string | null {
  const match = /^([GH])0*(\d+)/i.exec(q.trim());
  if (!match) return null;
  return `${(match[1] as string).toUpperCase()}${(match[2] as string).padStart(4, "0")}`;
}

/** A new filter: becomes the current one and is appended to the trail (oldest entries drop off). */
export function pushFilter(
  trail: readonly TrailEntry[],
  entry: TrailEntry,
): { side: ExplorerSide; q: string; trail: TrailEntry[] } {
  const last = trail[trail.length - 1];
  const same = last && last.side === entry.side && last.q === entry.q;
  const next = same ? [...trail] : [...trail, entry];
  return { side: entry.side, q: entry.q, trail: next.slice(-MAX_TRAIL) };
}

/** Back to crumb `index`: the trail is cut after it and it becomes the current filter. */
export function jumpToCrumb(
  trail: readonly TrailEntry[],
  index: number,
): { side: ExplorerSide; q: string; trail: TrailEntry[] } | null {
  const entry = trail[index];
  if (!entry) return null;
  return { side: entry.side, q: entry.q, trail: trail.slice(0, index + 1) };
}

export function clearTrailPatch(): QualityPatch {
  return { trail: [], q: "" };
}

/** Crumbs shown on phones: the last `keep`, and how many are folded away before them. */
export function visibleCrumbs<T>(trail: readonly T[], keep: number, expanded: boolean) {
  if (expanded || trail.length <= keep)
    return { hidden: 0, shown: trail.map((item, i) => ({ item, index: i })) };
  const offset = trail.length - keep;
  return {
    hidden: offset,
    shown: trail.slice(offset).map((item, i) => ({ item, index: offset + i })),
  };
}
