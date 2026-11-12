import type {
  BulkGroupCount,
  BulkOccurrence,
  BulkOriginalCount,
  BulkStaleDetail,
} from "./bulkTypes";

// Pure logic of the bulk lemma replace dialog: URL state, selection across pages, flattening.

export const PAGE_SIZES = [50, 100, 250, 500] as const;
export type PageSize = (typeof PAGE_SIZES)[number];
export const DEFAULT_PAGE_SIZE: PageSize = 100;

export type BulkScope = "chapter" | "book" | "testament" | "all";
export type Testament = "ot" | "nt";
export type BulkStep = "search" | "review" | "done";
export type GroupMode = "form" | "original";

export interface BulkQuery {
  from: string[];
  to: string;
  /** part-of-speech code, "" = all */
  pos: string;
  /** original-language group keys (G4680, H2450, added, none); empty = all */
  original: string[];
  scope: BulkScope;
  testament: Testament;
  bookId: number;
  chapter: number;
}

export type QueryProblem = "noFrom" | "noTo" | "same";

/** Why the query cannot be searched yet (null = fine). */
export function queryProblem(query: BulkQuery): QueryProblem | null {
  if (query.from.length === 0) return "noFrom";
  const to = query.to.trim();
  if (!to) return "noTo";
  if (query.from.includes(to)) return "same";
  return null;
}

/** Adds a lemma to the source list (trimmed, case-sensitive, no duplicates). */
export function addFrom(from: readonly string[], value: string): string[] {
  const lemma = value.trim();
  return lemma && !from.includes(lemma) ? [...from, lemma] : [...from];
}

// ---------------------------------------------------------------- URL state

export interface BulkUrlState {
  step: BulkStep | null;
  /** only set for the review step */
  query: BulkQuery | null;
  page: number;
  size: PageSize;
  /** explicit choice of the grouping; null = automatic (original when the server says ambiguous) */
  group: GroupMode | null;
  /** `?dlg=search&from=…&orig=…`: source lemmas / original keys prefilled in the search form (shared links). */
  prefill?: { from: string[]; original: string[] };
}

const OWN_KEYS = [
  "dlg",
  "from",
  "to",
  "pos",
  "orig",
  "scope",
  "ts",
  "book",
  "ch",
  "page",
  "size",
  "grp",
] as const;

const ORIGINAL_KEY = /^(?:[GH]\d{1,5}(?:\+[GH]\d{1,5})*|added|none)$/;
export const isOriginalKey = (value: string): boolean => ORIGINAL_KEY.test(value);

function toInt(value: string | null): number | null {
  if (value === null || !/^\d+$/.test(value)) return null;
  return Number.parseInt(value, 10);
}

export function parsePageSize(value: string | null): PageSize {
  const n = toInt(value);
  return (PAGE_SIZES as readonly number[]).includes(n ?? 0) ? (n as PageSize) : DEFAULT_PAGE_SIZE;
}

/** Reads dialog, step, search and paging from search params; invalid values fall back to defaults. */
export function parseBulkUrl(params: URLSearchParams): BulkUrlState {
  const dlg = params.get("dlg");
  const step: BulkStep | null = dlg === "search" || dlg === "review" || dlg === "done" ? dlg : null;
  const page = Math.max(1, toInt(params.get("page")) ?? 1);
  const size = parsePageSize(params.get("size"));
  const grp = params.get("grp");
  const group: GroupMode | null = grp === "form" || grp === "original" ? grp : null;
  if (step === "search") {
    const from = params.getAll("from").filter((value) => value.trim() !== "");
    const original = params.getAll("orig").filter(isOriginalKey);
    if (from.length > 0 || original.length > 0)
      return { step, query: null, page, size, group, prefill: { from, original } };
  }
  if (step !== "review") return { step, query: null, page, size, group };

  const scopeParam = params.get("scope");
  const scope: BulkScope =
    scopeParam === "chapter" || scopeParam === "book" || scopeParam === "testament"
      ? scopeParam
      : "all";
  const query: BulkQuery = {
    from: params.getAll("from").filter((value) => value.trim() !== ""),
    to: params.get("to") ?? "",
    pos: params.get("pos") ?? "",
    original: params.getAll("orig").filter(isOriginalKey),
    scope,
    testament: params.get("ts") === "ot" ? "ot" : "nt",
    bookId: toInt(params.get("book")) ?? 0,
    chapter: toInt(params.get("ch")) ?? 1,
  };
  const needsBook = scope === "book" || scope === "chapter";
  // A review link without a usable search cannot be restored: it opens the search step instead.
  if (queryProblem(query) || (needsBook && query.bookId === 0)) {
    return { step: "search", query: null, page: 1, size, group };
  }
  return { step, query, page, size, group };
}

/** Returns a copy of `params` with the dialog parameters replaced; other parameters stay. */
export function withBulkUrl(
  params: URLSearchParams,
  next: {
    step: BulkStep;
    query?: BulkQuery;
    page?: number;
    size?: PageSize;
    group?: GroupMode | null;
  },
): URLSearchParams {
  const result = clearBulkUrl(params);
  result.set("dlg", next.step);
  if (next.step === "review" && next.query) {
    const { query } = next;
    for (const lemma of query.from) result.append("from", lemma);
    result.set("to", query.to);
    if (query.pos) result.set("pos", query.pos);
    for (const key of query.original) result.append("orig", key);
    result.set("scope", query.scope);
    if (query.scope === "testament") result.set("ts", query.testament);
    if (query.scope === "book" || query.scope === "chapter")
      result.set("book", String(query.bookId));
    if (query.scope === "chapter") result.set("ch", String(query.chapter));
    result.set("page", String(next.page ?? 1));
    result.set("size", String(next.size ?? DEFAULT_PAGE_SIZE));
    if (next.group) result.set("grp", next.group);
  }
  return result;
}

export function clearBulkUrl(params: URLSearchParams): URLSearchParams {
  const result = new URLSearchParams(params);
  for (const key of OWN_KEYS) result.delete(key);
  return result;
}

// ---------------------------------------------------------------- paging

export function pageCount(total: number, size: number): number {
  return Math.max(1, Math.ceil(total / size));
}

export function clampPage(page: number, total: number, size: number): number {
  return Math.min(Math.max(1, page), pageCount(total, size));
}

/** Page that shows the row with this 0-based index. */
export function pageOfIndex(index: number, size: number): number {
  return Math.floor(index / size) + 1;
}

/** Keeps the first visible row when the page size changes. */
export function pageAfterSizeChange(page: number, oldSize: number, newSize: number): number {
  return pageOfIndex((page - 1) * oldSize, newSize);
}

// ---------------------------------------------------------------- selection

export type GroupKey = string;

/** Group key of an original ("" = no original filter / unknown; added and none are their own keys). */
export function normOrigKey(key?: string, kind?: string): string {
  if (key) return key;
  return kind === "added" || kind === "none" ? kind : "";
}
/** The key the backend compares at apply time: "" for added / none. */
export const expectedOrigKey = (key: string): string =>
  key === "added" || key === "none" ? "" : key;

/** Selection key of a (original, plain word, part of speech) group. */
export const groupKey = (plainWord?: string, partOfSpeech?: string, origKey?: string): GroupKey =>
  `${origKey ?? ""}\u0000${plainWord ?? ""}\u0000${partOfSpeech ?? ""}`;
export const memberKey = (group: BulkGroupCount): GroupKey =>
  groupKey(group.plainWord, group.partOfSpeech, normOrigKey(group.origKey));
export const rowGroupKey = (
  row: Pick<BulkOccurrence, "plainWord" | "partOfSpeech" | "origKey" | "origKind">,
): GroupKey => groupKey(row.plainWord, row.partOfSpeech, normOrigKey(row.origKey, row.origKind));
export const origKeyOfGroupKey = (key: GroupKey): string => key.split("\u0000")[0] ?? "";

/**
 * Selection over all pages without knowing all rows: a base (all / none), per-group overrides and
 * single-row flips against the base of their group. Everything is counted from the server's group
 * counts, so it stays correct while only one page is loaded.
 */
export interface Selection {
  readonly mode: "all" | "none";
  /** group key -> true (all selected) / false (none selected); overrides `mode` for that group */
  readonly groups: ReadonlyMap<GroupKey, boolean>;
  /** word id -> group key, for rows whose state differs from their group's base */
  readonly flips: ReadonlyMap<number, GroupKey>;
}

export const selectAll = (): Selection => ({ mode: "all", groups: new Map(), flips: new Map() });
export const selectNone = (): Selection => ({ mode: "none", groups: new Map(), flips: new Map() });

const baseOf = (sel: Selection, key: GroupKey): boolean =>
  sel.groups.get(key) ?? sel.mode === "all";

export function isSelected(sel: Selection, id: number, key: GroupKey): boolean {
  const base = baseOf(sel, key);
  return sel.flips.has(id) ? !base : base;
}

export function selectedInGroup(sel: Selection, group: BulkGroupCount): number {
  const key = memberKey(group);
  let flipped = 0;
  for (const flipKey of sel.flips.values()) if (flipKey === key) flipped++;
  return baseOf(sel, key) ? group.count - flipped : flipped;
}

export function selectedTotal(sel: Selection, groups: readonly BulkGroupCount[]): number {
  return groups.reduce((sum, group) => sum + selectedInGroup(sel, group), 0);
}

export type TriState = "checked" | "unchecked" | "mixed";

export function groupState(sel: Selection, group: BulkGroupCount): TriState {
  const n = selectedInGroup(sel, group);
  if (n <= 0) return "unchecked";
  return n >= group.count ? "checked" : "mixed";
}

export interface RowRef {
  id: number;
  key: GroupKey;
}

/** Sets the given rows to selected / not selected. */
export function setRows(sel: Selection, rows: readonly RowRef[], on: boolean): Selection {
  const flips = new Map(sel.flips);
  for (const row of rows) {
    if (baseOf(sel, row.key) === on) flips.delete(row.id);
    else flips.set(row.id, row.key);
  }
  return { ...sel, flips };
}

/** Toggles a row: a click on a selected row deselects it. */
export function toggleRow(sel: Selection, row: RowRef): Selection {
  return setRows(sel, [row], !isSelected(sel, row.id, row.key));
}

/** Selects or deselects a whole group on all pages. */
export function setGroup(sel: Selection, key: GroupKey, on: boolean): Selection {
  const groups = new Map(sel.groups);
  groups.set(key, on);
  const flips = new Map([...sel.flips].filter(([, flipKey]) => flipKey !== key));
  return { ...sel, groups, flips };
}

/** Group checkbox: anything but fully checked selects the whole group. */
export function toggleGroup(sel: Selection, group: BulkGroupCount): Selection {
  return toggleMembers(sel, [group]);
}

/** Tri-state of several (original, form) groups taken together: a form across originals, an original across forms. */
export function membersState(sel: Selection, members: readonly BulkGroupCount[]): TriState {
  const total = members.reduce((sum, g) => sum + g.count, 0);
  const n = members.reduce((sum, g) => sum + selectedInGroup(sel, g), 0);
  if (n <= 0) return "unchecked";
  return n >= total ? "checked" : "mixed";
}
export const selectedInMembers = (sel: Selection, members: readonly BulkGroupCount[]): number =>
  members.reduce((sum, g) => sum + selectedInGroup(sel, g), 0);

/** Anything but fully checked selects all members; a fully checked one deselects them. */
export function toggleMembers(sel: Selection, members: readonly BulkGroupCount[]): Selection {
  const on = membersState(sel, members) !== "checked";
  return members.reduce((next, g) => setGroup(next, memberKey(g), on), sel);
}

/** "Seite wählen/abwählen": selects the page unless it is already completely selected. */
export function togglePage(sel: Selection, rows: readonly RowRef[]): Selection {
  const allOn = rows.every((row) => isSelected(sel, row.id, row.key));
  return setRows(sel, rows, !allOn);
}

/** Range between two rows of the same page (shift-click): all get the state `on`. */
export function rowsBetween<T>(rows: readonly T[], a: number, b: number): T[] {
  const lo = Math.max(0, Math.min(a, b));
  const hi = Math.min(rows.length - 1, Math.max(a, b));
  return rows.slice(lo, hi + 1);
}

/** Forgets the single-row flips of these ids (e.g. stale rows that must not be sent again). */
export function dropFlips(sel: Selection, ids: Iterable<number>): Selection {
  const flips = new Map(sel.flips);
  for (const id of ids) flips.delete(id);
  return { ...sel, flips };
}

/** Marks rows as deselected (stale rows after a 409). */
export const deselectRows = (sel: Selection, rows: readonly RowRef[]): Selection =>
  setRows(sel, rows, false);

export interface IdPlan {
  /** one request for every id of the search (all selected groups share one original) */
  fetchAll: boolean;
  /** the shared original of `fetchAll` ("" for added / none / unknown) */
  allOrigKey: string;
  /** else: one request per group that starts out selected */
  fetchGroups: GroupKey[];
  /** ids selected although their group starts out unselected (flips), with their original */
  extra: { id: number; origKey: string }[];
  /** ids to drop from the fetched ones (flips in selected groups) */
  remove: Set<number>;
}

/**
 * Which id requests are needed to materialise the selection. The apply body carries explicit ids
 * and, parallel to them, the original the user saw, so every id must be fetched with its original:
 * one request when all groups share one original, else one per selected (original, form) group.
 */
export function planIds(sel: Selection, groups: readonly BulkGroupCount[]): IdPlan {
  const keys = groups.map(memberKey);
  const selectedKeys = keys.filter((key) => baseOf(sel, key));
  const originals = new Set(keys.map(origKeyOfGroupKey));
  const extra: { id: number; origKey: string }[] = [];
  const remove = new Set<number>();
  for (const [id, key] of sel.flips) {
    if (baseOf(sel, key)) remove.add(id);
    else extra.push({ id, origKey: expectedOrigKey(origKeyOfGroupKey(key)) });
  }
  const fetchAll = selectedKeys.length === keys.length && keys.length > 0 && originals.size <= 1;
  return {
    fetchAll,
    allOrigKey: expectedOrigKey([...originals][0] ?? ""),
    fetchGroups: fetchAll ? [] : selectedKeys,
    extra,
    remove,
  };
}

export interface FetchedIds {
  ids: readonly number[];
  /** original shared by these ids ("" for added / none) */
  origKey: string;
}

/** Combines fetched ids with the plan: distinct ids and the expected original per id, in the same order. */
export function resolveIds(
  plan: IdPlan,
  fetched: readonly FetchedIds[],
): { wordIds: number[]; expectedOriginal: string[] } {
  const originals = new Map<number, string>();
  for (const part of fetched) {
    for (const id of part.ids) {
      if (!plan.remove.has(id)) originals.set(id, expectedOrigKey(part.origKey));
    }
  }
  for (const { id, origKey } of plan.extra) originals.set(id, origKey);
  return { wordIds: [...originals.keys()], expectedOriginal: [...originals.values()] };
}

// ---------------------------------------------------------------- original-language guard

/** Grouping in use: the user's choice if there is one, else by original when the server says ambiguous. */
export function effectiveGroupMode(
  explicit: GroupMode | null,
  ambiguous: boolean | undefined,
): GroupMode {
  return explicit ?? (ambiguous ? "original" : "form");
}

/** German content classes: for them a group without an original usually marks a mapping gap. */
export const CONTENT_POS: ReadonlySet<string> = new Set(["NOUN", "PROPN", "VERB", "ADJ"]);

const isGap = (key: string) => key === "added" || key === "none";

/**
 * Starting selection of a review. Without the server's ambiguity flag everything is selected. With
 * it, only flagged groups are deselected: minority and part-of-speech-conflict originals, and
 * added / unmapped groups of content lemmas. Synonym groups (a second large original) stay
 * selected, and deselected groups stay in the list.
 */
export function defaultSelection(
  groups: readonly BulkGroupCount[],
  byOriginal: readonly BulkOriginalCount[],
  ambiguous: boolean,
): Selection {
  let sel = selectAll();
  if (!ambiguous) return sel;
  const flagged = new Map(byOriginal.map((o) => [o.key, o]));
  for (const group of groups) {
    const key = normOrigKey(group.origKey);
    const original = flagged.get(key);
    const drop =
      original?.minority ||
      original?.posConflict ||
      ((isGap(key) || original?.added || original?.unmapped) &&
        CONTENT_POS.has(group.partOfSpeech ?? ""));
    if (drop) sel = setGroup(sel, memberKey(group), false);
  }
  return sel;
}

/** Originals (other than added / none) that have rows but none of them selected. */
export function deselectedOriginalCount(sel: Selection, groups: readonly BulkGroupCount[]): number {
  const byKey = new Map<string, BulkGroupCount[]>();
  for (const group of groups) {
    const key = normOrigKey(group.origKey);
    if (key && !isGap(key)) byKey.set(key, [...(byKey.get(key) ?? []), group]);
  }
  if (byKey.size < 2) return 0;
  let n = 0;
  for (const members of byKey.values()) if (selectedInMembers(sel, members) === 0) n++;
  return n;
}

export interface LeftOut {
  key: string;
  word?: string;
  count: number;
}

/** Original groups with rows that the apply leaves unchanged (for the result step). */
export function leftOutOriginals(
  sel: Selection,
  groups: readonly BulkGroupCount[],
  byOriginal: readonly BulkOriginalCount[],
): LeftOut[] {
  const words = new Map(byOriginal.map((o) => [o.key, o.word]));
  const byKey = new Map<string, BulkGroupCount[]>();
  for (const group of groups) {
    const key = normOrigKey(group.origKey);
    if (key) byKey.set(key, [...(byKey.get(key) ?? []), group]);
  }
  const result: LeftOut[] = [];
  for (const [key, members] of byKey) {
    const total = members.reduce((sum, g) => sum + g.count, 0);
    const left = total - selectedInMembers(sel, members);
    if (left > 0) result.push({ key, word: words.get(key), count: left });
  }
  return result.sort((a, b) => Number(isGap(a.key)) - Number(isGap(b.key)) || b.count - a.count);
}

// ---------------------------------------------------------------- list items

/** (plain word, part of speech) taken over all its originals, or one (original, form) group. */
export interface FormGroup {
  plainWord?: string;
  partOfSpeech?: string;
  count: number;
  members: BulkGroupCount[];
}

export type ListItem =
  | {
      kind: "original";
      key: string;
      original: BulkOriginalCount;
      members: BulkGroupCount[];
      onPage: number;
      collapsed: boolean;
    }
  | { kind: "group"; key: GroupKey; group: FormGroup; onPage: number }
  | { kind: "row"; row: BulkOccurrence; index: number; key: GroupKey }
  | { kind: "verse"; row: BulkOccurrence };

const originalRank = (key: string) => (key === "added" ? 1 : key === "none" ? 2 : 0);

/** The page rows in display order: by original (original mode), then by form, reading order inside. */
export function sortPageRows(
  rows: readonly BulkOccurrence[],
  groups: readonly BulkGroupCount[],
  byOriginal: readonly BulkOriginalCount[] = [],
  mode: GroupMode = "form",
): BulkOccurrence[] {
  const formOrder = new Map<string, number>();
  groups.forEach((g) => {
    const form = `${g.plainWord ?? ""}\u0000${g.partOfSpeech ?? ""}`;
    if (!formOrder.has(form)) formOrder.set(form, formOrder.size);
  });
  const originalOrder = new Map(byOriginal.map((o, i) => [o.key, i]));
  const rank = (row: BulkOccurrence) => {
    const form =
      formOrder.get(`${row.plainWord ?? ""}\u0000${row.partOfSpeech ?? ""}`) ?? formOrder.size;
    if (mode === "form") return [0, 0, form];
    const key = normOrigKey(row.origKey, row.origKind);
    return [originalRank(key), originalOrder.get(key) ?? byOriginal.length, form];
  };
  return rows
    .map((row, i) => ({ row, i, r: rank(row) }))
    .sort(
      (a, b) =>
        (a.r[0] ?? 0) - (b.r[0] ?? 0) ||
        (a.r[1] ?? 0) - (b.r[1] ?? 0) ||
        (a.r[2] ?? 0) - (b.r[2] ?? 0) ||
        a.i - b.i,
    )
    .map(({ row }) => row);
}

/**
 * Flattens one page for the virtual list. The server sends a page in reading order, so the rows are
 * regrouped: by form only (`form`), or by original and then form (`original`, two header levels).
 * Group totals and selection come from the server's counts, `onPage` counts the rows shown. A
 * collapsed original shows only its header. `index` is the position inside the sorted page.
 */
export function flattenPage(
  rows: readonly BulkOccurrence[],
  groups: readonly BulkGroupCount[],
  expanded: ReadonlySet<number>,
  options: {
    mode?: GroupMode;
    byOriginal?: readonly BulkOriginalCount[];
    collapsed?: ReadonlySet<string>;
  } = {},
): ListItem[] {
  const mode = options.mode ?? "form";
  const byOriginal = options.byOriginal ?? [];
  const collapsed = options.collapsed ?? new Set<string>();
  const sorted = sortPageRows(rows, groups, byOriginal, mode);

  const originals = new Map(byOriginal.map((o) => [o.key, o]));
  const membersOfOriginal = new Map<string, BulkGroupCount[]>();
  const membersOfForm = new Map<string, BulkGroupCount[]>();
  for (const g of groups) {
    const o = normOrigKey(g.origKey);
    membersOfOriginal.set(o, [...(membersOfOriginal.get(o) ?? []), g]);
    const form = `${g.plainWord ?? ""}\u0000${g.partOfSpeech ?? ""}`;
    membersOfForm.set(form, [...(membersOfForm.get(form) ?? []), g]);
  }
  const count = (map: Map<string, number>, key: string) => map.set(key, (map.get(key) ?? 0) + 1);
  const onPageOriginal = new Map<string, number>();
  const onPageGroup = new Map<string, number>();
  for (const row of sorted) {
    count(onPageOriginal, normOrigKey(row.origKey, row.origKind));
    count(
      onPageGroup,
      mode === "original"
        ? rowGroupKey(row)
        : `${row.plainWord ?? ""}\u0000${row.partOfSpeech ?? ""}`,
    );
  }

  const items: ListItem[] = [];
  let previousOriginal: string | null = null;
  let previousGroup: string | null = null;
  sorted.forEach((row, index) => {
    const originalKey = normOrigKey(row.origKey, row.origKind);
    const form = `${row.plainWord ?? ""}\u0000${row.partOfSpeech ?? ""}`;
    const formGroupKey = mode === "original" ? rowGroupKey(row) : form;
    if (mode === "original" && originalKey !== previousOriginal) {
      const members = membersOfOriginal.get(originalKey) ?? [];
      items.push({
        kind: "original",
        key: originalKey,
        original: originals.get(originalKey) ?? {
          key: originalKey,
          count: members.reduce((sum, g) => sum + g.count, 0),
        },
        members,
        onPage: onPageOriginal.get(originalKey) ?? 0,
        collapsed: collapsed.has(originalKey),
      });
      previousOriginal = originalKey;
      previousGroup = null;
    }
    if (mode === "original" && collapsed.has(originalKey)) return;
    if (formGroupKey !== previousGroup) {
      const members =
        mode === "original"
          ? (membersOfOriginal.get(originalKey) ?? []).filter(
              (g) => `${g.plainWord ?? ""}\u0000${g.partOfSpeech ?? ""}` === form,
            )
          : (membersOfForm.get(form) ?? []);
      items.push({
        kind: "group",
        key: formGroupKey,
        group: {
          plainWord: row.plainWord,
          partOfSpeech: row.partOfSpeech,
          count: members.reduce((sum, g) => sum + g.count, 0),
          members,
        },
        onPage: onPageGroup.get(formGroupKey) ?? 0,
      });
      previousGroup = formGroupKey;
    }
    items.push({ kind: "row", row, index, key: rowGroupKey(row) });
    if (expanded.has(row.wordId)) items.push({ kind: "verse", row });
  });
  return items;
}

/** Items the cursor can stop on (headers and rows, not the expanded verses). */
export const isNavigable = (item: ListItem): boolean => item.kind !== "verse";

export const itemId = (item: ListItem): string =>
  item.kind === "original"
    ? `o:${item.key}`
    : item.kind === "group"
      ? `g:${item.key}`
      : item.kind === "row"
        ? `r:${item.row.wordId}`
        : `v:${item.row.wordId}`;

export interface StaleInfo {
  reason: "lemma" | "original";
  lemma?: string;
  origKey?: string;
}

/** Stale rows of a 409 body: `details` carry the reason, a plain `stale` list counts as "lemma". */
export function staleDetailsOf(body: unknown): Map<number, StaleInfo> {
  const result = new Map<number, StaleInfo>();
  const record = body as { stale?: unknown; details?: unknown } | null;
  if (Array.isArray(record?.details)) {
    for (const entry of record.details as BulkStaleDetail[]) {
      if (typeof entry?.wordId === "number") {
        result.set(entry.wordId, {
          reason: entry.reason === "original" ? "original" : "lemma",
          lemma: entry.lemma,
          origKey: entry.origKey,
        });
      }
    }
  }
  if (Array.isArray(record?.stale)) {
    for (const entry of record.stale) {
      const id = typeof entry === "number" ? entry : (entry as { wordId?: number })?.wordId;
      if (typeof id === "number" && !result.has(id)) result.set(id, { reason: "lemma" });
    }
  }
  return result;
}
