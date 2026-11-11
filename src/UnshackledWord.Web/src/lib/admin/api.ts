import { api } from "@/lib/api";

/** Mirrors lexio `Admin.ListInviteCodes.InviteCodeSummary`; nulls are omitted by the API. */
export interface InviteCodeSummary {
  id: number;
  code: string;
  /** Set only for an emailed invitation, absent for a shared code. */
  boundEmail?: string | null;
  maxSeats: number;
  seatsUsed: number;
  expiresAt: string;
  revokedAt?: string | null;
  note?: string | null;
  createdAt: string;
  createdByDisplayName: string;
  redeemedByDisplayNames: string[];
}

/** Mirrors lexio `Admin.CreateInviteCode.Request`: seats and expiry are both required. */
export interface CreateInviteCodeRequest {
  maxSeats: number;
  expiresAt: string;
  note?: string;
}

export interface CreateInviteCodeResponse {
  id: number;
  code: string;
  maxSeats: number;
  expiresAt: string;
  note?: string | null;
}

export type InviteCodeStatus = "active" | "revoked" | "expired" | "full";

export function inviteCodeStatus(code: InviteCodeSummary, now = new Date()): InviteCodeStatus {
  if (code.revokedAt) return "revoked";
  if (new Date(code.expiresAt) <= now) return "expired";
  if (code.seatsUsed >= code.maxSeats) return "full";
  return "active";
}

/** Typed wrappers for `/api/admin/invite-codes` (lexio shapes; admin role required). */
export const inviteCodesApi = {
  list: () => api.get<{ inviteCodes: InviteCodeSummary[] }>("/admin/invite-codes"),
  create: (body: CreateInviteCodeRequest) =>
    api.post<CreateInviteCodeResponse>("/admin/invite-codes", body),
  revoke: (id: number) => api.delete<void>(`/admin/invite-codes/${id}`),
};
