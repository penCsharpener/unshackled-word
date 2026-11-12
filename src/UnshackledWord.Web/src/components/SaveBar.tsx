import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "./Button";
import { FormError } from "./FormError";

export type SaveStatus = "idle" | "saving" | "saved" | "error";

export interface SaveBarProps {
  /** number of pending changes */
  count: number;
  status: SaveStatus;
  /** error text (status `error`), or a blocking hint such as "2 words have no lemma" */
  message?: string;
  /** count shown after a successful save */
  savedCount?: number;
  /** extra hint next to the count, e.g. the shortcut */
  hint?: string;
  saveLabel: string;
  /** disables the save button (nothing to save, blocked by validation) */
  disabled?: boolean;
  onSave: () => void;
  onDiscard: () => void;
  className?: string;
}

/**
 * Sticky bar for pending changes: count, Save, and Discard with an inline confirmation (no modal).
 * Texts come from `lemmas.save.*`.
 */
export function SaveBar({
  count,
  status,
  message,
  savedCount = 0,
  hint,
  saveLabel,
  disabled,
  onSave,
  onDiscard,
  className = "",
}: SaveBarProps) {
  const { t } = useTranslation();
  const [confirming, setConfirming] = useState(false);
  const root = useRef<HTMLElement>(null);

  useEffect(() => {
    if (confirming) root.current?.querySelector<HTMLElement>("[data-cancel]")?.focus();
  }, [confirming]);
  useEffect(() => {
    if (count === 0) setConfirming(false);
  }, [count]);

  const showSaved = count === 0 && status === "saved";
  if (count === 0 && !showSaved) return null;

  const saving = status === "saving";
  return (
    <section
      ref={root}
      aria-label={t("lemmas.save.region")}
      className={`sticky bottom-0 z-20 flex flex-wrap items-center gap-x-4 gap-y-2 border-t border-border bg-surface px-3 py-2 text-sm ${className}`}
    >
      <p aria-live="polite" className="font-medium">
        {showSaved ? (
          <span>✓ {t("lemmas.save.saved", { count: savedCount })}</span>
        ) : (
          <>
            {t("lemmas.save.pending", { count })}
            {hint ? <span className="ml-2 font-normal text-fg-muted">{hint}</span> : null}
          </>
        )}
      </p>
      {!showSaved && (
        <div className="ml-auto flex flex-wrap items-center gap-2">
          {confirming ? (
            <>
              <span>{t("lemmas.save.discardConfirm", { count })}</span>
              <Button
                size="sm"
                onClick={() => {
                  setConfirming(false);
                  onDiscard();
                }}
              >
                {t("lemmas.save.discardYes")}
              </Button>
              <Button size="sm" variant="ghost" data-cancel="" onClick={() => setConfirming(false)}>
                {t("lemmas.save.cancel")}
              </Button>
            </>
          ) : (
            <>
              <Button size="sm" variant="ghost" onClick={() => setConfirming(true)}>
                {t("lemmas.save.discard")}
              </Button>
              <Button size="sm" variant="primary" disabled={disabled || saving} onClick={onSave}>
                {saving
                  ? t("lemmas.save.saving")
                  : status === "error"
                    ? t("lemmas.save.retry")
                    : saveLabel}
              </Button>
            </>
          )}
        </div>
      )}
      {!showSaved && message ? <FormError message={message} /> : null}
    </section>
  );
}
