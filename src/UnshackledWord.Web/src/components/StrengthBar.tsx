import { useTranslation } from "react-i18next";

export interface StrengthBarProps {
  /** Strength before the answer, 0–5. */
  before: number;
  /** Strength after the answer, 0–5. */
  after: number;
}

const BLOCKS = [1, 2, 3, 4, 5];

/** Whole blocks of a 0–5 strength, the same comparison the server uses for `change`. */
function blocks(value: number): number {
  return Math.min(5, Math.max(0, Math.floor(value)));
}

/**
 * Strength as five blocks with the change in words (roadmap 0022.8f): moved (up), held, or dropped
 * ("Kommt in dieser Sitzung wieder"). Dropped fills the blocks in the warning colour.
 */
export function StrengthBar({ before, after }: StrengthBarProps) {
  const { t } = useTranslation();
  const from = blocks(before);
  const to = blocks(after);
  const change = to > from ? "moved" : to < from ? "dropped" : "held";
  const text = t(`learn.strength.${change}`);
  return (
    <div
      role="img"
      aria-label={t("learn.strength.label", { after: to, text })}
      className="flex flex-wrap items-center gap-3"
    >
      <span aria-hidden="true" className="flex gap-1">
        {BLOCKS.map((block) => (
          <span
            key={block}
            className={`h-3 w-6 rounded-sm ${
              block <= to
                ? change === "dropped"
                  ? "bg-sev-warning"
                  : "bg-accent"
                : "bg-chart-empty"
            }`}
          />
        ))}
      </span>
      <span className="text-sm text-fg-muted">{text}</span>
    </div>
  );
}
