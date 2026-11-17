import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Navigate } from "react-router";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { ApiError, describeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasRevisionOverviewRole, hasWriteRole } from "@/lib/auth/roles";
import { exportSeed, type SeedKind, saveBlob } from "@/lib/maintainer/api";
import { AgentRunsSection } from "./agent/AgentRunsSection";
import { ApiTokensSection } from "./agent/ApiTokensSection";
import { LinkRolesSection } from "./LinkRolesSection";
import { RevisionLevelsSection } from "./RevisionLevelsSection";
import { RevisionOverviewSection } from "./RevisionOverviewSection";
import { SeedRoundTripSection } from "./SeedRoundTripSection";

const KINDS: SeedKind[] = ["all", "morphology", "mappings", "revision"];

/** Maintainer tools; anonymous users go to sign-in, users without the role see a 403 note. */
export function MaintainerPage() {
  const { t } = useTranslation();
  const { status, user } = useAuth();
  const [kind, setKind] = useState<SeedKind>("all");

  const download = useMutation({
    mutationFn: async () => {
      const { blob, fileName } = await exportSeed(kind);
      saveBlob(blob, fileName);
      return fileName;
    },
  });

  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;

  const error = download.error;
  const message = !error
    ? ""
    : error instanceof ApiError && error.status === 409
      ? t("maintainer.seed.running")
      : describeApiError(error, t("maintainer.seed.failed"));

  return (
    <section className="mx-auto flex w-full max-w-4xl flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold">{t("maintainer.title")}</h1>
      {hasRevisionOverviewRole(user) ? (
        <>
          {hasWriteRole(user) ? (
            <div className="flex flex-col gap-3">
              <h2 className="text-lg font-semibold">{t("maintainer.seed.title")}</h2>
              <p className="text-fg-muted">{t("maintainer.seed.intro")}</p>
              <div className="flex flex-wrap items-center gap-3">
                <Select
                  label={t("maintainer.seed.kind")}
                  value={kind}
                  onChange={(value) => setKind(value as SeedKind)}
                  disabled={download.isPending}
                  options={KINDS.map((value) => ({
                    value,
                    label: t(`maintainer.seed.kinds.${value}`),
                  }))}
                />
                <Button
                  variant="primary"
                  disabled={download.isPending}
                  onClick={() => download.mutate()}
                >
                  {download.isPending
                    ? t("maintainer.seed.exporting")
                    : t("maintainer.seed.export")}
                </Button>
              </div>
              <FormError message={message} />
              {download.isSuccess && (
                <p role="status" className="text-sm text-fg-muted">
                  {t("maintainer.seed.done", { file: download.data })}
                </p>
              )}
            </div>
          ) : null}
          {hasWriteRole(user) ? <SeedRoundTripSection /> : null}
          {hasWriteRole(user) ? <LinkRolesSection /> : null}
          <RevisionOverviewSection />
          {hasWriteRole(user) ? <RevisionLevelsSection /> : null}
          {hasWriteRole(user) ? <AgentRunsSection /> : null}
          {hasWriteRole(user) ? <ApiTokensSection /> : null}
        </>
      ) : (
        <p role="alert" className="text-fg-muted">
          {t("admin.forbidden")}
        </p>
      )}
    </section>
  );
}
