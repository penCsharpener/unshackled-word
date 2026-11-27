import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { AudioField } from "@/components/AudioField";
import { FormError } from "@/components/FormError";
import { RankedBarList, type RankedItem } from "@/components/RankedBarList";
import { StatusBadge } from "@/components/StatusBadge";
import { VerseContext } from "@/components/VerseContext";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { useBibleStructure } from "@/lib/bible/queries";
import { formatStamp } from "@/lib/format";
import { getVocabEntry, getVocabRoot, type VocabEntryResponse, vocabLang } from "@/lib/vocab/api";
import { sampleVerseHref, sampleVerseReference } from "@/lib/vocab/verseReference";

const entryKey = (id: number) => ["vocab", "entry", id] as const;

/** Entry detail (screen 2): the lemma, its Strong's keys, the German renderings with their share and the sample verses. */
export function VocabDetailPage() {
  const { t, i18n } = useTranslation();
  const { id } = useParams();
  const entryId = Number(id);
  const validId = Number.isInteger(entryId) && entryId > 0;
  const structure = useBibleStructure();
  const { user } = useAuth();

  const query = useQuery({
    queryKey: entryKey(entryId),
    queryFn: ({ signal }) => getVocabEntry(entryId, signal),
    enabled: validId,
  });

  const rootId = query.data?.entry.root?.id;
  const family = useQuery({
    queryKey: ["vocab", "root", rootId] as const,
    queryFn: ({ signal }) => getVocabRoot(rootId as number, signal),
    enabled: rootId !== undefined,
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

  const others = (family.data?.members ?? []).filter((member) => member.entryId !== entry.id);

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
        <div className="flex items-center gap-3">
          {hasWriteRole(user) ? (
            <Link
              to={`/edit/vocab/${entry.id}`}
              className="rounded-md border border-border px-3 py-1.5 text-sm text-accent underline"
            >
              {t("vocab.detail.edit")}
            </Link>
          ) : null}
          <AudioField src={entry.audioUrl ?? null} readOnly />
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

      {entry.root ? (
        <section className="grid content-start gap-3 rounded-lg border border-border bg-surface p-4">
          <h2 className="text-lg font-semibold">{t("vocab.root.family")}</h2>
          <p
            lang={lang}
            dir={lang === "he" ? "rtl" : undefined}
            className="font-original text-xl"
            data-testid="family-root"
          >
            {entry.root.prefixes ? `${entry.root.prefixes} + ` : ""}
            <Link
              to={`/vocab?track=${entry.track}&root=${entry.root.id}`}
              className={entry.root.isSimple ? "text-fg-muted underline" : "text-accent underline"}
            >
              {entry.root.root}
            </Link>
          </p>
          {family.isError ? <FormError message={t("vocab.root.loadFailed")} /> : null}
          {family.data ? (
            <>
              {others.length === 0 ? (
                <p className="text-sm text-fg-muted">{t("vocab.root.noOthers")}</p>
              ) : (
                <ul className="grid gap-1 text-sm">
                  {others.map((member) => (
                    <li key={member.entryId} className="flex flex-wrap items-baseline gap-x-2">
                      <Link
                        to={`/vocab/${member.entryId}`}
                        lang={lang}
                        dir={lang === "he" ? "rtl" : undefined}
                        className="font-original text-base text-accent underline"
                      >
                        {member.lemma}
                      </Link>
                      {member.meaning ? <span>{member.meaning}</span> : null}
                      <span className="text-fg-muted tabular-nums">#{number(member.rank)}</span>
                    </li>
                  ))}
                </ul>
              )}
              {family.data.memberTotal > family.data.members.length ? (
                <Link
                  to={`/vocab?track=${entry.track}&root=${entry.root.id}`}
                  className="text-sm text-accent underline"
                >
                  {t("vocab.root.showAll", { count: number(family.data.memberTotal) })}
                </Link>
              ) : null}
            </>
          ) : null}
        </section>
      ) : null}

      {data.sampleVerses.length > 0 ? (
        <section className="grid gap-3">
          <h2 className="text-lg font-semibold">{t("vocab.detail.samples")}</h2>
          <ol className="grid gap-3">
            {data.sampleVerses.map((verse) => (
              <li key={`${verse.bibleBookId}-${verse.chapter}-${verse.verse}`}>
                <VerseContext
                  reference={sampleVerseReference(verse, structure.data, i18n.language)}
                  href={sampleVerseHref(verse)}
                  lang={lang}
                  original={verse.original}
                  german={verse.german}
                />
              </li>
            ))}
          </ol>
        </section>
      ) : null}
    </section>
  );
}
