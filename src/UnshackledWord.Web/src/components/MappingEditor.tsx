import {
  type DragEvent,
  type KeyboardEvent,
  useEffect,
  useId,
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
  computeGroups,
  connect,
  createHistory,
  diffStates,
  disconnect,
  hasEdge,
  linksToState,
  type MappingState,
  type NodeKey,
  nodeKey,
  OVER_ASSIGNED_FROM,
  parseNode,
  partners,
  setAdded,
  stateToLinks,
  unadd,
  undo,
  wordStatus,
} from "@/lib/bible/mappingEditor";
import type { GetVerseMappingResponse, VerseMappingLink } from "@/lib/bible/types";
import { useCoarsePointer } from "@/lib/useCoarsePointer";
import { Button } from "./Button";
import { MappingToken } from "./MappingToken";

export type MappingSaveError = "validation" | "forbidden" | "failed";

export interface MappingEditorProps {
  data: GetVerseMappingResponse;
  /** e.g. "Johannes 3,1" */
  reference: string;
  saving?: boolean;
  saveError?: MappingSaveError | null;
  /** Called with the complete new mapping of the verse; not called when nothing changed. */
  onSave: (links: VerseMappingLink[]) => void;
  /** Cancel, Escape, and Save without edits. */
  onCancel: () => void;
}

type DropTarget = NodeKey | "clear";
interface LinePath {
  id: string;
  d: string;
  dashed: boolean;
}

/** Inline editor below a verse: connect German words to the original words by drag and drop or select-then-tap. */
export function MappingEditor({
  data,
  reference,
  saving = false,
  saveError = null,
  onSave,
  onCancel,
}: MappingEditorProps) {
  const { t } = useTranslation();
  const coarse = useCoarsePointer();
  const readOnly = !data.editingEnabled;
  const hintId = useId();

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
  const [history, setHistory] = useState(() => createHistory(base));
  const work = history.present;
  const [selected, setSelected] = useState<NodeKey | null>(null);
  const [addedMode, setAddedMode] = useState(false);
  const [hover, setHover] = useState<NodeKey | null>(null);
  const [dragging, setDragging] = useState<NodeKey | null>(null);
  const [dropOver, setDropOver] = useState<DropTarget | null>(null);
  const [showDetails, setShowDetails] = useState(false);
  const [message, setMessage] = useState("");
  const [lines, setLines] = useState<LinePath[]>([]);
  const rootRef = useRef<HTMLElement>(null);
  const editRef = useRef<HTMLDivElement>(null);

  const groups = useMemo(
    () => computeGroups(germanOrder, originalOrder, work),
    [germanOrder, originalOrder, work],
  );
  const diff = useMemo(() => diffStates(base, work), [base, work]);
  const changes = changeCount(diff);

  const germanName = (id: number) => germanText.get(id) ?? "";
  const originalName = (id: number) => originalById.get(id)?.original ?? "";

  const activeNode = selected ?? hover;
  const activeMembers = useMemo(
    () => new Set(activeNode ? groups.members(activeNode) : []),
    [activeNode, groups],
  );
  const inActiveGroup = (node: NodeKey) =>
    activeMembers.has(node) && (groups.number(node) !== undefined || node === activeNode);

  useEffect(() => {
    rootRef.current?.querySelector<HTMLElement>("[data-node]")?.focus({ preventScroll: true });
  }, []);

  // Connection curves of the active group only; measured from the tiles after every render that can move them.
  // biome-ignore lint/correctness/useExhaustiveDependencies: re-measure when anything that moves tiles changes
  useLayoutEffect(() => {
    function measure() {
      const container = editRef.current;
      if (!container || !activeNode) return setLines([]);
      const origin = container.getBoundingClientRect();
      const rect = (node: NodeKey) =>
        container.querySelector(`[data-node="${node}"]`)?.getBoundingClientRect();
      const next: LinePath[] = [];
      for (const key of work.edges) {
        const [g, o] = key.split("|").map(Number) as [number, number];
        const gNode = nodeKey("g", g);
        const a = rect(gNode);
        const b = rect(nodeKey("o", o));
        if (!a || !b || !inActiveGroup(gNode)) continue;
        const x1 = a.left + a.width / 2 - origin.left;
        const y1 = a.bottom - origin.top;
        const x2 = b.left + b.width / 2 - origin.left;
        const y2 = b.top - origin.top;
        const my = (y1 + y2) / 2;
        next.push({
          id: key,
          d: `M${x1},${y1} C${x1},${my} ${x2},${my} ${x2},${y2}`,
          dashed: false,
        });
      }
      for (const [g, p] of work.added) {
        const gNode = nodeKey("g", g);
        const a = rect(gNode);
        const b = rect(nodeKey("g", p));
        if (!a || !b || !inActiveGroup(gNode)) continue;
        const x1 = a.left + a.width / 2 - origin.left;
        const y1 = a.bottom - origin.top;
        const x2 = b.left + b.width / 2 - origin.left;
        const y2 = b.bottom - origin.top;
        const dip = Math.max(y1, y2) + Math.min(24, Math.abs(x2 - x1) / 3 + 8);
        next.push({
          id: `+${g}`,
          d: `M${x1},${y1} Q${(x1 + x2) / 2},${dip} ${x2},${y2}`,
          dashed: true,
        });
      }
      setLines(next);
    }
    measure();
    window.addEventListener("resize", measure);
    return () => window.removeEventListener("resize", measure);
  }, [activeNode, work, showDetails, groups, activeMembers]);

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

  function connectPair(g: number, o: number) {
    edit(
      connect(work, g, o),
      t("bible.mapping.status.connected", { german: germanName(g), original: originalName(o) }),
      t("bible.mapping.status.alreadyConnected"),
    );
  }

  function markAdded(g: number, parent: number) {
    edit(
      setAdded(work, g, parent),
      t("bible.mapping.status.added", { word: germanName(g), parent: germanName(parent) }),
    );
  }

  function clearOf(node: NodeKey) {
    const { side, id } = parseNode(node);
    const name = side === "g" ? germanName(id) : originalName(id);
    edit(clearWord(work, side, id), t("bible.mapping.status.cleared", { word: name }));
  }

  function select(node: NodeKey) {
    if (readOnly) return setSelected(selected === node ? null : node);
    if (addedMode) {
      setAddedMode(false);
      if (selected && parseNode(node).side === "g" && node !== selected) {
        markAdded(parseNode(selected).id, parseNode(node).id);
      }
      return;
    }
    if (!selected || selected === node) return setSelected(selected === node ? null : node);
    const a = parseNode(selected);
    const b = parseNode(node);
    if (a.side === b.side) return setSelected(node);
    const g = a.side === "g" ? a.id : b.id;
    const o = a.side === "o" ? a.id : b.id;
    if (hasEdge(work, g, o)) {
      edit(
        disconnect(work, g, o),
        t("bible.mapping.status.disconnected", {
          german: germanName(g),
          original: originalName(o),
        }),
      );
    } else {
      connectPair(g, o);
    }
  }

  function dropOn(src: NodeKey, target: DropTarget) {
    if (target === "clear") return clearOf(src);
    if (src === target) return;
    const a = parseNode(src);
    const b = parseNode(target);
    if (a.side === "g" && b.side === "g") return markAdded(a.id, b.id);
    if (a.side === "o" && b.side === "o") return setMessage(t("bible.mapping.status.originalPair"));
    connectPair(a.side === "g" ? a.id : b.id, a.side === "o" ? a.id : b.id);
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
    if ((event.key === "Delete" || event.key === "Backspace") && selected && onToken) {
      event.preventDefault();
      clearOf(selected);
    }
  }

  function save() {
    if (changes === 0) return onCancel();
    onSave(stateToLinks(work, germanOrder, originalOrder));
  }

  function describe(node: NodeKey): string {
    const { side, id } = parseNode(node);
    if (side === "g") {
      const word = germanName(id);
      const parent = work.added.get(id);
      if (parent !== undefined) {
        return t("bible.mapping.label.germanAdded", { word, parent: germanName(parent) });
      }
      const list = partners(work, "g", id).map(originalName);
      return list.length
        ? t("bible.mapping.label.germanLinked", { word, partners: list.join(", ") })
        : t("bible.mapping.label.germanUnmapped", { word });
    }
    const o = originalById.get(id);
    const word = [o?.original, o?.lemma, o?.strongs, o?.morphologyCode, o?.gloss]
      .filter(Boolean)
      .join(", ");
    const list = partners(work, "o", id).map(germanName);
    return list.length
      ? t("bible.mapping.label.originalLinked", { word, partners: list.join(", ") })
      : t("bible.mapping.label.originalUnmapped", { word });
  }

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
      return t(
        parseNode(selected).side === "g"
          ? "bible.mapping.hint.selectedGerman"
          : "bible.mapping.hint.selectedOriginal",
      );
    }
    return t(coarse ? "bible.mapping.hint.touch" : "bible.mapping.hint.drag");
  }

  function activeInfo() {
    if (!activeNode) return null;
    const { side, id } = parseNode(activeNode);
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
          {partners(work, "o", id).length === 0 && (
            <span className="text-fg-muted"> · {t("bible.mapping.info.originalUnmapped")}</span>
          )}
        </p>
      );
    }
    const parent = work.added.get(id);
    const originals = partners(work, "g", id).flatMap((oid) => originalById.get(oid) ?? []);
    const kids = addedChildren(work, id).map(germanName);
    return (
      <p>
        <strong>{germanName(id)}</strong>{" "}
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
    const { side, id } = parseNode(selected);
    const parent = side === "g" ? work.added.get(id) : undefined;
    const pairs = partners(work, side, id).map((p) => ({
      g: side === "g" ? id : p,
      o: side === "g" ? p : id,
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
        {pairs.map(({ g, o }) => (
          <span
            key={`${g}|${o}`}
            className="map-chip"
            data-status={hasEdge(base, g, o) ? "unchanged" : "edited"}
          >
            {germanName(g)} –{" "}
            <span lang={originalLang} className="font-original">
              {originalName(o)}
            </span>
            {!readOnly && (
              <Button
                variant="ghost"
                size="sm"
                aria-label={t("bible.mapping.chip.disconnect", {
                  german: germanName(g),
                  original: originalName(o),
                })}
                onClick={() =>
                  edit(
                    disconnect(work, g, o),
                    t("bible.mapping.status.disconnected", {
                      german: germanName(g),
                      original: originalName(o),
                    }),
                  )
                }
              >
                ✕
              </Button>
            )}
          </span>
        ))}
        {!readOnly && side === "g" && parent === undefined && (
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

  function diffLines(): string[] {
    const g = germanName;
    const o = originalName;
    return [
      ...diff.connected.map((e) =>
        t("bible.mapping.diff.connected", { german: g(e.germanId), original: o(e.originalId) }),
      ),
      ...diff.disconnected.map((e) =>
        t("bible.mapping.diff.disconnected", { german: g(e.germanId), original: o(e.originalId) }),
      ),
      ...diff.addedWords.map((a) =>
        t("bible.mapping.diff.addedWord", { word: g(a.germanId), parent: g(a.parentId) }),
      ),
      ...diff.removedAddedWords.map((a) =>
        t("bible.mapping.diff.removedAddedWord", { word: g(a.germanId), parent: g(a.parentId) }),
      ),
    ];
  }

  const errorKey = saveError ? `bible.mapping.error.${saveError}` : null;

  return (
    <section
      ref={rootRef}
      data-mapping-editor=""
      aria-label={t("bible.mapping.panelLabel", { reference })}
      className="map-panel"
      onKeyDown={onKeyDown}
    >
      <div ref={editRef} className="map-edit">
        <svg className="map-lines" aria-hidden="true">
          {lines.map((line) => (
            <path key={line.id} d={line.d} data-dashed={line.dashed ? "" : undefined} />
          ))}
        </svg>
        <div className="map-sec">
          <p className="map-row-label">{t("bible.mapping.germanRow")}</p>
          <div className="map-row" lang="de">
            {germanWords.map((word) => {
              const node = nodeKey("g", word.id);
              const number = groups.number(node);
              const isAdded = work.added.has(word.id);
              const count = partners(work, "g", word.id).length;
              return (
                <MappingToken
                  key={word.id}
                  node={node}
                  side="g"
                  text={word.text}
                  badge={isAdded ? `+${number ?? "?"}` : number ? String(number) : "?"}
                  status={wordStatus(base, work, "g", word.id)}
                  added={isAdded}
                  unmapped={number === undefined}
                  warning={count >= OVER_ASSIGNED_FROM ? t("bible.mapping.warning") : undefined}
                  active={inActiveGroup(node)}
                  selected={selected === node}
                  dropTarget={dropOver === node}
                  dragging={dragging === node}
                  draggable={draggable}
                  label={describe(node)}
                  {...tokenHandlers}
                />
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
                  active={inActiveGroup(node)}
                  selected={selected === node}
                  dropTarget={dropOver === node}
                  dragging={dragging === node}
                  draggable={draggable}
                  label={describe(node)}
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
        {readOnly && <p className="mb-2 text-fg-muted">{t("bible.mapping.readOnly")}</p>}
        <div className="min-h-6">
          {activeInfo()}
          <p className="text-fg-muted">{lead()}</p>
        </div>
        {chips()}
        <p role="status" className="mt-2 min-h-5 text-fg-muted">
          {message}
        </p>
      </div>

      <div className="map-sec flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-fg-muted">
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
        {!readOnly && (
          <>
            <h3 className="text-xs font-semibold text-fg-muted">{t("bible.mapping.diff.title")}</h3>
            {changes === 0 ? (
              <p className="text-fg-muted">{t("bible.mapping.diff.none")}</p>
            ) : (
              <ul className="list-disc pl-5">
                {diffLines().map((line) => (
                  <li key={line}>{line}</li>
                ))}
              </ul>
            )}
          </>
        )}
        {errorKey && (
          <p role="alert" className="mt-2 font-semibold text-accent">
            {t(errorKey)}
          </p>
        )}
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
            <Button variant="ghost" size="sm" disabled aria-describedby={hintId}>
              {t("bible.mapping.actions.aiRedo")}
            </Button>
            <span id={hintId} className="text-xs text-fg-muted">
              {t("bible.mapping.actions.aiRedoHint")}
            </span>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="secondary" onClick={onCancel}>
              {readOnly ? t("bible.mapping.actions.close") : t("bible.mapping.actions.cancel")}
            </Button>
            {!readOnly && (
              <Button variant="primary" disabled={saving} onClick={save}>
                {saving
                  ? t("bible.mapping.actions.saving")
                  : changes > 0
                    ? t("bible.mapping.actions.saveCount", { count: changes })
                    : t("bible.mapping.actions.save")}
              </Button>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}
