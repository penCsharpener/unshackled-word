import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { bookName } from "@/lib/bible/books";
import type { BibleStructureBook } from "@/lib/bible/types";
import { formatNumber } from "@/lib/chart";
import { splitContext } from "@/lib/quality/checks";
import type { Finding } from "@/lib/quality/types";
import { PAGE_SIZES } from "@/lib/quality/url";
import { Button, buttonClasses } from "./Button";
import { FormError } from "./FormError";
import { Select } from "./Select";

export type FindingColumn = "ref" | "german" | "original" | "stts" | "pos" | "detail" | "severity";

export interface FindingJump {
  key: string;
  label: string;
  /** Opens in a new tab. */
  href?: string;
  /** Same-page action (e.g. explorer filter). */
  onClick?: () => void;
}

export interface FindingsTableProps {
  findings: Finding[];
  total: number;
  page: number;
  pageSize: number;
  onPage: (page: number) => void;
  onPageSize?: (size: number) => void;
  columns?: FindingColumn[];
  books: BibleStructureBook[];
  jumps: (finding: Finding) => FindingJump[];
  loading?: boolean;
  error?: boolean;
  onRetry?: () => void;
  emptyText?: ReactNode;
}

const ALL_COLUMNS: FindingColumn[] = ["ref", "german", "original", "detail", "severity"];
const SEVERITY_ICON = { error: "✕", warning: "▲", info: "ℹ" } as const;
const SEVERITY_BORDER = {
  error: "border-sev-error",
  warning: "border-sev-warning",
  info: "border-sev-info",
} as const;

function refLabel(finding: Finding, books: BibleStructureBook[], language: string) {
  const book = books.find((b) => b.id === finding.bibleBookId);
  const name = book ? bookName(book, language) : String(finding.bibleBookId);
  return `${name} ${finding.chapter},${finding.verse}`;
}

function Word({ finding }: { finding: Finding }) {
  const word = finding.german ?? finding.lemma ?? "";
  const parts = splitContext(finding.context, word);
  return (
    <span lang="de" className="font-scripture">
      {parts?.left ? <span className="text-fg-muted">{parts.left}</span> : null}
      {word ? <mark className="rounded-sm bg-mark-same text-fg">{word}</mark> : null}
      {parts?.right ? <span className="text-fg-muted">{parts.right}</span> : null}
    </span>
  );
}

function Original({ finding }: { finding: Finding }) {
  if (!finding.original && !finding.strongs) return <span className="text-fg-muted">–</span>;
  const lang = finding.lang === "H" ? "he" : "grc";
  return (
    <span className="flex flex-wrap items-baseline gap-x-2">
      {finding.original ? (
        <span lang={lang} dir={lang === "he" ? "rtl" : undefined} className="font-original text-lg">
          {finding.original}
        </span>
      ) : null}
      {finding.strongs ? <span className="text-sm text-fg-muted">{finding.strongs}</span> : null}
      {finding.gloss ? (
        <span lang="en" className="text-sm text-fg-muted">
          {finding.gloss}
        </span>
      ) : null}
    </span>
  );
}

function Severity({ finding, label }: { finding: Finding; label: string }) {
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-sm border-l-4 px-1 text-sm ${SEVERITY_BORDER[finding.severity]}`}
    >
      <span aria-hidden="true">{SEVERITY_ICON[finding.severity]}</span>
      {label}
    </span>
  );
}

/** Findings of one check (or explorer verse hits): table from 42rem, cards below; paged by the server. */
export function FindingsTable({
  findings,
  total,
  page,
  pageSize,
  onPage,
  onPageSize,
  columns = ALL_COLUMNS,
  books,
  jumps,
  loading = false,
  error = false,
  onRetry,
  emptyText,
}: FindingsTableProps) {
  const { t, i18n } = useTranslation();
  const language = i18n.language;
  const has = (column: FindingColumn) => columns.includes(column);
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);
  const lastPage = Math.max(1, Math.ceil(total / pageSize));

  const jumpButtons = (finding: Finding) => {
    const items = jumps(finding);
    if (items.length === 0) return null;
    return (
      <span className="flex flex-wrap gap-1">
        {items.map((jump) =>
          jump.href ? (
            <a
              key={jump.key}
              href={jump.href}
              target="_blank"
              rel="noopener noreferrer"
              className={buttonClasses({ variant: "secondary", size: "sm" })}
            >
              {jump.label}
              <span className="sr-only"> {t("quality.findings.newTab")}</span>
            </a>
          ) : (
            <Button key={jump.key} size="sm" variant="secondary" onClick={jump.onClick}>
              {jump.label}
            </Button>
          ),
        )}
      </span>
    );
  };

  const detail = (finding: Finding) => <span>{finding.detail}</span>;

  if (error) {
    return (
      <div className="grid gap-2">
        <FormError message={t("quality.states.error")} />
        {onRetry ? (
          <Button size="sm" className="justify-self-start" onClick={onRetry}>
            {t("quality.states.retry")}
          </Button>
        ) : null}
      </div>
    );
  }

  if (!loading && findings.length === 0) {
    return <p className="text-sm text-fg-muted">{emptyText ?? t("quality.states.emptyCheck")}</p>;
  }

  const pager = (
    <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
      <span aria-live="polite">
        {t("quality.findings.showing", {
          from: formatNumber(from, language),
          to: formatNumber(to, language),
          total: formatNumber(total, language),
        })}
      </span>
      <span className="flex flex-wrap items-center gap-2">
        {onPageSize ? (
          <Select
            label={t("quality.findings.pageSize")}
            value={String(pageSize)}
            onChange={(value) => onPageSize(Number(value))}
            options={PAGE_SIZES.map((size) => ({ value: String(size), label: String(size) }))}
          />
        ) : null}
        <Button size="sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>
          {t("quality.findings.prev")}
        </Button>
        <Button size="sm" disabled={page >= lastPage} onClick={() => onPage(page + 1)}>
          {t("quality.findings.next")}
        </Button>
      </span>
    </div>
  );

  return (
    <div className="grid gap-3" aria-busy={loading || undefined}>
      {loading && findings.length === 0 ? (
        <p className="text-sm text-fg-muted">{t("quality.states.loading")}</p>
      ) : null}
      <table className="hidden w-full text-left text-sm @2xl:table">
        <thead>
          <tr className="border-b border-border text-fg-muted">
            {has("ref") ? (
              <th className="py-2 pr-3 font-medium">{t("quality.findings.ref")}</th>
            ) : null}
            {has("german") ? (
              <th className="py-2 pr-3 font-medium">{t("quality.findings.german")}</th>
            ) : null}
            {has("original") ? (
              <th className="py-2 pr-3 font-medium">{t("quality.findings.original")}</th>
            ) : null}
            {has("stts") ? (
              <th className="py-2 pr-3 font-medium">{t("quality.findings.stts")}</th>
            ) : null}
            {has("pos") ? (
              <th className="py-2 pr-3 font-medium">{t("quality.findings.pos")}</th>
            ) : null}
            {has("detail") ? (
              <th className="py-2 pr-3 font-medium">{t("quality.findings.detail")}</th>
            ) : null}
            {has("severity") ? (
              <th className="py-2 pr-3 font-medium">{t("quality.findings.severity")}</th>
            ) : null}
            <th className="py-2 font-medium">
              <span className="sr-only">{t("quality.findings.open")}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {findings.map((finding, index) => (
            <tr
              // biome-ignore lint/suspicious/noArrayIndexKey: findings have no id; the page is replaced as a whole
              key={`${finding.check}-${finding.elbWordId ?? index}-${finding.stepWordId ?? ""}-${index}`}
              className="border-b border-border align-top"
            >
              {has("ref") ? (
                <td className="py-2 pr-3 whitespace-nowrap">
                  {refLabel(finding, books, language)}
                </td>
              ) : null}
              {has("german") ? (
                <td className="py-2 pr-3">
                  <Word finding={finding} />
                </td>
              ) : null}
              {has("original") ? (
                <td className="py-2 pr-3">
                  <Original finding={finding} />
                </td>
              ) : null}
              {has("stts") ? <td className="py-2 pr-3">{finding.stts ?? "–"}</td> : null}
              {has("pos") ? <td className="py-2 pr-3">{finding.pos ?? "–"}</td> : null}
              {has("detail") ? <td className="py-2 pr-3">{detail(finding)}</td> : null}
              {has("severity") ? (
                <td className="py-2 pr-3">
                  <Severity finding={finding} label={t(`quality.severity.${finding.severity}`)} />
                </td>
              ) : null}
              <td className="py-2">{jumpButtons(finding)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <ul className="grid gap-2 @2xl:hidden">
        {findings.map((finding, index) => (
          <li
            // biome-ignore lint/suspicious/noArrayIndexKey: findings have no id; the page is replaced as a whole
            key={`${finding.check}-${finding.elbWordId ?? index}-${finding.stepWordId ?? ""}-${index}`}
            className="grid gap-1 rounded-md border border-border bg-surface p-3 text-sm"
          >
            <div className="flex items-baseline justify-between gap-2">
              <span className="font-medium">{refLabel(finding, books, language)}</span>
              {has("severity") ? (
                <Severity finding={finding} label={t(`quality.severity.${finding.severity}`)} />
              ) : null}
            </div>
            {has("german") ? <Word finding={finding} /> : null}
            {has("original") ? <Original finding={finding} /> : null}
            {has("stts") || has("pos") ? (
              <span className="text-fg-muted">
                {t("quality.findings.stts")}: {finding.stts ?? "–"} · {t("quality.findings.pos")}:{" "}
                {finding.pos ?? "–"}
              </span>
            ) : null}
            {has("detail") ? <span className="text-fg-muted">{detail(finding)}</span> : null}
            {jumpButtons(finding)}
          </li>
        ))}
      </ul>
      {pager}
    </div>
  );
}
