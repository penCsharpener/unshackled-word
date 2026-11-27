import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { getVocabRoot, type VocabLang, type VocabRootDetail } from "@/lib/vocab/api";
import { Button } from "./Button";
import { StatusBadge } from "./StatusBadge";
import { Switch } from "./Switch";

export interface VocabFamilyFilterProps {
  /** `VocabRoots."Id"` of the active family filter (the `root` URL parameter). */
  rootId: number;
  lang: VocabLang;
  /** Include the entries of the sub-roots (default on in the URL). */
  subRoots: boolean;
  onSubRootsChange: (value: boolean) => void;
  /** Removes the filter (the chip's ×). */
  onClear: () => void;
  /** The list filtered to another family, for the "↑ parent" link. */
  rootHref: (rootId: number) => string;
}

/**
 * The active word-family filter of the vocab lists (roadmap 0030.3f): a removable chip "Wortfamilie: <root> ×",
 * the "mit Unterwurzeln" switch (only when the root has sub-roots) and the "↑ parent" link (only when it has a parent).
 * Loads the root from `GET api/vocab/roots/{id}`; the chip stays usable while that loads or fails.
 */
export function VocabFamilyFilter({
  rootId,
  lang,
  subRoots,
  onSubRootsChange,
  onClear,
  rootHref,
}: VocabFamilyFilterProps) {
  const { t } = useTranslation();
  const [detail, setDetail] = useState<VocabRootDetail | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setDetail(null);
    getVocabRoot(rootId, controller.signal)
      .then((response) => setDetail(response))
      .catch(() => undefined);
    return () => controller.abort();
  }, [rootId]);

  const label = detail?.root ?? `#${rootId}`;

  return (
    <div className="flex flex-wrap items-center gap-3" data-testid="family-filter">
      <span className="inline-flex items-center gap-1">
        <StatusBadge tone="info">
          {t("vocab.root.family")}:{" "}
          <span lang={lang} dir={lang === "he" ? "rtl" : undefined} className="font-original">
            {label}
          </span>
        </StatusBadge>
        <Button
          variant="ghost"
          size="sm"
          aria-label={t("vocab.root.clearFamily")}
          onClick={onClear}
        >
          ×
        </Button>
      </span>
      {detail && detail.subRoots.length > 0 ? (
        <Switch checked={subRoots} onCheckedChange={onSubRootsChange} className="text-sm">
          {t("vocab.root.withSubRoots")}
        </Switch>
      ) : null}
      {detail?.parent ? (
        <Link
          to={rootHref(detail.parent.id)}
          title={t("vocab.root.parentTitle")}
          className="text-sm text-accent underline"
        >
          {t("vocab.root.parent", { root: detail.parent.root })}
        </Link>
      ) : null}
    </div>
  );
}
