import {
  type DragEvent,
  type KeyboardEvent,
  type ReactNode,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { useTranslation } from "react-i18next";
import {
  addedChildren,
  changeCount,
  clearWord,
  commit,
  compareStates,
  computeGroups,
  connect,
  createHistory,
  diffStates,
  disconnect,
  type Edge,
  edgeKey,
  edgesOf,
  germanNodes,
  hasEdge,
  type LinkKind,
  lettersOf,
  linksToState,
  MAX_PART_LENGTH,
  type MappingState,
  mainMorpheme,
  morphemeOfEdge,
  type NodeKey,
  needsRoleFields,
  nodeKey,
  OVER_ASSIGNED_FROM,
  parseEdge,
  parseNode,
  partIssues,
  partners,
  partsFromCuts,
  partsToClear,
  readableServerError,
  roleIssues,
  roleOf,
  sameRole,
  setAdded,
  setMorpheme,
  setMultiHead,
  setPartLemma,
  setRole,
  splitWord,
  stateToLinks,
  unadd,
  undo,
  unsplit,
  wordStatus,
} from "@/lib/bible/mappingEditor";
import { useReverseGerman } from "@/lib/bible/mappingOrder";
import type { GetVerseMappingResponse, VerseMappingLink } from "@/lib/bible/types";
import { useCoarsePointer } from "@/lib/useCoarsePointer";
import { Button } from "./Button";
import { FormError } from "./FormError";
import { MappingToken } from "./MappingToken";
import { Select } from "./Select";
import { Switch } from "./Switch";
import { TextField } from "./TextField";
import { WordSplitter } from "./WordSplitter";

export type MappingSaveError = "validation" | "forbidden" | "failed";

export interface MappingEditorProps {
  /** The live verse; `data.links` is what a save is compared with. */
  data: GetVerseMappingResponse;
  /**
   * `edit` (default): change the mapping. `compare`: read-only review of `compareLinks` against `data.links` - links only the proposal
   * has are drawn solid in the ok tone, links only the live mapping has dashed in the error tone, shared links neutral (all lines
   * at once); no editing, the actions come from `footer`.
   */
  mode?: "edit" | "compare";
  /** Compare mode: the proposed links. */
  compareLinks?: VerseMappingLink[];
  /**
   * Edit mode: start from these links instead of `data.links` (an edit-merge starts from the proposal); the change list and the
   * word status still compare with `data.links`, and Save is offered even when both are equal.
   */
  initialLinks?: VerseMappingLink[];
  /** Extra content above the buttons (edit) or instead of them (compare): the review actions. */
  footer?: ReactNode;
  /** Text of the save button instead of "Speichern (n)". */
  saveLabel?: string;
  /** e.g. "Johannes 3,1" */
  reference: string;
  saving?: boolean;
  saveError?: MappingSaveError | null;
  /** Messages of a rejected save (400), e.g. from `describeApiErrors`; shown under the error with word ids replaced by the words. */
  saveErrorDetails?: readonly string[];
  /** Word ids that carry a "prüfen" marker (revision carry-over); omitted for the Elb1871 mapping. */
  review?: { german: ReadonlySet<number>; original: ReadonlySet<number> };
  /**
   * Compound words (`Gerstenernte` = `Gersten` + `ernte`) can be changed here (Elb1871 data); revision variants have no parts.
   * Stored parts are drawn either way.
   */
  partsEditable?: boolean;
  /**
   * Called with the complete new mapping of the verse; not called when nothing changed. `clearParts` is true when this save removes
   * compound parts the verse has today (the server refuses that otherwise).
   */
  onSave: (links: VerseMappingLink[], options: { clearParts: boolean }) => void;
  /** Shows the "KI neu zuordnen" action (maintainers on Elb1871 data); opens the run dialog for this verse. */
  onAiRedo?: () => void;
  /** Cancel, Escape, and Save without edits. */
  onCancel: () => void;
  /** Reports the number of unsaved link changes whenever it changes (0 = nothing pending). */
  onChangeCount?: (count: number) => void;
}

type DropTarget = NodeKey | "clear";
interface LinePath {
  id: string;
  d: string;
  dashed: boolean;
  /** roadmap 0018: a unit link is drawn muted */
  unit?: boolean;
  /** Compare mode: the line is new, gone or unchanged. */
  kind?: LinkKind;
}

/** Inline editor below a verse: connect German words to the original words by drag and drop or select-then-tap. */
/** One line of the change list; `originalId` is the original word it is about (hover link to the mapper). */
type DiffLine = { text: string; originalId?: number };

/** Wraps Hebrew and Greek runs of a plain text in `lang="he"` / `lang="grc"` spans so the original-text font and size apply; they highlight with their word. */
function withOriginal(
  { text, originalId }: DiffLine,
  hovered: number | null,
  setHovered: (id: number | null) => void,
): ReactNode[] {
  return text
    .split(
      /([\u0590-\u05FF\uFB1D-\uFB4F][\u0590-\u05FF\uFB1D-\uFB4F\s\u2066-\u2069]*|[\u0370-\u03FF\u1F00-\u1FFF][\u0370-\u03FF\u1F00-\u1FFF\s\u2066-\u2069]*)/u,
    )
    .map((part, i) =>
      i % 2 === 1 ? (
        // biome-ignore lint/a11y/noStaticElementInteractions: mouse-only highlight link to the mapper above, no action
        <span
          // biome-ignore lint/suspicious/noArrayIndexKey: static split of one string
          key={i}
          lang={/^[\u0370-\u03FF\u1F00-\u1FFF]/u.test(part) ? "grc" : "he"}
          className="original-text"
          data-hover={originalId !== undefined && hovered === originalId ? "" : undefined}
          onMouseEnter={originalId === undefined ? undefined : () => setHovered(originalId)}
          onMouseLeave={originalId === undefined ? undefined : () => setHovered(null)}
        >
          {part}
        </span>
      ) : (
        part
      ),
    );
}

export function MappingEditor({
  data,
  reference,
  saving = false,
  saveError = null,
  saveErrorDetails = [],
  review,
  mode = "edit",
  compareLinks,
  initialLinks,
  footer,
  saveLabel,
  partsEditable = false,
  onSave,
  onAiRedo,
  onCancel,
  onChangeCount,
}: MappingEditorProps) {
  const { t } = useTranslation();
  const coarse = useCoarsePointer();
  const compare = mode === "compare";
  const readOnly = !data.editingEnabled || compare;

  const germanWords = useMemo(
    () => [...data.germanWords].sort((a, b) => a.positionInVerse - b.positionInVerse),
    [data.germanWords],
  );
  const originalWords = useMemo(
    () => [...data.originalWords].sort((a, b) => a.position - b.position),
    [data.originalWords],
  );
  const germanOrder = useMemo(() => germanWords.map((w) => w.id), [germanWords]);
  const originalOrder = useMemo(() => originalWords.map((w) => w.id), [originalWords]);
  const germanText = useMemo(() => new Map(germanWords.map((w) => [w.id, w.text])), [germanWords]);
  const originalById = useMemo(() => new Map(originalWords.map((w) => [w.id, w])), [originalWords]);

  const base = useMemo(() => linksToState(data.links), [data.links]);
  /** Compare mode: the proposal's state and the combined state that is drawn. */
  const proposed = useMemo(() => linksToState(compareLinks ?? []), [compareLinks]);
  const comparison = useMemo(() => compareStates(base, proposed), [base, proposed]);
  const [history, setHistory] = useState(() =>
    createHistory(compare ? comparison.display : initialLinks ? linksToState(initialLinks) : base),
  );
  const work = history.present;
  /** What the change list and the save describe: the proposal when comparing, else the edited state. */
  const target = compare ? proposed : work;
  const [selected, setSelected] = useState<NodeKey | null>(null);
  const [addedMode, setAddedMode] = useState(false);
  const [hover, setHover] = useState<NodeKey | null>(null);
  const [hiddenKinds, setHiddenKinds] = useState<ReadonlySet<string>>(() => new Set());
  const [listHover, setListHover] = useState<number | null>(null);
  const [dragging, setDragging] = useState<NodeKey | null>(null);
  const [dropOver, setDropOver] = useState<DropTarget | null>(null);
  const [showDetails, setShowDetails] = useState(false);
  const [message, setMessage] = useState("");
  const [splitting, setSplitting] = useState<{
    id: number;
    cuts: number[];
    lemmas?: string[];
  } | null>(null);
  const [lines, setLines] = useState<LinePath[]>([]);
  /** Part lemma being typed; becomes one undo step on blur or Enter. */
  const [lemmaDraft, setLemmaDraft] = useState<{ ref: string; value: string } | null>(null);
  const [germanReversed, toggleGermanReversed] = useReverseGerman(data.language);
  const rootRef = useRef<HTMLElement>(null);
  const editRef = useRef<HTMLDivElement>(null);

  const groups = useMemo(
    () => computeGroups(germanOrder, originalOrder, work),
    [germanOrder, originalOrder, work],
  );
  const diff = useMemo(() => diffStates(base, target), [base, target]);
  const changes = changeCount(diff);
  useEffect(() => {
    onChangeCount?.(changes);
  }, [changes, onChangeCount]);

  const issues = useMemo(
    () => (compare ? [] : partIssues(work, germanText)),
    [compare, work, germanText],
  );
  /** Role fields go out only when needed, see `needsRoleFields`; then the editor also mirrors the server's role checks. */
  const roleAware = !compare && needsRoleFields(base, work);
  const rolesIssues = useMemo(
    () => (roleAware ? roleIssues(work, originalById) : []),
    [roleAware, work, originalById],
  );
  const dropped = useMemo(
    () => (compare ? [] : partsToClear(data.links, work)),
    [compare, data.links, work],
  );

  const germanName = (id: number) => germanText.get(id) ?? "";
  /** Text of a tile: the part for a part tile, else the whole word. */
  const germanLabel = (id: number, part?: number) =>
    (part === undefined ? undefined : work.splits.get(id)?.[part - 1]) ?? germanName(id);
  const originalName = (id: number) => originalById.get(id)?.original ?? "";

  const activeNode = selected ?? hover;
  const activeMembers = useMemo(
    () => new Set(activeNode ? groups.members(activeNode) : []),
    [activeNode, groups],
  );
  /** The German word (part) is linked to the original word hovered in the change list. */
  const listedGerman = (germanId: number, part?: number) =>
    listHover !== null &&
    [...work.edges].some((key) => {
      const e = parseEdge(key);
      return e.originalId === listHover && e.germanId === germanId && e.part === part;
    });
  const inActiveGroup = (node: NodeKey) =>
    activeMembers.has(node) && (groups.number(node) !== undefined || node === activeNode);

  // biome-ignore lint/correctness/useExhaustiveDependencies: only on mount; a review keeps focus on the page for its keys
  useEffect(() => {
    if (compare) return;
    rootRef.current?.querySelector<HTMLElement>("[data-node]")?.focus({ preventScroll: true });
  }, []);

  // Connection curves of the active group only; measured from the tiles after every render that can move them.
  // biome-ignore lint/correctness/useExhaustiveDependencies: re-measure when anything that moves tiles changes
  useLayoutEffect(() => {
    function measure() {
      const container = editRef.current;
      if (!container || !(compare || activeNode || listHover !== null)) return setLines([]);
      const origin = container.getBoundingClientRect();
      const rect = (node: NodeKey) =>
        container.querySelector(`[data-node="${node}"]`)?.getBoundingClientRect();
      const next: LinePath[] = [];
      for (const key of work.edges) {
        const edge = parseEdge(key);
        const gNode = nodeKey("g", edge.germanId, edge.part);
        const a = rect(gNode);
        const b = rect(nodeKey("o", edge.originalId));
        const kind = compare ? comparison.edgeKind.get(key) : undefined;
        const unit = work.roles.get(key)?.unit === true;
        const listed = listHover === edge.originalId;
        if (!a || !b || !(compare || listed || inActiveGroup(gNode))) continue;
        // A unit line is hidden by its kind switch or by the unit switch; the active group always shows.
        const hidden = (kind && hiddenKinds.has(kind)) || (unit && hiddenKinds.has("unit"));
        if (hidden && !listed && !inActiveGroup(gNode)) continue;
        const x1 = a.left + a.width / 2 - origin.left;
        const y1 = a.bottom - origin.top;
        const x2 = b.left + b.width / 2 - origin.left;
        const y2 = b.top - origin.top;
        const my = (y1 + y2) / 2;
        next.push({
          id: key,
          d: `M${x1},${y1} C${x1},${my} ${x2},${my} ${x2},${y2}`,
          dashed: false,
          unit,
          kind,
        });
      }
      for (const [g, p] of work.added) {
        const gNode = nodeKey("g", g);
        const a = rect(gNode);
        const b = rect(germanNodes(work, p)[0] ?? nodeKey("g", p));
        const kind = compare ? comparison.addedKind.get(g) : undefined;
        if (!a || !b || !(compare || inActiveGroup(gNode))) continue;
        const hiddenAdded = (kind && hiddenKinds.has(kind)) || hiddenKinds.has("addedWord");
        if (hiddenAdded && !inActiveGroup(gNode)) continue;
        const x1 = a.left + a.width / 2 - origin.left;
        const y1 = a.bottom - origin.top;
        const x2 = b.left + b.width / 2 - origin.left;
        const y2 = b.bottom - origin.top;
        const dip = Math.max(y1, y2) + Math.min(24, Math.abs(x2 - x1) / 3 + 8);
        next.push({
          id: `+${g}`,
          d: `M${x1},${y1} Q${(x1 + x2) / 2},${dip} ${x2},${y2}`,
          dashed: true,
          kind,
        });
      }
      setLines(next);
    }
    measure();
    window.addEventListener("resize", measure);
    return () => window.removeEventListener("resize", measure);
  }, [
    activeNode,
    work,
    showDetails,
    groups,
    activeMembers,
    germanReversed,
    hiddenKinds,
    listHover,
  ]);

  /** Applies an edit; `message` announces it, or `noop` when it changed nothing. */
  function edit(next: MappingState, done: string, noop?: string) {
    if (next === work) {
      if (noop) setMessage(noop);
      return;
    }
    setHistory((h) => commit(h, next));
    setMessage(done);
  }

  function doUndo() {
    if (history.past.length === 0) return;
    setHistory(undo);
    setMessage(t("bible.mapping.status.undone"));
  }

  function connectPair(g: number, o: number, part?: number) {
    edit(
      connect(work, g, o, part),
      t("bible.mapping.status.connected", {
        german: germanLabel(g, part),
        original: originalName(o),
      }),
      t("bible.mapping.status.alreadyConnected"),
    );
  }

  /** A part of a compound word cannot be an added word; the word itself can only after the split is undone. */
  function addWord(word: { id: number; part?: number }, parent: number) {
    if (word.part !== undefined || work.splits.has(word.id)) {
      return setMessage(t("bible.mapping.parts.noAdded", { word: germanName(word.id) }));
    }
    markAdded(word.id, parent);
  }

  function markAdded(g: number, parent: number) {
    edit(
      setAdded(work, g, parent),
      t("bible.mapping.status.added", { word: germanName(g), parent: germanName(parent) }),
    );
  }

  function clearOf(node: NodeKey) {
    const { side, id, part } = parseNode(node);
    const name = side === "g" ? germanLabel(id, part) : originalName(id);
    edit(clearWord(work, side, id, part), t("bible.mapping.status.cleared", { word: name }));
  }

  function select(node: NodeKey) {
    if (readOnly) return setSelected(selected === node ? null : node);
    if (addedMode) {
      setAddedMode(false);
      if (selected && parseNode(node).side === "g" && node !== selected) {
        addWord(parseNode(selected), parseNode(node).id);
      }
      return;
    }
    if (!selected || selected === node) return setSelected(selected === node ? null : node);
    const a = parseNode(selected);
    const b = parseNode(node);
    if (a.side === b.side) return setSelected(node);
    const gn = a.side === "g" ? a : b;
    const o = a.side === "o" ? a.id : b.id;
    if (hasEdge(work, gn.id, o, gn.part)) {
      edit(
        disconnect(work, gn.id, o, gn.part),
        t("bible.mapping.status.disconnected", {
          german: germanLabel(gn.id, gn.part),
          original: originalName(o),
        }),
      );
    } else {
      connectPair(gn.id, o, gn.part);
    }
  }

  function dropOn(src: NodeKey, target: DropTarget) {
    if (target === "clear") return clearOf(src);
    if (src === target) return;
    const a = parseNode(src);
    const b = parseNode(target);
    if (a.side === "g" && b.side === "g") return addWord(a, b.id);
    if (a.side === "o" && b.side === "o") return setMessage(t("bible.mapping.status.originalPair"));
    const gn = a.side === "g" ? a : b;
    connectPair(gn.id, a.side === "o" ? a.id : b.id, gn.part);
  }

  function validDrop(target: DropTarget): boolean {
    if (!dragging) return false;
    if (target === "clear") return true;
    return target !== dragging && !(dragging.startsWith("o") && target.startsWith("o"));
  }

  function endDrag() {
    setDragging(null);
    setDropOver(null);
  }

  function dragOver(target: DropTarget, event: DragEvent<HTMLElement>) {
    if (!validDrop(target)) return;
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = "link";
    setDropOver(target);
  }

  function drop(target: DropTarget, event: DragEvent<HTMLElement>) {
    if (!dragging) return;
    event.preventDefault();
    const src = dragging;
    endDrag();
    dropOn(src, target);
  }

  function onKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.key === "Escape") {
      if (compare && !selected) return;
      event.preventDefault();
      event.stopPropagation();
      if (selected || addedMode) {
        setSelected(null);
        setAddedMode(false);
      } else {
        onCancel();
      }
      return;
    }
    if (readOnly) return;
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "z") {
      event.preventDefault();
      doUndo();
      return;
    }
    const onToken = event.target instanceof HTMLElement && event.target.closest("[data-node]");
    if (event.key.toLowerCase() === "u" && !event.ctrlKey && !event.metaKey && !event.altKey) {
      if (selected && onToken && parseNode(selected).side === "g") {
        event.preventDefault();
        toggleTileRole(selected);
      }
      return;
    }
    if ((event.key === "Delete" || event.key === "Backspace") && selected && onToken) {
      event.preventDefault();
      clearOf(selected);
    }
  }

  function save() {
    // An edit-merge starts from the proposal, so it is saved even when it equals the live verse.
    if (changes === 0 && !initialLinks) return onCancel();
    if (issues.length > 0) return;
    // Legacy payload unless role fields are needed (`needsRoleFields`): any role field makes the server check the whole verse
    // strictly, and until the backfill (0018.4) every stored link is a head, so `habe getauft` would be rejected on an unrelated edit.
    onSave(stateToLinks(work, germanOrder, originalOrder, { roles: roleAware }), {
      clearParts: dropped.length > 0,
    });
  }

  const originalOf = (o: number) => originalById.get(o);
  const morphemeLabel = (o: number, strongs: string) => {
    const m = originalOf(o)?.morphemes?.find((x) => x.strongs === strongs);
    // Unicode isolates keep the Hebrew part from reordering the surrounding German/English text.
    return [m?.original && `\u2068${m.original}\u2069`, m?.gloss, strongs]
      .filter(Boolean)
      .join(" · ");
  };

  function commitLemma(germanId: number, part: number) {
    if (lemmaDraft?.ref !== `${germanId}.${part}`) return;
    setLemmaDraft(null);
    edit(
      setPartLemma(work, germanId, part, lemmaDraft.value.trim()),
      t("bible.mapping.roles.status.partLemma", { part: germanLabel(germanId, part) }),
    );
  }

  function toggleRole(edge: Edge) {
    const next = roleOf(work, edge) === "unit" ? "head" : "unit";
    const values = {
      german: germanLabel(edge.germanId, edge.part),
      original: originalName(edge.originalId),
    };
    edit(setRole(work, edge, next), t(`bible.mapping.roles.status.${next}`, values));
  }

  /** Key U on a German tile: all its links become units, or heads again when they all are units. */
  function toggleTileRole(node: NodeKey) {
    const { id, part } = parseNode(node);
    const list = edgesOf(work, "g", id, part);
    if (list.length === 0) return;
    const next = list.every((e) => roleOf(work, e) === "unit") ? "head" : "unit";
    let state = work;
    for (const e of list) state = setRole(state, e, next);
    const values = {
      german: germanLabel(id, part),
      original: list.map((e) => originalName(e.originalId)).join(", "),
    };
    edit(state, t(`bible.mapping.roles.status.${next}`, values));
  }

  function pickMorpheme(edge: Edge, strongs: string) {
    const main = mainMorpheme(originalOf(edge.originalId));
    // The main morpheme is stored as "no pick", unless the loaded link named it explicitly.
    const loaded = base.roles.get(edgeKey(edge.germanId, edge.originalId, edge.part))?.morpheme;
    const value = strongs === main ? (loaded === main ? main : undefined) : strongs;
    edit(
      setMorpheme(work, edge, value),
      t("bible.mapping.roles.status.morpheme", {
        german: germanLabel(edge.germanId, edge.part),
        morpheme: morphemeLabel(edge.originalId, strongs),
      }),
    );
  }

  function multiHead(originalId: number, morpheme: string, on: boolean) {
    edit(
      setMultiHead(work, originalId, morpheme, on, originalById),
      t(`bible.mapping.roles.status.${on ? "multiHead" : "multiHeadOff"}`, {
        original: morphemeName(originalId, morpheme),
      }),
    );
  }

  /** "וַיֹּאמֶר" or, for a word with several morphemes, "וַיֹּאמֶר (H9001)". */
  function morphemeName(originalId: number, morpheme: string) {
    const several = (originalOf(originalId)?.morphemes?.length ?? 0) > 1;
    return several && morpheme
      ? `${originalName(originalId)} (${morpheme})`
      : originalName(originalId);
  }

  /** Morphemes with several heads accepted (shown with a "withdraw" action). */
  const acceptedMultiHeads = useMemo(() => {
    const seen = new Map<string, { originalId: number; morpheme: string; germanIds: number[] }>();
    for (const [key, role] of work.roles) {
      if (!role.multiHead || role.unit || !work.edges.has(key)) continue;
      const [g, o] = key.split("|");
      const edge = { germanId: Number(g?.split(".")[0]), originalId: Number(o) };
      const morpheme = morphemeOfEdge(work, edge, originalById);
      const group = `${edge.originalId}|${morpheme}`;
      const entry = seen.get(group) ?? { originalId: edge.originalId, morpheme, germanIds: [] };
      if (!entry.germanIds.includes(edge.germanId)) entry.germanIds.push(edge.germanId);
      seen.set(group, entry);
    }
    return [...seen.values()];
  }, [work, originalById]);

  function describe(node: NodeKey): string {
    const { side, id, part } = parseNode(node);
    if (side === "g") {
      const word = germanName(id);
      const parent = work.added.get(id);
      if (parent !== undefined) {
        return t("bible.mapping.label.germanAdded", { word, parent: germanName(parent) });
      }
      const list = edgesOf(work, "g", id, part).map((e) =>
        roleOf(work, e) === "unit"
          ? `${originalName(e.originalId)} (${t("bible.mapping.roles.unitLabel")})`
          : originalName(e.originalId),
      );
      if (part !== undefined) {
        const values = {
          part: germanLabel(id, part),
          word,
          order: part,
          partners: list.join(", "),
        };
        return t(
          list.length ? "bible.mapping.parts.labelLinked" : "bible.mapping.parts.labelUnmapped",
          values,
        );
      }
      return list.length
        ? t("bible.mapping.label.germanLinked", { word, partners: list.join(", ") })
        : t("bible.mapping.label.germanUnmapped", { word });
    }
    const o = originalById.get(id);
    const word = [o?.original, o?.lemma, o?.strongs, o?.morphologyCode, o?.gloss]
      .filter(Boolean)
      .join(", ");
    const list = edgesOf(work, "o", id).map((e) => germanLabel(e.germanId, e.part));
    return list.length
      ? t("bible.mapping.label.originalLinked", { word, partners: list.join(", ") })
      : t("bible.mapping.label.originalUnmapped", { word });
  }

  const reviewText = t("bible.mapping.review");
  const reviewLabel = (label: string, flagged: boolean) =>
    flagged ? `${label}, ${reviewText}` : label;

  const isRtl = data.language === "hebrew";
  const originalLang = isRtl ? "he" : "grc";
  const draggable = !coarse && !readOnly;
  const tokenHandlers = {
    onSelect: select,
    onHover: setHover,
    onDragStart: (node: NodeKey, event: DragEvent<HTMLElement>) => {
      setDragging(node);
      if (event.dataTransfer) {
        event.dataTransfer.effectAllowed = "link";
        event.dataTransfer.setData("text/plain", node);
      }
    },
    onDragOver: (node: NodeKey, event: DragEvent<HTMLElement>) => dragOver(node, event),
    onDragLeave: (node: NodeKey) => setDropOver((current) => (current === node ? null : current)),
    onDrop: (node: NodeKey, event: DragEvent<HTMLElement>) => drop(node, event),
    onDragEnd: endDrag,
  };

  function lead(): string {
    if (readOnly) return t("bible.mapping.hint.readOnlySelect");
    if (addedMode && selected) {
      return t("bible.mapping.hint.pickParent", { word: germanName(parseNode(selected).id) });
    }
    if (selected) {
      return parseNode(selected).side === "g"
        ? `${t("bible.mapping.hint.selectedGerman")} ${t("bible.mapping.roles.keyHint")}`
        : t("bible.mapping.hint.selectedOriginal");
    }
    return t(coarse ? "bible.mapping.hint.touch" : "bible.mapping.hint.drag");
  }

  function activeInfo() {
    if (!activeNode) return null;
    const { side, id, part } = parseNode(activeNode);
    if (side === "o") {
      const o = originalById.get(id);
      if (!o) return null;
      return (
        <p>
          <span lang={originalLang} className="font-original text-lg">
            {o.original}
          </span>{" "}
          <span dir="ltr">
            {[o.lemma, o.strongs, o.morphologyCode, o.gloss].filter(Boolean).join(" · ")}
          </span>
          {edgesOf(work, "o", id).length === 0 && (
            <span className="text-fg-muted"> · {t("bible.mapping.info.originalUnmapped")}</span>
          )}
        </p>
      );
    }
    const parent = work.added.get(id);
    const originals = partners(work, "g", id, part).flatMap((oid) => originalById.get(oid) ?? []);
    const kids = part === undefined ? addedChildren(work, id).map(germanName) : [];
    return (
      <p>
        <strong>{germanLabel(id, part)}</strong>{" "}
        {part !== undefined && (
          <span className="text-fg-muted">
            {t("bible.mapping.parts.partOf", { word: germanName(id), order: part })}{" "}
          </span>
        )}
        {parent !== undefined ? (
          t("bible.mapping.info.addedTo", { parent: germanName(parent) })
        ) : originals.length ? (
          <>
            →{" "}
            {originals.map((o, index) => (
              <span key={o.id}>
                {index > 0 && ", "}
                <span lang={originalLang} className="font-original">
                  {o.original}
                </span>
                {o.strongs && <code className="text-xs"> {o.strongs}</code>}
              </span>
            ))}
          </>
        ) : (
          t("bible.mapping.info.unmapped")
        )}
        {kids.length > 0 && (
          <span className="text-fg-muted">
            {" "}
            {t("bible.mapping.info.addedWords", { words: kids.join(", ") })}
          </span>
        )}
      </p>
    );
  }

  function chips() {
    if (!selected) return null;
    const { side, id, part } = parseNode(selected);
    const parent = side === "g" ? work.added.get(id) : undefined;
    const pairs = edgesOf(work, side, id, part).map((e) => ({
      g: e.germanId,
      o: e.originalId,
      part: e.part,
    }));
    return (
      <div className="mt-2 flex flex-wrap items-center gap-2">
        {parent !== undefined && (
          <span
            className="map-chip"
            data-status={base.added.get(id) === parent ? "unchanged" : "edited"}
          >
            {t("bible.mapping.chip.addedTo", { parent: germanName(parent) })}
            {!readOnly && (
              <Button
                variant="ghost"
                size="sm"
                aria-label={t("bible.mapping.chip.unadd")}
                onClick={() => edit(unadd(work, id), t("bible.mapping.status.unadded"))}
              >
                ✕
              </Button>
            )}
          </span>
        )}
        {pairs.map(({ g, o, part: edgePart }) => {
          const edge = { germanId: g, originalId: o, part: edgePart };
          const key = edgeKey(g, o, edgePart);
          const unit = roleOf(work, edge) === "unit";
          const morphemes = originalOf(o)?.morphemes ?? [];
          const values = { german: germanLabel(g, edgePart), original: originalName(o) };
          const roleChanged =
            hasEdge(base, g, o, edgePart) && !sameRole(base.roles.get(key), work.roles.get(key));
          return (
            <span
              key={key}
              className="map-chip"
              data-status={hasEdge(base, g, o, edgePart) && !roleChanged ? "unchanged" : "edited"}
            >
              {germanLabel(g, edgePart)} –{" "}
              <span lang={originalLang} className="font-original">
                {originalName(o)}
              </span>
              {readOnly ? (
                unit && (
                  <span className="text-fg-muted">({t("bible.mapping.roles.unitLabel")})</span>
                )
              ) : (
                <Button
                  variant="ghost"
                  size="sm"
                  aria-pressed={unit}
                  aria-label={t("bible.mapping.roles.toggle", values)}
                  title={t("bible.mapping.roles.keyHint")}
                  onClick={() => toggleRole(edge)}
                >
                  {unit ? t("bible.mapping.roles.unit") : t("bible.mapping.roles.head")}
                </Button>
              )}
              {morphemes.length > 1 && (
                <Select
                  label={t("bible.mapping.roles.morphemeOf", values)}
                  labelHidden
                  disabled={readOnly}
                  value={morphemeOfEdge(work, edge, originalById)}
                  onChange={(value) => pickMorpheme(edge, value)}
                  options={morphemes.map((m) => ({
                    value: m.strongs,
                    label: `${m.isMain ? "★ " : ""}${[m.original, m.gloss].filter(Boolean).join(" · ")} (${m.strongs})`,
                    title: m.isMain ? t("bible.mapping.roles.main") : undefined,
                  }))}
                />
              )}
              {!readOnly && (
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={t("bible.mapping.chip.disconnect", {
                    german: germanLabel(g, edgePart),
                    original: originalName(o),
                  })}
                  onClick={() =>
                    edit(
                      disconnect(work, g, o, edgePart),
                      t("bible.mapping.status.disconnected", {
                        german: germanLabel(g, edgePart),
                        original: originalName(o),
                      }),
                    )
                  }
                >
                  ✕
                </Button>
              )}
            </span>
          );
        })}
        {side === "g" && part !== undefined && (partsEditable || work.partLemmas.size > 0) && (
          <TextField
            size="sm"
            required={false}
            maxLength={40}
            disabled={readOnly || !partsEditable}
            label={t("bible.mapping.roles.partLemma", { part: germanLabel(id, part) })}
            value={
              lemmaDraft?.ref === `${id}.${part}`
                ? lemmaDraft.value
                : (work.partLemmas.get(`${id}.${part}`) ?? "")
            }
            onChange={(value) => setLemmaDraft({ ref: `${id}.${part}`, value })}
            onBlur={() => commitLemma(id, part)}
            onKeyDown={(event) => {
              if (event.key === "Enter") {
                event.preventDefault();
                commitLemma(id, part);
              }
            }}
          />
        )}
        {!readOnly && side === "g" && parent === undefined && part === undefined && (
          <Button
            variant="secondary"
            size="sm"
            aria-pressed={addedMode}
            onClick={() => setAddedMode(!addedMode)}
          >
            {t("bible.mapping.chip.markAdded")}
          </Button>
        )}
        {!readOnly && (pairs.length > 0 || parent !== undefined) && (
          <Button variant="ghost" size="sm" onClick={() => clearOf(selected)}>
            {t("bible.mapping.chip.clear")}
          </Button>
        )}
        {!readOnly &&
          partsEditable &&
          side === "g" &&
          part === undefined &&
          parent === undefined &&
          !work.splits.has(id) &&
          lettersOf(germanName(id)).length >= 2 && (
            <Button
              variant="secondary"
              size="sm"
              aria-pressed={splitting?.id === id}
              onClick={() => setSplitting(splitting?.id === id ? null : { id, cuts: [] })}
            >
              {t("bible.mapping.split.open")}
            </Button>
          )}
        {!readOnly && partsEditable && side === "g" && part !== undefined && (
          <Button
            variant="secondary"
            size="sm"
            onClick={() => {
              edit(
                unsplit(work, id),
                t("bible.mapping.parts.status.unsplit", { word: germanName(id) }),
              );
              setSelected(nodeKey("g", id));
            }}
          >
            {t("bible.mapping.parts.unsplit")}
          </Button>
        )}
        <Button
          variant="ghost"
          size="sm"
          onClick={() => {
            setSelected(null);
            setAddedMode(false);
          }}
        >
          {t("bible.mapping.chip.deselect")}
        </Button>
      </div>
    );
  }

  function diffLines(): DiffLine[] {
    const g = germanName;
    const o = originalName;
    const partName = (id: number, part?: number, source: MappingState = work) =>
      (part === undefined ? undefined : source.splits.get(id)?.[part - 1]) ?? g(id);
    const work = target;
    const line = (text: string, originalId?: number): DiffLine => ({ text, originalId });
    return [
      ...diff.splitWords.map((w) =>
        t("bible.mapping.parts.diff.split", { word: g(w.germanId), parts: w.parts.join(" + ") }),
      ),
      ...diff.unsplitWords.map((w) =>
        t("bible.mapping.parts.diff.unsplit", { word: g(w.germanId), parts: w.parts.join(" + ") }),
      ),
      ...diff.connected.map((e) =>
        line(
          t("bible.mapping.diff.connected", {
            german: partName(e.germanId, e.part),
            original: o(e.originalId),
          }),
          e.originalId,
        ),
      ),
      ...diff.disconnected.map((e) =>
        line(
          t("bible.mapping.diff.disconnected", {
            german: partName(e.germanId, e.part, base),
            original: o(e.originalId),
          }),
          e.originalId,
        ),
      ),
      ...diff.roleChanges.flatMap((e) => {
        const key = edgeKey(e.germanId, e.originalId, e.part);
        const before = base.roles.get(key);
        const after = work.roles.get(key);
        const values = {
          german: partName(e.germanId, e.part),
          original: `\u2068${o(e.originalId)}\u2069`,
        };
        const out: string[] = [];
        if (Boolean(before?.unit) !== Boolean(after?.unit)) {
          out.push(
            t("bible.mapping.roles.diff.role", {
              ...values,
              role: t(`bible.mapping.roles.${after?.unit ? "unit" : "head"}`),
            }),
          );
        }
        if ((before?.morpheme ?? "") !== (after?.morpheme ?? "")) {
          const m = morphemeOfEdge(work, e, originalById);
          out.push(
            t("bible.mapping.roles.diff.morpheme", {
              ...values,
              morpheme: morphemeLabel(e.originalId, m),
            }),
          );
        }
        if (Boolean(before?.multiHead) !== Boolean(after?.multiHead) && after?.multiHead) {
          out.push(t("bible.mapping.roles.diff.multiHead", values));
        }
        return out.map((text) => line(text, e.originalId));
      }),
      ...diff.partLemmaChanges.map((p) =>
        p.lemma
          ? t("bible.mapping.roles.diff.partLemma", {
              part: partName(p.germanId, p.part),
              lemma: p.lemma,
            })
          : t("bible.mapping.roles.diff.partLemmaCleared", { part: partName(p.germanId, p.part) }),
      ),
      ...diff.addedWords.map((a) =>
        t("bible.mapping.diff.addedWord", { word: g(a.germanId), parent: g(a.parentId) }),
      ),
      ...diff.removedAddedWords.map((a) =>
        t("bible.mapping.diff.removedAddedWord", { word: g(a.germanId), parent: g(a.parentId) }),
      ),
    ].map((l) => (typeof l === "string" ? line(l) : l));
  }

  const errorKey = saveError ? `bible.mapping.error.${saveError}` : null;

  return (
    <section
      ref={rootRef}
      data-mapping-editor={compare ? undefined : ""}
      data-mode={mode}
      aria-label={t("bible.mapping.panelLabel", { reference })}
      className="map-panel"
      onKeyDown={onKeyDown}
    >
      <div ref={editRef} className="map-edit">
        <svg className="map-lines" aria-hidden="true">
          {lines.map((line) => (
            <path
              key={line.id}
              d={line.d}
              data-dashed={line.dashed ? "" : undefined}
              data-role={line.unit ? "unit" : undefined}
              data-kind={line.kind}
            />
          ))}
        </svg>
        <div className="map-sec">
          <div className="map-row-head">
            <p className="map-row-label">
              {germanReversed ? t("bible.mapping.germanRowReversed") : t("bible.mapping.germanRow")}
            </p>
            <Button
              variant="ghost"
              size="sm"
              aria-pressed={germanReversed}
              title={t("bible.mapping.reverseGermanHint")}
              onClick={toggleGermanReversed}
            >
              {t("bible.mapping.reverseGerman")}
            </Button>
          </div>
          <div className="map-row" lang="de" data-reversed={germanReversed ? "" : undefined}>
            {germanWords.map((word) => {
              const nodes = germanNodes(work, word.id);
              const split = work.splits.has(word.id);
              const tokens = nodes.map((node) => {
                const part = split ? parseNode(node).part : undefined;
                const number = groups.number(node);
                const isAdded = work.added.has(word.id);
                const count = partners(work, "g", word.id, part).length;
                return (
                  <MappingToken
                    key={node}
                    node={node}
                    side="g"
                    text={germanLabel(word.id, part)}
                    part={part === undefined ? undefined : { order: part, count: nodes.length }}
                    badge={isAdded ? `+${number ?? "?"}` : number ? String(number) : "?"}
                    status={wordStatus(base, work, "g", word.id, part)}
                    added={isAdded}
                    unmapped={number === undefined}
                    unit={
                      count > 0 &&
                      edgesOf(work, "g", word.id, part).every((e) => roleOf(work, e) === "unit")
                    }
                    warning={count >= OVER_ASSIGNED_FROM ? t("bible.mapping.warning") : undefined}
                    active={inActiveGroup(node) || listedGerman(word.id, part)}
                    selected={selected === node}
                    dropTarget={dropOver === node}
                    dragging={dragging === node}
                    draggable={draggable}
                    review={review?.german.has(word.id) ? reviewText : undefined}
                    label={reviewLabel(describe(node), review?.german.has(word.id) === true)}
                    {...tokenHandlers}
                  />
                );
              });
              return split ? (
                <span key={word.id} className="map-word">
                  {tokens}
                </span>
              ) : (
                tokens
              );
            })}
          </div>
        </div>
        <div className="map-sec">
          <p className="map-row-label">
            {t("bible.mapping.originalRow", {
              language: t(`bible.mapping.language.${data.language}`),
              direction: t(`bible.mapping.direction.${isRtl ? "rtl" : "ltr"}`),
            })}
          </p>
          <div className="map-row map-row-original" dir={isRtl ? "rtl" : "ltr"}>
            {originalWords.map((word) => {
              const node = nodeKey("o", word.id);
              const number = groups.number(node);
              return (
                <MappingToken
                  key={word.id}
                  node={node}
                  side="o"
                  text={word.original}
                  lang={originalLang}
                  lemma={word.lemma}
                  badge={number ? String(number) : "–"}
                  status={wordStatus(base, work, "o", word.id)}
                  unmapped={number === undefined}
                  active={inActiveGroup(node) || listHover === word.id}
                  selected={selected === node}
                  dropTarget={dropOver === node}
                  dragging={dragging === node}
                  draggable={draggable}
                  review={review?.original.has(word.id) ? reviewText : undefined}
                  label={reviewLabel(describe(node), review?.original.has(word.id) === true)}
                  showDetails={showDetails}
                  details={
                    <>
                      {word.strongs && <code>{word.strongs}</code>}
                      {word.morphologyCode && <code>{word.morphologyCode}</code>}
                      {word.gloss && <span>{word.gloss}</span>}
                    </>
                  }
                  {...tokenHandlers}
                />
              );
            })}
          </div>
          {draggable && (
            // biome-ignore lint/a11y/noStaticElementInteractions: mouse-only drop target; the keyboard path is the "clear" action in the selection chips
            <div
              className="map-dropzone"
              data-drop={dropOver === "clear" ? "" : undefined}
              onDragOver={(event) => dragOver("clear", event)}
              onDragLeave={() => setDropOver((current) => (current === "clear" ? null : current))}
              onDrop={(event) => drop("clear", event)}
            >
              {t("bible.mapping.dropzone")}
            </div>
          )}
        </div>
      </div>

      <div className="map-sec">
        {readOnly && !compare && (
          <p className="mb-2 text-fg-muted">{t("bible.mapping.readOnly")}</p>
        )}
        <div className="min-h-6">
          {activeInfo()}
          <p className="text-fg-muted">{lead()}</p>
        </div>
        {chips()}
        {splitting && selected === nodeKey("g", splitting.id) && (
          <WordSplitter
            word={germanName(splitting.id)}
            cuts={splitting.cuts}
            lemmas={splitting.lemmas}
            onLemmasChange={(lemmas) => setSplitting({ ...splitting, lemmas })}
            onChange={(cuts) => setSplitting({ ...splitting, cuts })}
            onCancel={() => setSplitting(null)}
            onConfirm={() => {
              const parts = partsFromCuts(germanName(splitting.id), splitting.cuts);
              edit(
                splitWord(work, splitting.id, parts, splitting.lemmas),
                t("bible.mapping.parts.status.split", {
                  word: germanName(splitting.id),
                  parts: parts.join(" + "),
                }),
              );
              setSelected(nodeKey("g", splitting.id, 1));
              setSplitting(null);
            }}
          />
        )}
        <p role="status" className="mt-2 min-h-5 text-fg-muted">
          {message}
        </p>
      </div>

      <div className="map-sec flex flex-wrap items-center gap-x-5 gap-y-2 text-xs text-fg-muted">
        {compare ? (
          (["added", "removed", "unchanged", "unit", "addedWord"] as const).map((kind) => (
            <Switch
              key={kind}
              checked={!hiddenKinds.has(kind)}
              title={t("bible.mapping.compare.legendToggle")}
              onCheckedChange={(on) =>
                setHiddenKinds((prev) => {
                  const next = new Set(prev);
                  if (on) next.delete(kind);
                  else next.add(kind);
                  return next;
                })
              }
            >
              <span className="map-key" data-kind={kind} aria-hidden="true" />
              {t(`bible.mapping.compare.legend.${kind}`)}
            </Switch>
          ))
        ) : (
          <>
            <span className="inline-flex items-center gap-1">
              <span className="map-badge" data-status="unchanged" aria-hidden="true">
                3
              </span>
              {t("bible.mapping.legend.unchanged")}
            </span>
            <span className="inline-flex items-center gap-1">
              <span className="map-badge" data-status="edited" aria-hidden="true">
                3
              </span>
              {t("bible.mapping.legend.edited")}
            </span>
            <span className="inline-flex items-center gap-1">
              <span className="map-badge" data-status="unchanged" aria-hidden="true">
                +2
              </span>
              {t("bible.mapping.legend.added")}
            </span>
            <span className="inline-flex items-center gap-1">
              <span className="map-badge" data-status="none" aria-hidden="true">
                –
              </span>
              {t("bible.mapping.legend.none")}
            </span>
          </>
        )}
        <Button
          variant="secondary"
          size="sm"
          aria-pressed={showDetails}
          onClick={() => setShowDetails(!showDetails)}
        >
          {t("bible.mapping.legend.details")}
        </Button>
      </div>

      <div className="map-sec">
        {(!readOnly || compare) && (
          <>
            <h3 className="text-xs font-semibold text-fg-muted">
              {t(compare ? "bible.mapping.compare.changes" : "bible.mapping.diff.title")}
            </h3>
            {changes === 0 ? (
              <p className="text-fg-muted">
                {t(compare ? "bible.mapping.compare.none" : "bible.mapping.diff.none")}
              </p>
            ) : (
              <ul className="list-disc pl-5">
                {diffLines().map((line) => (
                  <li key={line.text}>{withOriginal(line, listHover, setListHover)}</li>
                ))}
              </ul>
            )}
          </>
        )}
        {issues.map((issue) => {
          const message = t(`bible.mapping.parts.issue.${issue.code}`, {
            word: germanName(issue.germanId),
            part: issue.part === undefined ? "" : germanLabel(issue.germanId, issue.part),
            max: MAX_PART_LENGTH,
          });
          return (
            <FormError
              key={`${issue.code}${issue.germanId}.${issue.part ?? 0}`}
              message={message}
            />
          );
        })}
        {dropped.length > 0 && changes > 0 && (
          <p role="status" className="mt-2 text-fg-muted">
            {t("bible.mapping.parts.clearNote", { words: dropped.map(germanName).join(", ") })}
          </p>
        )}
        {roleAware && !readOnly && (rolesIssues.length > 0 || acceptedMultiHeads.length > 0) && (
          <div className="mt-2 flex flex-col gap-1">
            <p className="text-fg-muted">{t("bible.mapping.roles.aware")}</p>
            {rolesIssues.map((issue) => {
              const message = t(`bible.mapping.roles.issue.${issue.code}`, {
                original: morphemeName(issue.originalId, issue.morpheme),
                words: issue.germanIds.map(germanName).join(", "),
              });
              return (
                <div
                  key={`${issue.code}${issue.originalId}|${issue.morpheme}`}
                  className="flex flex-wrap items-center gap-2"
                >
                  <FormError message={message} />
                  {issue.code === "multiHead" && (
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => multiHead(issue.originalId, issue.morpheme, true)}
                    >
                      {t("bible.mapping.roles.acceptMultiHead")}
                    </Button>
                  )}
                </div>
              );
            })}
            {acceptedMultiHeads.map((m) => (
              <div
                key={`${m.originalId}|${m.morpheme}`}
                className="flex flex-wrap items-center gap-2"
              >
                <span className="text-fg-muted">
                  {t("bible.mapping.roles.multiHeadAccepted", {
                    original: morphemeName(m.originalId, m.morpheme),
                    words: m.germanIds.map(germanName).join(", "),
                  })}
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => multiHead(m.originalId, m.morpheme, false)}
                >
                  {t("bible.mapping.roles.withdrawMultiHead")}
                </Button>
              </div>
            ))}
          </div>
        )}
        {errorKey && (
          <div role="alert" className="mt-2">
            <p className="font-semibold text-accent">{t(errorKey)}</p>
            {saveError === "validation" && saveErrorDetails.length > 0 && (
              <>
                <p className="text-fg-muted">{t("bible.mapping.roles.serverErrors")}</p>
                <ul className="list-disc pl-5" lang="en">
                  {saveErrorDetails.map((detail) => (
                    <li key={detail}>
                      {readableServerError(
                        detail,
                        (id) => germanText.get(id),
                        (id) => originalById.get(id)?.original,
                      )}
                    </li>
                  ))}
                </ul>
              </>
            )}
          </div>
        )}
        {footer && <div className="mt-3">{footer}</div>}
        {!compare && (
          <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
            <div className="flex flex-wrap items-center gap-2">
              {!readOnly && (
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={history.past.length === 0}
                  onClick={doUndo}
                >
                  <span aria-hidden="true">↶</span>
                  {history.past.length > 0
                    ? t("bible.mapping.actions.undoCount", { count: history.past.length })
                    : t("bible.mapping.actions.undo")}
                </Button>
              )}
              {onAiRedo && (
                <Button variant="ghost" size="sm" onClick={onAiRedo}>
                  {t("bible.mapping.actions.aiRedo")}
                </Button>
              )}
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Button variant="secondary" onClick={onCancel}>
                {readOnly ? t("bible.mapping.actions.close") : t("bible.mapping.actions.cancel")}
              </Button>
              {!readOnly && (
                <Button variant="primary" disabled={saving || issues.length > 0} onClick={save}>
                  {saving
                    ? t("bible.mapping.actions.saving")
                    : saveLabel
                      ? saveLabel
                      : changes > 0
                        ? t("bible.mapping.actions.saveCount", { count: changes })
                        : t("bible.mapping.actions.save")}
                </Button>
              )}
            </div>
          </div>
        )}
      </div>
    </section>
  );
}
