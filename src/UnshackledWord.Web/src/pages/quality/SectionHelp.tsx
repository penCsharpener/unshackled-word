import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

/** Collapsed "what does this area check?" explanation under a section heading. */
export function SectionHelp({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <details className="rounded-md border border-border bg-surface">
      <summary className="cursor-pointer px-3 py-2 text-sm font-medium text-fg">
        {t("quality.help.toggle")}
      </summary>
      <div className="grid gap-3 border-t border-border p-3 text-sm text-fg-muted">{children}</div>
    </details>
  );
}

/** What a check finds and how to proceed, for the overview list and the findings header. */
export function CheckHelp({ code }: { code: string }) {
  const { t } = useTranslation();
  return (
    <>
      <p>
        <span className="font-medium text-fg">{t("quality.help.what")}: </span>
        {t(`quality.help.checks.${code}.what`)}
      </p>
      <p>
        <span className="font-medium text-fg">{t("quality.help.fix")}: </span>
        {t(`quality.help.checks.${code}.fix`)}
      </p>
    </>
  );
}
