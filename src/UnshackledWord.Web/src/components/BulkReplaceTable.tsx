import type { KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import type { BulkReplaceRow } from "@/lib/revision/types";

export const rowKey = (row: Pick<BulkReplaceRow, "hebRefId" | "occurrence">) =>
  `${row.hebRefId}:${row.occurrence}`;

export interface BulkReplaceTableProps {
  rows: readonly BulkReplaceRow[];
  /** keys (`hebRefId:occurrence`) of the checked rows */
  checked: ReadonlySet<string>;
  onToggle: (key: string, checked: boolean) => void;
  /** Reference text of a row in the reader language, e.g. "1Mo 2,22". */
  referenceOf: (row: BulkReplaceRow) => string;
  /** Reader link of a row. */
  pathOf: (row: BulkReplaceRow) => string;
  /** Verses already reviewed in this browser (`hebRefId`). */
  reviewed?: ReadonlySet<number>;
  label: string;
}

/** Search hits with a checkbox each: reference, excerpt with the hit marked, Strong's and hints. */
/** Arrow up/down moves the focus to the previous/next row checkbox of the list. */
function moveFocus(event: KeyboardEvent<HTMLInputElement>) {
  if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
  const boxes = [
    ...(event.currentTarget
      .closest("ul")
      ?.querySelectorAll<HTMLInputElement>('input[type="checkbox"]') ?? []),
  ];
  const at = boxes.indexOf(event.currentTarget);
  const next = boxes[at + (event.key === "ArrowDown" ? 1 : -1)];
  if (!next) return;
  event.preventDefault();
  next.focus();
  next.scrollIntoView?.({ block: "nearest" });
}

export function BulkReplaceTable({
  rows,
  checked,
  onToggle,
  referenceOf,
  pathOf,
  reviewed,
  label,
}: BulkReplaceTableProps) {
  const { t } = useTranslation();
  return (
    <ul aria-label={label} className="grid gap-2">
      {rows.map((row) => {
        const key = rowKey(row);
        const reference = referenceOf(row);
        const on = checked.has(key);
        return (
          <li
            key={key}
            className={`flex items-start gap-3 rounded-md border border-border bg-surface p-3 ${on ? "" : "opacity-60"}`}
          >
            <input
              type="checkbox"
              checked={on}
              onChange={(event) => onToggle(key, event.target.checked)}
              onKeyDown={moveFocus}
              aria-label={t("bulkReplace.table.select", { reference, n: row.occurrence })}
              className="mt-1 size-4 accent-accent"
            />
            <div className="grid min-w-0 flex-1 gap-1">
              <div className="flex flex-wrap items-center gap-2 text-sm">
                <Link
                  to={pathOf(row)}
                  target="_blank"
                  className="font-medium text-accent underline"
                >
                  {reference}
                  <span className="sr-only"> {t("bulkReplace.table.newTab")}</span>
                  <span aria-hidden="true"> ↗</span>
                </Link>
                {row.strongs.length === 0 ? (
                  <span className="rounded-full border border-border bg-surface-muted px-2 text-xs text-fg-muted">
                    {t("bulkReplace.table.unlinked")}
                  </span>
                ) : (
                  row.strongs.map((strongs) => (
                    <span
                      key={strongs}
                      className="rounded-full border border-border bg-surface-muted px-2 text-xs"
                    >
                      {strongs}
                    </span>
                  ))
                )}
                {row.lemma ? <span className="text-xs text-fg-muted">{row.lemma}</span> : null}
                {row.targetVersion !== undefined ? (
                  <span className="rounded-md bg-surface-muted px-1.5 text-xs text-fg-muted">
                    {t("bulkReplace.table.targetVersion", { version: row.targetVersion })}
                  </span>
                ) : null}
                {row.targetDiffers ? (
                  <span
                    className="rounded-md bg-surface-muted px-1.5 text-xs text-fg-muted"
                    title={t("bulkReplace.table.differsHint")}
                  >
                    {t("bulkReplace.table.differs")}
                  </span>
                ) : null}
                {reviewed?.has(row.hebRefId) ? (
                  <span className="text-xs text-fg-muted">✓ {t("bulkReplace.table.reviewed")}</span>
                ) : null}
              </div>
              <p className="font-serif text-base">
                … {row.contextBefore}{" "}
                <mark className="rounded-sm bg-mark-linked px-0.5">
                  <span className="sr-only">{t("bulkReplace.table.hit")} </span>
                  {row.found}
                </mark>{" "}
                {row.contextAfter} …
              </p>
              {row.articleBefore ? (
                <p className="text-xs text-fg-muted">
                  ⚠ {t("bulkReplace.table.article", { article: row.articleBefore })}
                </p>
              ) : null}
            </div>
          </li>
        );
      })}
    </ul>
  );
}
