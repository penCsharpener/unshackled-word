import { useTranslation } from "react-i18next";

export interface PassagePreviewCardProps {
  /** Viewport position of the card's corner; with `above` it is the bottom edge. */
  position: { x: number; y: number; above?: boolean };
  /** Optional heading line (the formatted reference). */
  title?: string;
  /** Loaded verses; `undefined` while there is nothing to show yet. */
  verses?: { verse: number; text: string }[];
  loading?: boolean;
  error?: boolean;
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
      className={`pointer-events-none fixed inset-auto m-0 h-fit overflow-visible z-50 ${position.above ? "-translate-y-full" : ""} w-80 rounded-lg border border-border bg-surface p-3 font-serif text-base text-fg shadow-lg`}
    >
      {title ? <p className="mb-1 font-sans font-medium">{title}</p> : null}
      {verses ? (
        verses.map((v) => (
          <p key={v.verse}>
            {range && <sup className="me-1 font-sans text-sm text-fg-muted">{v.verse}</sup>}
            {v.text}
          </p>
        ))
      ) : error ? (
        <p className="text-fg-muted">{t("bible.sidePanel.previewError")}</p>
      ) : loading ? (
        <p className="text-fg-muted">{t("bible.sidePanel.previewLoading")}</p>
      ) : null}
    </div>
  );
}
