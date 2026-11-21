import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import type { VocabEntryItem, VocabLang } from "@/lib/vocab/api";
import { Button } from "./Button";
import { StatusBadge } from "./StatusBadge";

/** One list row: the API item plus the language of its course track. */
export interface VocabEntryRow extends VocabEntryItem {
  lang: VocabLang;
}

export interface VocabEntryListProps {
  /** `editor` adds the editor columns (roadmap 0022.4f); until then both modes render the public table. */
  mode: "public" | "editor";
  entries: VocabEntryRow[];
  linkTo: (id: number) => string;
  /** Called by the play button, which only renders for entries with audio. */
  onPlay?: (id: number) => void;
}

/** Vocabulary table from `md` up, a card list below. Greek and Hebrew lemmas get their language and direction. */
export function VocabEntryList({ entries, linkTo, onPlay }: VocabEntryListProps) {
  const { t, i18n } = useTranslation();
  const number = (value: number) => value.toLocaleString(i18n.language);

  const lemma = (entry: VocabEntryRow, size: "text-lg" | "text-xl") => (
    <Link
      to={linkTo(entry.id)}
      lang={entry.lang}
      dir={entry.lang === "he" ? "rtl" : undefined}
      className={`font-original ${size} text-accent underline`}
    >
      {entry.lemma}
    </Link>
  );

  const unreviewed = (entry: VocabEntryRow) =>
    entry.reviewed ? null : (
      <span className="ml-2">
        <StatusBadge tone="warning">{t("vocab.list.unreviewed")}</StatusBadge>
      </span>
    );

  const play = (entry: VocabEntryRow) =>
    entry.hasAudio ? (
      <Button
        variant="ghost"
        size="sm"
        aria-label={t("vocab.list.play")}
        onClick={() => onPlay?.(entry.id)}
      >
        ▶
      </Button>
    ) : (
      <span className="text-fg-muted">
        <span aria-hidden="true">–</span>
        <span className="sr-only">{t("vocab.list.noAudio")}</span>
      </span>
    );

  return (
    <div className="grid gap-2">
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full text-left text-sm">
          <thead className="text-fg-muted">
            <tr className="border-b border-border">
              <th className="py-2 pr-4 font-medium">{t("vocab.list.columns.rank")}</th>
              <th className="py-2 pr-4 font-medium">{t("vocab.list.columns.lemma")}</th>
              <th className="py-2 pr-4 font-medium">{t("vocab.list.columns.form")}</th>
              <th className="py-2 pr-4 font-medium">{t("vocab.list.columns.meanings")}</th>
              <th className="py-2 pr-4 text-right font-medium">
                {t("vocab.list.columns.occurrences")}
              </th>
              <th className="py-2 font-medium">
                <span className="sr-only">{t("vocab.list.columns.audio")}</span>
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {entries.map((entry) => (
              <tr key={entry.id} className="hover:bg-surface-muted">
                <td className="py-2 pr-4 tabular-nums text-fg-muted">{entry.rank}</td>
                <td className="py-2 pr-4">
                  {lemma(entry, "text-lg")}
                  <span className="ml-2 text-fg-muted">{entry.transliteration}</span>
                </td>
                <td
                  lang={entry.lang}
                  dir={entry.lang === "he" ? "rtl" : undefined}
                  className="py-2 pr-4 font-original text-base"
                >
                  {entry.dictionaryForm ?? <span className="font-sans text-fg-muted">–</span>}
                </td>
                <td className="py-2 pr-4">
                  {entry.meanings.join(", ")}
                  {unreviewed(entry)}
                </td>
                <td className="py-2 pr-4 text-right tabular-nums">{number(entry.occurrences)}</td>
                <td className="py-2">{play(entry)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ol className="grid gap-2 md:hidden">
        {entries.map((entry) => (
          <li key={entry.id} className="grid gap-1 rounded-lg border border-border bg-surface p-3">
            <div className="flex items-baseline justify-between gap-2">
              {lemma(entry, "text-xl")}
              <span className="text-sm tabular-nums text-fg-muted">
                {t("vocab.list.cardStats", {
                  rank: entry.rank,
                  occurrences: number(entry.occurrences),
                })}
              </span>
            </div>
            {entry.dictionaryForm ? (
              <span
                lang={entry.lang}
                dir={entry.lang === "he" ? "rtl" : undefined}
                className="font-original text-base text-fg-muted"
              >
                {entry.dictionaryForm}
              </span>
            ) : null}
            <div className="flex items-center justify-between gap-2 text-sm">
              <span>
                {entry.meanings.join(", ")}
                {unreviewed(entry)}
              </span>
              {play(entry)}
            </div>
          </li>
        ))}
      </ol>
    </div>
  );
}
