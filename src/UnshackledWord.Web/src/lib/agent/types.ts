import type {
  VerseMappingGermanWord,
  VerseMappingLink,
  VerseMappingOriginalWord,
} from "@/lib/bible/types";

/** Contracts of the agent endpoints (`api/maintainer/agent-runs`, `api/maintainer/api-tokens`), roadmap 0016. Money is USD. */

/** `lemma` (roadmap 0018): links stay fixed, the model decides lemmas, STTS, roles, Hebrew morphemes and pronoun -> added word. */
export type AgentMode = "redo" | "fill-open" | "lemma";

export type AgentRunStatus =
  | "queued"
  | "running"
  | "waiting-for-provider"
  | "completed"
  | "failed"
  | "cancelled"
  | "budget-exceeded";

/** Statuses of a run that a worker is still on; the UI polls while one of these is shown. */
export const isRunActive = (status: string): boolean =>
  status === "queued" || status === "running" || status === "waiting-for-provider";

/** Statuses a maintainer can continue with "resume". */
export const canResumeRun = (status: string): boolean =>
  status === "failed" || status === "cancelled" || status === "budget-exceeded";

export interface AgentProfile {
  name: string;
  kind: string;
  model: string;
  usable: boolean;
  unusableReason?: string;
  maxTokensPerRun?: number;
  maxCostPerRun?: number;
  maxVersesPerPackage?: number;
  maxParallel: number;
}

/** Range and settings shared by estimate and start; `toChapter` / `toVerse` default on the server. */
export interface AgentRunRangeRequest {
  book: number;
  fromChapter: number;
  fromVerse: number;
  toChapter?: number;
  toVerse?: number;
  profile: string;
  /** Overrides the profile's model. */
  model?: string;
  mode?: AgentMode;
}

export interface StartAgentRunRequest extends AgentRunRangeRequest {
  maxTokens?: number;
  maxCostUsd?: number;
}

export interface ResumeAgentRunRequest {
  maxTokens?: number;
  maxCostUsd?: number;
}

export interface AgentTokenStats {
  germanTokens: number;
  removedPunctuation: number;
  fixed: number;
  hint: number;
  open: number;
  lemmaFixed: number;
  lemmaHint: number;
  lemmaOpen: number;
  verses: number;
  skippedVerses: number;
}

export interface AgentRunEstimate {
  profile: string;
  model: string;
  promptVersion: string;
  mode: string;
  packages: number;
  /** Verses the model is asked about. */
  verses: number;
  skippedVerses: number;
  prefixTokens: number;
  estimatedInputTokens: number;
  estimatedCachedInputTokens: number;
  estimatedOutputTokens: number;
  /** Input plus output including the retry margin: the number compared with the cap. */
  estimatedTotalTokens: number;
  estimatedCostUsd: number;
  maxTokens?: number;
  maxCostUsd?: number;
  exceedsCap: boolean;
  stats: AgentTokenStats;
  warnings: string[];
}

export interface AgentRun {
  id: number;
  profile: string;
  model: string;
  promptVersion: string;
  book: number;
  /** BBCCCVVV */
  fromRefId: number;
  toRefId: number;
  mode: string;
  status: AgentRunStatus;
  statusNote?: string;
  inputTokens: number;
  cachedInputTokens: number;
  outputTokens: number;
  costUsd: number;
  cacheRate: number;
  estimatedTokens?: number;
  estimatedCostUsd?: number;
  maxTokens?: number;
  maxCostUsd?: number;
  createdAt: string;
  startedAt?: string;
  finishedAt?: string;
  packages: number;
  packagesCompleted: number;
  packagesFailed: number;
  proposedVerses: number;
  flaggedVerses: number;
  /** Proposals of the run that still wait for a review. */
  openProposals: number;
  /** A worker of the API process is running it. */
  active: boolean;
}

export interface ListAgentRunsResponse {
  runs: AgentRun[];
}

export interface ApiToken {
  id: number;
  userId: number;
  name: string;
  /** First characters of the token, to tell tokens apart. */
  prefix: string;
  scope: string;
  createdByUserId: number;
  createdAt: string;
  expiresAt: string;
  revokedAt?: string;
  lastUsedAt?: string;
  active: boolean;
}

export type ApiTokenState = "active" | "revoked" | "expired";

/** A revoked token stays revoked even when it would also have expired. */
export const apiTokenState = (token: Pick<ApiToken, "active" | "revokedAt">): ApiTokenState =>
  token.revokedAt ? "revoked" : token.active ? "active" : "expired";

export interface ListApiTokensResponse {
  tokens: ApiToken[];
}

export interface ApiTokenUser {
  id: number;
  name: string;
  roles: string[];
}

export interface ListApiTokenUsersResponse {
  users: ApiTokenUser[];
}

/** Either `userId` or `model` (the server then uses / creates the user `ai:<model>`). */
export interface CreateApiTokenRequest {
  userId?: number;
  model?: string;
  name: string;
  expiresInDays?: number;
}

export interface CreateApiTokenResponse {
  token: ApiToken;
  /** The token itself, shown once. */
  secret: string;
}

export type AgentTone = "neutral" | "info" | "ok" | "warning" | "error";

/** Tone of a run's status badge. */
export function runTone(status: string): AgentTone {
  switch (status) {
    case "completed":
      return "ok";
    case "failed":
      return "error";
    case "budget-exceeded":
      return "warning";
    case "cancelled":
      return "neutral";
    default:
      return "info";
  }
}

/** Status values of a proposal (`AgentProposalStatuses`). */
export type ProposalStatus = "open" | "merged" | "edited-merged" | "discarded" | "superseded";

export const PROPOSAL_STATUSES: readonly ProposalStatus[] = [
  "open",
  "merged",
  "edited-merged",
  "discarded",
  "superseded",
];

/** Tone of a proposal's status badge. */
export function proposalTone(status: string): AgentTone {
  switch (status) {
    case "open":
      return "info";
    case "merged":
    case "edited-merged":
      return "ok";
    case "discarded":
      return "neutral";
    default:
      return "warning";
  }
}

export interface AgentProposalSummary {
  id: number;
  runId: number;
  book: number;
  chapter: number;
  verse: number;
  status: ProposalStatus;
  flagCount: number;
  hasLemmaChanges: boolean;
  /** Lemma mode: roles, morphemes, part lemmas or pronoun -> added word change. */
  hasRoleChanges?: boolean;
  hasParts: boolean;
  hasNote: boolean;
  createdAt: string;
  reviewedByUserId?: number;
  reviewedAt?: string;
}

export interface ListAgentProposalsResponse {
  runId: number;
  proposals: AgentProposalSummary[];
}

/** A link of the proposal compared with the live mapping (German and original word included). */
export interface AgentLinkDiff {
  elbWordId: number;
  german: string;
  stepWordId?: number;
  original?: string;
  isAddedWord: boolean;
  parentElbWordId?: number;
  parentGerman?: string;
  partOrder?: number;
  germanWordPart?: string;
}

export interface AgentLemmaDiff {
  elbWordId: number;
  german: string;
  currentLemma?: string;
  currentStts?: string;
  beforeLemma?: string;
  lemma: string;
  stts?: string;
  partOfSpeech?: string;
  /** The live lemma is not the one the proposal started from: a plain merge is refused. */
  changed: boolean;
}

/** A word of the verse with its live lemma; `proposed` = the proposal changes it. */
export interface AgentVerseLemma {
  elbWordId: number;
  german: string;
  lemma?: string;
  stts?: string;
  proposed: boolean;
}

/** One link change of a lemma-mode proposal; `before*` = stored when the package was built. */
export interface AgentRoleDiff {
  elbWordId: number;
  german: string;
  stepWordId?: number;
  original?: string;
  /** `head` or `unit`; absent when the link becomes an added word */
  role?: string;
  /** picked Hebrew morpheme; absent = the main morpheme */
  strongsNumber?: string;
  multiHead: boolean;
  partOrder?: number;
  partLemma?: string;
  beforeRole?: string;
  beforeStrongsNumber?: string;
  beforeMultiHead: boolean;
  beforePartLemma?: string;
  /** The linked word becomes an added word with this parent. */
  addedParentElbWordId?: number;
  addedParentGerman?: string;
  /** `model` or `rules` (the deterministic role rules) */
  source: string;
}

export interface AgentFlagDiff {
  code: string;
  message: string;
  elbWordId?: number;
  stepWordId?: number;
}

export interface AgentProposalDiff {
  id: number;
  runId: number;
  profile: string;
  model: string;
  aiUserId: number;
  book: number;
  chapter: number;
  verse: number;
  language: "greek" | "hebrew";
  status: ProposalStatus;
  note?: string;
  reviewedByUserId?: number;
  reviewedAt?: string;
  stale: boolean;
  staleReason?: string;
  germanWords: VerseMappingGermanWord[];
  originalWords: VerseMappingOriginalWord[];
  unchanged: AgentLinkDiff[];
  added: AgentLinkDiff[];
  removed: AgentLinkDiff[];
  proposedLinks: VerseMappingLink[];
  /** The live links with their role fields (older API: absent, then the diff links are used). */
  currentLinks?: VerseMappingLink[];
  lemmaChanges: AgentLemmaDiff[];
  /** Every word of the verse with its live lemma (older API: absent). */
  verseLemmas?: AgentVerseLemma[];
  /** Lemma mode only (empty otherwise). */
  roleChanges?: AgentRoleDiff[];
  flags: AgentFlagDiff[];
}

export interface AgentLemmaEdit {
  elbWordId: number;
  lemma: string;
  stts?: string;
  partOfSpeech?: string;
}

export interface EditMergeRequest {
  links: VerseMappingLink[];
  lemmas: AgentLemmaEdit[];
  clearParts: boolean;
}

export interface MergeProposalResponse {
  id: number;
  status: string;
  authorUserId: number;
  reviewerUserId: number;
  linksWritten: number;
  lemmasWritten: number;
}

export interface DiscardProposalResponse {
  id: number;
  status: string;
}

export interface BulkMergeFailure {
  id: number;
  chapter: number;
  verse: number;
  /** stale, invalid, forbidden or not-open */
  reason: string;
  messages: string[];
}

export interface BulkMergeResponse {
  runId: number;
  merged: number;
  skippedFlagged: number;
  failed: BulkMergeFailure[];
}

/** Scorer result of a run (`GET maintainer/agent-runs/{id}/score`, roadmap 0018.10); mirrors `AgentScoreContracts.cs`. */
export interface AgentScoreRatio {
  total: number;
  correct: number;
  accuracy?: number;
}

export interface AgentScoreOverlap {
  expected: number;
  actual: number;
  matched: number;
  precision?: number;
  recall?: number;
}

export interface AgentScoreCheck {
  code: string;
  value?: number;
  required: number;
  ok: boolean;
}

export interface AgentScoreError {
  item: string;
  text?: string;
  expected?: string;
  actual?: string;
}

export interface AgentScoreVerse {
  ref: string;
  bibleBookId: number;
  chapter: number;
  verse: number;
  /** `proposal`, `silent` or `no-proposal` */
  status: string;
  lemmaErrors: AgentScoreError[];
  roleErrors: AgentScoreError[];
  missingLinks: AgentScoreError[];
  extraLinks: AgentScoreError[];
  flags: string[];
  inputTokens: number;
  outputTokens: number;
  costUsd: number;
}

export interface AgentScore {
  runId: number;
  mode: string;
  profile: string;
  model: string;
  promptVersion: string;
  runStatus: string;
  referenceVersion: string;
  computedAt: string;
  verses: {
    reference: number;
    scored: number;
    notInRun: number;
    withProposal: number;
    silent: number;
    hardRejections: number;
    extraRunVerses: number;
  };
  lemmas: AgentScoreRatio;
  stts: AgentScoreRatio;
  partLemmas: AgentScoreRatio;
  roles: AgentScoreRatio;
  morphemes: AgentScoreRatio;
  roleMorphemes: AgentScoreRatio;
  links: AgentScoreOverlap;
  addedWords: AgentScoreOverlap;
  flags: {
    total: number;
    versesFlagged: number;
    perVerse?: number;
    byCode: Record<string, number>;
  };
  cost: {
    inputTokens: number;
    cachedInputTokens: number;
    outputTokens: number;
    costUsd: number;
    verses: number;
    inputTokensPerVerse?: number;
    outputTokensPerVerse?: number;
    costUsdPerVerse?: number;
  };
  bulkMerge: { meets: boolean; checks: AgentScoreCheck[]; reasons: string[] };
  perVerse: AgentScoreVerse[];
}
