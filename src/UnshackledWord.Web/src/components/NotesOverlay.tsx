import {
  Fragment,
  type KeyboardEvent,
  lazy,
  type ReactNode,
  type RefObject,
  Suspense,
  useEffect,
  useRef,
  useState,
} from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { describeApiError } from "@/lib/api";
import type { VerseWords } from "@/lib/bible/types";
import { wordKey } from "@/lib/bible/words";
import {
  anchorFor,
  type Note,
  type NoteTarget,
  type NoteVisibility,
  noteEndVerse,
  noteExcerpt,
  noteVerse,
} from "@/lib/notes/api";
import {
  useCreateNote,
  useDeleteNote,
  useSetNoteVisibility,
  useUpdateNote,
} from "@/lib/notes/queries";
import { Button, buttonClasses } from "./Button";
import { Dialog } from "./Dialog";
import { EmptyState } from "./EmptyState";
import { Switch } from "./Switch";

// MDXEditor is heavy: both components are separate chunks, loaded only once a note is shown or written.
const loadEditor = () => import("./NoteEditor");
const loadView = () => import("./NoteView");
const NoteEditor = lazy(() => loadEditor().then((m) => ({ default: m.NoteEditor })));
const NoteView = lazy(() => loadView().then((m) => ({ default: m.NoteView })));

export type NotesFilter = "passage" | "chapter";

/** What the overlay opens for; a new object reopens it from scratch. */
export interface NotesOverlayRequest {
  /** the clicked marker's anchor, the verse/chapter action's or the selection's target */
  passage: NoteTarget;
  /** note selected on open (marker, deep link) */
  noteId?: string;
  /** opens the editor for a new note at `passage` */
  write?: boolean;
  /** list filter on open; default `passage` */
  filter?: NotesFilter;
}

export interface NotesOverlayProps {
  /** undefined: closed */
  request?: NotesOverlayRequest;
  onClose: () => void;
  bookId: number;
  chapter: number;
  /** "Römer 8" */
  chapterLabel: string;
  /** shown words of the chapter, for the verse excerpt in the header */
  verses?: readonly VerseWords[];
  /** the chapter's notes in reading order */
  notes: Note[];
  status: "pending" | "error" | "success";
  onRetry: () => void;
  signedIn: boolean;
  showOthers: boolean;
  onShowOthersChange: (value: boolean) => void;
  /** owner action on a drifted words note: close and pick a new place in the text */
  onReanchor?: (note: Note) => void;
}

/** The anchor of a saved note as a target. */
export function noteTarget(note: Note): NoteTarget {
  const verse = noteVerse(note);
  if (verse === undefined) return { kind: "chapter" };
  const { anchor } = note;
  if (anchor.kind === "verses") {
    return { kind: "verses", verse, endVerse: noteEndVerse(note) ?? verse };
  }
  if (anchor.kind === "words") {
    return {
      kind: "words",
      verse,
      startPosition: anchor.startPosition ?? 0,
      endPosition: anchor.endPosition ?? anchor.startPosition ?? 0,
      quote: anchor.quotedText ?? "",
    };
  }
  return { kind: "verse", verse };
}

/** Verses of a target; undefined for the chapter. */
function verseSpan(target: NoteTarget): [number, number] | undefined {
  if (target.kind === "chapter") return undefined;
  return [target.verse, target.kind === "verses" ? target.endVerse : target.verse];
}

/** "Diese Stelle": chapter notes for a chapter passage, otherwise every note whose verses overlap the passage. */
export function notesAtPassage(notes: readonly Note[], passage: NoteTarget): Note[] {
  const span = verseSpan(passage);
  return notes.filter((note) => {
    const start = noteVerse(note);
    if (!span || start === undefined) return !span && start === undefined;
    const end = noteEndVerse(note) ?? start;
    return start <= span[1] && end >= span[0];
  });
}

/** Words shown before the first highlighted word, so the highlight stays inside the one-line excerpt. */
const EXCERPT_LEAD = 4;

/** One line of the verse text; the anchored words of a words target get `bg-mark-note`. */
function VerseExcerpt({
  verse,
  target,
  note,
}: {
  verse: VerseWords | undefined;
  target: NoteTarget;
  note?: Note;
}) {
  if (!verse || target.kind === "chapter") return null;
  const mapped = note?.mappedWords && new Set(note.mappedWords.map((word) => word.revisionWordId));
  const highlight = target.kind === "words" && (!note || note.anchorState === "ok");
  const marked = verse.words.map(
    (word) =>
      highlight &&
      (mapped
        ? word.revisionWordId !== undefined && mapped.has(word.revisionWordId)
        : word.basePosition !== undefined &&
          word.basePosition >= target.startPosition &&
          word.basePosition <= target.endPosition),
  );
  const first = marked.indexOf(true);
  const from = first > EXCERPT_LEAD ? first - EXCERPT_LEAD : 0;
  return (
    <p lang="de" data-testid="notes-excerpt" className="truncate font-scripture text-base text-fg">
      {from > 0 && "… "}
      {verse.words.slice(from).map((word, index) => (
        <Fragment key={wordKey(word)}>
          {index > 0 && " "}
          <span className={marked[from + index] ? "rounded-sm bg-mark-note" : undefined}>
            {word.text}
          </span>
        </Fragment>
      ))}
    </p>
  );
}

function PencilIcon() {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className="size-4"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M12 20h9M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" />
    </svg>
  );
}

function TrashIcon() {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className="size-4"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M3 6h18M8 6V4h8v2M19 6l-1 14H6L5 6M10 11v6M14 11v6" />
    </svg>
  );
}

/**
 * Notes palette over the reader (roadmap 0023, replaces the side panel): `Dialog` docked at the top, full screen
 * below 48rem. List column with the filter chips "Diese Stelle" / "Kapitel", arrow keys through the rows and
 * "Neue Notiz"; the pane shows the selected note (`NoteView`) or the editor (`NoteEditor`), both lazy chunks.
 */
export function NotesOverlay(props: NotesOverlayProps) {
  const { request, onClose } = props;
  const { t } = useTranslation();
  const initialFocusRef = useRef<HTMLElement>(null);
  // the editor's popups (link dialog, block type menu) must live inside the modal, the page behind it is inert
  const [popupHost, setPopupHost] = useState<HTMLDivElement | null>(null);
  const [busy, setBusy] = useState(false);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState<ReactNode>(null);

  return (
    <Dialog
      open={request !== undefined}
      onClose={onClose}
      title={title || t("notes.overlay.title")}
      description={description}
      size="lg"
      placement="top"
      bare
      dismissible={!busy}
      initialFocusRef={request?.write ? undefined : initialFocusRef}
    >
      {request && (
        <NotesOverlayBody
          key={JSON.stringify(request)}
          {...props}
          request={request}
          initialFocusRef={initialFocusRef}
          popupHost={popupHost}
          onBusyChange={setBusy}
          onHeader={(nextTitle, nextDescription) => {
            setTitle(nextTitle);
            setDescription(nextDescription);
          }}
        />
      )}
      <div ref={setPopupHost} />
    </Dialog>
  );
}

type Mode = "view" | "edit" | "new";

function NotesOverlayBody({
  request,
  bookId,
  chapter,
  chapterLabel,
  verses,
  notes,
  status,
  onRetry,
  signedIn,
  showOthers,
  onShowOthersChange,
  onReanchor,
  initialFocusRef,
  popupHost,
  onBusyChange,
  onHeader,
}: NotesOverlayProps & {
  request: NotesOverlayRequest;
  initialFocusRef: RefObject<HTMLElement | null>;
  popupHost: HTMLDivElement | null;
  onBusyChange: (busy: boolean) => void;
  onHeader: (title: string, description: ReactNode) => void;
}) {
  const { t } = useTranslation();
  const { passage } = request;
  const [filter, setFilter] = useState<NotesFilter>(request.filter ?? "passage");
  const [mode, setMode] = useState<Mode>(request.write ? "new" : "view");
  const [selectedId, setSelectedId] = useState(request.noteId);
  /** below md the list and the pane take turns */
  const [phoneView, setPhoneView] = useState<"list" | "detail">(
    request.write || request.noteId ? "detail" : "list",
  );
  const [confirmDelete, setConfirmDelete] = useState<Note>();
  const rowRefs = useRef(new Map<string, HTMLButtonElement>());
  const create = useCreateNote();
  const update = useUpdateNote();
  const remove = useDeleteNote();
  const setVisibility = useSetNoteVisibility();

  const atPassage = notesAtPassage(notes, passage);
  const shown = filter === "passage" ? atPassage : notes;
  const selected =
    shown.find((note) => note.id === selectedId) ??
    (selectedId === undefined ? shown[0] : notes.find((note) => note.id === selectedId)) ??
    shown[0];

  const reference = (target: NoteTarget) => {
    if (target.kind === "chapter") return chapterLabel;
    if (target.kind === "verses") return `${chapterLabel},${target.verse}–${target.endVerse}`;
    return `${chapterLabel},${target.verse}`;
  };
  const anchorLabel = (target: NoteTarget) => {
    if (target.kind === "chapter") return t("notes.anchorChapter", { reference: chapterLabel });
    return target.kind === "words" && target.quote
      ? t("notes.anchorWords", { quote: target.quote, reference: reference(target) })
      : t("notes.anchorVerse", { reference: reference(target) });
  };

  // header: the note being written, else the selected note, else the passage
  const headerNote = mode === "new" ? undefined : selected;
  const headerTarget = headerNote ? noteTarget(headerNote) : passage;
  const headerVerse =
    headerTarget.kind === "chapter"
      ? undefined
      : verses?.find((verse) => verse.verse === headerTarget.verse);
  const headerTitle = reference(headerTarget);
  const headerKey = `${headerTitle}|${headerNote?.id ?? ""}|${headerVerse ? "v" : ""}|${JSON.stringify(headerTarget)}`;
  // biome-ignore lint/correctness/useExhaustiveDependencies: the key covers everything the header shows
  useEffect(() => {
    onHeader(
      headerTitle,
      headerVerse ? (
        <VerseExcerpt verse={headerVerse} target={headerTarget} note={headerNote} />
      ) : null,
    );
  }, [headerKey]);

  const saving = create.isPending || update.isPending || setVisibility.isPending;
  useEffect(
    () => onBusyChange(saving || remove.isPending),
    [saving, remove.isPending, onBusyChange],
  );

  // the view chunk is fetched as soon as the overlay shows notes, so the first selection renders without a wait
  useEffect(() => {
    if (notes.length > 0) void loadView();
  }, [notes.length]);

  const resetMutations = () => {
    create.reset();
    update.reset();
    remove.reset();
    setVisibility.reset();
  };

  function select(note: Note, focus = false) {
    resetMutations();
    setSelectedId(note.id);
    setMode("view");
    if (focus) rowRefs.current.get(note.id)?.focus();
  }

  function startNew() {
    resetMutations();
    setMode("new");
    setPhoneView("detail");
  }

  /** A new note is created private; another visibility is a second call. If that fails, the note's editor opens with the error. */
  async function saveDraft(bodyMarkdown: string, visibility: NoteVisibility) {
    let note: Note | undefined;
    try {
      note = await create.mutateAsync({
        anchor: anchorFor(bookId, chapter, passage),
        bodyMarkdown,
      });
    } catch {
      return; // shown in the editor
    }
    if (!note?.id) return;
    setSelectedId(note.id);
    setMode("view");
    if (visibility === "private") return;
    try {
      await setVisibility.mutateAsync({ id: note.id, visibility });
    } catch {
      setMode("edit");
    }
  }

  /** Only what changed is sent: an unchanged body must not send an approved note back to the review. */
  async function saveExisting(note: Note, bodyMarkdown: string, visibility: NoteVisibility) {
    try {
      if (bodyMarkdown !== note.bodyMarkdown) {
        await update.mutateAsync({ id: note.id, bodyMarkdown });
      }
      if (visibility !== note.visibility) {
        await setVisibility.mutateAsync({ id: note.id, visibility });
      }
      setMode("view");
    } catch {
      // shown in the editor
    }
  }

  function onListKeyDown(event: KeyboardEvent<HTMLUListElement>) {
    if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
    if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
    event.preventDefault();
    if (shown.length === 0) return;
    const index = selected ? shown.indexOf(selected) : -1;
    const next =
      event.key === "ArrowDown"
        ? shown[Math.min(index + 1, shown.length - 1)]
        : shown[Math.max(index - 1, 0)];
    if (next) select(next, true);
  }

  const fallback = <p className="text-fg-muted">{t("notes.loadingEditor")}</p>;
  const editorError = update.isError
    ? describeApiError(update.error, t("notes.saveError"))
    : setVisibility.isError
      ? describeApiError(setVisibility.error, t("notes.visibility.saveError"))
      : null;

  const chip = (value: NotesFilter, label: string, count: number) => (
    <Button
      variant="secondary"
      size="sm"
      aria-pressed={filter === value}
      onClick={() => {
        setFilter(value);
        if (mode === "view") setSelectedId(undefined);
      }}
    >
      {label}
      <span className="text-xs text-fg-muted">{count}</span>
    </Button>
  );

  let pane: ReactNode;
  if (mode === "new") {
    pane = signedIn ? (
      <Suspense fallback={fallback}>
        <NoteEditor
          label={t("notes.newNote", { anchor: anchorLabel(passage) })}
          saving={create.isPending || setVisibility.isPending}
          error={create.isError ? describeApiError(create.error, t("notes.saveError")) : null}
          overlayContainer={popupHost}
          onCancel={() => {
            create.reset();
            setMode("view");
          }}
          onSave={(bodyMarkdown, visibility) => void saveDraft(bodyMarkdown, visibility)}
        />
      </Suspense>
    ) : (
      <div role="status" className="flex flex-wrap items-center gap-2">
        <p>{t("notes.signInPrompt")}</p>
        <Link to="/login" className={buttonClasses({ variant: "secondary", size: "sm" })}>
          {t("notes.signIn")}
        </Link>
        <Button variant="ghost" size="sm" onClick={() => setMode("view")}>
          {t("notes.cancel")}
        </Button>
      </div>
    );
  } else if (selected && mode === "edit" && selected.isOwn) {
    pane = (
      <Suspense fallback={fallback}>
        <NoteEditor
          label={t("notes.editNote", { anchor: anchorLabel(noteTarget(selected)) })}
          initialMarkdown={selected.bodyMarkdown}
          initialVisibility={selected.visibility}
          status={selected}
          saving={update.isPending || setVisibility.isPending}
          error={editorError}
          overlayContainer={popupHost}
          onCancel={() => {
            resetMutations();
            setMode("view");
          }}
          onSave={(bodyMarkdown, visibility) =>
            void saveExisting(selected, bodyMarkdown, visibility)
          }
        />
      </Suspense>
    );
  } else if (selected) {
    const drifted = selected.anchorState === "drifted";
    pane = (
      <article aria-label={anchorLabel(noteTarget(selected))} className="flex flex-col gap-2">
        <div className="flex items-start gap-2">
          <p className="min-w-0 flex-1 font-medium text-fg-muted">
            {anchorLabel(noteTarget(selected))}
          </p>
          {selected.isOwn && (
            <>
              <Button
                variant="ghost"
                size="sm"
                aria-label={t("notes.edit")}
                title={t("notes.edit")}
                onClick={() => {
                  resetMutations();
                  setMode("edit");
                }}
              >
                <PencilIcon />
              </Button>
              <Button
                variant="ghost"
                size="sm"
                aria-label={t("notes.delete")}
                title={t("notes.delete")}
                disabled={remove.isPending}
                onClick={() => {
                  remove.reset();
                  setConfirmDelete(selected);
                }}
              >
                <TrashIcon />
              </Button>
            </>
          )}
        </div>
        {drifted && selected.anchor.quotedText && (
          <p className="text-xs italic text-fg-muted">
            {t("notes.driftHint", { quote: selected.anchor.quotedText })}
          </p>
        )}
        {selected.isOwn && drifted && onReanchor && (
          <div>
            <Button variant="secondary" size="sm" onClick={() => onReanchor(selected)}>
              {t("notes.reanchor")}
            </Button>
          </div>
        )}
        <Suspense fallback={fallback}>
          <NoteView
            markdown={selected.bodyLinked}
            authorDisplayName={selected.authorDisplayName}
            createdAt={selected.createdAt}
            updatedAt={selected.updatedAt}
            status={selected.isOwn ? selected : undefined}
          />
        </Suspense>
        {remove.isError && (
          <p role="alert" className="text-sev-error">
            {describeApiError(remove.error, t("notes.deleteError"))}
          </p>
        )}
      </article>
    );
  } else if (status === "success") {
    pane = (
      <EmptyState>
        {filter === "passage" ? t("notes.overlay.emptyPassage") : t("notes.empty")}
      </EmptyState>
    );
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col font-sans text-sm md:flex-row">
      <div
        className={`flex min-h-0 flex-col border-border md:w-56 md:shrink-0 md:border-e max-md:flex-1 ${
          phoneView === "detail" ? "max-md:hidden" : ""
        }`}
      >
        <fieldset className="flex flex-wrap gap-2 border-b border-border p-3">
          <legend className="sr-only">{t("notes.overlay.filterGroup")}</legend>
          {chip("passage", t("notes.overlay.filterPassage"), atPassage.length)}
          {chip("chapter", t("notes.overlay.filterChapter"), notes.length)}
        </fieldset>
        <div className="min-h-0 flex-1 overflow-y-auto p-2">
          {status === "pending" && <p className="p-2 text-fg-muted">{t("notes.loading")}</p>}
          {status === "error" && (
            <div role="alert" className="flex flex-wrap items-center gap-2 p-2">
              <span>{t("notes.loadError")}</span>
              <Button variant="ghost" size="sm" onClick={onRetry}>
                {t("bible.retry")}
              </Button>
            </div>
          )}
          <ul
            aria-label={t("notes.overlay.list")}
            className="flex flex-col gap-1"
            onKeyDown={onListKeyDown}
          >
            {shown.map((note) => {
              const current = mode !== "new" && note.id === selected?.id;
              return (
                <li key={note.id}>
                  <Button
                    ref={(element) => {
                      if (element) rowRefs.current.set(note.id, element);
                      else rowRefs.current.delete(note.id);
                      if (current && element) initialFocusRef.current = element;
                    }}
                    variant="ghost"
                    size="sm"
                    aria-current={current ? "true" : undefined}
                    data-note-id={note.id}
                    className={`w-full text-start ${current ? "bg-mark-same" : ""}`}
                    onClick={() => {
                      select(note);
                      setPhoneView("detail");
                    }}
                  >
                    <span className="flex w-full min-w-0 flex-col gap-0.5">
                      <span className="flex items-baseline justify-between gap-2">
                        <span className="truncate font-medium">
                          {anchorLabel(noteTarget(note))}
                        </span>
                        <span
                          className={`shrink-0 text-xs ${note.isOwn ? "text-accent" : "text-note-other"}`}
                        >
                          {note.isOwn ? t("notes.overlay.you") : note.authorDisplayName}
                        </span>
                      </span>
                      <span className="line-clamp-2 font-normal text-fg-muted">
                        {noteExcerpt(note.bodyLinked)}
                      </span>
                    </span>
                  </Button>
                </li>
              );
            })}
          </ul>
        </div>
        <div className="flex flex-col items-start gap-2 border-t border-border p-3">
          <Button variant="primary" size="sm" onClick={startNew}>
            {t("notes.overlay.newNote")}
          </Button>
          <Switch
            checked={showOthers}
            onCheckedChange={onShowOthersChange}
            className="text-sm text-fg-muted"
          >
            {t("notes.showOthers")}
          </Switch>
        </div>
      </div>
      <div
        className={`min-h-0 flex-1 overflow-y-auto p-4 ${phoneView === "list" ? "max-md:hidden" : ""}`}
      >
        <Button
          variant="ghost"
          size="sm"
          className="mb-2 md:hidden"
          onClick={() => {
            if (mode !== "view") resetMutations();
            setMode("view");
            setPhoneView("list");
          }}
        >
          <span aria-hidden="true">←</span>
          {t("notes.overlay.back")}
        </Button>
        {pane}
      </div>
      <Dialog
        open={confirmDelete !== undefined}
        onClose={() => setConfirmDelete(undefined)}
        title={t("notes.deleteTitle")}
        footer={
          <>
            <Button variant="secondary" onClick={() => setConfirmDelete(undefined)}>
              {t("notes.cancel")}
            </Button>
            <Button
              variant="primary"
              onClick={() => {
                const note = confirmDelete;
                setConfirmDelete(undefined);
                if (!note) return;
                remove.mutate(note.id, {
                  onSuccess: () => {
                    setSelectedId(undefined);
                    setMode("view");
                  },
                });
              }}
            >
              {t("notes.deleteConfirm")}
            </Button>
          </>
        }
      >
        <p>{t("notes.deleteText")}</p>
      </Dialog>
    </div>
  );
}
