import { useQueryClient } from "@tanstack/react-query";
import { Fragment, useEffect, useId, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useNavigate, useParams } from "react-router";
import { BibleChapterPicker } from "@/components/BibleChapterPicker";
import { Button, buttonClasses } from "@/components/Button";
import { type WordDetailCloseReason, WordDetailPopover } from "@/components/WordDetailPopover";
import { adjacentChapter, bookName, chapterPath, parseChapterParams } from "@/lib/bible/books";
import { chapterWordsQuery, useBibleStructure, useChapterWords } from "@/lib/bible/queries";
import type { BibleStructureBook, ChapterRef, HighlightKind, WordToken } from "@/lib/bible/types";
import { useWordDetail, wordDetailQuery, wordDetailSource } from "@/lib/bible/wordDetail";
import { buildHighlights, phraseWordIds } from "@/lib/bible/words";

const HIGHLIGHT_CLASSES: Record<HighlightKind, string> = {
  same: "bg-mark-same",
  related: "bg-mark-related",
};
/** Words translated together with the open word; an underline so it combines with the hover highlight. */
/** Hover/focus must rest this long on a word before its detail is prefetched. */
const PREFETCH_DELAY_MS = 200;
const PHRASE_CLASSES = "underline decoration-accent decoration-2 underline-offset-4";

function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  return (
    target instanceof HTMLInputElement ||
    target instanceof HTMLTextAreaElement ||
    target instanceof HTMLSelectElement ||
    target.isContentEditable
  );
}

export function ChapterPage() {
  const { t, i18n } = useTranslation();
  const params = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const structure = useBibleStructure();
  const books = structure.data;

  const ref = parseChapterParams(params.bookId, params.chapter, books);
  const verses = useChapterWords(books ? ref : null);
  const [hoveredId, setHoveredId] = useState<number>();
  const [selectedId, setSelectedId] = useState<number>();
  const triggerRef = useRef<HTMLElement | null>(null);
  const popoverId = useId();
  const selectedVerse = verses.data?.find((v) => v.words.some((w) => w.id === selectedId));
  const selectedWord = selectedVerse?.words.find((w) => w.id === selectedId);
  const detail = useWordDetail(selectedWord, selectedVerse?.words);
  const phrase = useMemo(
    () => phraseWordIds(selectedId !== undefined ? detail.data : undefined),
    [selectedId, detail.data],
  );
  const activeId = hoveredId ?? selectedId;
  const highlights = useMemo(() => {
    const data = verses.data ?? [];
    const active = data.flatMap((v) => v.words).find((w) => w.id === activeId);
    return buildHighlights(active, data);
  }, [verses.data, activeId]);

  const prefetchTimer = useRef<ReturnType<typeof setTimeout>>(undefined);
  function startIntent(word: WordToken, verseWords: WordToken[]) {
    setHoveredId(word.id);
    clearTimeout(prefetchTimer.current);
    prefetchTimer.current = setTimeout(() => {
      void queryClient.prefetchQuery(wordDetailQuery(word, verseWords));
    }, PREFETCH_DELAY_MS);
  }
  function endIntent() {
    setHoveredId(undefined);
    clearTimeout(prefetchTimer.current);
  }
  useEffect(() => () => clearTimeout(prefetchTimer.current), []);

  function toggle(word: WordToken, element: HTMLElement) {
    if (selectedId === word.id) {
      setSelectedId(undefined);
      return;
    }
    triggerRef.current = element;
    setSelectedId(word.id);
  }

  function close(reason: WordDetailCloseReason) {
    setSelectedId(undefined);
    if (reason !== "outside") triggerRef.current?.focus();
  }

  const prev = books && ref ? adjacentChapter(books, ref, -1) : null;
  const next = books && ref ? adjacentChapter(books, ref, 1) : null;
  const prevPath = prev ? chapterPath(prev) : null;
  const nextPath = next ? chapterPath(next) : null;

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.defaultPrevented) return;
      if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
      if (isTypingTarget(event.target)) return;
      if (selectedId !== undefined) return;
      if (event.key === "ArrowLeft" && prevPath) navigate(prevPath);
      if (event.key === "ArrowRight" && nextPath) navigate(nextPath);
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [prevPath, nextPath, navigate, selectedId]);

  const bookId = ref?.bookId;
  const chapterNumber = ref?.chapter;
  // biome-ignore lint/correctness/useExhaustiveDependencies: closing on every chapter change is the point
  useEffect(() => {
    setSelectedId(undefined);
  }, [bookId, chapterNumber]);

  useEffect(() => {
    if (verses.isSuccess && next) {
      void queryClient.prefetchQuery(chapterWordsQuery(next.bookId, next.chapter));
    }
  }, [verses.isSuccess, next, queryClient]);

  const hash = location.hash;
  useEffect(() => {
    if (!verses.isSuccess || !hash.startsWith("#v")) return;
    document.getElementById(hash.slice(1))?.scrollIntoView();
  }, [verses.isSuccess, hash]);

  if (structure.isError) {
    return (
      <div role="alert" className="flex flex-wrap items-center gap-4">
        <p>{t("bible.structureError")}</p>
        <Button variant="secondary" onClick={() => structure.refetch()}>
          {t("bible.retry")}
        </Button>
      </div>
    );
  }

  if (!books) {
    return <p className="text-fg-muted">{t("bible.chapter.loading")}</p>;
  }

  if (!ref) {
    return (
      <>
        <BibleChapterPicker books={books} />
        <h1 className="mt-6 font-scripture text-3xl font-semibold">
          {t("bible.chapter.notFound")}
        </h1>
        <Link to="/bible" className="mt-4 inline-block text-accent underline">
          {t("bible.chapter.toOverview")}
        </Link>
      </>
    );
  }

  const book = books.find((b) => b.id === ref.bookId) as BibleStructureBook;
  const language = i18n.language;
  const reference = (r: ChapterRef) => {
    const target = books.find((b) => b.id === r.bookId) as BibleStructureBook;
    return `${bookName(target, language)} ${r.chapter}`;
  };
  const title = `${reference(ref)} – ${t("app.name")}`;

  function renderLink(
    target: ChapterRef | null,
    direction: "previous" | "next",
    withLabel: boolean,
  ) {
    const arrow = direction === "previous" ? "‹" : "›";
    if (!target) {
      return (
        <span
          aria-disabled="true"
          className={`${buttonClasses({ variant: "ghost", size: "sm" })} opacity-50`}
        >
          <span aria-hidden="true">{arrow}</span>
          <span className="sr-only">
            {t(direction === "previous" ? "bible.chapter.noPrevious" : "bible.chapter.noNext")}
          </span>
        </span>
      );
    }
    const label = reference(target);
    return (
      <Link
        to={chapterPath(target)}
        aria-label={t(`bible.chapter.${direction}`, { reference: label })}
        className={buttonClasses({ variant: "ghost", size: "sm" })}
      >
        {direction === "previous"
          ? `${arrow} ${withLabel ? label : ""}`
          : `${withLabel ? label : ""} ${arrow}`}
      </Link>
    );
  }

  return (
    <>
      <title>{title}</title>
      <BibleChapterPicker books={books} currentBookId={book.id} currentChapter={ref.chapter} />
      <header className="mt-6 flex items-center justify-between gap-4">
        {renderLink(prev, "previous", false)}
        <h1 className="text-center font-scripture text-3xl font-semibold md:text-4xl">
          {reference(ref)}
        </h1>
        {renderLink(next, "next", false)}
      </header>
      <article
        lang="de"
        aria-busy={verses.isLoading}
        className="mx-auto mt-6 max-w-prose font-scripture text-lg leading-relaxed md:text-xl 2xl:text-2xl"
      >
        {verses.isLoading && <p className="text-fg-muted">{t("bible.chapter.loading")}</p>}
        {verses.isError && (
          <div role="alert" className="flex flex-wrap items-center gap-4">
            <p>{t("bible.chapter.error")}</p>
            <Button variant="secondary" onClick={() => verses.refetch()}>
              {t("bible.retry")}
            </Button>
          </div>
        )}
        {verses.isSuccess && verses.data.length === 0 && (
          <p className="text-fg-muted">{t("bible.chapter.empty")}</p>
        )}
        {verses.isSuccess &&
          verses.data.map((v) => (
            <p key={v.verse} id={`v${v.verse}`} className="mt-2 scroll-mt-4">
              <sup className="mr-1 text-xs text-fg-muted">{v.verse}</sup>
              {v.words.map((word) => {
                const interactive = word.senses.length > 0;
                const kind = highlights.get(word.id);
                return (
                  <Fragment key={word.id}>
                    <Button
                      variant="inline"
                      data-word-trigger=""
                      aria-haspopup="dialog"
                      aria-expanded={selectedId === word.id}
                      aria-controls={selectedId === word.id ? popoverId : undefined}
                      aria-label={t("bible.wordDetail.trigger", { word: word.text })}
                      onClick={(e) => toggle(word, e.currentTarget)}
                      onMouseEnter={interactive ? () => startIntent(word, v.words) : undefined}
                      onMouseLeave={interactive ? endIntent : undefined}
                      onFocus={interactive ? () => startIntent(word, v.words) : undefined}
                      onBlur={interactive ? endIntent : undefined}
                      className={`${kind ? HIGHLIGHT_CLASSES[kind] : ""} ${phrase.has(word.id) ? PHRASE_CLASSES : ""} ${selectedId === word.id ? "word-anchor" : ""}`}
                    >
                      {word.text}
                    </Button>{" "}
                  </Fragment>
                );
              })}
            </p>
          ))}
      </article>
      {selectedWord && (
        <WordDetailPopover
          id={popoverId}
          word={selectedWord}
          status={detail.status}
          detail={detail.data}
          isSample={wordDetailSource === "mock"}
          onRetry={() => detail.refetch()}
          onClose={close}
        />
      )}
      <footer className="mx-auto mt-8 flex max-w-prose justify-between">
        {renderLink(prev, "previous", true)}
        {renderLink(next, "next", true)}
      </footer>
    </>
  );
}
