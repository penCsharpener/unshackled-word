import { useTranslation } from "react-i18next";
import { ScriptureText, StrongsLink } from "@/components/ScriptureText";
import { formatMorphology } from "@/lib/bible/morphology";
import { type HeaderWordInfo as HeaderWordInfoData, useHeaderInfo } from "@/lib/headerInfo";

/**
 * Original word, lemma, Strong's and morphology of the hovered word, in the centre of the sticky top
 * menu. Info that the reader shows in the page margin (`inMargin`, from 1024px) is left out here.
 */
export function HeaderWordInfo() {
  const info = useHeaderInfo();
  if (!info) return <div className="min-w-0 flex-1" aria-hidden="true" />;
  return (
    <div className={`min-w-0 flex-1 ${info.inMargin ? "lg:hidden" : ""}`}>
      <WordInfoBody info={info} className="items-center text-center" />
    </div>
  );
}

/** The same info as a small box in the page margin (hover info, "HI"; never pinned). */
export function WordInfoCard({ info }: { info: HeaderWordInfoData }) {
  return (
    <div className="rounded-md border border-border bg-surface-muted p-3 shadow-sm">
      <WordInfoBody info={info} className="items-start text-start" wrap stacked />
    </div>
  );
}

function WordInfoBody({
  info,
  className,
  wrap = false,
  stacked = false,
}: {
  info: HeaderWordInfoData;
  className: string;
  wrap?: boolean;
  /** every source-language item on its own line (a Hebrew word next to Latin text breaks the line direction), plus gloss and lexicon text */
  stacked?: boolean;
}) {
  const { t } = useTranslation();
  const truncate = wrap ? "" : "truncate";

  const morphology = info.morphology
    ? formatMorphology(info.morphology, t)
    : (info.morphologyCode ?? "");
  const lang = info.language === "hebrew" ? "he" : info.language === "aramaic" ? "arc" : "grc";
  const showLemma = info.lemma && info.lemma !== info.original;
  const dir = info.language === "hebrew" || info.language === "aramaic" ? "rtl" : "ltr";
  if (stacked) {
    return (
      <div className={`flex min-w-0 flex-col gap-1 leading-tight ${className}`} aria-live="off">
        <p
          lang={lang}
          dir={dir}
          className={`w-full font-original text-fg ${dir === "rtl" ? "text-3xl" : "text-2xl"}`}
        >
          {info.original}
        </p>
        {showLemma && (
          <p className="w-full text-base text-fg-muted">
            {t("bible.mapping.headerInfo.lemma")}
            <span
              lang={lang}
              dir={dir}
              className={`block font-original ${dir === "rtl" ? "text-2xl" : "text-xl"}`}
            >
              {info.lemma}
            </span>
          </p>
        )}
        {(info.strongs || morphology) && (
          <p className="w-full text-base text-fg-muted" dir="ltr">
            {info.strongs && <StrongsLink value={info.strongs} />}
            {info.strongs && morphology && " · "}
            {morphology}
          </p>
        )}
        {info.editionNote && <p className="w-full text-base text-fg-muted">{info.editionNote}</p>}
        {info.gloss && (
          <p lang="en" className="w-full text-lg text-fg">
            {info.gloss}
          </p>
        )}
        {info.definition && (
          <p lang="en" className="w-full text-base text-fg-muted">
            <ScriptureText text={info.definition} segments={info.definitionSegments} />
          </p>
        )}
      </div>
    );
  }
  return (
    <div className={`flex min-w-0 flex-col leading-tight ${className}`} aria-live="off">
      <p className={`max-w-full ${truncate}`}>
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
      {morphology && <p className={`max-w-full text-xs text-fg-muted ${truncate}`}>{morphology}</p>}
      {info.editionNote && (
        <p className={`max-w-full text-xs text-fg-muted ${truncate}`}>{info.editionNote}</p>
      )}
    </div>
  );
}
