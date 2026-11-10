import { useTranslation } from "react-i18next";
import { formatMorphology } from "@/lib/bible/morphology";
import { useHeaderInfo } from "@/lib/headerInfo";

/** Centre of the sticky top menu: original word, lemma, Strong's and morphology of the hovered word. */
export function HeaderWordInfo() {
  const { t } = useTranslation();
  const info = useHeaderInfo();
  if (!info) return <div className="min-w-0 flex-1" aria-hidden="true" />;

  const morphology = info.morphology
    ? formatMorphology(info.morphology, t)
    : (info.morphologyCode ?? "");
  const lang = info.language === "hebrew" ? "he" : info.language === "aramaic" ? "arc" : "grc";
  const showLemma = info.lemma && info.lemma !== info.original;
  return (
    <div
      className="flex min-w-0 flex-1 flex-col items-center text-center leading-tight"
      aria-live="off"
    >
      <p className="max-w-full truncate">
        <span lang={lang} className="font-original text-xl text-fg">
          {info.original}
        </span>
        {showLemma && (
          <span className="ms-2 text-sm text-fg-muted">
            {t("bible.mapping.headerInfo.lemma")}{" "}
            <span lang={lang} className="font-original">
              {info.lemma}
            </span>
          </span>
        )}
        {info.strongs && <span className="ms-2 text-xs text-fg-muted">{info.strongs}</span>}
      </p>
      {morphology && <p className="max-w-full truncate text-xs text-fg-muted">{morphology}</p>}
    </div>
  );
}
