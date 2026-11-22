import { useTranslation } from "react-i18next";
import type { ChangeReviewChange, ChangeReviewItem } from "@/lib/vocab/changeReview";
import { Button } from "./Button";
import { StatusBadge } from "./StatusBadge";

export interface ChangeReviewListProps {
  items: ChangeReviewItem[];
  /** "Weiterlernen": the card keeps its progress. */
  onKeep: (entryId: number) => void;
  /** "Zurücksetzen" (changed cards) or "Entfernen" (removed cards): the card comes back as new. */
  onReset: (entryId: number) => void;
  /** Disables the item buttons while a request runs. */
  disabled?: boolean;
}

/** The revised or removed words of the learner (roadmap 0022.8bf): before and after per change, keep or reset per word. */
export function ChangeReviewList({
  items,
  onKeep,
  onReset,
  disabled = false,
}: ChangeReviewListProps) {
  const { t } = useTranslation();
  return (
    <ul className="grid gap-3">
      {items.length === 0 ? (
        <li className="text-sm text-fg-muted">{t("learn.changes.empty")}</li>
      ) : null}
      {items.map((item) => (
        <li key={item.entryId} className="grid gap-2 rounded-lg border border-border p-3">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <span
              lang={item.lang}
              dir={item.lang === "he" ? "rtl" : undefined}
              className="font-original text-xl"
            >
              {item.lemma}
            </span>
            {item.form ? (
              <span
                lang={item.lang}
                dir={item.lang === "he" ? "rtl" : undefined}
                className="font-original text-sm text-fg-muted"
              >
                {item.form}
              </span>
            ) : null}
          </div>
          {item.changes.map((change) => (
            <ChangeBody key={change.key} change={change} item={item} />
          ))}
          <div className="flex flex-wrap gap-2">
            {item.removed ? (
              <Button
                variant="secondary"
                size="sm"
                disabled={disabled}
                onClick={() => onReset(item.entryId)}
              >
                {t("learn.changes.remove")}
              </Button>
            ) : (
              <>
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={disabled}
                  onClick={() => onKeep(item.entryId)}
                >
                  {t("learn.changes.keep")}
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={disabled}
                  onClick={() => onReset(item.entryId)}
                >
                  {t("learn.changes.reset")}
                </Button>
              </>
            )}
          </div>
        </li>
      ))}
    </ul>
  );
}

/** One change of a card: its label, the values before (struck out when gone) and after, and the note. */
function ChangeBody({ change, item }: { change: ChangeReviewChange; item: ChangeReviewItem }) {
  const { t } = useTranslation();
  const before = change.before ?? [];
  const after = change.after ?? [];
  const lang = item.lang;
  const dir = lang === "he" ? "rtl" : undefined;
  return (
    <div className="grid gap-1">
      <div>
        <StatusBadge tone={item.removed ? "warning" : "info"}>{change.kind}</StatusBadge>
      </div>
      {before.length > 0 ? (
        <p className="text-sm">
          <span className="text-fg-muted">{t("learn.changes.before")}</span>{" "}
          {before.map((value, index) => (
            <span key={value}>
              {index > 0 ? ", " : null}
              {after.includes(value) ? (
                <span lang={lang} dir={dir} className="font-original">
                  {value}
                </span>
              ) : (
                <s lang={lang} dir={dir} className="font-original text-fg-muted">
                  {value}
                </s>
              )}
            </span>
          ))}
          {after.length > 0 ? (
            <>
              {" "}
              <span className="text-fg-muted">→ {t("learn.changes.after")}</span>{" "}
              <span lang={lang} dir={dir} className="font-original font-medium">
                {after.join(", ")}
              </span>
            </>
          ) : null}
        </p>
      ) : null}
      {change.note ? <p className="text-sm text-fg-muted">{change.note}</p> : null}
    </div>
  );
}
