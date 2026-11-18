import {
  createContext,
  type ReactNode,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { useTranslation } from "react-i18next";
import { Button } from "./Button";

export type ToastVariant = "success" | "error" | "info";

interface ToastItem {
  id: number;
  message: string;
  variant: ToastVariant;
}

interface ToastApi {
  /** Shows a short confirmation; success/info stay 5 s, errors 10 s, or until closed. */
  show: (message: string, variant?: ToastVariant) => void;
}

const ToastContext = createContext<ToastApi | null>(null);

const DURATION_MS: Record<ToastVariant, number> = { success: 5000, info: 5000, error: 10000 };
const ICONS: Record<ToastVariant, string> = { success: "✓", info: "○", error: "✕" };
const BORDERS: Record<ToastVariant, string> = {
  success: "border-sev-ok",
  info: "border-sev-info",
  error: "border-sev-error",
};

/** Toast notifications: mount once in the app root, call `useToast().show(message, variant)`. */
export function ToastProvider({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const nextId = useRef(0);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);
  const show = useCallback((message: string, variant: ToastVariant = "success") => {
    nextId.current += 1;
    const id = nextId.current;
    setToasts((current) => [...current, { id, message, variant }]);
  }, []);
  const api = useMemo(() => ({ show }), [show]);

  // Both live regions always exist so that screen readers announce what is added later;
  // errors are assertive (aria-live), everything else polite (role="status").
  const renderRegion = (errors: boolean) =>
    toasts
      .filter((toast) => (toast.variant === "error") === errors)
      .map((toast) => (
        <ToastView key={toast.id} toast={toast} closeLabel={t("toast.close")} onDismiss={dismiss} />
      ));

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="toast-stack pointer-events-none fixed inset-x-0 bottom-0 z-50 flex flex-col items-center gap-2 p-4 sm:items-end">
        <div
          role="status"
          aria-live="polite"
          className="flex w-full max-w-sm flex-col gap-2 empty:hidden"
        >
          {renderRegion(false)}
        </div>
        <div aria-live="assertive" className="flex w-full max-w-sm flex-col gap-2 empty:hidden">
          {renderRegion(true)}
        </div>
      </div>
    </ToastContext.Provider>
  );
}

function ToastView({
  toast,
  closeLabel,
  onDismiss,
}: {
  toast: ToastItem;
  closeLabel: string;
  onDismiss: (id: number) => void;
}) {
  const { id, variant } = toast;
  useEffect(() => {
    const timer = setTimeout(() => onDismiss(id), DURATION_MS[variant]);
    return () => clearTimeout(timer);
  }, [id, variant, onDismiss]);

  return (
    <div
      data-variant={variant}
      className={`toast pointer-events-auto flex items-start gap-3 rounded-md border bg-surface p-3 text-sm text-fg shadow-lg ${BORDERS[variant]}`}
    >
      <span aria-hidden="true">{ICONS[variant]}</span>
      <p className="min-w-0 flex-1 wrap-break-word">{toast.message}</p>
      <Button
        variant="ghost"
        size="sm"
        aria-label={closeLabel}
        title={closeLabel}
        onClick={() => onDismiss(id)}
      >
        <span aria-hidden="true">✕</span>
      </Button>
    </div>
  );
}

export function useToast(): ToastApi {
  const api = useContext(ToastContext);
  if (!api) throw new Error("useToast must be used inside <ToastProvider>");
  return api;
}
