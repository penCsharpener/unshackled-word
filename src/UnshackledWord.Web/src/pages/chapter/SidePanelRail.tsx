import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { SIDE_PANEL_MODES, type SidePanelMode } from "@/lib/bible/sidePanel";

function ModeIcon({ mode }: { mode: SidePanelMode }) {
  if (mode === "tsk") {
    return (
      <svg
        aria-hidden="true"
        viewBox="0 0 24 24"
        className="size-5"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        <path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01" />
      </svg>
    );
  }
  if (mode === "original") {
    return (
      <span aria-hidden="true" className="font-original text-base leading-none">
        Αα
      </span>
    );
  }
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className="size-5"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <circle cx="12" cy="12" r="9" />
      <path d="m5.6 5.6 12.8 12.8" />
    </svg>
  );
}

export interface SidePanelRailProps {
  mode: SidePanelMode;
  onModeChange: (mode: SidePanelMode) => void;
  /** state of the data behind the current mode */
  status: "idle" | "loading" | "error" | "empty";
  onRetry: () => void;
}

/** Sticky head of the left margin: heading of the shown content and the TSK / Grundtext / off control. */
export function SidePanelRail({ mode, onModeChange, status, onRetry }: SidePanelRailProps) {
  const { t } = useTranslation();
  const heading =
    mode === "tsk"
      ? t("bible.sidePanel.headingTsk")
      : mode === "original"
        ? t("bible.sidePanel.headingOriginal")
        : "";
  const kind = mode === "original" ? "Original" : "Tsk";

  return (
    <aside aria-label={t("bible.sidePanel.rail")} className="side-rail">
      <div className="side-rail-head flex flex-col items-end gap-2 font-sans">
        <div className="flex items-center gap-3">
          <h2 className={`text-sm font-medium text-fg-muted ${mode === "off" ? "sr-only" : ""}`}>
            {heading || t("bible.sidePanel.rail")}
          </h2>
          <fieldset className="flex gap-1">
            <legend className="sr-only">{t("bible.sidePanel.modeGroup")}</legend>
            {SIDE_PANEL_MODES.map((value) => (
              <Button
                key={value}
                variant="secondary"
                size="sm"
                aria-pressed={mode === value}
                aria-label={t(`bible.sidePanel.mode.${value}`)}
                title={t(`bible.sidePanel.mode.${value}`)}
                onClick={() => onModeChange(value)}
              >
                <ModeIcon mode={value} />
              </Button>
            ))}
          </fieldset>
        </div>
        <div role="status" className="text-end text-xs text-fg-muted">
          {mode !== "off" && status === "loading" && t(`bible.sidePanel.loading${kind}`)}
          {mode !== "off" && status === "empty" && t("bible.sidePanel.emptyChapter")}
        </div>
        {mode !== "off" && status === "error" && (
          <div role="alert" className="flex items-center gap-2 text-xs">
            <span>{t(`bible.sidePanel.error${kind}`)}</span>
            <Button variant="ghost" size="sm" onClick={onRetry}>
              {t("bible.retry")}
            </Button>
          </div>
        )}
        <p role="status" className="sr-only">
          {t(`bible.sidePanel.announce.${mode}`)}
        </p>
      </div>
    </aside>
  );
}
