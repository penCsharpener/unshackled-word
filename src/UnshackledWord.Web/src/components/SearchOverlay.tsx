import { useQuery } from "@tanstack/react-query";
import { type KeyboardEvent, type ReactNode, useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { ApiError, describeApiError } from "@/lib/api";
import { bookName } from "@/lib/bible/books";
import { structureQuery } from "@/lib/bible/queries";
import {
  isLookupQuery,
  originalTextAttributes,
  useOriginalsLookup,
} from "@/lib/bible/strongsStats";
import type {
  OriginalLanguage,
  OriginalsLookupLemma,
  OriginalsLookupOriginal,
} from "@/lib/bible/types";
import { isVerseSearchQuery, originalsQueryOf, useVerseSearch } from "@/lib/bible/verseSearch";
import { useSubmittedQuery } from "@/lib/search/useSubmittedQuery";
import { useCoarsePointer } from "@/lib/useCoarsePointer";
import { BookBarChart } from "./BookBarChart";
import { Button } from "./Button";
import { Dialog } from "./Dialog";
import { DonutChart, type DonutSlice } from "./DonutChart";
import { EmptyState } from "./EmptyState";
import { TextField } from "./TextField";
import { hitOptionId, hitPath, VerseHitList } from "./VerseHitList";

export interface SearchOverlayProps {
  open: boolean;
  onClose: () => void;
}

type Tab = "verses" | "originals";

function OriginalText({ language, children }: { language: OriginalLanguage; children: ReactNode }) {
  const { lang, dir } = originalTextAttributes(language);
  return (
    <span lang={lang} dir={dir} className="font-original">
      {children}
    </span>
  );
}

function originalSlice(original: OriginalsLookupOriginal): DonutSlice {
  const name = [original.strongs, original.lemma].filter(Boolean).join(" ");
  return {
    key: original.strongs,
    text: original.gloss ? `${name} – ${original.gloss}` : name,
    value: original.count,
    label: (
      <>
        <span className="font-medium">{original.strongs}</span>
        {original.lemma ? (
          <>
            {" "}
            <OriginalText language={original.language}>{original.lemma}</OriginalText>
          </>
        ) : null}
        {original.gloss ? <span className="text-fg-muted"> – {original.gloss}</span> : null}
      </>
    ),
  };
}

/** One German lemma of a query word with its originals; very frequent words wait for a click. */
function LemmaBlock({
  typed,
  lemma,
  onNavigate,
}: {
  typed: string;
  lemma: OriginalsLookupLemma;
  onNavigate: () => void;
}) {
  const { t } = useTranslation();
  const [revealed, setRevealed] = useState(false);
  // Refetches only this word with includeFrequent=true; an empty query keeps the hook idle.
  const full = useOriginalsLookup(revealed ? typed : "", { includeFrequent: true });
  const shown = revealed
    ? (full.data?.tokens
        .flatMap((token) => token.lemmas)
        .find((l) => l.lemma === lemma.lemma && l.pos === lemma.pos) ?? lemma)
    : lemma;

  const same = typed.toLowerCase() === lemma.lemma.toLowerCase();
  const title = `${same ? lemma.lemma : t("bible.stats.donut.resolved", { typed, lemma: lemma.lemma })}${
    lemma.pos ? `, ${t(`bible.pos.${lemma.pos}`, { defaultValue: lemma.pos })}` : ""
  }`;

  if (shown.highFrequency && shown.originals.length === 0) {
    return (
      <EmptyState title={<span lang="de">{title}</span>}>
        <div className="grid justify-items-start gap-2">
          <p>{t("search.originals.frequent")}</p>
          {revealed && full.isError ? <p role="alert">{t("search.originals.error")}</p> : null}
          <Button
            variant="secondary"
            size="sm"
            disabled={revealed && full.isFetching}
            onClick={() => (revealed ? full.refetch() : setRevealed(true))}
          >
            {revealed && full.isFetching
              ? t("search.originals.loading")
              : t("search.originals.showAnyway")}
          </Button>
        </div>
      </EmptyState>
    );
  }
  if (shown.originals.length === 0) return null;
  return (
    <DonutChart
      title={<span lang="de">{title}</span>}
      subtitle={t("search.originals.total", { count: shown.occurrences })}
      slices={shown.originals.map(originalSlice)}
      centerLabel={t("search.originals.centerLabel")}
      sliceHref={(strongs) => `/bible/strongs/${encodeURIComponent(strongs)}`}
      onSliceNavigate={onNavigate}
    />
  );
}

/** The originals column: per query word the Greek / Hebrew words it is mapped to, from the stats snapshot. */
function OriginalsPanel({ query, onNavigate }: { query: string; onNavigate: () => void }) {
  const { t } = useTranslation();
  const words = originalsQueryOf(query);
  const lookup = useOriginalsLookup(words);

  if (words === "" || !isLookupQuery(words)) {
    return <EmptyState>{t("search.originals.prompt")}</EmptyState>;
  }
  if (lookup.isError) {
    if (lookup.error instanceof ApiError && lookup.error.status === 503) {
      return (
        <EmptyState title={t("search.originals.notReady")}>
          {t("search.originals.notReadyHint")}
        </EmptyState>
      );
    }
    return (
      <EmptyState>
        <div role="alert" className="grid justify-items-start gap-2">
          <p>{describeApiError(lookup.error, t("search.originals.error"))}</p>
          <Button variant="secondary" size="sm" onClick={() => lookup.refetch()}>
            {t("search.retry")}
          </Button>
        </div>
      </EmptyState>
    );
  }
  if (!lookup.data) return <EmptyState>{t("search.originals.loading")}</EmptyState>;

  const tokens = lookup.data.tokens.filter((token) => token.lemmas.length > 0);
  if (tokens.length === 0) return <EmptyState>{t("search.originals.none")}</EmptyState>;
  return (
    <div className="grid gap-4">
      {tokens.flatMap((token) =>
        token.lemmas.map((lemma) => (
          <LemmaBlock
            key={`${token.token}|${lemma.lemma}|${lemma.pos ?? ""}`}
            typed={token.token}
            lemma={lemma}
            onNavigate={onNavigate}
          />
        )),
      )}
    </div>
  );
}

/**
 * Search palette over the reader: verse full text search (ranked hits with the matched words marked,
 * hits per book as filter) next to the original words of every query word. Opened from the menu
 * (magnifier or `F`); the typed text and the filter survive closing.
 */
export function SearchOverlay({ open, onClose }: SearchOverlayProps) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const coarse = useCoarsePointer();
  const listId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [input, setInput] = useState("");
  const { query, submit } = useSubmittedQuery(input);
  const [tab, setTab] = useState<Tab>("verses");
  // The book filter belongs to the query it was set for.
  const [filter, setFilter] = useState<{ query: string; bookId: number | null }>({
    query: "",
    bookId: null,
  });
  const bookId = filter.query === query ? filter.bookId : null;
  const [cursor, setCursor] = useState({ key: "", index: -1 });
  const resultKey = `${query}|${bookId ?? 0}`;
  const activeIndex = cursor.key === resultKey ? cursor.index : -1;

  const structure = useQuery({ ...structureQuery(), enabled: open });
  const books = structure.data ?? [];
  const search = useVerseSearch(open ? query : "", bookId);
  const language = i18n.language;

  const pages = search.data?.pages ?? [];
  const first = pages[0];
  const hits = pages.flatMap((page) => page.hits);
  const searching = query !== "";
  const invalid = searching && !isVerseSearchQuery(query);
  const dimmed = search.isPlaceholderData || (search.isFetching && !search.isFetchingNextPage);

  const openHit = (index: number) => {
    const hit = hits[index];
    if (!hit) return;
    navigate(hitPath(hit));
    onClose();
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown" && hits.length > 0) {
      event.preventDefault();
      setCursor({ key: resultKey, index: Math.min(activeIndex + 1, hits.length - 1) });
    } else if (event.key === "ArrowUp" && hits.length > 0) {
      event.preventDefault();
      setCursor({ key: resultKey, index: Math.max(activeIndex - 1, -1) });
    } else if (event.key === "Enter") {
      event.preventDefault();
      if (activeIndex >= 0) openHit(activeIndex);
      else submit();
    }
  };

  const selectedBook = bookId === null ? undefined : books.find((b) => b.id === bookId);
  const showList = searching && !invalid && first !== undefined;

  const versesColumn = (
    <section
      aria-label={t("search.tabs.verses")}
      aria-busy={dimmed || undefined}
      className={`grid min-h-0 content-start gap-3 overflow-y-auto p-4 ${tab === "verses" ? "" : "max-lg:hidden"}`}
    >
      <h3 className="text-sm font-semibold text-fg-muted max-lg:hidden">
        {t("search.tabs.verses")}
      </h3>
      {/* Polite live region: announced after each result */}
      <p role="status" aria-live="polite" className="text-sm text-fg-muted">
        {showList && !dimmed ? t("search.count", { count: first.total }) : ""}
      </p>
      {!searching ? <EmptyState>{t("search.hint")}</EmptyState> : null}
      {invalid ? (
        <p role="alert" className="text-sm text-fg">
          {t("search.tooLong")}
        </p>
      ) : null}
      {searching && !invalid && search.isError ? (
        <div role="alert" className="grid justify-items-start gap-2 text-sm">
          <p>{describeApiError(search.error, t("search.error"))}</p>
          {!(search.error instanceof ApiError && search.error.status === 400) ? (
            <Button variant="secondary" size="sm" onClick={() => search.refetch()}>
              {t("search.retry")}
            </Button>
          ) : null}
        </div>
      ) : null}
      {searching && !invalid && !search.isError && !first ? (
        <EmptyState>{t("search.loading")}</EmptyState>
      ) : null}
      {showList ? (
        <div className={`grid gap-3 ${dimmed ? "opacity-60" : ""}`}>
          {first.tokens
            .filter((token) => token.lemmas && token.lemmas.length > 0)
            .map((token) => (
              <p key={token.text} className="text-sm text-fg-muted" data-testid="widened">
                {t("search.widened", { word: token.text, lemmas: token.lemmas?.join(", ") })}
              </p>
            ))}
          {first.total > 0 && books.length > 0 ? (
            <details className="rounded-md border border-border p-3">
              <summary className="cursor-pointer text-sm font-medium">
                {t("search.books.filterBy")}
              </summary>
              <div className="mt-3">
                <BookBarChart
                  books={books}
                  counts={first.bookCounts.map((c) => ({ bibleBookId: c.bookId, count: c.count }))}
                  testaments={["ot", "nt"]}
                  title={t("search.books.title")}
                  summary={t("search.books.summary", {
                    // The counts ignore the book filter, so their sum is the total of the whole query.
                    count: first.bookCounts.reduce((sum, c) => sum + c.count, 0),
                    books: first.bookCounts.length,
                    total: books.length,
                  })}
                  selectedBookId={bookId}
                  onSelectBook={(id) => setFilter({ query, bookId: id })}
                />
              </div>
            </details>
          ) : null}
          {selectedBook ? (
            <div>
              <Button
                variant="secondary"
                size="sm"
                aria-label={t("search.filter.remove", { book: bookName(selectedBook, language) })}
                onClick={() => setFilter({ query, bookId: null })}
              >
                {t("search.filter.book", { book: bookName(selectedBook, language) })}
                <span aria-hidden="true">✕</span>
              </Button>
            </div>
          ) : null}
          {first.total === 0 ? (
            <EmptyState>{t("search.empty", { query })}</EmptyState>
          ) : (
            <>
              <VerseHitList
                id={listId}
                label={t("search.results")}
                hits={hits}
                books={books}
                language={language}
                activeIndex={activeIndex}
                onSelect={(hit) => openHit(hits.indexOf(hit))}
                onLoadMore={() => search.fetchNextPage()}
                hasMore={search.hasNextPage}
                loading={search.isFetchingNextPage}
              />
              {search.hasNextPage ? (
                <p className="text-xs text-fg-muted">
                  {t("search.shown", { shown: hits.length, total: first.total })}
                </p>
              ) : null}
            </>
          )}
        </div>
      ) : null}
    </section>
  );

  const originalsColumn = (
    <section
      aria-label={t("search.tabs.originals")}
      className={`grid min-h-0 content-start gap-3 overflow-y-auto p-4 ${tab === "originals" ? "" : "max-lg:hidden"}`}
    >
      <h3 className="text-sm font-semibold text-fg-muted max-lg:hidden">
        {t("search.originals.heading")}
      </h3>
      <OriginalsPanel query={query} onNavigate={onClose} />
    </section>
  );

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t("search.title")}
      titleHidden
      size="lg"
      placement="top"
      bare
      // On touch devices the keyboard would cover a restored result, so only an empty search focuses the field.
      initialFocusRef={coarse && input !== "" ? undefined : inputRef}
    >
      <div className="grid gap-2 border-b border-border px-4 pt-2 pb-3">
        <TextField
          type="search"
          label={t("search.label")}
          labelHidden
          required={false}
          value={input}
          onChange={setInput}
          inputRef={inputRef}
          placeholder={t("search.placeholder")}
          autoComplete="off"
          spellCheck={false}
          enterKeyHint="search"
          role="combobox"
          aria-autocomplete="list"
          aria-expanded={hits.length > 0}
          aria-controls={hits.length > 0 ? listId : undefined}
          aria-activedescendant={activeIndex >= 0 ? hitOptionId(listId, activeIndex) : undefined}
          onKeyDown={onKeyDown}
        />
        <fieldset className="flex gap-2 lg:hidden">
          <legend className="sr-only">{t("search.tabs.label")}</legend>
          {(["verses", "originals"] as const).map((id) => (
            <Button
              key={id}
              variant="secondary"
              size="sm"
              aria-pressed={tab === id}
              onClick={() => setTab(id)}
            >
              {t(`search.tabs.${id}`)}
            </Button>
          ))}
        </fieldset>
      </div>
      <div className="grid min-h-0 flex-1 grid-rows-1 lg:grid-cols-2 lg:divide-x lg:divide-border">
        {versesColumn}
        {originalsColumn}
      </div>
    </Dialog>
  );
}
