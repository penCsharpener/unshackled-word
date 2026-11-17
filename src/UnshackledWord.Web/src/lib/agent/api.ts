import { api } from "@/lib/api";
import type {
  AgentProfile,
  AgentProposalDiff,
  AgentRun,
  AgentRunEstimate,
  AgentRunRangeRequest,
  AgentScore,
  BulkMergeResponse,
  CreateApiTokenRequest,
  CreateApiTokenResponse,
  DiscardProposalResponse,
  EditMergeRequest,
  ListAgentProposalsResponse,
  ListAgentRunsResponse,
  ListApiTokensResponse,
  ListApiTokenUsersResponse,
  MergeProposalResponse,
  ResumeAgentRunRequest,
  StartAgentRunRequest,
} from "./types";

const runs = "/maintainer/agent-runs";
const tokens = "/maintainer/api-tokens";
const proposals = "/maintainer/agent-proposals";

export const agentApi = {
  profiles: (signal?: AbortSignal) => api.get<AgentProfile[]>(`${runs}/profiles`, signal),
  estimate: (body: AgentRunRangeRequest, signal?: AbortSignal) =>
    api.post<AgentRunEstimate>(`${runs}/estimate`, body, signal),
  start: (body: StartAgentRunRequest) => api.post<AgentRun>(runs, body),
  list: (limit: number, signal?: AbortSignal) =>
    api.get<ListAgentRunsResponse>(`${runs}?limit=${limit}`, signal),
  get: (id: number, signal?: AbortSignal) => api.get<AgentRun>(`${runs}/${id}`, signal),
  score: (id: number, reference?: string, signal?: AbortSignal) =>
    api.get<AgentScore>(
      `${runs}/${id}/score${reference ? `?reference=${encodeURIComponent(reference)}` : ""}`,
      signal,
    ),
  storedScore: (id: number, signal?: AbortSignal) =>
    api.get<AgentScore>(`${runs}/${id}/score/stored`, signal),
  cancel: (id: number) => api.post<AgentRun>(`${runs}/${id}/cancel`, {}),
  resume: (id: number, body: ResumeAgentRunRequest) =>
    api.post<AgentRun>(`${runs}/${id}/resume`, body),

  listProposals: (runId: number, signal?: AbortSignal) =>
    api.get<ListAgentProposalsResponse>(`${runs}/${runId}/proposals`, signal),
  proposal: (id: number, signal?: AbortSignal) =>
    api.get<AgentProposalDiff>(`${proposals}/${id}`, signal),
  merge: (id: number) => api.post<MergeProposalResponse>(`${proposals}/${id}/merge`, {}),
  editMerge: (id: number, body: EditMergeRequest) =>
    api.post<MergeProposalResponse>(`${proposals}/${id}/edit-merge`, body),
  discard: (id: number) => api.post<DiscardProposalResponse>(`${proposals}/${id}/discard`, {}),
  mergeUnflagged: (runId: number) =>
    api.post<BulkMergeResponse>(`${runs}/${runId}/proposals/merge-unflagged`, { confirm: true }),

  listTokens: (signal?: AbortSignal) => api.get<ListApiTokensResponse>(tokens, signal),
  tokenUsers: (signal?: AbortSignal) =>
    api.get<ListApiTokenUsersResponse>(`${tokens}/users`, signal),
  createToken: (body: CreateApiTokenRequest) => api.post<CreateApiTokenResponse>(tokens, body),
  revokeToken: (id: number) => api.post<void>(`${tokens}/${id}/revoke`, {}),
};
