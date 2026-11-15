import { useTranslation } from "react-i18next";
import { StatTile } from "@/components/StatTile";
import { formatNumber } from "@/lib/chart";
import {
  type CheckGroupKey,
  checkGroup,
  deltaOf,
  tileSeverity,
  visibleChecks,
} from "@/lib/quality/checks";
import { SECTION_IDS, scrollToId } from "@/lib/quality/scroll";
import type { OverviewResponse } from "@/lib/quality/types";
import { CheckHelp, SectionHelp } from "./SectionHelp";

const GROUPS: CheckGroupKey[] = ["mapping", "lemma"];

export function OverviewSection({
  overview,
  selectedCheck,
  onSelectCheck,
}: {
  overview: OverviewResponse;
  selectedCheck?: string;
  onSelectCheck: (code: string) => void;
}) {
  const { t, i18n } = useTranslation();
  const checks = visibleChecks(overview.checks);
  const title = (code: string) => t(`quality.checks.${code}.title`, { defaultValue: code });

  return (
    <section aria-labelledby="quality-overview" className="grid gap-4">
      <h2 id="quality-overview" className="text-lg font-semibold">
        {t("quality.sections.overview")}
      </h2>
      <SectionHelp>
        <p>{t("quality.help.overview.intro")}</p>
        {GROUPS.map((group) => {
          const items = checks.filter((check) => checkGroup(check.code) === group);
          if (items.length === 0) return null;
          return (
            <div key={group} className="grid gap-2">
              <p className="font-medium text-fg">{t(`quality.help.overview.${group}`)}</p>
              <dl className="grid gap-3">
                {items.map((check) => (
                  <div key={check.code} className="grid gap-1">
                    <dt className="font-medium text-fg">
                      {check.code} · {title(check.code)}
                    </dt>
                    <dd className="grid gap-1">
                      <CheckHelp code={check.code} />
                    </dd>
                  </div>
                ))}
              </dl>
            </div>
          );
        })}
      </SectionHelp>
      {GROUPS.map((group) => {
        const items = checks.filter((check) => checkGroup(check.code) === group);
        if (items.length === 0) return null;
        return (
          <div key={group} className="grid gap-2">
            <h3 className="text-sm font-medium text-fg-muted">{t(`quality.groups.${group}`)}</h3>
            <div className="grid grid-cols-2 gap-3 @2xl:grid-cols-3 @6xl:grid-cols-6">
              {items.map((check) => {
                const severity = tileSeverity(check);
                const delta = deltaOf(check);
                return (
                  <StatTile
                    key={check.code}
                    code={check.code}
                    title={title(check.code)}
                    value={formatNumber(check.count, i18n.language)}
                    sub={
                      check.sub
                        ? t(`quality.sub.${check.sub.key}`, {
                            count: check.sub.count,
                            defaultValue: "{{count, number}}",
                          })
                        : undefined
                    }
                    delta={
                      delta
                        ? t("quality.delta", {
                            sign: delta > 0 ? "+" : "−",
                            count: Math.abs(delta),
                          })
                        : undefined
                    }
                    severity={severity}
                    severityLabel={t(`quality.severity.${severity}`)}
                    selected={selectedCheck === check.code}
                    onSelect={() => {
                      if (check.code === "C1") {
                        scrollToId(SECTION_IDS.coverage);
                        return;
                      }
                      onSelectCheck(check.code);
                      scrollToId(SECTION_IDS.findings);
                    }}
                  />
                );
              })}
            </div>
          </div>
        );
      })}
    </section>
  );
}
