import { useTranslation } from "react-i18next";
import type { VerseOriginalText } from "@/lib/bible/types";

export interface PassagePreviewVerse {
  verse: number;
  text: string;
  /** Original text of the verse; when any verse has one the card shows it next to (from md) or under the German. */
  original?: VerseOriginalText;
}

export interface PassagePreviewCardProps {
  /** Viewport position of the card's corner; with `above` it is the bottom edge. */
  position: { x: number; y: number; above?: boolean };
  /** Optional heading line (the formatted reference). */
  title?: string;
  /** Loaded verses; `undefined` while there is nothing to show yet. */
  verses?: PassagePreviewVerse[];
  loading?: boolean;
  error?: boolean;
}

/** Card width in px: `w-80`, with originals `md:w-160` from the md breakpoint (48rem). */
export const PREVIEW_CARD_WIDTH = 320;
export const PREVIEW_CARD_WIDTH_WIDE = 640;
export const PREVIEW_CARD_WIDE_QUERY = "(min-width: 48rem)";

/** True when the card shows originals and therefore widens from md. */
export const hasOriginals = (verses?: PassagePreviewVerse[]) =>
  verses?.some((v) => v.original) ?? false;

function OriginalText({ original }: { original: VerseOriginalText }) {
  const rtl = original.language !== "greek";
  const lang =
    original.language === "greek" ? "grc" : original.language === "aramaic" ? "arc" : "he";
  return (
    <span
      lang={lang}
      dir={rtl ? "rtl" : "ltr"}
      className={`font-original leading-snug ${rtl ? "text-xl" : "text-lg"}`}
    >
      {original.text}
    </span>
  );
}

/** Hover card with the text of a passage. Presentational: the caller owns the data and the position. */
export function PassagePreviewCard({
  position,
  title,
  verses,
  loading,
  error,
}: PassagePreviewCardProps) {
  const { t } = useTranslation();
  const range = (verses?.length ?? 0) > 1;
  const wide = hasOriginals(verses);
  const number = (verse: number) =>
    range && <sup className="me-1 font-sans text-sm text-fg-muted">{verse}</sup>;
  return (
    <div
      role="tooltip"
      data-testid="cross-reference-preview"
      popover="manual"
      ref={(element) => {
        // measured pixel position: the only value that cannot be a design token
        element?.style.setProperty("left", `${position.x}px`);
        element?.style.setProperty("top", `${position.y}px`);
        // top layer: escapes ancestors that act as the containing block of `fixed` and stays above native popovers
        try {
          if (element && !element.matches(":popover-open")) element.showPopover();
        } catch {
          // no popover support (jsdom, old browsers): the plain fixed card is shown
        }
      }}
      className={`pointer-events-none fixed inset-auto m-0 h-fit overflow-visible z-50 ${position.above ? "-translate-y-full" : ""} w-80 ${wide ? "md:w-160" : ""} rounded-lg border border-border bg-surface p-3 font-serif text-base text-fg shadow-lg`}
    >
      {title ? <p className="mb-1 font-sans font-medium">{title}</p> : null}
      {verses ? (
        wide ? (
          // one grid row per verse from md (German left, original right); below md the original follows its German
          <div
            data-testid="preview-columns"
            className="grid grid-cols-1 gap-x-4 gap-y-1 md:grid-cols-2"
          >
            {verses.map((v) => [
              <p key={`de-${v.verse}`} data-column="german">
                {number(v.verse)}
                {v.text}
              </p>,
              <p
                key={`orig-${v.verse}`}
                data-column="original"
                dir={v.original && v.original.language !== "greek" ? "rtl" : undefined}
                className="mb-1 md:mb-0"
              >
                {v.original && number(v.verse)}
                {v.original && <OriginalText original={v.original} />}
              </p>,
            ])}
          </div>
        ) : (
          verses.map((v) => (
            <p key={v.verse}>
              {number(v.verse)}
              {v.text}
            </p>
          ))
        )
      ) : error ? (
        <p className="text-fg-muted">{t("bible.sidePanel.previewError")}</p>
      ) : loading ? (
        <p className="text-fg-muted">{t("bible.sidePanel.previewLoading")}</p>
      ) : null}
    </div>
  );
}
