import { useLayoutEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams, useSearchParams } from "react-router";
import { Button, buttonClasses } from "@/components/Button";
import { StrongsStatsHeader } from "@/components/StrongsStatsHeader";
import { bookName } from "@/lib/bible/books";
import { useBibleStructure } from "@/lib/bible/queries";
import { writeSidePanelMode } from "@/lib/bible/sidePanel";
import {
  parseBookFilter,
  parseCorpus,
  parsePage,
  parseRenderingFilter,
  useLxxStrongsOccurrences,
  useStrongsOccurrences,
} from "@/lib/bible/strongsOccurrences";
import type {
  BibleStructureBook,
  LxxStrongsOccurrence,
  StrongsOccurrence,
  StrongsOccurrenceToken,
} from "@/lib/bible/types";
import { normalizeStrongs } from "@/lib/bible/wordDetail";

const words = (tokens: StrongsOccurrenceToken[]) => tokens.map((token) => token.text).join(" ");

/**
 * The German context on one side of the keyword. On phones the CSS cuts it to a few lines; this marks
 * it `data-clipped` only when text really is cut, so short context gets no fade.
 */
function KwicContext({
  side,
  tokens,
}: {
  side: "before" | "after";
  tokens: StrongsOccurrenceToken[];
}) {
  const box = useRef<HTMLDivElement>(null);
  const text = useRef<HTMLParagraphElement>(null);

  // biome-ignore lint/correctness/useExhaustiveDependencies: re-measure when the text changes
  useLayoutEffect(() => {
    const outer = box.current;
    const inner = text.current;
    if (!outer || !inner) return;
    const measure = () => {
      outer.toggleAttribute("data-clipped", inner.offsetHeight > outer.clientHeight + 1);
    };
    measure();
    if (typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver(measure);
    observer.observe(outer);
    return () => observer.disconnect();
  }, [tokens]);

  if (tokens.length === 0) return <div className={`kwic-${side}`} />;
  return (
    <div ref={box} className={`kwic-${side}`}>
      <p ref={text} lang="de" className="kwic-text">
        {words(tokens)}
      </p>
    </div>
  );
}

/**
 * The original words left or right of the row's word, above the German context. The sides are visual:
 * for Hebrew (right-to-left) the words that come later in the text stand on the left.
 */
function OriginalContext({
  side,
  tokens,
  lang,
  dir,
}: {
  side: "left" | "right";
  tokens: StrongsOccurrenceToken[];
  lang: string;
  dir: string;
}) {
  if (tokens.length === 0) return <div className={`kwic-orig-${side}`} />;
  return (
    <p lang={lang} dir={dir} className={`kwic-orig-${side} font-original text-lg text-fg-muted`}>
      {words(tokens)}
    </p>
  );
}

function KwicRow({
  item,
  reference,
  lang,
  dir,
}: {
  item: StrongsOccurrence;
  reference: string;
  lang: string;
  dir: string;
}) {
  const verse = item.originalVerse ?? [];
  const at = verse.findIndex((token) => token.matched);
  const earlier = at < 0 ? [] : verse.slice(0, at);
  const later = at < 0 ? [] : verse.slice(at + 1);
  return (
    <li className="kwic-row border-b border-border py-2 hover:bg-surface-muted">
      <div className="kwic-left">
        <Link
          to={`/bible/${item.bibleBookId}/${item.chapter}#v${item.verse}`}
          className="kwic-ref font-sans text-sm font-medium text-accent underline"
        >
          {reference}
        </Link>
        <KwicContext side="before" tokens={item.before} />
      </div>
      <OriginalContext side="left" tokens={dir === "rtl" ? later : earlier} lang={lang} dir={dir} />
      <OriginalContext
        side="right"
        tokens={dir === "rtl" ? earlier : later}
        lang={lang}
        dir={dir}
      />
      {item.original && (
        <span lang={lang} dir={dir} className="kwic-original font-original text-lg text-accent">
          {item.original}
        </span>
      )}
      <span lang="de" className="kwic-key font-semibold">
        {item.keyword.length === 0 ? (
          <span aria-hidden="true" className="font-normal text-fg-muted">
            …
          </span>
        ) : (
          item.keyword.map((token, index) => (
            // biome-ignore lint/suspicious/noArrayIndexKey: tokens are positional and can repeat
            <span key={index}>
              {index > 0 && " "}
              {token.matched ? (
                <mark className="rounded-sm bg-mark-same px-1 text-fg">{token.text}</mark>
              ) : (
                <span className="font-normal text-fg-muted">{token.text}</span>
              )}
            </span>
          ))
        )}
        {item.keywordSuffix && <span className="kwic-punct font-normal">{item.keywordSuffix}</span>}
      </span>
      <KwicContext side="after" tokens={item.after} />
    </li>
  );
}

/**
 * One confirmed LXX occurrence (roadmap 0028.9f): Rahlfs label, the Greek verse with the tagged word marked and the Elb1871 verse it maps to.
 * The link opens the reader at that Elb verse with the LXX side panel (the reader reads the stored panel mode).
 */
function LxxRow({
  item,
  books,
  language,
}: {
  item: LxxStrongsOccurrence;
  books: BibleStructureBook[] | undefined;
  language: string;
}) {
  const { t } = useTranslation();
  const elb = item.elb;
  const elbBook = elb ? books?.find((b) => b.id === elb.bibleBookId) : undefined;
  const elbReference = elb
    ? `${elbBook ? bookName(elbBook, language) : elb.bibleBookId} ${elb.chapter},${elb.verse}`
    : "";
  const label = (
    <span className="font-sans text-sm font-medium">
      {item.ref}
      {item.textForm !== "" && item.textForm !== "default" && ` (${item.textForm})`}
    </span>
  );
  return (
    <li className="border-b border-border py-3 hover:bg-surface-muted">
      {elb ? (
        <Link
          to={`/bible/${elb.bibleBookId}/${elb.chapter}#v${elb.verse}`}
          onClick={() => writeSidePanelMode("lxx")}
          aria-label={t("bible.strongs.lxx.open", { reference: item.ref })}
          className="text-accent underline"
        >
          {label}
        </Link>
      ) : (
        <span className="text-fg-muted">{label}</span>
      )}
      <p lang="el" className="mt-1 font-original text-lg">
        {item.words.map((token, index) => (
          // biome-ignore lint/suspicious/noArrayIndexKey: tokens are positional and can repeat
          <span key={index}>
            {index > 0 && " "}
            {token.matched ? (
              <mark className="rounded-sm bg-mark-linked px-1 text-fg">{token.text}</mark>
            ) : (
              token.text
            )}
          </span>
        ))}
      </p>
      <p className="mt-1 text-sm text-fg-muted">
        {elb ? (
          <>
            <span className="font-sans">
              {elb.elbIsAnchor
                ? t("bible.strongs.lxx.elbAnchor", { reference: elbReference })
                : t("bible.strongs.lxx.elb", { reference: elbReference })}
            </span>
            {!elb.elbIsAnchor && elb.text && (
              <>
                {" "}
                <span lang="de" className="font-scripture">
                  {elb.text}
                </span>
              </>
            )}
          </>
        ) : (
          <span className="font-sans">{t("bible.strongs.lxx.noElb")}</span>
        )}
      </p>
    </li>
  );
}

export function StrongsPage() {
  const { t, i18n } = useTranslation();
  const params = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const strongs = normalizeStrongs(params.strongs ?? "");
  const page = parsePage(searchParams.get("page"));
  const book = parseBookFilter(searchParams.get("book"));
  const rendering = parseRenderingFilter(searchParams.get("rendering"));
  const structure = useBibleStructure();
  // Only a Greek number has an LXX list; the corpus switch (roadmap 0028.9f) shows NT and LXX for it and nothing for a Hebrew one.
  const greek = strongs?.startsWith("G") ?? false;
  const lxx = greek && parseCorpus(searchParams.get("corpus")) === "lxx";
  const ntOccurrences = useStrongsOccurrences(lxx ? undefined : strongs, page, { book, rendering });
  const lxxOccurrences = useLxxStrongsOccurrences(lxx ? strongs : undefined, page);
  const occurrences = lxx ? lxxOccurrences : ntOccurrences;
  const data = occurrences.data;

  function setCorpus(next: "nt" | "lxx") {
    setSearchParams((previous) => {
      const params = new URLSearchParams(previous);
      params.delete("page");
      if (next === "lxx") params.set("corpus", "lxx");
      else params.delete("corpus");
      return params;
    });
  }

  /** Sets or clears one filter; a changed filter always starts again at page 1. */
  function setFilter(name: "book" | "rendering", value: string | null) {
    setSearchParams((previous) => {
      const next = new URLSearchParams(previous);
      next.delete("page");
      if (value === null) next.delete(name);
      else next.set(name, value);
      return next;
    });
  }

  if (!strongs) {
    return (
      <>
        <h1 className="font-scripture text-3xl font-semibold">{t("bible.strongs.notFound")}</h1>
        <Link to="/bible" className="mt-4 inline-block text-accent underline">
          {t("bible.chapter.toOverview")}
        </Link>
      </>
    );
  }

  const heading = data?.strongs ?? strongs;
  const lang = data?.language === "hebrew" ? "he" : "el";
  const dir = data?.language === "hebrew" ? "rtl" : "ltr";
  const bookEntry = book === undefined ? undefined : structure.data?.find((b) => b.id === book);

  function pageHref(target: number) {
    const next = new URLSearchParams(searchParams);
    next.set("page", String(target));
    return `?${next}`;
  }

  function pageLink(target: number, label: string, enabled: boolean) {
    if (!enabled) {
      return (
        <span
          aria-disabled="true"
          className={`${buttonClasses({ variant: "secondary", size: "sm" })} opacity-50`}
        >
          {label}
        </span>
      );
    }
    return (
      <Link to={pageHref(target)} className={buttonClasses({ variant: "secondary", size: "sm" })}>
        {label}
      </Link>
    );
  }

  function pager(current: number, pages: number) {
    return (
      <nav
        aria-label={t("bible.strongs.pagination")}
        className="mt-6 flex items-center justify-between gap-4"
      >
        {pageLink(current - 1, t("bible.strongs.previous"), current > 1)}
        <span className="text-fg-muted">{t("bible.strongs.page", { page: current, pages })}</span>
        {pageLink(current + 1, t("bible.strongs.next"), current < pages)}
      </nav>
    );
  }

  return (
    <>
      <title>{`${heading} – ${t("app.name")}`}</title>
      <StrongsStatsHeader
        strongs={strongs}
        bookId={book}
        rendering={rendering}
        onSelectBook={(bookId) => setFilter("book", bookId === null ? null : String(bookId))}
        onSelectRendering={(key) => setFilter("rendering", key)}
      />
      <h2 className="mt-12 font-scripture text-2xl font-semibold">
        {t("bible.strongs.allOccurrences")}
      </h2>
      {greek && (
        <fieldset className="mt-3 flex flex-wrap items-center gap-2">
          <legend className="sr-only">{t("bible.strongs.corpus.label")}</legend>
          <Button
            variant={lxx ? "secondary" : "primary"}
            size="sm"
            aria-pressed={!lxx}
            onClick={() => setCorpus("nt")}
          >
            {t("bible.strongs.corpus.nt")}
          </Button>
          <Button
            variant={lxx ? "primary" : "secondary"}
            size="sm"
            aria-pressed={lxx}
            onClick={() => setCorpus("lxx")}
          >
            {t("bible.strongs.corpus.lxx")}
          </Button>
        </fieldset>
      )}
      {!lxx && (book !== undefined || rendering !== undefined) && (
        <fieldset className="mt-3 flex flex-wrap items-center gap-2">
          <legend className="sr-only">{t("bible.strongs.filter.label")}</legend>
          {book !== undefined && (
            <Button
              variant="secondary"
              size="sm"
              aria-label={t("bible.strongs.filter.remove", {
                name: bookEntry ? bookName(bookEntry, i18n.language) : book,
              })}
              onClick={() => setFilter("book", null)}
            >
              {t("bible.strongs.filter.book", {
                name: bookEntry ? bookName(bookEntry, i18n.language) : book,
              })}
              <span aria-hidden="true">×</span>
            </Button>
          )}
          {rendering !== undefined && (
            <Button
              variant="secondary"
              size="sm"
              aria-label={t("bible.strongs.filter.remove", { name: rendering })}
              onClick={() => setFilter("rendering", null)}
            >
              {t("bible.strongs.filter.rendering", { word: rendering })}
              <span aria-hidden="true">×</span>
            </Button>
          )}
          {book !== undefined && rendering !== undefined && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() =>
                setSearchParams((previous) => {
                  const next = new URLSearchParams(previous);
                  next.delete("page");
                  next.delete("book");
                  next.delete("rendering");
                  return next;
                })
              }
            >
              {t("bible.strongs.filter.clearAll")}
            </Button>
          )}
        </fieldset>
      )}
      {data && (
        <p className="mt-2 text-fg-muted">{t("bible.strongs.count", { count: data.totalItems })}</p>
      )}
      {occurrences.isLoading && <p className="mt-6 text-fg-muted">{t("bible.strongs.loading")}</p>}
      {occurrences.isError && (
        <div role="alert" className="mt-6 flex flex-wrap items-center gap-4">
          <p>{t("bible.strongs.error")}</p>
          <Button variant="secondary" onClick={() => occurrences.refetch()}>
            {t("bible.retry")}
          </Button>
        </div>
      )}
      {lxx && data && (data.unconfirmedCount ?? 0) > 0 && (
        <p className="mt-2 text-sm text-fg-muted">
          {t("bible.strongs.lxx.unconfirmed", { count: data.unconfirmedCount })}
        </p>
      )}
      {lxx && data && data.totalItems === 0 && (
        <p className="mt-6 text-fg-muted">{t("bible.strongs.lxx.empty")}</p>
      )}
      {lxx && data && (data.lxxItems?.length ?? 0) > 0 && (
        <>
          <ol
            aria-label={t("bible.strongs.lxx.list")}
            aria-busy={occurrences.isPlaceholderData}
            className="mt-6 font-scripture"
          >
            {data.lxxItems?.map((item) => (
              <LxxRow
                key={item.lxxWordId}
                item={item}
                books={structure.data}
                language={i18n.language}
              />
            ))}
          </ol>
          {pager(data.page, data.totalPages)}
        </>
      )}
      {!lxx && data && data.items.length === 0 && (
        <p className="mt-6 text-fg-muted">{t("bible.strongs.empty")}</p>
      )}
      {!lxx && data && data.items.length > 0 && (
        <>
          <ol
            aria-label={t("bible.strongs.list")}
            aria-busy={occurrences.isPlaceholderData}
            className="kwic mt-6 font-scripture"
          >
            {data.items.map((item, index) => {
              const book = structure.data?.find((b) => b.id === item.bibleBookId);
              const reference = `${book ? bookName(book, i18n.language) : item.bibleBookId} ${item.chapter},${item.verse}`;
              return (
                <KwicRow
                  // biome-ignore lint/suspicious/noArrayIndexKey: a verse can hold the same Strong's number twice
                  key={`${item.bibleBookId}-${item.chapter}-${item.verse}-${index}`}
                  item={item}
                  reference={reference}
                  lang={lang}
                  dir={dir}
                />
              );
            })}
          </ol>
          {pager(data.page, data.totalPages)}
        </>
      )}
      <p className="mt-8 text-xs text-fg-muted">{t("bible.wordDetail.attribution")}</p>
    </>
  );
}
