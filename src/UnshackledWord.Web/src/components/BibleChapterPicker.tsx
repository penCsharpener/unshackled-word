import { type KeyboardEvent, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { bookName, buildPickerGroups, chapterPath } from "@/lib/bible/books";
import type { BibleStructureBook, PickerGroup, Testament } from "@/lib/bible/types";
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
}

function tabId(testament: Testament): string {
  return `bible-picker-tab-${testament}`;
}

export function BibleChapterPicker({
  books,
  currentBookId,
  currentChapter,
  defaultTestament,
}: BibleChapterPickerProps) {
  const { t, i18n } = useTranslation();
  const [openTestament, setOpenTestament] = useState<Testament | null>(defaultTestament ?? null);
  const [expandedKey, setExpandedKey] = useState<string | null>(null);
  const [expandTouched, setExpandTouched] = useState(false);

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
    <nav aria-label={t("bible.picker.label")} onKeyDown={handleKeyDown}>
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
        <section
          id={PANEL_ID}
          aria-label={t(`bible.testament.${openTestament}`)}
          className="mt-2 grid grid-cols-1 gap-2 bg-surface p-2 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-5 xl:grid-cols-7 2xl:grid-cols-8"
        >
          {groups.map((group) => {
            const expanded = activeExpandedKey === group.key;
            const bookTitle = group.title;
            const book = books?.find((candidate) => candidate.id === group.bookId);
            const name = book ? bookName(book, i18n.language) : bookTitle;
            return (
              <div key={group.key}>
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
                  <span className="hidden px-3 py-1 sm:block">{bookTitle}</span>
                </h3>
                <div className={expanded ? "flex flex-wrap" : "hidden flex-wrap sm:flex"}>
                  {group.chapters.map((chapter) => {
                    const isCurrent = group.bookId === currentBookId && chapter === currentChapter;
                    return (
                      <Link
                        key={chapter}
                        to={chapterPath({ bookId: group.bookId, chapter })}
                        aria-label={t("bible.picker.chapterLink", {
                          book: name,
                          chapter,
                        })}
                        aria-current={isCurrent ? "page" : undefined}
                        onClick={() => setOpenTestament(null)}
                        className={`flex h-10 grow basis-10 items-center justify-center text-sm hover:bg-accent hover:text-accent-fg ${
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
      )}
    </nav>
  );
}
