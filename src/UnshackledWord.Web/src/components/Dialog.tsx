import { type ReactNode, type RefObject, useEffect, useId, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "./Button";

export interface DialogProps {
  open: boolean;
  /** Called for the close button, Esc and a click on the backdrop (the parent owns `open`). */
  onClose: () => void;
  title: string;
  /** Line under the title, e.g. a step indicator; also the accessible description. */
  description?: ReactNode;
  size?: "md" | "lg";
  /** Element to focus on open; defaults to the first focusable element. */
  initialFocusRef?: RefObject<HTMLElement | null>;
  footer?: ReactNode;
  /** false blocks Esc, the backdrop and the close button (e.g. while a request runs). */
  dismissible?: boolean;
  /** Body without padding and scrolling, for content that scrolls itself (virtual lists). */
  bare?: boolean;
  /** `top` docks the dialog near the top edge from md (command palette); `center` is the default. */
  placement?: "center" | "top";
  /** Keeps the title for assistive tech only (the content is self-explanatory, e.g. a search box). */
  titleHidden?: boolean;
  children: ReactNode;
}

const sizes = { md: "md:max-w-2xl", lg: "md:max-w-5xl" } as const;
const placements = { center: "m-auto", top: "mx-auto md:mt-12 md:mb-auto" } as const;

/**
 * Modal dialog on the native `<dialog>` element: focus trap, inert page, top layer and Esc come from
 * the browser; focus returns to the element that was focused before opening. Full screen below 48rem.
 */
export function Dialog({
  open,
  onClose,
  title,
  description,
  size = "md",
  initialFocusRef,
  footer,
  dismissible = true,
  bare = false,
  placement = "center",
  titleHidden = false,
  children,
}: DialogProps) {
  const { t } = useTranslation();
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const descriptionId = useId();
  const close = useRef(onClose);
  close.current = onClose;
  const canDismiss = useRef(dismissible);
  canDismiss.current = dismissible;

  useEffect(() => {
    const dialog = ref.current;
    if (!open || !dialog) return;
    const previous = document.activeElement as HTMLElement | null;
    if (!dialog.open) dialog.showModal();
    initialFocusRef?.current?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      // a dialog opened inside this one (e.g. a delete confirm) handles its own Esc
      const inner = event.target instanceof Element ? event.target.closest("dialog") : null;
      if (inner && inner !== dialog && dialog.contains(inner)) return;
      event.preventDefault();
      if (canDismiss.current) close.current();
    };
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("keydown", onKey);
      if (dialog.open) dialog.close();
      previous?.focus?.();
    };
  }, [open, initialFocusRef]);

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      // Esc: keep control over the closing (the parent decides), the native cancel is suppressed
      onCancel={(event) => {
        event.preventDefault();
        if (canDismiss.current) close.current();
      }}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && canDismiss.current) close.current();
      }}
      className={`${placements[placement]} w-full ${sizes[size]} max-h-dvh flex-col overflow-hidden rounded-lg border border-border bg-surface p-0 text-fg backdrop:bg-fg/40 open:flex md:max-h-5/6 max-md:h-dvh max-md:max-w-none max-md:rounded-none ${
        size === "lg" ? (placement === "top" ? "md:h-4/5" : "md:h-5/6") : ""
      }`}
    >
      {open && (
        <>
          <header
            className={`flex items-start gap-3 px-4 ${
              titleHidden ? "justify-end pt-2" : "border-b border-border py-3"
            }`}
          >
            <div className={titleHidden ? "sr-only" : "min-w-0 flex-1"}>
              <h2 id={titleId} className="text-lg font-semibold">
                {title}
              </h2>
              {description ? (
                <div id={descriptionId} className="text-sm text-fg-muted">
                  {description}
                </div>
              ) : null}
            </div>
            <Button
              variant="ghost"
              size="sm"
              aria-label={t("dialog.close")}
              disabled={!dismissible}
              onClick={onClose}
            >
              <span aria-hidden="true">✕</span>
            </Button>
          </header>
          <div
            className={bare ? "flex min-h-0 flex-1 flex-col" : "min-h-0 flex-1 overflow-y-auto p-4"}
          >
            {children}
          </div>
          {footer ? (
            <footer className="flex flex-wrap items-center gap-3 border-t border-border px-4 py-3">
              {footer}
            </footer>
          ) : null}
        </>
      )}
    </dialog>
  );
}
