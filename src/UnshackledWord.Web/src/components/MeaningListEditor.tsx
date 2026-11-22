import { type KeyboardEvent, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "./Button";
import { Switch } from "./Switch";

/** One meaning of the vocab editor; `id` is absent until the meaning is saved. */
export interface MeaningRow {
  id?: number;
  german: string;
  /** Head links at derivation time; null for manual meanings. */
  count: number | null;
  /** Percent of the head links at derivation time; null for manual meanings. */
  share: number | null;
  isAccepted: boolean;
  origin: "derived" | "manual";
  isPinned: boolean;
}

export interface MeaningListEditorProps {
  rows: MeaningRow[];
  onChange: (rows: MeaningRow[]) => void;
}

/**
 * Meanings of one entry in their order: drag and drop (grip handle; arrow keys on the focused handle for the keyboard), accept (Switch), pin (`aria-pressed`) and delete for manual meanings.
 * Controlled: every change is passed to `onChange` with the whole list; the editor page saves it.
 */
export function MeaningListEditor({ rows, onChange }: MeaningListEditorProps) {
  const { t, i18n } = useTranslation();
  const format = (value: number) =>
    value.toLocaleString(i18n.language, { maximumFractionDigits: 1 });

  const update = (index: number, patch: Partial<MeaningRow>) =>
    onChange(rows.map((row, position) => (position === index ? { ...row, ...patch } : row)));

  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [overIndex, setOverIndex] = useState<number | null>(null);

  const moveTo = (index: number, target: number) => {
    const row = rows[index];
    if (!row || target === index || target < 0 || target >= rows.length) return;
    const next = [...rows];
    next.splice(index, 1);
    next.splice(target, 0, row);
    onChange(next);
  };

  const endDrag = () => {
    setDragIndex(null);
    setOverIndex(null);
  };

  const handleKey = (index: number, event: KeyboardEvent) => {
    if (event.key !== "ArrowUp" && event.key !== "ArrowDown") return;
    event.preventDefault();
    moveTo(index, index + (event.key === "ArrowUp" ? -1 : 1));
  };

  return (
    <ol className="grid gap-2">
      {rows.map((row, index) => {
        const details = [
          t(`vocab.editor.meanings.origin.${row.origin}`),
          row.count != null ? t("vocab.editor.meanings.count", { count: format(row.count) }) : null,
          row.share != null ? t("vocab.editor.meanings.share", { share: format(row.share) }) : null,
        ]
          .filter(Boolean)
          .join(" · ");

        return (
          <li
            key={row.id ?? `new-${index}`}
            draggable
            onDragStart={(event) => {
              event.dataTransfer.effectAllowed = "move";
              event.dataTransfer.setData("text/plain", String(index));
              setDragIndex(index);
            }}
            onDragOver={(event) => {
              if (dragIndex === null) return;
              event.preventDefault();
              event.dataTransfer.dropEffect = "move";
              setOverIndex(index);
            }}
            onDrop={(event) => {
              event.preventDefault();
              if (dragIndex !== null) moveTo(dragIndex, index);
              endDrag();
            }}
            onDragEnd={endDrag}
            className={`flex cursor-grab flex-wrap items-center gap-2 rounded-md border bg-surface p-2 text-sm ${overIndex === index && dragIndex !== null && dragIndex !== index ? "border-accent" : "border-border"} ${dragIndex === index ? "opacity-50" : ""} ${row.isAccepted ? "" : "text-fg-muted"}`}
          >
            <Button
              variant="ghost"
              size="sm"
              aria-label={t("vocab.editor.meanings.drag")}
              onKeyDown={(event) => handleKey(index, event)}
            >
              ⠿
            </Button>
            <span className="grid min-w-0 flex-1 gap-0.5">
              <span lang="de" className="font-medium">
                {row.german}
              </span>
              <span className="text-xs text-fg-muted tabular-nums">{details}</span>
            </span>
            <Switch
              checked={row.isAccepted}
              onCheckedChange={(isAccepted) => update(index, { isAccepted })}
            >
              {t("vocab.editor.meanings.accepted")}
            </Switch>
            <Button
              variant="secondary"
              size="sm"
              aria-pressed={row.isPinned}
              onClick={() => update(index, { isPinned: !row.isPinned })}
            >
              {t("vocab.editor.meanings.pin")}
            </Button>
            {row.origin === "manual" ? (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => onChange(rows.filter((_, position) => position !== index))}
              >
                {t("vocab.editor.meanings.delete")}
              </Button>
            ) : null}
          </li>
        );
      })}
    </ol>
  );
}
