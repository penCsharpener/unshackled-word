import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { buttonClasses } from "./Button";
import { ProgressBar } from "./ProgressBar";

export interface CourseCardProps {
  title: string;
  subtitle: string;
  /** Signed-in learners get the start link; anonymous visitors get the word list instead. */
  signedIn: boolean;
  /** Learner numbers of the course; shown only when given (the API has no per-user numbers yet). */
  stats?: { known: number; due: number; new: number; coverage: number };
  /** Name of the text the coverage refers to, e.g. "NT-Textes". */
  coverageText: string;
  /** Study session route, e.g. `/learn/nt-greek`. */
  to: string;
  /** Word list route, e.g. `/vocab?track=G`. */
  listTo: string;
}

/** One course on `/learn` (roadmap 0022.8f, 0022.12f): title, size, the learner's numbers, coverage and the start link. */
export function CourseCard({
  title,
  subtitle,
  signedIn,
  stats,
  coverageText,
  to,
  listTo,
}: CourseCardProps) {
  const { t, i18n } = useTranslation();
  const number = (value: number) => value.toLocaleString(i18n.language);
  const percent = (value: number) =>
    value.toLocaleString(i18n.language, { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  return (
    <article className="grid content-start gap-3 rounded-lg border border-border bg-surface p-4">
      <div className="grid gap-1">
        <h2 className="text-lg font-semibold">{title}</h2>
        <p className="text-sm text-fg-muted">{subtitle}</p>
      </div>
      {signedIn && stats ? (
        <>
          <div className="grid gap-1">
            <ProgressBar
              value={stats.coverage}
              max={100}
              label={t("learn.courses.coverageLabel")}
            />
            <p className="text-sm">
              {t("learn.courses.coverage", {
                coverage: percent(stats.coverage),
                text: coverageText,
              })}
            </p>
          </div>
          <dl className="grid grid-cols-3 gap-2 text-sm">
            <div>
              <dt className="text-fg-muted">{t("learn.courses.known")}</dt>
              <dd className="text-lg font-semibold tabular-nums">{number(stats.known)}</dd>
            </div>
            <div>
              <dt className="text-fg-muted">{t("learn.courses.due")}</dt>
              <dd className="text-lg font-semibold tabular-nums">{number(stats.due)}</dd>
            </div>
            <div>
              <dt className="text-fg-muted">{t("learn.courses.new")}</dt>
              <dd className="text-lg font-semibold tabular-nums">{number(stats.new)}</dd>
            </div>
          </dl>
        </>
      ) : null}
      {signedIn ? (
        <Link to={to} className={buttonClasses({ variant: "primary", size: "md" })}>
          {t("learn.courses.start")}
        </Link>
      ) : (
        <Link to={listTo} className="text-sm text-accent underline">
          {t("learn.courses.list")}
        </Link>
      )}
    </article>
  );
}
