import {
  keepPreviousData,
  queryOptions,
  useMutation,
  useQueries,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { refreshAfterMappingChange } from "@/lib/bible/verseMapping";
import { lemmaKeys } from "@/lib/lemmas/queries";
import { agentApi } from "./api";
import {
  type AgentRun,
  type AgentRunRangeRequest,
  type CreateApiTokenRequest,
  type EditMergeRequest,
  isRunActive,
  type ResumeAgentRunRequest,
  type StartAgentRunRequest,
} from "./types";

export const POLL_MS = 2000;
const RUN_LIST_LIMIT = 50;

export const agentKeys = {
  all: ["maintainer", "agent"] as const,
  profiles: ["maintainer", "agent", "profiles"] as const,
  estimate: (request: AgentRunRangeRequest) =>
    ["maintainer", "agent", "estimate", request] as const,
  runs: ["maintainer", "agent", "runs"] as const,
  score: (id: number) => ["maintainer", "agent", "score", id] as const,
  run: (id: number) => ["maintainer", "agent", "run", id] as const,
  proposals: (runId: number) => ["maintainer", "agent", "proposals", runId] as const,
  proposal: (id: number) => ["maintainer", "agent", "proposal", id] as const,
  tokens: ["maintainer", "agent", "tokens"] as const,
  tokenUsers: ["maintainer", "agent", "tokenUsers"] as const,
};

/** Provider profiles; unusable ones come with a reason (no API key). */
export function useAgentProfiles(enabled = true) {
  return useQuery({
    queryKey: agentKeys.profiles,
    queryFn: ({ signal }) => agentApi.profiles(signal),
    enabled,
  });
}

/**
 * Estimate of a run (a POST that calls no model and stores nothing), kept only while the start dialog is open.
 * `request` is null until the range is valid; the previous estimate stays visible while a new one loads.
 */
export function useAgentEstimate(request: AgentRunRangeRequest | null, enabled: boolean) {
  return useQuery({
    queryKey: agentKeys.estimate(request ?? { book: 0, fromChapter: 0, fromVerse: 0, profile: "" }),
    queryFn: ({ signal }) => agentApi.estimate(request as AgentRunRangeRequest, signal),
    enabled: enabled && request !== null,
    staleTime: 30_000,
    gcTime: 60_000,
    retry: false,
    placeholderData: keepPreviousData,
  });
}

/** One estimate per request (multi-block runs); `requests` empty or null disables all. Same cache entries as `useAgentEstimate`. */
export function useAgentEstimates(requests: AgentRunRangeRequest[] | null) {
  return useQueries({
    queries: (requests ?? []).map((request) => ({
      queryKey: agentKeys.estimate(request),
      queryFn: ({ signal }: { signal: AbortSignal }) => agentApi.estimate(request, signal),
      staleTime: 30_000,
      gcTime: 60_000,
      retry: false,
    })),
  });
}

/** Runs of the last days; refetches every 2 s while one is queued or running. */
export function useAgentRuns(enabled = true) {
  return useQuery(
    queryOptions({
      queryKey: agentKeys.runs,
      queryFn: ({ signal }) => agentApi.list(RUN_LIST_LIMIT, signal),
      select: (response) => response.runs,
      enabled,
      staleTime: 0,
      refetchInterval: (query) =>
        query.state.data?.runs.some((run) => isRunActive(run.status)) ? POLL_MS : false,
    }),
  );
}

/** One run; refetches every 2 s while it is queued or running. */
export function useAgentRun(id: number | null) {
  return useQuery({
    queryKey: agentKeys.run(id ?? 0),
    queryFn: ({ signal }) => agentApi.get(id as number, signal),
    enabled: id !== null,
    staleTime: 0,
    refetchInterval: (query) =>
      query.state.data && isRunActive(query.state.data.status) ? POLL_MS : false,
  });
}

/** Puts a changed run into the cache and refreshes the list, so the next poll continues from it. */
function useStoreRun() {
  const queryClient = useQueryClient();
  return (run: AgentRun) => {
    queryClient.setQueryData(agentKeys.run(run.id), run);
    return queryClient.invalidateQueries({ queryKey: agentKeys.runs });
  };
}

export function useStartAgentRun() {
  const store = useStoreRun();
  return useMutation({
    mutationFn: (body: StartAgentRunRequest) => agentApi.start(body),
    onSuccess: store,
  });
}

export function useCancelAgentRun() {
  const store = useStoreRun();
  return useMutation({ mutationFn: (id: number) => agentApi.cancel(id), onSuccess: store });
}

export function useResumeAgentRun() {
  const store = useStoreRun();
  return useMutation({
    mutationFn: ({ id, ...body }: ResumeAgentRunRequest & { id: number }) =>
      agentApi.resume(id, body),
    onSuccess: store,
  });
}

export function useApiTokens(enabled = true) {
  return useQuery({
    queryKey: agentKeys.tokens,
    queryFn: ({ signal }) => agentApi.listTokens(signal),
    select: (response) => response.tokens,
    enabled,
    staleTime: 0,
  });
}

/** Users a token can be issued for; `ai:<model>` users come first. */
export function useApiTokenUsers(enabled = true) {
  return useQuery({
    queryKey: agentKeys.tokenUsers,
    queryFn: ({ signal }) => agentApi.tokenUsers(signal),
    select: (response) => response.users,
    enabled,
    staleTime: 0,
  });
}

/**
 * Creates a token. The response carries the secret: it is never cached (`gcTime: 0`) and is handed to the caller's
 * `onSuccess` only; call `reset()` once it sits in component state. Only the token list is invalidated.
 */
export function useCreateApiToken() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateApiTokenRequest) => agentApi.createToken(body),
    gcTime: 0,
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: agentKeys.tokens }),
        queryClient.invalidateQueries({ queryKey: agentKeys.tokenUsers }),
      ]),
  });
}

export function useRevokeApiToken() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => agentApi.revokeToken(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: agentKeys.tokens }),
  });
}

/** Every proposal of a run (all statuses; the page filters), newest verse order as the server sends it. */
export function useAgentProposals(runId: number | null) {
  return useQuery({
    queryKey: agentKeys.proposals(runId ?? 0),
    queryFn: ({ signal }) => agentApi.listProposals(runId as number, signal),
    select: (response) => response.proposals,
    enabled: runId !== null,
    staleTime: 0,
  });
}

/** One proposal against the live verse; never reused, so a review always sees the live state of the words. */
export function useAgentProposal(id: number | null) {
  return useQuery({
    queryKey: agentKeys.proposal(id ?? 0),
    queryFn: ({ signal }) => agentApi.proposal(id as number, signal),
    enabled: id !== null,
    staleTime: 0,
    gcTime: 0,
    refetchOnWindowFocus: false,
  });
}

export interface ReviewedVerse {
  runId: number;
  book: number;
  chapter: number;
  verse: number;
}

/** What shows a merged verse (chapter words, side panel, word details, lemma grid) plus the lists of the review page. */
export function useRefreshAfterReview() {
  const queryClient = useQueryClient();
  return async (verse: ReviewedVerse) => {
    await Promise.all([
      refreshAfterMappingChange(queryClient, {
        bookId: verse.book,
        chapter: verse.chapter,
        verse: verse.verse,
      }),
      queryClient.invalidateQueries({ queryKey: lemmaKeys.chapter(verse.book, verse.chapter) }),
      queryClient.invalidateQueries({ queryKey: agentKeys.proposals(verse.runId) }),
      queryClient.invalidateQueries({ queryKey: agentKeys.run(verse.runId) }),
      queryClient.invalidateQueries({ queryKey: agentKeys.runs }),
    ]);
  };
}

/** Plain merge: the proposal's links and lemmas are written as they are (author = the AI user). */
export function useMergeProposal() {
  return useMutation({ mutationFn: (id: number) => agentApi.merge(id) });
}

/** Edit-merge: the maintainer's links and lemmas replace the proposal's (author = the maintainer). */
export function useEditMergeProposal() {
  return useMutation({
    mutationFn: ({ id, ...body }: EditMergeRequest & { id: number }) =>
      agentApi.editMerge(id, body),
  });
}

/** Discard has no undo. */
export function useDiscardProposal() {
  return useMutation({ mutationFn: (id: number) => agentApi.discard(id) });
}

/** Merges every open unflagged proposal of the run; the caller shows the confirmation first. */
export function useBulkMergeProposals() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (runId: number) => agentApi.mergeUnflagged(runId),
    onSuccess: async (response) => {
      // Many verses may have changed: drop the cached chapter, mapping and lemma data of the book wholesale.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["bible"] }),
        queryClient.invalidateQueries({ queryKey: ["lemmas"] }),
        queryClient.invalidateQueries({ queryKey: agentKeys.proposals(response.runId) }),
        queryClient.invalidateQueries({ queryKey: agentKeys.run(response.runId) }),
        queryClient.invalidateQueries({ queryKey: agentKeys.runs }),
      ]);
    },
  });
}

/**
 * Score of a run. Never fetched on its own: `refetch()` ("Bewerten") computes it against the newest reference file and the
 * server stores it with the run; the result stays in the cache while the page is open.
 */
export function useAgentScore(runId: number) {
  return useQuery({
    queryKey: agentKeys.score(runId),
    queryFn: ({ signal }) => agentApi.score(runId, undefined, signal),
    enabled: false,
    retry: false,
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: 30 * 60_000,
  });
}

/** The score stored with the run by its last scoring (404 = never scored, then the panel just offers "Bewerten"). */
export function useStoredAgentScore(runId: number) {
  return useQuery({
    queryKey: [...agentKeys.score(runId), "stored"] as const,
    queryFn: ({ signal }) => agentApi.storedScore(runId, signal),
    retry: false,
    staleTime: Number.POSITIVE_INFINITY,
  });
}
