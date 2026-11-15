import { type KeyboardEvent, useEffect, useLayoutEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { bookName, buildPickerGroups, chapterPath } from "@/lib/bible/books";
import type { BibleStructureBook, ChapterRef, PickerGroup, Testament } from "@/lib/bible/types";
import { Button } from "./Button";

const PANEL_ID = "bible-picker-panel";
const testaments: readonly Testament[] = ["ot", "nt"];

export interface BibleChapterPickerProps {
  /** undefined while loading: renders a disabled bar */
  books: BibleStructureBook[] | undefined;
  currentBookId?: number;
  currentChapter?: number;
  /** testament open on first render; omitted = collapsed */
  defaultTestament?: Testament;
  /** link target of a chapter; defaults to the reader (`chapterPath`) */
  chapterHref?: (ref: ChapterRef) => string;
  /** `overlay` (default): the open panel covers the page full width under the menu; `inline`: part of the page flow */
  panel?: "overlay" | "inline";
}

function tabId(testament: Testament): string {
  return `bible-picker-tab-${testament}`;
}

/** Books are as wide as they have chapters (about ten per column, 1 to 6 columns) and wrap to the next row. */
const BOOK_BASIS: Record<number, string> = {
  1: "sm:basis-28",
  2: "sm:basis-20",
  3: "sm:basis-30",
  4: "sm:basis-40",
  5: "sm:basis-50",
  6: "sm:basis-60",
};
function bookColumns(chapterCount: number): number {
  return Math.min(6, Math.max(1, Math.ceil(chapterCount / 10)));
}

export function BibleChapterPicker({
  books,
  currentBookId,
  currentChapter,
  defaultTestament,
  chapterHref = chapterPath,
  panel = "overlay",
}: BibleChapterPickerProps) {
  const { t, i18n } = useTranslation();
  const [openTestament, setOpenTestament] = useState<Testament | null>(defaultTestament ?? null);
  const [expandedKey, setExpandedKey] = useState<string | null>(null);
  const [expandTouched, setExpandTouched] = useState(false);
  const navRef = useRef<HTMLElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const overlay = panel === "overlay";

  // The overlay starts right under the bar the picker sits in (header or sticky row).
  useLayoutEffect(() => {
    if (!overlay || !openTestament) return;
    const bar = navRef.current?.closest(".sticky-picker, .app-header");
    if (bar) {
      panelRef.current?.style.setProperty(
        "--picker-panel-top",
        `${bar.getBoundingClientRect().bottom}px`,
      );
    }
  }, [overlay, openTestament]);

  useEffect(() => {
    if (!overlay || !openTestament) return;
    function onPointerDown(event: PointerEvent) {
      if (event.target instanceof Node && navRef.current?.contains(event.target)) return;
      setOpenTestament(null);
    }
    document.addEventListener("pointerdown", onPointerDown);
    return () => document.removeEventListener("pointerdown", onPointerDown);
  }, [overlay, openTestament]);

  const groups: PickerGroup[] =
    books && openTestament
      ? buildPickerGroups(books, openTestament, i18n.language, (key, opts = {}) => t(key, opts))
      : [];
  const currentGroupKey = groups.find(
    (group) =>
      group.bookId === currentBookId &&
      (currentChapter === undefined || group.chapters.includes(currentChapter)),
  )?.key;
  const activeExpandedKey = expandTouched ? expandedKey : (currentGroupKey ?? null);

  function toggleTestament(testament: Testament) {
    setOpenTestament((open) => (open === testament ? null : testament));
  }

  function handleKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.key !== "Escape" || !openTestament) {
      return;
    }
    const previous = openTestament;
    setOpenTestament(null);
    document.getElementById(tabId(previous))?.focus();
  }

  return (
    <nav ref={navRef} aria-label={t("bible.picker.label")} onKeyDown={handleKeyDown}>
      <div className="flex">
        {testaments.map((testament) => (
          <Button
            key={testament}
            id={tabId(testament)}
            variant={openTestament === testament ? "primary" : "secondary"}
            className="flex-1 rounded-none! border-0!"
            disabled={!books}
            aria-expanded={openTestament === testament}
            aria-controls={PANEL_ID}
            onClick={() => toggleTestament(testament)}
          >
            {t(`bible.testament.${testament}`)}
          </Button>
        ))}
      </div>
      {!books && <p className="mt-2 text-sm text-fg-muted">{t("bible.picker.loading")}</p>}
      {books && books.length === 0 && (
        <p className="mt-2 text-sm text-fg-muted">{t("bible.picker.empty")}</p>
      )}
      {openTestament && (
        <div ref={panelRef} className={overlay ? "picker-panel" : undefined}>
          <section
            id={PANEL_ID}
            aria-label={t(`bible.testament.${openTestament}`)}
            className={`grid grid-cols-1 gap-2 bg-surface p-2 sm:flex sm:flex-wrap ${overlay ? "px-4 md:px-8" : "mt-2"}`}
          >
            {groups.map((group) => {
              const expanded = activeExpandedKey === group.key;
              const columns = bookColumns(group.chapters.length);
              const bookTitle = group.title;
              const book = books?.find((candidate) => candidate.id === group.bookId);
              const name = book ? bookName(book, i18n.language) : bookTitle;
              return (
                <div key={group.key} className={`sm:grow ${BOOK_BASIS[columns]}`}>
                  <h3 className="bg-surface-muted text-sm font-semibold">
                    <Button
                      variant="ghost"
                      size="sm"
                      className="w-full justify-start text-left sm:hidden"
                      aria-expanded={expanded}
                      aria-label={t("bible.picker.showChapters", { book: bookTitle })}
                      onClick={() => {
                        setExpandTouched(true);
                        setExpandedKey(expanded ? null : group.key);
                      }}
                    >
                      {bookTitle}
                    </Button>
                    <span className="hidden whitespace-nowrap px-3 py-1 sm:block">{bookTitle}</span>
                  </h3>
                  <div className={expanded ? "flex flex-wrap" : "hidden flex-wrap sm:flex"}>
                    {group.chapters.map((chapter) => {
                      const isCurrent =
                        group.bookId === currentBookId && chapter === currentChapter;
                      return (
                        <Link
                          key={chapter}
                          to={chapterHref({ bookId: group.bookId, chapter })}
                          aria-label={t("bible.picker.chapterLink", {
                            book: name,
                            chapter,
                          })}
                          aria-current={isCurrent ? "page" : undefined}
                          onClick={() => setOpenTestament(null)}
                          className={`flex h-10 grow basis-10 ${columns === 1 ? "sm:basis-full" : ""} items-center justify-center text-sm hover:bg-accent hover:text-accent-fg ${
                            isCurrent ? "bg-accent text-accent-fg" : "text-fg"
                          }`}
                        >
                          {chapter}
                        </Link>
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </section>
        </div>
      )}
    </nav>
  );
}
