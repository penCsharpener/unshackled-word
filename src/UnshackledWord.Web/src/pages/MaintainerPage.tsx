import { useMutation } from "@tanstack/react-query";
import { type ReactNode, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Navigate, useSearchParams } from "react-router";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { Tabs } from "@/components/Tabs";
import { ApiError, describeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import type { Me } from "@/lib/auth/api";
import { hasRevisionOverviewRole, hasWriteRole, isAdmin } from "@/lib/auth/roles";
import { saveBlob } from "@/lib/download";
import { exportSeed, type SeedKind } from "@/lib/maintainer/api";
import { AgentRunsSection } from "./agent/AgentRunsSection";
import { ApiTokensSection } from "./agent/ApiTokensSection";
import { LinkRolesSection } from "./LinkRolesSection";
import { LxxSection } from "./LxxSection";
import { RevisionLevelsSection } from "./RevisionLevelsSection";
import { RevisionOverviewSection } from "./RevisionOverviewSection";
import { SeedRoundTripSection } from "./SeedRoundTripSection";
import { VerseSimilaritySection } from "./VerseSimilaritySection";
import { VocabJobsSection } from "./VocabJobsSection";

const KINDS: SeedKind[] = ["all", "morphology", "mappings", "revision", "vocab"];

/** Tabs of the page in display order; the active one is kept in the `tab` search param. */
const MAINTAINER_TABS = ["seed", "revision", "data", "lxx", "vocab", "ai"] as const;
type MaintainerTab = (typeof MAINTAINER_TABS)[number];

/** Reviewers only see the revision overview; maintainers and admins see every tab. */
function visibleTabs(user: Me | null | undefined): MaintainerTab[] {
  if (hasWriteRole(user)) return [...MAINTAINER_TABS];
  return hasRevisionOverviewRole(user) ? ["revision"] : [];
}

/** Maintainer tools; anonymous users go to sign-in, users without the role see a 403 note. */
export function MaintainerPage() {
  const { t } = useTranslation();
  const { status, user } = useAuth();
  const [kind, setKind] = useState<SeedKind>("all");
  const [searchParams, setSearchParams] = useSearchParams();

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

  const tabs = visibleTabs(user);
  const requested = searchParams.get("tab");
  const active = tabs.find((tab) => tab === requested) ?? tabs[0];

  function selectTab(key: string) {
    setSearchParams(
      (params) => {
        const next = new URLSearchParams(params);
        next.set("tab", key);
        return next;
      },
      { replace: true },
    );
  }

  const seedExport = (
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
        <Button variant="primary" disabled={download.isPending} onClick={() => download.mutate()}>
          {download.isPending ? t("maintainer.seed.exporting") : t("maintainer.seed.export")}
        </Button>
      </div>
      <FormError message={message} />
      {download.isSuccess && (
        <p role="status" className="text-sm text-fg-muted">
          {t("maintainer.seed.done", { file: download.data })}
        </p>
      )}
    </div>
  );

  const write = hasWriteRole(user);
  const content: Record<MaintainerTab, ReactNode> = {
    seed: (
      <>
        {seedExport}
        <SeedRoundTripSection />
      </>
    ),
    revision: (
      <>
        <RevisionOverviewSection />
        {write ? <RevisionLevelsSection /> : null}
      </>
    ),
    data: (
      <>
        <LinkRolesSection />
        <VerseSimilaritySection />
      </>
    ),
    lxx: <LxxSection />,
    vocab: <VocabJobsSection />,
    ai: (
      <>
        <AgentRunsSection />
        <ApiTokensSection />
      </>
    ),
  };

  return (
    <section className="mx-auto flex w-full min-w-0 max-w-4xl flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold">{t("maintainer.title")}</h1>
      {isAdmin(user) ? (
        <p className="text-sm">
          <Link to="/edit/setup" className="text-accent underline">
            {t("setup.link")}
          </Link>
        </p>
      ) : null}
      {active ? (
        <Tabs
          label={t("maintainer.tabs.label")}
          idPrefix="maintainer"
          tabs={tabs.map((key) => ({ key, label: t(`maintainer.tabs.${key}`) }))}
          active={active}
          onChange={selectTab}
        >
          {content[active]}
        </Tabs>
      ) : (
        <p role="alert" className="text-fg-muted">
          {t("admin.forbidden")}
        </p>
      )}
    </section>
  );
}
