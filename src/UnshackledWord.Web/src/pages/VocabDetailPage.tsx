import { useQuery } from "@tanstack/react-query";
import { Fragment } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { FormError } from "@/components/FormError";
import { RankedBarList, type RankedItem } from "@/components/RankedBarList";
import { StatusBadge } from "@/components/StatusBadge";
import { ApiError } from "@/lib/api";
import { bookAbbreviation } from "@/lib/bible/books";
import { useBibleStructure } from "@/lib/bible/queries";
import { formatStamp } from "@/lib/format";
import {
  getVocabEntry,
  type VocabEntryResponse,
  type VocabSampleWord,
  vocabLang,
} from "@/lib/vocab/api";

const entryKey = (id: number) => ["vocab", "entry", id] as const;

/** Sample verse words; the hit words carry the entry. */
function Words({ words }: { words: VocabSampleWord[] }) {
  return [...words]
    .sort((a, b) => a.position - b.position)
    .map((word, index) => (
      <Fragment key={word.position}>
        {index > 0 ? " " : null}
        {word.isHit ? (
          <mark className="rounded-sm bg-mark-linked px-0.5 text-fg">{word.text}</mark>
        ) : (
          word.text
        )}
      </Fragment>
    ));
}

/** Entry detail (screen 2): the lemma, its Strong's keys, the German renderings with their share and the sample verses. */
export function VocabDetailPage() {
  const { t, i18n } = useTranslation();
  const { id } = useParams();
  const entryId = Number(id);
  const validId = Number.isInteger(entryId) && entryId > 0;
  const structure = useBibleStructure();

  const query = useQuery({
    queryKey: entryKey(entryId),
    queryFn: ({ signal }) => getVocabEntry(entryId, signal),
    enabled: validId,
  });

  const number = (value: number) => value.toLocaleString(i18n.language);

  if (!validId || query.isError) {
    const notFound = !validId || (query.error instanceof ApiError && query.error.status === 404);
    return (
      <section className="mx-auto flex w-full max-w-6xl flex-col gap-4 p-4">
        <Link to="/vocab" className="text-sm text-accent underline">
          {t("vocab.detail.back")}
        </Link>
        <FormError message={notFound ? t("vocab.detail.notFound") : t("vocab.detail.loadFailed")} />
      </section>
    );
  }
  if (!query.data) return null;

  const data: VocabEntryResponse = query.data;
  const { entry } = data;
  const lang = vocabLang(entry.track);
  const testament = t(`vocab.detail.testaments.${entry.track}`);
  const meta = [
    entry.transliteration,
    entry.rank != null ? t("vocab.detail.rank", { rank: entry.rank, testament }) : null,
    t("vocab.detail.occurrences", { count: number(entry.occurrences) }),
  ]
    .filter(Boolean)
    .join(" · ");

  const renderings: RankedItem[] = data.renderings.map((rendering) => ({
    key: rendering.germanKey,
    label: rendering.germanDisplay,
    sub:
      rendering.fromRevisionCount > 0
        ? t("vocab.detail.renderingsSub", { count: number(rendering.fromRevisionCount) })
        : undefined,
    value: rendering.linkCount,
    share: rendering.share / 100,
  }));

  const bookName = (bibleBookId: number) => {
    const book = structure.data?.find((item) => item.id === bibleBookId);
    return book ? bookAbbreviation(book, i18n.language) : String(bibleBookId);
  };

  return (
    <section className="mx-auto flex w-full max-w-6xl flex-col gap-6 p-4">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="grid gap-1">
          <Link to="/vocab" className="text-sm text-accent underline">
            {t("vocab.detail.back")}
          </Link>
          <h1 className="flex flex-wrap items-baseline gap-3">
            <span
              lang={lang}
              dir={lang === "he" ? "rtl" : undefined}
              className="font-original text-4xl"
            >
              {entry.lemma}
            </span>
            {entry.dictionaryForm ? (
              <span
                lang={lang}
                dir={lang === "he" ? "rtl" : undefined}
                className="font-original text-2xl text-fg-muted"
              >
                {entry.dictionaryForm}
              </span>
            ) : null}
          </h1>
          <p className="text-sm text-fg-muted tabular-nums">{meta}</p>
          <div className="flex flex-wrap items-center gap-2 pt-1">
            {entry.reviewed ? null : (
              <StatusBadge tone="warning">{t("vocab.list.unreviewed")}</StatusBadge>
            )}
            {data.strongsKeys.length > 0 ? (
              <span className="text-sm text-fg-muted">{t("vocab.detail.strongs")}</span>
            ) : null}
            {data.strongsKeys.map((key) => (
              <Link
                key={key.strongsKey}
                to={`/bible/strongs/${key.strongsKey}`}
                className="rounded-md border border-border px-2 py-0.5 text-sm text-accent underline"
              >
                {key.strongsKey}
              </Link>
            ))}
          </div>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {data.meanings.length > 0 ? (
          <section className="grid content-start gap-3 rounded-lg border border-border bg-surface p-4">
            <h2 className="text-lg font-semibold">{t("vocab.detail.meanings")}</h2>
            <ol className="grid gap-1 text-sm">
              {data.meanings.map((meaning) => (
                <li key={`${meaning.rank}-${meaning.german}`} className="font-medium">
                  {meaning.german}
                </li>
              ))}
            </ol>
            <p className="text-xs text-fg-muted">{t("vocab.detail.meaningsNote")}</p>
          </section>
        ) : null}
        <div className="grid content-start gap-3 rounded-lg border border-border bg-surface p-4">
          <RankedBarList title={t("vocab.detail.renderings")} items={renderings} />
          <p className="text-xs text-fg-muted">
            {data.refreshedAt
              ? t("vocab.detail.renderingsAsOf", {
                  time: formatStamp(data.refreshedAt, i18n.language),
                })
              : t("vocab.detail.renderingsNever")}
          </p>
        </div>
      </div>

      {data.sampleVerses.length > 0 ? (
        <section className="grid gap-3">
          <h2 className="text-lg font-semibold">{t("vocab.detail.samples")}</h2>
          <ol className="grid gap-3">
            {data.sampleVerses.map((verse) => (
              <li
                key={`${verse.bibleBookId}-${verse.chapter}-${verse.verse}`}
                className="grid gap-1 rounded-md bg-surface-muted p-3"
              >
                <Link
                  to={`/bible/${verse.bibleBookId}/${verse.chapter}#v${verse.verse}`}
                  className="text-sm font-medium text-accent underline"
                >
                  {`${bookName(verse.bibleBookId)} ${verse.chapter},${verse.verse}`}
                </Link>
                <p
                  lang={lang}
                  dir={lang === "he" ? "rtl" : undefined}
                  className="font-original text-lg"
                >
                  <Words words={verse.original} />
                </p>
                <p lang="de" className="font-scripture text-base">
                  <Words words={verse.german} />
                </p>
              </li>
            ))}
          </ol>
        </section>
      ) : null}
    </section>
  );
}
