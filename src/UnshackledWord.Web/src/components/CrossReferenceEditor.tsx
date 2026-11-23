import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { describeApiError } from "@/lib/api";
import { curationRequest, TSK_COMMENT_MAX, useSaveTskCuration } from "@/lib/bible/tskCuration";
import type { CrossReference, TskRating } from "@/lib/bible/types";
import { Button } from "./Button";
import { FormError } from "./FormError";
import { TextField } from "./TextField";

export interface CrossReferenceEditorProps {
  /** The verse the link starts at (Elberfelder numbering). */
  source: { bookId: number; chapter: number; verse: number };
  /** The link being curated; its `rating` and `comment` are the starting state. */
  reference: CrossReference;
  /** Readable target, e.g. "Psalm 33,6" (heading and dialog name). */
  label: string;
  /** Viewport position of the card's corner (measured by the caller); with `above` it is the bottom edge. */
  position: { x: number; y: number; above?: boolean };
  /** Called on cancel, Escape, a click outside and after a successful save. */
  onClose: () => void;
}

/** Card width in px (`w-80`), for callers that clamp the position. */
export const CROSS_REFERENCE_EDITOR_WIDTH = 320;

type Choice = TskRating | "neutral";
const CHOICES: readonly Choice[] = ["highlight", "neutral", "hide"];

/**
 * Popover editor of one TSK link (roadmap 0024.3f): Hervorheben / Neutral / Ausblenden and a comment (max 500).
 * Maintainers save directly (`PUT tsk/curation`); suggestions by other users follow in 0024.6f.
 */
export function CrossReferenceEditor({
  source,
  reference,
  label,
  position,
  onClose,
}: CrossReferenceEditorProps) {
  const { t } = useTranslation();
  const save = useSaveTskCuration();
  const initialChoice: Choice = reference.rating ?? "neutral";
  const [choice, setChoice] = useState<Choice>(initialChoice);
  const [comment, setComment] = useState(reference.comment ?? "");
  const root = useRef<HTMLDivElement>(null);
  const changed = choice !== initialChoice || comment.trim() !== (reference.comment ?? "");

  // Escape and a click outside close the editor, like the other popovers of the app.
  useEffect(() => {
    const onPointer = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) onClose();
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("pointerdown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [onClose]);

  useEffect(() => {
    root.current?.querySelector<HTMLElement>("[aria-pressed='true']")?.focus();
  }, []);

  const submit = () =>
    save.mutate(curationRequest(source, reference, choice === "neutral" ? null : choice, comment), {
      onSuccess: onClose,
    });

  return (
    <div
      ref={(element) => {
        root.current = element;
        // measured pixel position: the only value that cannot be a design token
        element?.style.setProperty("left", `${position.x}px`);
        element?.style.setProperty("top", `${position.y}px`);
        // top layer: escapes the clipped side panel and stays above the hover card
        try {
          if (element && !element.matches(":popover-open")) element.showPopover();
        } catch {
          // no popover support (jsdom, old browsers): the plain fixed card is shown
        }
      }}
      role="dialog"
      aria-label={t("bible.tskCuration.editorLabel", { reference: label })}
      data-testid="cross-reference-editor"
      popover="manual"
      className={`fixed inset-auto z-50 m-0 h-fit w-80 ${position.above ? "-translate-y-full" : ""} flex flex-col gap-3 rounded-lg border border-border bg-surface p-3 font-sans text-sm text-fg shadow-lg`}
    >
      <p className="font-medium">{label}</p>
      <fieldset className="flex gap-1">
        <legend className="sr-only">{t("bible.tskCuration.rating")}</legend>
        {CHOICES.map((value) => (
          <Button
            key={value}
            variant="secondary"
            size="sm"
            aria-pressed={choice === value}
            onClick={() => setChoice(value)}
          >
            {t(`bible.tskCuration.choice.${value}`)}
          </Button>
        ))}
      </fieldset>
      <div className="flex flex-col gap-1">
        <TextField
          multiline
          rows={3}
          size="sm"
          required={false}
          label={t("bible.tskCuration.comment")}
          value={comment}
          maxLength={TSK_COMMENT_MAX}
          onChange={setComment}
          onKeyDown={(event) => {
            if ((event.ctrlKey || event.metaKey) && event.key === "Enter" && changed) submit();
          }}
        />
        <p className="text-end text-xs text-fg-muted">
          {t("bible.tskCuration.counter", { length: comment.length, max: TSK_COMMENT_MAX })}
        </p>
      </div>
      <FormError
        message={save.isError ? describeApiError(save.error, t("bible.tskCuration.error")) : ""}
      />
      <div className="flex justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={onClose}>
          {t("bible.tskCuration.cancel")}
        </Button>
        <Button variant="primary" size="sm" disabled={!changed || save.isPending} onClick={submit}>
          {save.isPending ? t("bible.tskCuration.saving") : t("bible.tskCuration.save")}
        </Button>
      </div>
    </div>
  );
}
