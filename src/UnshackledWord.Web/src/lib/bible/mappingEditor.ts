import type { VerseMappingLink } from "./types";

/**
 * Pure state of the mapping editor. A verse mapping is a set of edges (German word - original word)
 * plus "added words": German words without an original that belong to another German word.
 * All operations return the same object when nothing changes, so callers can tell a no-op.
 */

export type Side = "g" | "o";
/** Key of a word tile: `g12` (German word 12) or `o5` (original word 5). */
export type NodeKey = `${Side}${number}`;

export interface MappingState {
  /** `${germanId}|${originalId}` */
  readonly edges: ReadonlySet<string>;
  /** added German word -> German word it belongs to (never itself an added word) */
  readonly added: ReadonlyMap<number, number>;
}

export interface Edge {
  germanId: number;
  originalId: number;
}
export interface AddedWord {
  germanId: number;
  parentId: number;
}

export const emptyState: MappingState = { edges: new Set(), added: new Map() };

export const nodeKey = (side: Side, id: number): NodeKey => `${side}${id}`;
export function parseNode(node: string): { side: Side; id: number } {
  return { side: node.startsWith("o") ? "o" : "g", id: Number(node.slice(1)) };
}

const edgeKey = (germanId: number, originalId: number) => `${germanId}|${originalId}`;
function parseEdge(key: string): Edge {
  const [g, o] = key.split("|");
  return { germanId: Number(g), originalId: Number(o) };
}

export function linksToState(links: VerseMappingLink[]): MappingState {
  const edges = new Set<string>();
  const added = new Map<number, number>();
  for (const link of links) {
    if (link.isAddedWord && link.parentElbWordId !== undefined) {
      added.set(link.elbWordId, link.parentElbWordId);
    } else if (link.stepWordId !== undefined) {
      edges.add(edgeKey(link.elbWordId, link.stepWordId));
    }
  }
  return { edges, added };
}

/** Rows to PUT: edges first, then added words, each in German reading order. Unmapped words have no row. */
export function stateToLinks(
  state: MappingState,
  germanOrder: number[],
  originalOrder: number[],
): VerseMappingLink[] {
  const gPos = new Map(germanOrder.map((id, i) => [id, i]));
  const oPos = new Map(originalOrder.map((id, i) => [id, i]));
  const pos = (m: Map<number, number>, id: number) => m.get(id) ?? Number.MAX_SAFE_INTEGER;
  const edges = [...state.edges]
    .map(parseEdge)
    .sort(
      (a, b) =>
        pos(gPos, a.germanId) - pos(gPos, b.germanId) ||
        pos(oPos, a.originalId) - pos(oPos, b.originalId),
    )
    .map((e) => ({ elbWordId: e.germanId, stepWordId: e.originalId, isAddedWord: false }));
  const added = [...state.added]
    .sort(([a], [b]) => pos(gPos, a) - pos(gPos, b))
    .map(([id, parent]) => ({ elbWordId: id, isAddedWord: true, parentElbWordId: parent }));
  return [...edges, ...added];
}

export function hasEdge(state: MappingState, germanId: number, originalId: number): boolean {
  return state.edges.has(edgeKey(germanId, originalId));
}

/** Ids of the words on the other side that `id` is connected to. */
export function partners(state: MappingState, side: Side, id: number): number[] {
  const out: number[] = [];
  for (const key of state.edges) {
    const e = parseEdge(key);
    if (side === "g" && e.germanId === id) out.push(e.originalId);
    if (side === "o" && e.originalId === id) out.push(e.germanId);
  }
  return out;
}

/** Added words that belong to `germanId`. */
export function addedChildren(state: MappingState, germanId: number): number[] {
  return [...state.added].filter(([, parent]) => parent === germanId).map(([id]) => id);
}

export function connect(state: MappingState, germanId: number, originalId: number): MappingState {
  if (hasEdge(state, germanId, originalId)) return state;
  const edges = new Set(state.edges).add(edgeKey(germanId, originalId));
  const added = new Map(state.added);
  added.delete(germanId);
  return { edges, added };
}

export function disconnect(
  state: MappingState,
  germanId: number,
  originalId: number,
): MappingState {
  if (!hasEdge(state, germanId, originalId)) return state;
  const edges = new Set(state.edges);
  edges.delete(edgeKey(germanId, originalId));
  return { edges, added: state.added };
}

/** Makes `germanId` an added word of `parentId` (a parent that is itself added is resolved to its own parent). */
export function setAdded(state: MappingState, germanId: number, parentId: number): MappingState {
  const parent = state.added.get(parentId) ?? parentId;
  if (germanId === parent || state.added.get(germanId) === parent) return state;
  const edges = new Set([...state.edges].filter((key) => parseEdge(key).germanId !== germanId));
  const added = new Map<number, number>();
  for (const [child, p] of state.added) added.set(child, p === germanId ? parent : p);
  added.set(germanId, parent);
  return { edges, added };
}

export function unadd(state: MappingState, germanId: number): MappingState {
  if (!state.added.has(germanId)) return state;
  const added = new Map(state.added);
  added.delete(germanId);
  return { edges: state.edges, added };
}

/** Drops every connection of a word (and, for a German word, its "added" status). */
export function clearWord(state: MappingState, side: Side, id: number): MappingState {
  const edges = new Set(
    [...state.edges].filter((key) => {
      const e = parseEdge(key);
      return (side === "g" ? e.germanId : e.originalId) !== id;
    }),
  );
  const unadded = side === "g" ? unadd(state, id) : state;
  if (edges.size === state.edges.size && unadded === state) return state;
  return { edges, added: unadded.added };
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

/** Connected components of German and original words; edges and added-word links both connect. */
export function computeGroups(
  germanOrder: number[],
  originalOrder: number[],
  state: MappingState,
): Groups {
  const parent = new Map<string, string>();
  const find = (x: string): string => {
    let root = x;
    while (parent.get(root) !== root) root = parent.get(root) ?? root;
    parent.set(x, root);
    return root;
  };
  const nodes: NodeKey[] = [
    ...germanOrder.map((id) => nodeKey("g", id)),
    ...originalOrder.map((id) => nodeKey("o", id)),
  ];
  for (const n of nodes) parent.set(n, n);
  const union = (a: string, b: string) => {
    if (parent.has(a) && parent.has(b)) parent.set(find(a), find(b));
  };
  for (const key of state.edges) {
    const e = parseEdge(key);
    union(nodeKey("g", e.germanId), nodeKey("o", e.originalId));
  }
  for (const [id, p] of state.added) union(nodeKey("g", id), nodeKey("g", p));

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

/** `edited`: connections differ from the loaded state; `none`: not connected at all. */
export function wordStatus(
  base: MappingState,
  work: MappingState,
  side: Side,
  id: number,
): WordStatus {
  const sig = (s: MappingState) =>
    `${partners(s, side, id)
      .sort((a, b) => a - b)
      .join(",")}#${side === "g" ? (s.added.get(id) ?? "") : ""}`;
  if (sig(base) !== sig(work)) return "edited";
  const connected = partners(work, side, id).length > 0 || (side === "g" && work.added.has(id));
  return connected ? "unchanged" : "none";
}

/** A German word with this many originals or more is probably over-assigned. */
export const OVER_ASSIGNED_FROM = 3;

export interface MappingDiff {
  connected: Edge[];
  disconnected: Edge[];
  addedWords: AddedWord[];
  removedAddedWords: AddedWord[];
}

export function diffStates(base: MappingState, work: MappingState): MappingDiff {
  const edgesOf = (a: MappingState, b: MappingState) =>
    [...a.edges].filter((key) => !b.edges.has(key)).map(parseEdge);
  const addedOf = (a: MappingState, b: MappingState) =>
    [...a.added]
      .filter(([id, parent]) => b.added.get(id) !== parent)
      .map(([germanId, parentId]) => ({ germanId, parentId }));
  return {
    connected: edgesOf(work, base),
    disconnected: edgesOf(base, work),
    addedWords: addedOf(work, base),
    removedAddedWords: addedOf(base, work),
  };
}

export const changeCount = (diff: MappingDiff): number =>
  diff.connected.length +
  diff.disconnected.length +
  diff.addedWords.length +
  diff.removedAddedWords.length;
