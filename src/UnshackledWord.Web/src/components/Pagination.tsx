import { type KeyboardEvent, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "./Button";
import { TextField } from "./TextField";

/** A page number or a gap (ellipsis) in the page-number window. */
export type PageWindowItem = number | "gap";

/** Neighbours of the current page from `sm` up, and below it (phones show fewer numbers). */
const WIDE_SIBLINGS = 2;
const NARROW_SIBLINGS = 0;

/** Number of pages for `total` items at `pageSize` per page (at least 1). */
export function pageCountOf(total: number, pageSize: number): number {
  if (pageSize <= 0) return 1;
  return Math.max(1, Math.ceil(total / pageSize));
}

/**
 * The page numbers to show: always the first and the last page, `siblings` pages on each side of the current one and a gap
 * where pages are left out (e.g. `1 … 4 5 6 7 8 … 41`). Near either end the window keeps its width, so the row does not
 * jump; a gap always stands for at least two pages. The current page is clamped to 1..pageCount.
 */
export function pageWindow(
  page: number,
  pageCount: number,
  siblings = WIDE_SIBLINGS,
): PageWindowItem[] {
  if (pageCount < 1) return [];
  const all = (from: number, to: number) =>
    Array.from({ length: to - from + 1 }, (_, i) => from + i);
  // first + last + current + siblings on both sides + two gaps
  const slots = 2 * siblings + 5;
  if (pageCount <= slots) return all(1, pageCount);
  const current = Math.min(Math.max(1, page), pageCount);
  let start = current - siblings;
  let end = current + siblings;
  if (start <= 3) {
    start = 2;
    end = 2 * siblings + 3;
  } else if (end >= pageCount - 2) {
    start = pageCount - 2 * siblings - 2;
    end = pageCount - 1;
  }
  const items: PageWindowItem[] = [1];
  if (start > 2) items.push("gap");
  items.push(...all(start, end));
  if (end < pageCount - 1) items.push("gap");
  items.push(pageCount);
  return items;
}

/** One rendered entry of the number row; `wide` / `narrow` say in which layout (from `sm` / below `sm`) it shows. */
export interface PagerItem {
  key: string;
  /** Absent for a gap. */
  page?: number;
  wide: boolean;
  narrow: boolean;
}

/** Merges the wide and the narrow window into one row in page order, each entry marked with the layouts it shows in. */
export function pagerItems(page: number, pageCount: number): PagerItem[] {
  const numbers = (items: PageWindowItem[]) =>
    new Set(items.filter((item): item is number => item !== "gap"));
  const wide = numbers(pageWindow(page, pageCount, WIDE_SIBLINGS));
  const narrow = numbers(pageWindow(page, pageCount, NARROW_SIBLINGS));
  const pages = [...new Set([...wide, ...narrow])].sort((a, b) => a - b);
  const items: PagerItem[] = [];
  let lastWide = 0;
  let lastNarrow = 0;
  for (const p of pages) {
    const inWide = wide.has(p);
    const inNarrow = narrow.has(p);
    const gapWide = inWide && lastWide > 0 && p - lastWide > 1;
    const gapNarrow = inNarrow && lastNarrow > 0 && p - lastNarrow > 1;
    if (gapWide || gapNarrow) items.push({ key: `gap-${p}`, wide: gapWide, narrow: gapNarrow });
    items.push({ key: `page-${p}`, page: p, wide: inWide, narrow: inNarrow });
    if (inWide) lastWide = p;
    if (inNarrow) lastNarrow = p;
  }
  return items;
}

/** The page a "go to page" entry asks for, clamped to 1..pageCount; null for anything that is not a whole number. */
export function parseJump(value: string, pageCount: number): number | null {
  const text = value.trim();
  if (!/^\d+$/.test(text)) return null;
  return Math.min(Math.max(1, Number(text)), Math.max(1, pageCount));
}

/** Tailwind classes of an entry that shows only in one of the two layouts. */
function layoutClass(item: PagerItem): string {
  if (item.wide && item.narrow) return "";
  return item.wide ? "hidden sm:block" : "sm:hidden";
}

export interface PaginationProps {
  /** Current page, 1-based. */
  page: number;
  pageCount: number;
  onPageChange: (page: number) => void;
  /** Accessible name of the navigation, default "Seiten". */
  label?: string;
  className?: string;
}

/**
 * Page navigation: first, previous, page numbers with gaps (current page `aria-current="page"`), next, last and a small
 * "go to page" field (Enter jumps, clamped, invalid input ignored). Renders nothing for a single page; wraps on narrow screens,
 * which also show fewer numbers.
 */
export function Pagination({
  page,
  pageCount,
  onPageChange,
  label,
  className = "",
}: PaginationProps) {
  const { t } = useTranslation();
  const [jump, setJump] = useState("");
  if (pageCount <= 1) return null;
  const current = Math.min(Math.max(1, page), pageCount);
  const go = (target: number) => {
    if (target !== current) onPageChange(target);
  };
  const onJumpKey = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key !== "Enter") return;
    event.preventDefault();
    const target = parseJump(jump, pageCount);
    if (target === null) return;
    setJump("");
    go(target);
  };
  const atStart = current <= 1;
  const atEnd = current >= pageCount;

  return (
    <nav
      aria-label={label ?? t("pagination.label")}
      className={`flex flex-wrap items-center gap-2 ${className}`}
    >
      <ul className="flex flex-wrap items-center gap-1">
        <li>
          <Button
            size="sm"
            aria-label={t("pagination.first")}
            disabled={atStart}
            onClick={() => go(1)}
          >
            «
          </Button>
        </li>
        <li>
          <Button
            size="sm"
            aria-label={t("pagination.previous")}
            disabled={atStart}
            onClick={() => go(current - 1)}
          >
            ‹
          </Button>
        </li>
        {pagerItems(current, pageCount).map((item) =>
          item.page === undefined ? (
            <li
              key={item.key}
              aria-hidden="true"
              className={`px-1 text-fg-muted ${layoutClass(item)}`}
            >
              …
            </li>
          ) : (
            <li key={item.key} className={layoutClass(item)}>
              <Button
                size="sm"
                variant={item.page === current ? "primary" : "secondary"}
                aria-label={t("pagination.page", { page: item.page })}
                aria-current={item.page === current ? "page" : undefined}
                className={`tabular-nums ${item.page === current ? "font-semibold" : ""}`}
                onClick={() => go(item.page as number)}
              >
                {item.page}
              </Button>
            </li>
          ),
        )}
        <li>
          <Button
            size="sm"
            aria-label={t("pagination.next")}
            disabled={atEnd}
            onClick={() => go(current + 1)}
          >
            ›
          </Button>
        </li>
        <li>
          <Button
            size="sm"
            aria-label={t("pagination.last")}
            disabled={atEnd}
            onClick={() => go(pageCount)}
          >
            »
          </Button>
        </li>
      </ul>
      <div className="flex items-center gap-2 text-sm text-fg-muted">
        <span aria-hidden="true">{t("pagination.jump")}</span>
        <TextField
          type="number"
          size="sm"
          required={false}
          labelHidden
          label={t("pagination.jumpLabel", { last: pageCount })}
          inputMode="numeric"
          min={1}
          max={pageCount}
          className="w-20"
          value={jump}
          onChange={setJump}
          onKeyDown={onJumpKey}
        />
      </div>
    </nav>
  );
}
