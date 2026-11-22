import "@mdxeditor/editor/style.css";
import { headingsPlugin, linkPlugin, listsPlugin, MDXEditor, quotePlugin } from "@mdxeditor/editor";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { formatStamp } from "@/lib/format";
import { mdxTranslation } from "@/lib/notes/mdxTranslation";

export interface NoteViewProps {
  /** raw Markdown body (the reference-linked body follows in roadmap 0023.3) */
  markdown: string;
  authorDisplayName: string;
  /** ISO timestamps */
  createdAt: string;
  updatedAt?: string;
}

/** Read-only rendering of one note (MDXEditor in read-only mode, lazy-loaded chunk) with author and date. */
export function NoteView({ markdown, authorDisplayName, createdAt, updatedAt }: NoteViewProps) {
  const { t, i18n } = useTranslation();
  const plugins = useMemo(() => [headingsPlugin(), listsPlugin(), quotePlugin(), linkPlugin()], []);
  const stamp = formatStamp(updatedAt ?? createdAt, i18n.language);

  return (
    <div className="flex flex-col gap-1 font-sans text-sm">
      {/* the editor only reads `markdown` on mount, so a changed body remounts it */}
      <MDXEditor
        key={markdown}
        markdown={markdown}
        readOnly
        plugins={plugins}
        translation={mdxTranslation}
        suppressHtmlProcessing
        className="note-md"
        contentEditableClassName="note-md-content"
      />
      <p className="text-xs text-fg-muted">
        {t(updatedAt ? "notes.authorEdited" : "notes.author", {
          author: authorDisplayName,
          date: stamp,
        })}
      </p>
    </div>
  );
}

export default NoteView;
