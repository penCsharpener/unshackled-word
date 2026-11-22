import { type Ref, useImperativeHandle, useState } from "react";

/**
 * Stand-in for `@mdxeditor/editor` in page tests (`vi.mock("@mdxeditor/editor", () => import("@/test/mdxEditorMock"))`):
 * a textarea for the editor, the raw Markdown for read-only mode, no-op plugins and toolbar items. The real editor
 * has its own test (`NoteEditor.test.tsx`).
 */
interface MockEditorProps {
  markdown: string;
  readOnly?: boolean;
  onChange?: (markdown: string, initialMarkdownNormalize: boolean) => void;
  ref?: Ref<{ getMarkdown: () => string }>;
}

export function MDXEditor({ markdown, readOnly, onChange, ref }: MockEditorProps) {
  const [value, setValue] = useState(markdown);
  useImperativeHandle(ref, () => ({ getMarkdown: () => value }), [value]);
  if (readOnly) return <div data-testid="note-body">{markdown}</div>;
  return (
    <textarea
      aria-label="Notiztext"
      value={value}
      onChange={(event) => {
        setValue(event.target.value);
        onChange?.(event.target.value, false);
      }}
    />
  );
}

const plugin = () => ({});
const Nothing = () => null;
export const headingsPlugin = plugin;
export const listsPlugin = plugin;
export const quotePlugin = plugin;
export const linkPlugin = plugin;
export const linkDialogPlugin = plugin;
export const markdownShortcutPlugin = plugin;
export const maxLengthPlugin = plugin;
export const diffSourcePlugin = plugin;
export const toolbarPlugin = plugin;
export const BlockTypeSelect = Nothing;
export const BoldItalicUnderlineToggles = Nothing;
export const CodeToggle = Nothing;
export const CreateLink = Nothing;
export const DiffSourceToggleWrapper = Nothing;
export const ListsToggle = Nothing;
export const Separator = Nothing;
export const UndoRedo = Nothing;
