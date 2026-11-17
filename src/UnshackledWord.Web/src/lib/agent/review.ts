import type { VerseMappingLink } from "@/lib/bible/types";
import type {
  AgentLinkDiff,
  AgentProposalDiff,
  AgentProposalSummary,
  AgentRoleDiff,
  ProposalStatus,
} from "./types";

/** Status filter of the list: one status, or every one. */
export type StatusFilter = ProposalStatus | "all";

export function filterProposals(
  proposals: readonly AgentProposalSummary[],
  status: StatusFilter,
  flaggedOnly: boolean,
  /** Only lemma-mode proposals that change roles, morphemes, part lemmas or turn a pronoun into an added word. */
  rolesOnly = false,
): AgentProposalSummary[] {
  return proposals.filter(
    (p) =>
      (status === "all" || p.status === status) &&
      (!flaggedOnly || p.flagCount > 0) &&
      (!rolesOnly || p.hasRoleChanges === true),
  );
}

/** Open proposals the bulk merge takes (no flags) and the open ones it leaves for a look. */
export function bulkCounts(proposals: readonly AgentProposalSummary[]) {
  const open = proposals.filter((p) => p.status === "open");
  const unflagged = open.filter((p) => p.flagCount === 0).length;
  return { unflagged, flagged: open.length - unflagged };
}

/**
 * The open proposal to show after `currentId` was merged or discarded: the next open one in list order, else the closest
 * earlier one; null when none is left.
 */
export function nextOpenProposal(
  proposals: readonly AgentProposalSummary[],
  currentId: number,
): number | null {
  const index = proposals.findIndex((p) => p.id === currentId);
  const open = (p: AgentProposalSummary) => p.status === "open" && p.id !== currentId;
  const after = proposals.slice(index + 1).find(open);
  if (after) return after.id;
  const before = proposals.slice(0, Math.max(index, 0)).findLast(open);
  return before?.id ?? null;
}

/** A diff link as the mapping editor and the save endpoints know it (the German / original texts are dropped). */
export function toMappingLink(link: AgentLinkDiff): VerseMappingLink {
  return {
    elbWordId: link.elbWordId,
    isAddedWord: link.isAddedWord,
    ...(link.stepWordId === undefined ? {} : { stepWordId: link.stepWordId }),
    ...(link.parentElbWordId === undefined ? {} : { parentElbWordId: link.parentElbWordId }),
    ...(link.partOrder === undefined ? {} : { partOrder: link.partOrder }),
    ...(link.germanWordPart === undefined ? {} : { germanWordPart: link.germanWordPart }),
  };
}

/**
 * The live links as the editor's base: `currentLinks` carries the stored roles and morphemes, so a role-aware edit-merge compares
 * against them; an older API without it falls back to the diff links (no role fields).
 */
export function liveLinks(
  diff: Pick<AgentProposalDiff, "currentLinks" | "unchanged" | "removed">,
): VerseMappingLink[] {
  return diff.currentLinks ?? [...diff.unchanged, ...diff.removed].map(toMappingLink);
}

/** What a role change does, for the review table: became an added word, role, morpheme, multi-head and part-lemma changes. */
export interface RoleChangeParts {
  added: boolean;
  role: boolean;
  morpheme: boolean;
  multiHead: boolean;
  partLemma: boolean;
}

export function roleChangeParts(change: AgentRoleDiff): RoleChangeParts {
  const added = change.role === undefined;
  return {
    added,
    role: !added && (change.beforeRole ?? "head") !== change.role,
    morpheme: !added && (change.beforeStrongsNumber ?? "") !== (change.strongsNumber ?? ""),
    multiHead: !added && change.beforeMultiHead !== change.multiHead,
    partLemma: !added && (change.beforePartLemma ?? "") !== (change.partLemma ?? ""),
  };
}

/**
 * Keys that act on the review page (j/k, m, e, d, ?) stay out of the way while the maintainer types or works in an editor,
 * while a dialog is open, and with any modifier held (browser and system shortcuts).
 */
export function ignoresReviewKey(event: KeyboardEvent): boolean {
  if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey) return true;
  const target = event.target;
  if (target instanceof HTMLElement) {
    if (target.isContentEditable) return true;
    if (
      target.closest("input, textarea, select, [contenteditable='true'], [data-mapping-editor]")
    ) {
      return true;
    }
  }
  return document.querySelector("dialog[open]") !== null;
}
