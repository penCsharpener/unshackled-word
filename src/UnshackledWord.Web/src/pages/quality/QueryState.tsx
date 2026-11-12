import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";

/** Renders loading skeleton / error with retry / content of one independent section query. */
export function QueryState<T>({
  query,
  children,
}: {
  query: { isError: boolean; data: T | undefined; refetch: () => unknown };
  children: (data: T) => ReactNode;
}) {
  const { t } = useTranslation();
  if (query.isError) {
    return (
      <div className="grid gap-2">
        <FormError message={t("quality.states.error")} />
        <Button size="sm" className="justify-self-start" onClick={() => void query.refetch()}>
          {t("quality.states.retry")}
        </Button>
      </div>
    );
  }
  if (query.data === undefined) {
    return (
      <div aria-busy="true">
        <EmptyState variant="comingSoon" skeletonRows={3}>
          {t("quality.states.loading")}
        </EmptyState>
      </div>
    );
  }
  return <>{children(query.data)}</>;
}
