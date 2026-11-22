import "@mdxeditor/editor/style.css";
import {
  BlockTypeSelect,
  BoldItalicUnderlineToggles,
  CodeToggle,
  CreateLink,
  DiffSourceToggleWrapper,
  diffSourcePlugin,
  headingsPlugin,
  ListsToggle,
  linkDialogPlugin,
  linkPlugin,
  listsPlugin,
  MDXEditor,
  type MDXEditorMethods,
  markdownShortcutPlugin,
  maxLengthPlugin,
  quotePlugin,
  Separator,
  toolbarPlugin,
  UndoRedo,
} from "@mdxeditor/editor";
import { useEffect, useId, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { mdxTranslation } from "@/lib/notes/mdxTranslation";
import { Button } from "./Button";
import { Dialog } from "./Dialog";

/** Same limit as the API (`NoteBody.MaxLength`). */
export const NOTE_MAX_LENGTH = 20_000;

export interface NoteEditorProps {
  /** anchor line above the editor, e.g. "Neue Notiz zu Römer 8,28" */
  label: string;
  /** body of an existing note; empty for a new one */
  initialMarkdown?: string;
  saving?: boolean;
  /** message of a failed save or delete */
  error?: string | null;
  onSave: (markdown: string) => void;
  onCancel: () => void;
  /** existing notes only: deletes after a confirm dialog */
  onDelete?: () => void;
  deleting?: boolean;
}

function Toolbar() {
  return (
    <DiffSourceToggleWrapper options={["rich-text", "source"]}>
      <UndoRedo />
      <Separator />
      <BoldItalicUnderlineToggles options={["Bold", "Italic"]} />
      <CodeToggle />
      <Separator />
      <BlockTypeSelect />
      <ListsToggle options={["bullet", "number"]} />
      <CreateLink />
    </DiffSourceToggleWrapper>
  );
}

/**
 * Markdown editor of one note (MDXEditor, lazy-loaded chunk): bold, italic, inline code, paragraph / quote / H3 / H4,
 * bulleted and numbered lists, links, undo/redo and a Markdown source view. Ctrl+S (Cmd+S) saves.
 */
export function NoteEditor({
  label,
  initialMarkdown = "",
  saving = false,
  error,
  onSave,
  onCancel,
  onDelete,
  deleting = false,
}: NoteEditorProps) {
  const { t } = useTranslation();
  const labelId = useId();
  const editorRef = useRef<MDXEditorMethods>(null);
  const [markdown, setMarkdown] = useState(initialMarkdown);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const empty = markdown.trim().length === 0;
  const busy = saving || deleting;

  const plugins = useMemo(
    () => [
      headingsPlugin({ allowedHeadingLevels: [3, 4] }),
      listsPlugin(),
      quotePlugin(),
      linkPlugin(),
      linkDialogPlugin(),
      markdownShortcutPlugin(),
      maxLengthPlugin(NOTE_MAX_LENGTH),
      diffSourcePlugin({ viewMode: "rich-text" }),
      toolbarPlugin({ toolbarContents: () => <Toolbar /> }),
    ],
    [],
  );

  // Ctrl+S (Cmd+S) saves, also while the toolbar has focus; the browser's own "save page" stays quiet.
  const saveRef = useRef<() => void>(() => {});
  saveRef.current = () => {
    const current = editorRef.current?.getMarkdown() ?? markdown;
    if (current.trim().length > 0 && !busy) onSave(current);
  };
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && !event.altKey && event.key.toLowerCase() === "s") {
        event.preventDefault();
        saveRef.current();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  return (
    <section aria-labelledby={labelId} className="flex flex-col gap-2 font-sans text-sm">
      <p id={labelId} className="font-medium text-fg-muted">
        {label}
      </p>
      <MDXEditor
        ref={editorRef}
        markdown={initialMarkdown}
        onChange={setMarkdown}
        plugins={plugins}
        translation={mdxTranslation}
        suppressHtmlProcessing
        autoFocus={{ defaultSelection: "rootEnd" }}
        placeholder={t("notes.placeholder")}
        className="note-md note-md-edit"
        contentEditableClassName="note-md-content"
      />
      {error && (
        <p role="alert" className="text-sev-error">
          {error}
        </p>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <Button
          variant="primary"
          size="sm"
          disabled={empty || busy}
          onClick={() => saveRef.current()}
          title={t("notes.saveShortcut")}
        >
          {saving ? t("notes.saving") : t("notes.save")}
        </Button>
        <Button variant="ghost" size="sm" disabled={busy} onClick={onCancel}>
          {t("notes.cancel")}
        </Button>
        {onDelete && (
          <Button
            variant="ghost"
            size="sm"
            className="ms-auto text-sev-error"
            disabled={busy}
            onClick={() => setConfirmDelete(true)}
          >
            {t("notes.delete")}
          </Button>
        )}
      </div>
      {onDelete && (
        <Dialog
          open={confirmDelete}
          onClose={() => setConfirmDelete(false)}
          title={t("notes.deleteTitle")}
          footer={
            <>
              <Button variant="secondary" onClick={() => setConfirmDelete(false)}>
                {t("notes.cancel")}
              </Button>
              <Button
                variant="primary"
                onClick={() => {
                  setConfirmDelete(false);
                  onDelete();
                }}
              >
                {t("notes.deleteConfirm")}
              </Button>
            </>
          }
        >
          <p>{t("notes.deleteText")}</p>
        </Dialog>
      )}
    </section>
  );
}

export default NoteEditor;
