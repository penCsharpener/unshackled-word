import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import type { VocabEntryItem, VocabLang, VocabRoot } from "@/lib/vocab/api";
import type { VocabFormOrigin } from "@/lib/vocab/editorApi";
import { Button } from "./Button";
import { StatusBadge } from "./StatusBadge";

/** One list row: the API item plus the language of its course track. Editor rows add the editor fields. */
export interface VocabEntryRow extends Omit<VocabEntryItem, "rank" | "meanings"> {
  meanings: string[];
  /** Null for morphemes and retired entries (editor list only). */
  rank: number | null;
  lang: VocabLang;
  /** Editor only: where the study form comes from. */
  formOrigin?: VocabFormOrigin | null;
  /** Editor only: users with a card on the entry. */
  learners?: number;
}

export interface VocabEntryListProps {
  /** `editor` adds the editor columns (form origin, audio and reviewed as yes/no, learners); no play button. */
  mode: "public" | "editor";
  entries: VocabEntryRow[];
  linkTo: (id: number) => string;
  /** Called by the play button, which only renders for entries with audio (public mode). */
  onPlay?: (id: number) => void;
  /**
   * The list filtered by a search text. The root's Strong's link opens the root entry directly when it is on this page,
   * otherwise the list searched for the root (its entry is the first hit); without it, the Strong's page.
   */
  searchTo?: (query: string) => string;
  /** The list filtered to one word family (roadmap 0030.3); with it the root text is a link to the family. */
  rootTo?: (rootId: number) => string;
}

/** Vocabulary table from `md` up, a card list below. Greek and Hebrew lemmas get their language and direction. */
export function VocabEntryList({
  mode,
  entries,
  linkTo,
  onPlay,
  searchTo,
  rootTo,
}: VocabEntryListProps) {
  const { t, i18n } = useTranslation();
  const number = (value: number) => value.toLocaleString(i18n.language);
  const editor = mode === "editor";

  /** `stretch` extends the link over its positioned parent (the table cell), so the whole cell is the click target. */
  const lemma = (entry: VocabEntryRow, size: "text-lg" | "text-xl", stretch = false) => (
    <Link
      to={linkTo(entry.id)}
      lang={entry.lang}
      dir={entry.lang === "he" ? "rtl" : undefined}
      className={`font-original ${size} text-accent underline${stretch ? " after:absolute after:inset-0" : ""}`}
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

  const yesNo = (value: boolean) => (value ? t("vocab.editor.yes") : t("vocab.editor.no"));

  const agentBadge = (entry: VocabEntryRow) =>
    editor && entry.formOrigin === "agent" ? (
      <span className="ml-2">
        <StatusBadge tone="info">{t("vocab.editor.formOrigin.agent")}</StatusBadge>
      </span>
    ) : null;

  const rootHref = (root: VocabRoot, key: string) => {
    const target = entries.find((entry) => entry.strongsKeys.includes(key));
    if (target) return linkTo(target.id);
    return searchTo ? searchTo(root.root) : `/bible/strongs/${encodeURIComponent(key)}`;
  };

  const rootBadge = (root: VocabRoot) =>
    root.origin === "agent" ? (
      <span className="ml-2" title={t("vocab.root.agentTitle")}>
        <StatusBadge tone="info">{t("vocab.root.agent")}</StatusBadge>
      </span>
    ) : null;

  /** The bare root in the original script, a link to its family filter; a simple word (its own root) is dimmed. */
  const rootText = (entry: VocabEntryRow, root: VocabRoot) => {
    const text = (
      <span
        lang={entry.lang}
        dir={entry.lang === "he" ? "rtl" : undefined}
        title={root.isSimple ? t("vocab.root.simple") : undefined}
        data-simple={root.isSimple || undefined}
        className={`font-original${root.isSimple ? " text-fg-muted" : ""}`}
      >
        {root.root}
      </span>
    );
    if (!rootTo) return text;
    return (
      <Link
        to={rootTo(root.id)}
        aria-label={t("vocab.root.openFamily", { root: root.root })}
        className={`underline decoration-dotted underline-offset-2${root.isSimple ? "" : " text-accent"}`}
      >
        {text}
      </Link>
    );
  };

  const rootStrongs = (root: VocabRoot) =>
    root.strongsKey ? (
      <Link
        to={rootHref(root, root.strongsKey)}
        aria-label={t("vocab.root.openRoot", { key: root.strongsKey })}
        className="tabular-nums text-accent underline"
      >
        {root.strongsKey}
      </Link>
    ) : null;

  /** The three root cells (Präfixe, Wurzel, Wurzel-Strong's); empty cells while no root is known. */
  const rootCells = (entry: VocabEntryRow) => {
    const root = entry.root;
    return (
      <>
        <td lang={entry.lang} className="whitespace-nowrap py-2 pr-4 font-original text-base">
          {root?.prefixes ?? null}
        </td>
        <td className="whitespace-nowrap py-2 pr-4 text-base">
          {root ? (
            <>
              {rootText(entry, root)}
              {rootBadge(root)}
            </>
          ) : null}
        </td>
        <td className="py-2 pr-4">{root ? rootStrongs(root) : null}</td>
      </>
    );
  };

  /** Phone width: the three columns as one line under the lemma, `σύν + ἐπί · μαρτυρέω (G3140)`. */
  const rootLine = (entry: VocabEntryRow) => {
    const root = entry.root;
    if (!root) return null;
    return (
      <span data-testid="root-line" className="text-sm text-fg-muted">
        {root.prefixes ? (
          <>
            <span lang={entry.lang} className="font-original text-fg">
              {root.prefixes}
            </span>
            {" · "}
          </>
        ) : null}
        <span className={root.isSimple ? undefined : "text-fg"}>{rootText(entry, root)}</span>
        {root.strongsKey ? <> ({rootStrongs(root)})</> : null}
        {rootBadge(root)}
      </span>
    );
  };

  const rootHeaders = (
    <>
      <th className="py-2 pr-4 font-medium">{t("vocab.root.prefixes")}</th>
      <th className="py-2 pr-4 font-medium">{t("vocab.root.root")}</th>
      <th className="py-2 pr-4 font-medium">{t("vocab.root.strongs")}</th>
    </>
  );

  return (
    <div className="grid gap-2">
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full text-left text-sm">
          <thead className="text-fg-muted">
            <tr className="border-b border-border">
              {editor ? (
                <>
                  <th className="py-2 pr-4 font-medium">{t("vocab.editor.columns.rank")}</th>
                  <th className="py-2 pr-4 font-medium">{t("vocab.editor.columns.lemma")}</th>
                  <th className="py-2 pr-4 font-medium">{t("vocab.editor.columns.form")}</th>
                  {rootHeaders}
                  <th className="py-2 pr-4 font-medium">{t("vocab.editor.columns.meanings")}</th>
                  <th className="py-2 pr-4 font-medium">{t("vocab.editor.columns.audio")}</th>
                  <th className="py-2 pr-4 font-medium">{t("vocab.editor.columns.reviewed")}</th>
                  <th className="py-2 pr-4 text-right font-medium">
                    {t("vocab.editor.columns.learners")}
                  </th>
                </>
              ) : (
                <>
                  <th className="py-2 pr-4 font-medium">{t("vocab.list.columns.rank")}</th>
                  <th className="py-2 pr-4 font-medium">{t("vocab.list.columns.lemma")}</th>
                  <th className="py-2 pr-4 font-medium">{t("vocab.list.columns.form")}</th>
                  {rootHeaders}
                  <th className="py-2 pr-4 font-medium">{t("vocab.list.columns.meanings")}</th>
                  <th className="py-2 pr-4 text-right font-medium">
                    {t("vocab.list.columns.occurrences")}
                  </th>
                  <th className="py-2 font-medium">
                    <span className="sr-only">{t("vocab.list.columns.audio")}</span>
                  </th>
                </>
              )}
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {entries.map((entry) => (
              <tr key={entry.id} className="hover:bg-surface-muted">
                <td className="py-2 pr-4 tabular-nums text-fg-muted">{entry.rank ?? "–"}</td>
                <td className="relative py-2 pr-4">
                  {lemma(entry, "text-lg", true)}
                  <span className="ml-2 text-fg-muted">{entry.transliteration}</span>
                </td>
                <td
                  lang={entry.lang}
                  dir={entry.lang === "he" ? "rtl" : undefined}
                  className="py-2 pr-4 font-original text-base"
                >
                  {entry.dictionaryForm ?? <span className="font-sans text-fg-muted">–</span>}
                  {agentBadge(entry)}
                </td>
                {rootCells(entry)}
                <td className="py-2 pr-4">
                  {entry.meanings.join(", ")}
                  {editor ? null : unreviewed(entry)}
                </td>
                {editor ? (
                  <>
                    <td className="py-2 pr-4">{yesNo(entry.hasAudio)}</td>
                    <td className="py-2 pr-4">{yesNo(entry.reviewed)}</td>
                    <td className="py-2 pr-4 text-right tabular-nums">
                      {number(entry.learners ?? 0)}
                    </td>
                  </>
                ) : (
                  <>
                    <td className="py-2 pr-4 text-right tabular-nums">
                      {number(entry.occurrences)}
                    </td>
                    <td className="py-2">{play(entry)}</td>
                  </>
                )}
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
              {editor ? (
                <span className="text-sm tabular-nums text-fg-muted">
                  {t("vocab.editor.cardRank", { rank: entry.rank ?? "–" })}
                </span>
              ) : (
                <span className="text-sm tabular-nums text-fg-muted">
                  {t("vocab.list.cardStats", {
                    rank: entry.rank,
                    occurrences: number(entry.occurrences),
                  })}
                </span>
              )}
            </div>
            {entry.dictionaryForm ? (
              <span
                lang={entry.lang}
                dir={entry.lang === "he" ? "rtl" : undefined}
                className="font-original text-base text-fg-muted"
              >
                {entry.dictionaryForm}
                {agentBadge(entry)}
              </span>
            ) : null}
            {rootLine(entry)}
            {editor ? (
              <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
                <span>
                  {entry.meanings.join(", ")}
                  {unreviewed(entry)}
                </span>
                <span className="tabular-nums text-fg-muted">
                  {t("vocab.editor.learners", { count: number(entry.learners ?? 0) })}
                </span>
              </div>
            ) : (
              <div className="flex items-center justify-between gap-2 text-sm">
                <span>
                  {entry.meanings.join(", ")}
                  {unreviewed(entry)}
                </span>
                {play(entry)}
              </div>
            )}
          </li>
        ))}
      </ol>
    </div>
  );
}
