import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { Select } from "@/components/Select";
import { Switch } from "@/components/Switch";
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
  /** state of the data behind the current mode; `emptyHighlighted`: "Nur hervorgehobene" is on and none are in the chapter */
  status: "idle" | "loading" | "error" | "empty" | "emptyHighlighted";
  onRetry: () => void;
  /** TSK header switches (roadmap 0024.3f), shown in `tsk` mode. */
  /** Edition filter (roadmap 0025.6f), shown in `original` mode; `disabled` in OT chapters (Hebrew/Aramaic). */
  edition?: {
    value: string | null;
    /** main edition codes in catalog order */
    codes: readonly string[];
    onChange: (value: string | null) => void;
    disabled: boolean;
  };
  tsk?: {
    onlyHighlighted: boolean;
    onOnlyHighlightedChange: (value: boolean) => void;
    /** Edit mode (clicking a link opens its editor); only passed for logged-in users. */
    edit?: { checked: boolean; onChange: (value: boolean) => void };
    /** Curate mode "Ausgeblendete zeigen"; only passed for Admin/Maintainer. */
    curate?: { checked: boolean; onChange: (value: boolean) => void };
    /** Link mode "Verknüpfen" (roadmap 0024.5f); only passed for Admin/Maintainer. */
    link?: { checked: boolean; onChange: (value: boolean) => void };
  };
}

/** Sticky head of the left margin: heading of the shown content and the TSK / Grundtext / off control. */
export function SidePanelRail({
  mode,
  onModeChange,
  status,
  onRetry,
  edition,
  tsk,
}: SidePanelRailProps) {
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
      <div className="side-rail-head flex flex-col items-end gap-2 bg-bg pb-2 font-sans">
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
        {mode === "original" && edition && (
          <div
            title={
              edition.disabled
                ? t("bible.sidePanel.edition.disabledOt")
                : t("bible.sidePanel.edition.info")
            }
          >
            <Select
              label={t("bible.sidePanel.edition.label")}
              value={edition.value ?? ""}
              disabled={edition.disabled}
              onChange={(value) => edition.onChange(value || null)}
              options={[
                { value: "", label: t("bible.sidePanel.edition.all") },
                ...edition.codes.map((code) => ({ value: code, label: code })),
              ]}
            />
          </div>
        )}
        {mode === "tsk" && tsk && (
          <div className="flex flex-wrap justify-end gap-x-4 gap-y-1 text-xs">
            {tsk.edit && (
              <Switch checked={tsk.edit.checked} onCheckedChange={tsk.edit.onChange}>
                {t("bible.tskCuration.editMode")}
              </Switch>
            )}
            <Switch checked={tsk.onlyHighlighted} onCheckedChange={tsk.onOnlyHighlightedChange}>
              {t("bible.tskCuration.onlyHighlighted")}
            </Switch>
            {tsk.curate && (
              <Switch checked={tsk.curate.checked} onCheckedChange={tsk.curate.onChange}>
                {t("bible.tskCuration.showHidden")}
              </Switch>
            )}
            {tsk.link && (
              <Switch checked={tsk.link.checked} onCheckedChange={tsk.link.onChange}>
                {t("bible.tskLink.toggle")}
              </Switch>
            )}
          </div>
        )}
        <div role="status" className="text-end text-xs text-fg-muted">
          {mode !== "off" && status === "loading" && t(`bible.sidePanel.loading${kind}`)}
          {mode !== "off" && status === "empty" && t("bible.sidePanel.emptyChapter")}
          {mode === "tsk" &&
            status === "emptyHighlighted" &&
            t("bible.tskCuration.emptyHighlighted")}
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
