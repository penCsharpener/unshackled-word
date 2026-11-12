import type { TFunction } from "i18next";
import { useCallback } from "react";
import type { FindingJump } from "@/components/FindingsTable";
import { filterForFinding, jumpTargets } from "@/lib/quality/checks";
import { SECTION_IDS, scrollToId } from "@/lib/quality/scroll";
import type { Finding } from "@/lib/quality/types";
import type { TrailEntry } from "@/lib/quality/url";

/** Jump buttons of a finding: fix places open in a new tab, "Filter" sets the explorer on this page. */
export function useJumps(t: TFunction, openFilter: (entry: TrailEntry) => void) {
  return useCallback(
    (finding: Finding): FindingJump[] => {
      const jumps: FindingJump[] = jumpTargets(finding).map((target) => ({
        key: target.kind,
        label: t(`quality.jump.${target.kind}`),
        href: target.href,
      }));
      const filter = filterForFinding(finding);
      if (filter) {
        jumps.push({
          key: "filter",
          label: t("quality.jump.filter"),
          onClick: () => {
            openFilter(filter);
            scrollToId(SECTION_IDS.explorer);
          },
        });
      }
      return jumps;
    },
    [t, openFilter],
  );
}
