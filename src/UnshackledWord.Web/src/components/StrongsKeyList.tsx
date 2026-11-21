import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Button } from "./Button";

export interface StrongsKeyListProps {
  keys: { key: string; occurrences: number }[];
  /** Called with the key whose "Verschieben…" button was pressed. */
  onMove: (key: string) => void;
}

/** Strong's keys of a vocab entry with their occurrences, each linked to its Strong's page and movable to another entry. */
export function StrongsKeyList({ keys, onMove }: StrongsKeyListProps) {
  const { t, i18n } = useTranslation();

  if (keys.length === 0) {
    return <p className="text-sm text-fg-muted">{t("vocab.editor.strongs.empty")}</p>;
  }

  return (
    <ul className="divide-y divide-border text-sm">
      {keys.map(({ key, occurrences }) => (
        <li key={key} className="flex items-center justify-between gap-3 py-2">
          <span className="flex items-baseline gap-3">
            <Link to={`/bible/strongs/${key}`} className="text-accent underline">
              {key}
            </Link>
            <span className="tabular-nums text-fg-muted">
              {t("vocab.editor.strongs.occurrences", {
                count: occurrences.toLocaleString(i18n.language),
              })}
            </span>
          </span>
          <Button variant="ghost" size="sm" onClick={() => onMove(key)}>
            {t("vocab.editor.strongs.move")}
          </Button>
        </li>
      ))}
    </ul>
  );
}
