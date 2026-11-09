import { useLayoutEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams, useSearchParams } from "react-router";
import { Button, buttonClasses } from "@/components/Button";
import { bookName } from "@/lib/bible/books";
import { useBibleStructure } from "@/lib/bible/queries";
import { parsePage, useStrongsOccurrences } from "@/lib/bible/strongsOccurrences";
import type { StrongsOccurrence, StrongsOccurrenceToken } from "@/lib/bible/types";
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

export function StrongsPage() {
  const { t, i18n } = useTranslation();
  const params = useParams();
  const [searchParams] = useSearchParams();
  const strongs = normalizeStrongs(params.strongs ?? "");
  const page = parsePage(searchParams.get("page"));
  const structure = useBibleStructure();
  const occurrences = useStrongsOccurrences(strongs, page);
  const data = occurrences.data;

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
      <Link to={`?page=${target}`} className={buttonClasses({ variant: "secondary", size: "sm" })}>
        {label}
      </Link>
    );
  }

  return (
    <>
      <title>{`${heading} – ${t("app.name")}`}</title>
      <h1 className="font-scripture text-3xl font-semibold">
        {t("bible.strongs.title", { strongs: heading })}
      </h1>
      {data && (
        <p className="mt-2 text-fg-muted">
          {t("bible.strongs.count", { count: data.totalItems })}
          {" · "}
          {t(`bible.wordDetail.language.${data.language}`)}
        </p>
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
      {data && data.items.length === 0 && (
        <p className="mt-6 text-fg-muted">{t("bible.strongs.empty")}</p>
      )}
      {data && data.items.length > 0 && (
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
          <nav
            aria-label={t("bible.strongs.pagination")}
            className="mt-6 flex items-center justify-between gap-4"
          >
            {pageLink(page - 1, t("bible.strongs.previous"), page > 1)}
            <span className="text-fg-muted">
              {t("bible.strongs.page", { page: data.page, pages: data.totalPages })}
            </span>
            {pageLink(page + 1, t("bible.strongs.next"), page < data.totalPages)}
          </nav>
        </>
      )}
      <p className="mt-8 text-xs text-fg-muted">{t("bible.wordDetail.attribution")}</p>
    </>
  );
}
