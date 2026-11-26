import type { AgentRunEstimate } from "./types";

/** Sum of the estimates of several blocks (one run each): counts and money add up, warnings are merged, caps are per run. */
export function sumEstimates(list: readonly AgentRunEstimate[]): AgentRunEstimate {
  const first = list[0] as AgentRunEstimate;
  const sum = (pick: (e: AgentRunEstimate) => number) => list.reduce((a, e) => a + pick(e), 0);
  return {
    ...first,
    packages: sum((e) => e.packages),
    verses: sum((e) => e.verses),
    skippedVerses: sum((e) => e.skippedVerses),
    estimatedInputTokens: sum((e) => e.estimatedInputTokens),
    estimatedCachedInputTokens: sum((e) => e.estimatedCachedInputTokens),
    estimatedOutputTokens: sum((e) => e.estimatedOutputTokens),
    estimatedTotalTokens: sum((e) => e.estimatedTotalTokens),
    estimatedCostUsd: Math.round(sum((e) => e.estimatedCostUsd) * 10_000) / 10_000,
    exceedsCap: list.some((e) => e.exceedsCap),
    warnings: [...new Set(list.flatMap((e) => e.warnings))],
  };
}
