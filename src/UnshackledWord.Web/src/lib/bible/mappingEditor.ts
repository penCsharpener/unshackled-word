import type { MappingRole, VerseMappingLink, VerseMappingOriginalWord } from "./types";

/**
 * Pure state of the mapping editor. A verse mapping is a set of edges (German word - original word)
 * plus "added words": German words without an original that belong to another German word, plus "splits":
 * compound German words (`Gerstenernte` = `Gersten` + `ernte`) whose parts are linked on their own.
 * All operations return the same object when nothing changes, so callers can tell a no-op.
 */

export type Side = "g" | "o";
/** Key of a word tile: `g12` (German word 12), `g12.2` (its second part) or `o5` (original word 5). */
export type NodeKey = `${Side}${number}`;

export interface MappingState {
  /** `${germanRef}|${originalId}`; germanRef is `12` (whole word) or `12.2` (part 2 of a split word) */
  readonly edges: ReadonlySet<string>;
  /** added German word -> German word it belongs to (never itself an added word) */
  readonly added: ReadonlyMap<number, number>;
  /** split German word -> texts of its parts in order (at least two); its edges all belong to a part */
  readonly splits: ReadonlyMap<number, readonly string[]>;
  /** Roadmap 0018: role, morpheme and multi-head flag of an edge (keyed like `edges`); only edges that differ from the default. */
  readonly roles: ReadonlyMap<string, EdgeRole>;
  /** Roadmap 0018: lemma of a compound part, keyed by its German ref (`12.2`). */
  readonly partLemmas: ReadonlyMap<string, string>;
}

/** Non-default link data of an edge; an edge without an entry is a head on the main morpheme. */
export interface EdgeRole {
  unit?: true;
  /** Hebrew morpheme (`H9001`); undefined = main morpheme */
  morpheme?: string;
  /** several heads accepted on this morpheme (only on heads) */
  multiHead?: true;
}

export interface Edge {
  germanId: number;
  originalId: number;
  /** 1-based part of a split German word; undefined for a whole-word edge */
  part?: number;
}
export interface AddedWord {
  germanId: number;
  parentId: number;
}
export interface SplitWord {
  germanId: number;
  parts: readonly string[];
}

/** Length of the part column on the server (`GermanWordParts.MaxPartLength`). */
export const MAX_PART_LENGTH = 30;

export const emptyState: MappingState = {
  edges: new Set(),
  added: new Map(),
  splits: new Map(),
  roles: new Map(),
  partLemmas: new Map(),
};

/** Keeps only the role entries of edges that still exist (a removed and re-added link starts as a plain head). */
function withEdges(state: MappingState, edges: ReadonlySet<string>): ReadonlyMap<string, EdgeRole> {
  if ([...state.roles.keys()].every((key) => edges.has(key))) return state.roles;
  return new Map([...state.roles].filter(([key]) => edges.has(key)));
}

/** Drops the part lemmas of one German word. */
function withoutPartLemmas(state: MappingState, germanId: number): ReadonlyMap<string, string> {
  const prefix = `${germanId}.`;
  if (![...state.partLemmas.keys()].some((key) => key.startsWith(prefix))) return state.partLemmas;
  return new Map([...state.partLemmas].filter(([key]) => !key.startsWith(prefix)));
}

export const nodeKey = (side: Side, id: number, part?: number): NodeKey =>
  part === undefined ? `${side}${id}` : (`${side}${id}.${part}` as NodeKey);
export function parseNode(node: string): { side: Side; id: number; part?: number } {
  const [id, part] = node.slice(1).split(".");
  return {
    side: node.startsWith("o") ? "o" : "g",
    id: Number(id),
    ...(part === undefined ? {} : { part: Number(part) }),
  };
}

export const germanRef = (germanId: number, part?: number) =>
  part === undefined ? `${germanId}` : `${germanId}.${part}`;
export const edgeKey = (germanId: number, originalId: number, part?: number) =>
  `${germanRef(germanId, part)}|${originalId}`;
export function parseEdge(key: string): Edge {
  const [g, o] = key.split("|");
  const [id, part] = (g ?? "").split(".");
  return {
    germanId: Number(id),
    originalId: Number(o),
    ...(part === undefined ? {} : { part: Number(part) }),
  };
}

/** Tiles of a German word in reading order: its parts when split, else the word itself. */
export function germanNodes(state: MappingState, germanId: number): NodeKey[] {
  const parts = state.splits.get(germanId);
  return parts
    ? parts.map((_, index) => nodeKey("g", germanId, index + 1))
    : [nodeKey("g", germanId)];
}

export function linksToState(links: VerseMappingLink[]): MappingState {
  const added = new Map<number, number>();
  const raw: {
    germanId: number;
    originalId: number;
    order?: number;
    role?: EdgeRole;
    partLemma?: string;
  }[] = [];
  const partTexts = new Map<number, Map<number, string>>();
  for (const link of links) {
    if (link.isAddedWord && link.parentElbWordId !== undefined) {
      added.set(link.elbWordId, link.parentElbWordId);
    } else if (link.stepWordId !== undefined) {
      const order =
        link.partOrder !== undefined && link.germanWordPart ? link.partOrder : undefined;
      const role: EdgeRole = {
        ...(link.role === "unit" ? { unit: true as const } : {}),
        ...(link.strongsNumber ? { morpheme: link.strongsNumber } : {}),
        ...(link.allowMultiHead && link.role !== "unit" ? { multiHead: true as const } : {}),
      };
      raw.push({
        germanId: link.elbWordId,
        originalId: link.stepWordId,
        order,
        role: Object.keys(role).length > 0 ? role : undefined,
        partLemma: order !== undefined && link.partLemma ? link.partLemma : undefined,
      });
      if (order !== undefined && link.germanWordPart) {
        const texts = partTexts.get(link.elbWordId) ?? new Map<number, string>();
        texts.set(order, link.germanWordPart);
        partTexts.set(link.elbWordId, texts);
      }
    }
  }
  // A word with parts keeps them only when it has two or more; the order numbers become the rank (1..n).
  const splits = new Map<number, readonly string[]>();
  const rank = new Map<number, Map<number, number>>();
  for (const [germanId, texts] of partTexts) {
    const orders = [...texts.keys()].sort((a, b) => a - b);
    if (orders.length < 2) continue;
    splits.set(
      germanId,
      orders.map((order) => texts.get(order) as string),
    );
    rank.set(germanId, new Map(orders.map((order, index) => [order, index + 1])));
  }
  const edges = new Set<string>();
  const roles = new Map<string, EdgeRole>();
  const partLemmas = new Map<string, string>();
  for (const { germanId, originalId, order, role, partLemma } of raw) {
    const ranks = rank.get(germanId);
    const part = ranks === undefined ? undefined : order === undefined ? null : ranks.get(order);
    if (part === null) continue;
    const key = edgeKey(germanId, originalId, part);
    edges.add(key);
    if (role) roles.set(key, role);
    if (part !== undefined && partLemma) partLemmas.set(germanRef(germanId, part), partLemma);
  }
  return { edges, added, splits, roles, partLemmas };
}

/**
 * Rows to PUT: edges first, then added words, each in German reading order. Unmapped words have no row.
 *
 * `roles: false` (default) writes the legacy payload without any role field. `roles: true` writes `role` on every real link plus
 * `strongsNumber`, `allowMultiHead` and `partLemma` where set. The server switches to strict role-aware validation (one head per
 * morpheme) as soon as any link carries one of these fields, so the caller sends them only when it must (`needsRoleFields`).
 */
export function stateToLinks(
  state: MappingState,
  germanOrder: number[],
  originalOrder: number[],
  options: { roles?: boolean } = {},
): VerseMappingLink[] {
  const gPos = new Map(germanOrder.map((id, i) => [id, i]));
  const oPos = new Map(originalOrder.map((id, i) => [id, i]));
  const pos = (m: Map<number, number>, id: number) => m.get(id) ?? Number.MAX_SAFE_INTEGER;
  const edges = [...state.edges]
    .map(parseEdge)
    .sort(
      (a, b) =>
        pos(gPos, a.germanId) - pos(gPos, b.germanId) ||
        (a.part ?? 0) - (b.part ?? 0) ||
        pos(oPos, a.originalId) - pos(oPos, b.originalId),
    )
    .map((e): VerseMappingLink => {
      const text = e.part === undefined ? undefined : state.splits.get(e.germanId)?.[e.part - 1];
      const link: VerseMappingLink = {
        elbWordId: e.germanId,
        stepWordId: e.originalId,
        isAddedWord: false,
        ...(text === undefined ? {} : { partOrder: e.part, germanWordPart: text }),
      };
      if (!options.roles) return link;
      const role = state.roles.get(edgeKey(e.germanId, e.originalId, e.part));
      const lemma =
        text === undefined
          ? undefined
          : state.partLemmas.get(germanRef(e.germanId, e.part))?.trim();
      return {
        ...link,
        role: role?.unit ? "unit" : "head",
        ...(role?.morpheme ? { strongsNumber: role.morpheme } : {}),
        ...(lemma ? { partLemma: lemma } : {}),
        ...(role?.multiHead && !role.unit ? { allowMultiHead: true } : {}),
      };
    });
  const added = [...state.added]
    .sort(([a], [b]) => pos(gPos, a) - pos(gPos, b))
    .map(([id, parent]) => ({ elbWordId: id, isAddedWord: true, parentElbWordId: parent }));
  return [...edges, ...added];
}

export function hasEdge(
  state: MappingState,
  germanId: number,
  originalId: number,
  part?: number,
): boolean {
  return state.edges.has(edgeKey(germanId, originalId, part));
}

/** Edges of one tile: a German word (all its parts, or just `part`) or an original word. */
export function edgesOf(state: MappingState, side: Side, id: number, part?: number): Edge[] {
  return [...state.edges].map(parseEdge).filter((e) => {
    if (side === "o") return e.originalId === id;
    return e.germanId === id && (part === undefined || e.part === part);
  });
}

/** Ids of the words on the other side that `id` (or its `part`) is connected to, without repeats. */
export function partners(state: MappingState, side: Side, id: number, part?: number): number[] {
  const ids = edgesOf(state, side, id, part).map((e) => (side === "g" ? e.originalId : e.germanId));
  return [...new Set(ids)];
}

/** Added words that belong to `germanId`. */
export function addedChildren(state: MappingState, germanId: number): number[] {
  return [...state.added].filter(([, parent]) => parent === germanId).map(([id]) => id);
}

/** True when `part` is a valid part of `germanId` (undefined is valid for a word that is not split). */
function validPart(state: MappingState, germanId: number, part?: number): boolean {
  const parts = state.splits.get(germanId);
  if (part === undefined) return parts === undefined;
  return parts !== undefined && Number.isInteger(part) && part >= 1 && part <= parts.length;
}

export function connect(
  state: MappingState,
  germanId: number,
  originalId: number,
  part?: number,
): MappingState {
  if (!validPart(state, germanId, part) || hasEdge(state, germanId, originalId, part)) return state;
  const edges = new Set(state.edges).add(edgeKey(germanId, originalId, part));
  const added = new Map(state.added);
  added.delete(germanId);
  return { ...state, edges, added };
}

export function disconnect(
  state: MappingState,
  germanId: number,
  originalId: number,
  part?: number,
): MappingState {
  if (!hasEdge(state, germanId, originalId, part)) return state;
  const edges = new Set(state.edges);
  edges.delete(edgeKey(germanId, originalId, part));
  return { ...state, edges, roles: withEdges(state, edges) };
}

/**
 * Makes `germanId` an added word of `parentId` (a parent that is itself added is resolved to its own parent).
 * An added word has no parts: a split word is joined again.
 */
export function setAdded(state: MappingState, germanId: number, parentId: number): MappingState {
  const parent = state.added.get(parentId) ?? parentId;
  if (germanId === parent || state.added.get(germanId) === parent) return state;
  const edges = new Set([...state.edges].filter((key) => parseEdge(key).germanId !== germanId));
  const added = new Map<number, number>();
  for (const [child, p] of state.added) added.set(child, p === germanId ? parent : p);
  added.set(germanId, parent);
  const splits = new Map(state.splits);
  splits.delete(germanId);
  return {
    edges,
    added,
    splits,
    roles: withEdges(state, edges),
    partLemmas: withoutPartLemmas(state, germanId),
  };
}

export function unadd(state: MappingState, germanId: number): MappingState {
  if (!state.added.has(germanId)) return state;
  const added = new Map(state.added);
  added.delete(germanId);
  return { ...state, added };
}

/**
 * Splits a German word into parts (two or more, in reading order). The links the whole word had are dropped: every part
 * is linked on its own. Does nothing for fewer than two non-empty parts, an added word or the same split again.
 */
export function splitWord(
  state: MappingState,
  germanId: number,
  parts: readonly string[],
  lemmas: readonly string[] = [],
): MappingState {
  if (parts.length < 2 || parts.some((p) => p.length === 0) || state.added.has(germanId)) {
    return state;
  }
  const current = state.splits.get(germanId);
  if (current && current.length === parts.length && current.every((p, i) => p === parts[i])) {
    return state;
  }
  const edges = new Set([...state.edges].filter((key) => parseEdge(key).germanId !== germanId));
  const splits = new Map(state.splits);
  splits.set(germanId, [...parts]);
  const partLemmas = new Map(withoutPartLemmas(state, germanId));
  parts.forEach((_, index) => {
    const lemma = lemmas[index]?.trim();
    if (lemma) partLemmas.set(germanRef(germanId, index + 1), lemma);
  });
  return { ...state, edges, splits, roles: withEdges(state, edges), partLemmas };
}

/** "Teilung aufheben": the word is one tile again and keeps the links of all its parts as whole-word links. */
export function unsplit(state: MappingState, germanId: number): MappingState {
  if (!state.splits.has(germanId)) return state;
  const edges = new Set<string>();
  const roles = new Map<string, EdgeRole>();
  for (const key of state.edges) {
    const edge = parseEdge(key);
    const next = edge.germanId === germanId ? edgeKey(edge.germanId, edge.originalId) : key;
    edges.add(next);
    const role = state.roles.get(key);
    if (role && !roles.has(next)) roles.set(next, role);
  }
  const splits = new Map(state.splits);
  splits.delete(germanId);
  return { ...state, edges, splits, roles, partLemmas: withoutPartLemmas(state, germanId) };
}

/** Drops every connection of a word or of one `part` (and, for a whole German word, its "added" status). */
export function clearWord(
  state: MappingState,
  side: Side,
  id: number,
  part?: number,
): MappingState {
  const edges = new Set(
    [...state.edges].filter((key) => {
      const e = parseEdge(key);
      if (side === "o") return e.originalId !== id;
      return !(e.germanId === id && (part === undefined || e.part === part));
    }),
  );
  const unadded = side === "g" && part === undefined ? unadd(state, id) : state;
  if (edges.size === state.edges.size && unadded === state) return state;
  return { ...state, edges, added: unadded.added, roles: withEdges(state, edges) };
}

/** Undo stack of one editor session: a snapshot per edit. */
export interface History {
  readonly present: MappingState;
  readonly past: readonly MappingState[];
}
export const createHistory = (present: MappingState): History => ({ present, past: [] });
export function commit(history: History, next: MappingState): History {
  if (next === history.present) return history;
  return { present: next, past: [...history.past, history.present] };
}
export function undo(history: History): History {
  const previous = history.past[history.past.length - 1];
  if (!previous) return history;
  return { present: previous, past: history.past.slice(0, -1) };
}

export interface Groups {
  /** 1-based group number in German reading order, undefined for a word without any connection */
  number: (node: NodeKey) => number | undefined;
  /** members of the group of `node` (including itself) */
  members: (node: NodeKey) => NodeKey[];
}

/**
 * Connected components of German and original words; edges and added-word links both connect.
 * A split word contributes one node per part (an added word that belongs to it joins the first part).
 */
export function computeGroups(
  germanOrder: number[],
  originalOrder: number[],
  state: MappingState,
): Groups {
  const parent = new Map<string, string>();
  const find = (x: string): string => {
    let root = x;
    // a node that is not on the board (e.g. the whole word of a split one) is its own root
    while (parent.has(root) && parent.get(root) !== root) root = parent.get(root) as string;
    if (parent.has(x)) parent.set(x, root);
    return root;
  };
  const nodes: NodeKey[] = [
    ...germanOrder.flatMap((id) => germanNodes(state, id)),
    ...originalOrder.map((id) => nodeKey("o", id)),
  ];
  for (const n of nodes) parent.set(n, n);
  const union = (a: string, b: string) => {
    if (parent.has(a) && parent.has(b)) parent.set(find(a), find(b));
  };
  for (const key of state.edges) {
    const e = parseEdge(key);
    union(nodeKey("g", e.germanId, e.part), nodeKey("o", e.originalId));
  }
  for (const [id, p] of state.added) union(nodeKey("g", id), germanNodes(state, p)[0] as NodeKey);

  const byRoot = new Map<string, NodeKey[]>();
  for (const n of nodes) {
    const root = find(n);
    byRoot.set(root, [...(byRoot.get(root) ?? []), n]);
  }
  const numbers = new Map<string, number>();
  for (const n of nodes) {
    const root = find(n);
    if ((byRoot.get(root)?.length ?? 0) > 1 && !numbers.has(root))
      numbers.set(root, numbers.size + 1);
  }
  return {
    number: (node) => numbers.get(find(node)),
    members: (node) => byRoot.get(find(node)) ?? [node],
  };
}

export type WordStatus = "unchanged" | "edited" | "none";

/** `edited`: connections (or the split) differ from the loaded state; `none`: not connected at all. */
export function wordStatus(
  base: MappingState,
  work: MappingState,
  side: Side,
  id: number,
  part?: number,
): WordStatus {
  const sig = (s: MappingState) => {
    const links = edgesOf(s, side, id, part)
      .map(
        (e) =>
          `${side === "g" ? e.originalId : germanRef(e.germanId, e.part)}${roleSig(s.roles.get(edgeKey(e.germanId, e.originalId, e.part)))}`,
      )
      .sort();
    const own =
      side === "g"
        ? `${s.added.get(id) ?? ""}#${(s.splits.get(id) ?? []).join("+")}#${part === undefined ? "" : (s.partLemmas.get(germanRef(id, part)) ?? "")}`
        : "";
    return `${links.join(",")}#${own}`;
  };
  if (sig(base) !== sig(work)) return "edited";
  const connected =
    edgesOf(work, side, id, part).length > 0 || (side === "g" && work.added.has(id));
  return connected ? "unchanged" : "none";
}

/** A German word with this many originals or more is probably over-assigned. */
export const OVER_ASSIGNED_FROM = 3;

export interface MappingDiff {
  connected: Edge[];
  disconnected: Edge[];
  addedWords: AddedWord[];
  removedAddedWords: AddedWord[];
  splitWords: SplitWord[];
  unsplitWords: SplitWord[];
  /** links in both states whose role, morpheme or multi-head flag changed */
  roleChanges: Edge[];
  /** parts (same split in both states) whose lemma changed */
  partLemmaChanges: { germanId: number; part: number; lemma?: string }[];
}

export function diffStates(base: MappingState, work: MappingState): MappingDiff {
  const edgesDiff = (a: MappingState, b: MappingState) =>
    [...a.edges].filter((key) => !b.edges.has(key)).map(parseEdge);
  const addedDiff = (a: MappingState, b: MappingState) =>
    [...a.added]
      .filter(([id, parent]) => b.added.get(id) !== parent)
      .map(([germanId, parentId]) => ({ germanId, parentId }));
  const same = (a: readonly string[] | undefined, b: readonly string[] | undefined) =>
    a !== undefined && b !== undefined && a.length === b.length && a.every((p, i) => p === b[i]);
  const splitDiff = (a: MappingState, b: MappingState) =>
    [...a.splits]
      .filter(([id, parts]) => !same(parts, b.splits.get(id)))
      .map(([germanId, parts]) => ({ germanId, parts }));
  const splitWords = splitDiff(work, base);
  const unsplitWords = splitDiff(base, work);
  // A word that was only joined or split again keeps the same originals: the split line says it, its edges are not listed.
  const originalsOf = (state: MappingState, id: number) =>
    new Set(edgesOf(state, "g", id).map((e) => e.originalId));
  const restructured = new Set(
    [...splitWords, ...unsplitWords]
      .map((w) => w.germanId)
      .filter((id) => {
        const before = originalsOf(base, id);
        const after = originalsOf(work, id);
        return before.size === after.size && [...before].every((o) => after.has(o));
      }),
  );
  const kept = (edge: Edge) => !restructured.has(edge.germanId);
  const roleChanges = [...work.edges]
    .filter((key) => base.edges.has(key) && !sameRole(base.roles.get(key), work.roles.get(key)))
    .map(parseEdge);
  const partLemmaChanges: MappingDiff["partLemmaChanges"] = [];
  for (const [germanId, parts] of work.splits) {
    if (!same(parts, base.splits.get(germanId))) continue;
    parts.forEach((_, index) => {
      const ref = germanRef(germanId, index + 1);
      const lemma = work.partLemmas.get(ref);
      if ((base.partLemmas.get(ref) ?? "") !== (lemma ?? "")) {
        partLemmaChanges.push({ germanId, part: index + 1, ...(lemma ? { lemma } : {}) });
      }
    });
  }
  return {
    roleChanges,
    partLemmaChanges,
    connected: edgesDiff(work, base).filter(kept),
    disconnected: edgesDiff(base, work).filter(kept),
    addedWords: addedDiff(work, base),
    removedAddedWords: addedDiff(base, work),
    splitWords,
    unsplitWords,
  };
}

export const changeCount = (diff: MappingDiff): number =>
  diff.connected.length +
  diff.disconnected.length +
  diff.addedWords.length +
  diff.removedAddedWords.length +
  diff.splitWords.length +
  diff.unsplitWords.length +
  diff.roleChanges.length +
  diff.partLemmaChanges.length;

/**
 * German words whose stored links carry parts that a save of `work` would remove (the server then needs `clearParts`).
 * Read from the stored links, not from the state, so a lone leftover part that the editor shows as a whole word counts too.
 */
export function partsToClear(stored: readonly VerseMappingLink[], work: MappingState): number[] {
  const withParts = new Set(
    stored.filter((l) => l.partOrder !== undefined || l.germanWordPart).map((l) => l.elbWordId),
  );
  return [...withParts].filter((id) => !work.splits.has(id));
}

export type PartIssueCode = "tooFewParts" | "tooLong" | "notSpelled" | "unlinked" | "added";
export interface PartIssue {
  code: PartIssueCode;
  germanId: number;
  /** 1-based part the issue is about (`tooLong`, `unlinked`) */
  part?: number;
}

/** Lower-cased letters and digits only, like the server (`GermanWordParts.Normalize`). */
export const normalizePart = (text: string): string =>
  text.replace(/[^\p{L}\p{Nd}]/gu, "").toLowerCase();

/**
 * Mirrors the server's part rules so the editor can say what is wrong before saving: two or more parts, each part at most 30
 * characters and linked to at least one original word, no parts on an added word, the parts spell the word.
 */
export function partIssues(
  state: MappingState,
  germanText: ReadonlyMap<number, string>,
): PartIssue[] {
  const issues: PartIssue[] = [];
  for (const [germanId, parts] of state.splits) {
    if (parts.length < 2) issues.push({ code: "tooFewParts", germanId });
    if (state.added.has(germanId)) issues.push({ code: "added", germanId });
    parts.forEach((text, index) => {
      const part = index + 1;
      if (text.length > MAX_PART_LENGTH) issues.push({ code: "tooLong", germanId, part });
      if (edgesOf(state, "g", germanId, part).length === 0) {
        issues.push({ code: "unlinked", germanId, part });
      }
    });
    const word = germanText.get(germanId);
    if (word !== undefined && normalizePart(parts.join("")) !== normalizePart(word)) {
      issues.push({ code: "notSpelled", germanId });
    }
  }
  return issues;
}

/** Letters of a word (code points, so umlauts and combined letters count once). */
export const lettersOf = (word: string): string[] => Array.from(word);

/** Splits `word` after the given letter counts (`cuts`: 1..length-1, any order, repeats ignored) into its parts. */
export function partsFromCuts(word: string, cuts: readonly number[]): string[] {
  const letters = lettersOf(word);
  const points = [...new Set(cuts)]
    .filter((cut) => Number.isInteger(cut) && cut > 0 && cut < letters.length)
    .sort((a, b) => a - b);
  const bounds = [0, ...points, letters.length];
  return bounds.slice(1).map((end, index) => letters.slice(bounds[index], end).join(""));
}

/** Toggles a cut: adds it, or removes it when it is already there; the result is sorted. */
export const toggleCut = (cuts: readonly number[], cut: number): number[] =>
  cuts.includes(cut) ? cuts.filter((c) => c !== cut) : [...cuts, cut].sort((a, b) => a - b);

export type LinkKind = "added" | "removed" | "unchanged";

export interface CompareState {
  /** Both mappings in one state, so every link can be drawn: the proposal's words and parts, plus the live links that still fit. */
  display: MappingState;
  /** Kind of every edge of `display` (keyed like `MappingState.edges`). */
  edgeKind: ReadonlyMap<string, LinkKind>;
  /** Kind of every added-word link of `display` (keyed by the added word). */
  addedKind: ReadonlyMap<number, LinkKind>;
}

const sameParts = (a: readonly string[] | undefined, b: readonly string[] | undefined) =>
  a === undefined || b === undefined
    ? a === b
    : a.length === b.length && a.every((part, index) => part === b[index]);

/**
 * Review view of a proposal: the proposal's result (its parts) with the live links drawn along. A live link of a word whose parts
 * differ in the proposal has no tile to draw it on; it is left out here and stays in the change list.
 */
export function compareStates(live: MappingState, proposed: MappingState): CompareState {
  const edgeKind = new Map<string, LinkKind>();
  const edges = new Set<string>();
  for (const key of proposed.edges) {
    edges.add(key);
    edgeKind.set(key, live.edges.has(key) ? "unchanged" : "added");
  }
  for (const key of live.edges) {
    if (proposed.edges.has(key)) continue;
    const { germanId } = parseEdge(key);
    if (!sameParts(live.splits.get(germanId), proposed.splits.get(germanId))) continue;
    edges.add(key);
    edgeKind.set(key, "removed");
  }
  const added = new Map(proposed.added);
  const addedKind = new Map<number, LinkKind>();
  for (const [id, parent] of proposed.added) {
    addedKind.set(id, live.added.get(id) === parent ? "unchanged" : "added");
  }
  const linked = new Set([...proposed.edges].map((key) => parseEdge(key).germanId));
  for (const [id, parent] of live.added) {
    if (proposed.added.has(id) || linked.has(id)) continue;
    added.set(id, parent);
    addedKind.set(id, "removed");
  }
  return {
    display: {
      edges,
      added,
      splits: proposed.splits,
      roles: new Map([...live.roles, ...proposed.roles]),
      partLemmas: proposed.partLemmas,
    },
    edgeKind,
    addedKind,
  };
}

// ---- Roadmap 0018: link roles (head/unit), Hebrew morphemes, part lemmas --------------------------------------------------------

const roleSig = (role: EdgeRole | undefined) =>
  role ? `:${role.unit ? "u" : "h"}${role.morpheme ?? ""}${role.multiHead ? "*" : ""}` : "";

export const sameRole = (a: EdgeRole | undefined, b: EdgeRole | undefined): boolean =>
  roleSig(a) === roleSig(b);

export const roleOf = (state: MappingState, edge: Edge): MappingRole =>
  state.roles.get(edgeKey(edge.germanId, edge.originalId, edge.part))?.unit ? "unit" : "head";

function withRole(state: MappingState, key: string, next: EdgeRole): MappingState {
  if (!state.edges.has(key) || sameRole(state.roles.get(key), next)) return state;
  const roles = new Map(state.roles);
  if (Object.keys(next).length === 0) roles.delete(key);
  else roles.set(key, next);
  return { ...state, roles };
}

/** Sets the role of one link; a unit loses its multi-head flag. */
export function setRole(state: MappingState, edge: Edge, role: MappingRole): MappingState {
  const key = edgeKey(edge.germanId, edge.originalId, edge.part);
  const { unit: _unit, multiHead, ...rest } = state.roles.get(key) ?? {};
  return withRole(
    state,
    key,
    role === "unit" ? { ...rest, unit: true } : { ...rest, ...(multiHead ? { multiHead } : {}) },
  );
}

/** Picks the Hebrew morpheme a link renders; undefined = the main morpheme. */
export function setMorpheme(
  state: MappingState,
  edge: Edge,
  morpheme: string | undefined,
): MappingState {
  const key = edgeKey(edge.germanId, edge.originalId, edge.part);
  const { morpheme: _old, ...rest } = state.roles.get(key) ?? {};
  return withRole(state, key, morpheme ? { ...rest, morpheme } : rest);
}

/** Lemma of a compound part (`Gerste` of `Gersten`); empty clears it. */
export function setPartLemma(
  state: MappingState,
  germanId: number,
  part: number,
  lemma: string,
): MappingState {
  if (!validPart(state, germanId, part) || part === undefined) return state;
  const ref = germanRef(germanId, part);
  if ((state.partLemmas.get(ref) ?? "") === lemma) return state;
  const partLemmas = new Map(state.partLemmas);
  if (lemma) partLemmas.set(ref, lemma);
  else partLemmas.delete(ref);
  return { ...state, partLemmas };
}

/** Main morpheme of a Hebrew word (undefined for Greek or a word without morphemes). */
export const mainMorpheme = (word: VerseMappingOriginalWord | undefined): string | undefined =>
  word?.morphemes?.find((m) => m.isMain)?.strongs;

/** Morpheme a link renders: its picked one, else the main morpheme of its word ("" for Greek). */
export function morphemeOfEdge(
  state: MappingState,
  edge: Edge,
  originals: ReadonlyMap<number, VerseMappingOriginalWord>,
): string {
  const picked = state.roles.get(edgeKey(edge.germanId, edge.originalId, edge.part))?.morpheme;
  return picked ?? mainMorpheme(originals.get(edge.originalId)) ?? "";
}

/** The links of one source morpheme (`originalId` + morpheme as `morphemeOfEdge` returns it). */
export function morphemeEdges(
  state: MappingState,
  originalId: number,
  morpheme: string,
  originals: ReadonlyMap<number, VerseMappingOriginalWord>,
): Edge[] {
  return edgesOf(state, "o", originalId).filter(
    (e) => morphemeOfEdge(state, e, originals) === morpheme,
  );
}

/** Accepts (or withdraws) several heads on one source morpheme: the flag goes on every head link of it. */
export function setMultiHead(
  state: MappingState,
  originalId: number,
  morpheme: string,
  on: boolean,
  originals: ReadonlyMap<number, VerseMappingOriginalWord>,
): MappingState {
  let next = state;
  for (const edge of morphemeEdges(state, originalId, morpheme, originals)) {
    const key = edgeKey(edge.germanId, edge.originalId, edge.part);
    const { multiHead: _old, ...rest } = next.roles.get(key) ?? {};
    if (rest.unit) continue;
    next = withRole(next, key, on ? { ...rest, multiHead: true } : rest);
  }
  return next;
}

export type RoleIssueCode = "multiHead" | "unitWithoutHead";
export interface RoleIssue {
  code: RoleIssueCode;
  originalId: number;
  /** morpheme as `morphemeOfEdge` returns it ("" for Greek) */
  morpheme: string;
  /** the head links (`multiHead`) or the unit links (`unitWithoutHead`) */
  germanIds: number[];
}

/**
 * Mirrors the server's role-aware checks (invariants 1 and 2 of roadmap 0018) so the editor can name them before saving: more than
 * one head on a morpheme without the multi-head flag, and units on a morpheme without a head. The server is the judge: it also knows
 * the `zum`/`im` exception (a contraction is the head of the preposition and a unit of the article), which the editor does not.
 */
export function roleIssues(
  state: MappingState,
  originals: ReadonlyMap<number, VerseMappingOriginalWord>,
): RoleIssue[] {
  const byMorpheme = new Map<string, { originalId: number; morpheme: string; edges: Edge[] }>();
  for (const key of state.edges) {
    const edge = parseEdge(key);
    const morpheme = morphemeOfEdge(state, edge, originals);
    const group = `${edge.originalId}|${morpheme}`;
    const entry = byMorpheme.get(group) ?? { originalId: edge.originalId, morpheme, edges: [] };
    entry.edges.push(edge);
    byMorpheme.set(group, entry);
  }
  const issues: RoleIssue[] = [];
  for (const { originalId, morpheme, edges } of byMorpheme.values()) {
    const keyOf = (e: Edge) => edgeKey(e.germanId, e.originalId, e.part);
    const heads = edges.filter((e) => !state.roles.get(keyOf(e))?.unit);
    const units = edges.filter((e) => state.roles.get(keyOf(e))?.unit);
    const ids = (list: Edge[]) => [...new Set(list.map((e) => e.germanId))];
    if (heads.length > 1 && !heads.some((e) => state.roles.get(keyOf(e))?.multiHead)) {
      issues.push({ code: "multiHead", originalId, morpheme, germanIds: ids(heads) });
    }
    if (heads.length === 0 && units.length > 0) {
      issues.push({ code: "unitWithoutHead", originalId, morpheme, germanIds: ids(units) });
    }
  }
  return issues;
}

const hasRoleData = (state: MappingState) => state.roles.size > 0 || state.partLemmas.size > 0;

/**
 * True when a save must carry the role fields (role-aware save); false: send the legacy payload.
 *
 * The server validates strictly ("one head per morpheme") as soon as any link carries `role`, `strongsNumber`, `partLemma` or
 * `allowMultiHead`. Until the backfill (roadmap 0018.4) every stored link is a head, so a verse with `habe getauft` both head would be
 * rejected on an unrelated edit. So the fields go out only when the loaded verse already had non-default role data, or when this
 * edit session set some (a role, a morpheme, a part lemma, a multi-head flag); a legacy save keeps the stored values on the server.
 */
export const needsRoleFields = (base: MappingState, work: MappingState): boolean =>
  hasRoleData(base) || hasRoleData(work);

/**
 * Makes a server validation message readable: ids of German and original words are replaced by the words
 * (`Original word 17 morpheme H9001: 2 head links (words 3, 4)` -> `Original word וַיֹּאמֶר morpheme H9001: 2 head links (words Und, sprach)`).
 */
export function readableServerError(
  message: string,
  germanText: (id: number) => string | undefined,
  originalText: (id: number) => string | undefined,
): string {
  const german = (id: string) => germanText(Number(id)) ?? id;
  return message
    .replace(/Original word (\d+)/g, (all, id: string) => {
      const text = originalText(Number(id));
      return text ? `Original word ${text}` : all;
    })
    .replace(
      /\bwords ((?:\d+, )*\d+)/g,
      (_, ids: string) => `words ${ids.split(", ").map(german).join(", ")}`,
    )
    .replace(/\b([Ww])ord (\d+)/g, (_, w: string, id: string) => `${w}ord ${german(id)}`);
}
