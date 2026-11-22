import { lazy, Suspense, useEffect, useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Button, buttonClasses } from "@/components/Button";
import { describeApiError } from "@/lib/api";
import {
  anchorFor,
  type Note,
  type NoteTarget,
  noteEndVerse,
  noteExcerpt,
  noteVerse,
} from "@/lib/notes/api";
import { useCreateNote, useDeleteNote, useUpdateNote } from "@/lib/notes/queries";

// MDXEditor is heavy: both components are separate chunks, loaded only once a note is shown or written.
const loadEditor = () => import("@/components/NoteEditor");
const loadView = () => import("@/components/NoteView");
const NoteEditor = lazy(() => loadEditor().then((m) => ({ default: m.NoteEditor })));
const NoteView = lazy(() => loadView().then((m) => ({ default: m.NoteView })));

export interface NotesPanelProps {
  /** `rail`: inside the sticky left margin (from 120rem); `sheet`: bottom sheet on phones, drawer from 48rem */
  variant: "rail" | "sheet";
  bookId: number;
  chapter: number;
  /** "Römer 8" */
  chapterLabel: string;
  notes: Note[];
  status: "pending" | "error" | "success";
  onRetry: () => void;
  /** note to expand and scroll into view; a new `token` repeats that for the same note */
  focus?: { id: string; token: number };
  onFocus: (id: string) => void;
  /** anchor of a note being written */
  draft?: NoteTarget;
  onDraftDone: () => void;
  signedIn: boolean;
  /** `sheet` only; also on `Esc` outside the editor */
  onClose?: () => void;
  /** owner action on a drifted words note: pick a new place in the text */
  onReanchor?: (note: Note) => void;
}

/** The anchor of a saved note as a target, for its reference line. */
function noteTarget(note: Note): NoteTarget {
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

function isTyping(target: EventTarget | null): boolean {
  return (
    target instanceof Element &&
    target.closest("input, textarea, select, dialog, [contenteditable='true']") !== null
  );
}

/** The chapter's notes in reading order (chapter notes first); one editor open at a time. */
export function NotesPanel({
  variant,
  bookId,
  chapter,
  chapterLabel,
  notes,
  status,
  onRetry,
  focus,
  onFocus,
  draft,
  onDraftDone,
  signedIn,
  onClose,
  onReanchor,
}: NotesPanelProps) {
  const { t } = useTranslation();
  const headingId = useId();
  const listRef = useRef<HTMLUListElement>(null);
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(() => new Set());
  const [editingId, setEditingId] = useState<string>();
  const create = useCreateNote();
  const update = useUpdateNote();
  const remove = useDeleteNote();

  const anchorLabel = (target: NoteTarget) => {
    if (target.kind === "chapter") return t("notes.anchorChapter", { reference: chapterLabel });
    const reference =
      target.kind === "verses"
        ? `${chapterLabel},${target.verse}–${target.endVerse}`
        : `${chapterLabel},${target.verse}`;
    return target.kind === "words" && target.quote
      ? t("notes.anchorWords", { quote: target.quote, reference })
      : t("notes.anchorVerse", { reference });
  };

  // Esc closes the sheet, but not while typing in the editor or answering its delete dialog
  useEffect(() => {
    if (!onClose) return;
    const close = onClose;
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== "Escape" || event.defaultPrevented || isTyping(event.target)) return;
      close();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  // the clicked marker's note opens and comes into view
  useEffect(() => {
    if (!focus) return;
    setExpanded((current) => new Set(current).add(focus.id));
    listRef.current
      ?.querySelector(`[data-note-id="${focus.id}"]`)
      ?.scrollIntoView({ block: "nearest" });
  }, [focus]);

  // a new draft closes an open editor, so there is only one Ctrl+S target
  useEffect(() => {
    if (draft) setEditingId(undefined);
  }, [draft]);

  // the editor chunk is fetched as soon as the panel shows notes, so the first click opens without a wait
  useEffect(() => {
    if (notes.length > 0) void loadView();
  }, [notes.length]);

  const toggle = (id: string) =>
    setExpanded((current) => {
      const next = new Set(current);
      if (!next.delete(id)) next.add(id);
      return next;
    });

  const fallback = <p className="text-fg-muted">{t("notes.loadingEditor")}</p>;

  return (
    <section
      aria-labelledby={headingId}
      className={`font-sans text-sm ${variant === "sheet" ? "notes-sheet" : "notes-rail"}`}
    >
      <div className="flex items-center justify-between gap-2">
        <h2
          id={headingId}
          className={`font-medium text-fg-muted ${variant === "rail" ? "sr-only" : ""}`}
        >
          {t("notes.heading", { reference: chapterLabel })}
        </h2>
        {onClose && (
          <Button variant="ghost" size="sm" onClick={onClose}>
            {t("notes.close")}
          </Button>
        )}
      </div>

      {draft &&
        (signedIn ? (
          <div className="mt-2 rounded-md border border-border bg-surface p-2">
            <Suspense fallback={fallback}>
              <NoteEditor
                label={t("notes.newNote", {
                  anchor: anchorLabel(draft),
                })}
                saving={create.isPending}
                error={create.isError ? describeApiError(create.error, t("notes.saveError")) : null}
                onCancel={() => {
                  create.reset();
                  onDraftDone();
                }}
                onSave={(bodyMarkdown) =>
                  create.mutate(
                    {
                      anchor: anchorFor(bookId, chapter, draft),
                      bodyMarkdown,
                    },
                    {
                      onSuccess: (note) => {
                        onDraftDone();
                        if (note?.id) onFocus(note.id);
                      },
                    },
                  )
                }
              />
            </Suspense>
          </div>
        ) : (
          <div role="status" className="mt-2 flex flex-wrap items-center gap-2">
            <p>{t("notes.signInPrompt")}</p>
            <Link to="/login" className={buttonClasses({ variant: "secondary", size: "sm" })}>
              {t("notes.signIn")}
            </Link>
            <Button variant="ghost" size="sm" onClick={onDraftDone}>
              {t("notes.cancel")}
            </Button>
          </div>
        ))}

      {status === "pending" && <p className="mt-2 text-fg-muted">{t("notes.loading")}</p>}
      {status === "error" && (
        <div role="alert" className="mt-2 flex items-center gap-2">
          <span>{t("notes.loadError")}</span>
          <Button variant="ghost" size="sm" onClick={onRetry}>
            {t("bible.retry")}
          </Button>
        </div>
      )}
      {status === "success" && notes.length === 0 && !draft && (
        <p className="mt-2 text-fg-muted">{t("notes.empty")}</p>
      )}

      <ul ref={listRef} className="mt-2 flex flex-col gap-2">
        {notes.map((note) => {
          const open = expanded.has(note.id);
          const editing = editingId === note.id;
          const label = anchorLabel(noteTarget(note));
          const drifted = note.anchorState === "drifted";
          return (
            <li
              key={note.id}
              data-note-id={note.id}
              className="scroll-mt-4 rounded-md border border-border bg-surface p-2"
            >
              <Button
                variant="ghost"
                size="sm"
                className="w-full justify-between text-start"
                aria-expanded={open}
                onClick={() => toggle(note.id)}
              >
                <span className="font-medium">{label}</span>
                {!note.isOwn && (
                  <span className="text-xs text-fg-muted">{note.authorDisplayName}</span>
                )}
              </Button>
              {drifted && note.anchor.quotedText && (
                <p className="mt-1 px-2 text-xs italic text-fg-muted">
                  {t("notes.driftHint", { quote: note.anchor.quotedText })}
                </p>
              )}
              {!open && <p className="mt-1 px-2 text-fg-muted">{noteExcerpt(note.bodyMarkdown)}</p>}
              {open && (
                <div className="mt-1 px-2">
                  <Suspense fallback={fallback}>
                    {editing ? (
                      <NoteEditor
                        label={t("notes.editNote", { anchor: label })}
                        initialMarkdown={note.bodyMarkdown}
                        saving={update.isPending}
                        deleting={remove.isPending}
                        error={
                          update.isError
                            ? describeApiError(update.error, t("notes.saveError"))
                            : remove.isError
                              ? describeApiError(remove.error, t("notes.deleteError"))
                              : null
                        }
                        onCancel={() => {
                          update.reset();
                          remove.reset();
                          setEditingId(undefined);
                        }}
                        onSave={(bodyMarkdown) =>
                          update.mutate(
                            { id: note.id, bodyMarkdown },
                            { onSuccess: () => setEditingId(undefined) },
                          )
                        }
                        onDelete={() =>
                          remove.mutate(note.id, { onSuccess: () => setEditingId(undefined) })
                        }
                      />
                    ) : (
                      <NoteView
                        markdown={note.bodyMarkdown}
                        authorDisplayName={note.authorDisplayName}
                        createdAt={note.createdAt}
                        updatedAt={note.updatedAt}
                      />
                    )}
                  </Suspense>
                  {note.isOwn && !editing && drifted && onReanchor && (
                    <Button
                      variant="secondary"
                      size="sm"
                      className="mt-1 me-1"
                      onClick={() => onReanchor(note)}
                    >
                      {t("notes.reanchor")}
                    </Button>
                  )}
                  {note.isOwn && !editing && (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="mt-1"
                      onClick={() => {
                        update.reset();
                        remove.reset();
                        onDraftDone();
                        setEditingId(note.id);
                      }}
                    >
                      {t("notes.edit")}
                    </Button>
                  )}
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}
