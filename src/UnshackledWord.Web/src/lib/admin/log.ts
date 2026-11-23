import { api } from "@/lib/api";

/** Actions written to the admin log; mirrors `Application.Features.UserAdmin.AdminActions.All` (roadmap 0020, 05-api.md; note review actions from roadmap 0023.5). */
export const ADMIN_ACTIONS = [
  "user_created",
  "user_updated",
  "role_granted",
  "role_revoked",
  "user_deactivated",
  "user_reactivated",
  "password_reset_sent",
  "welcome_sent",
  "invite_created",
  "invite_revoked",
  "invite_emailed",
  "note_approved",
  "note_rejected",
  "note_hidden",
  "note_unhidden",
] as const;

export type AdminAction = (typeof ADMIN_ACTIONS)[number];

/** Mirrors `Domain.WebApi.UserAdmin.AdminLogItem`; nulls are omitted by the API. */
export interface AdminLogItem {
  id: number;
  createdAt: string;
  action: string;
  actorUserId: number;
  actorDisplayName: string;
  targetUserId?: number | null;
  targetDisplayName?: string | null;
  inviteCodeId?: number | null;
  details?: unknown;
}

export interface AdminLogResponse {
  items: AdminLogItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AdminLogQuery {
  userId?: number;
  action?: string;
  page: number;
  pageSize?: number;
}

export const ADMIN_LOG_PAGE_SIZE = 50;

export function adminLogQueryString(query: AdminLogQuery): string {
  const params = new URLSearchParams();
  if (query.userId !== undefined) params.set("userId", String(query.userId));
  if (query.action) params.set("action", query.action);
  params.set("page", String(query.page));
  params.set("pageSize", String(query.pageSize ?? ADMIN_LOG_PAGE_SIZE));
  return params.toString();
}

/** Typed wrapper for `/api/admin/log` (admin role required). */
export const adminLogApi = {
  list: (query: AdminLogQuery, signal?: AbortSignal) =>
    api.get<AdminLogResponse>(`/admin/log?${adminLogQueryString(query)}`, signal),
};

/** A positive integer from a query-string value, or undefined for anything else. */
export function parseUserId(value: string | null): number | undefined {
  if (!value || !/^\d+$/.test(value)) return undefined;
  const id = Number(value);
  return id > 0 ? id : undefined;
}

/** One line of `key: value` pairs for the log's details (the shape depends on the action); "" when empty. */
export function detailsSummary(details: unknown): string {
  if (details === null || details === undefined) return "";
  if (typeof details === "string") return details;
  if (typeof details === "number" || typeof details === "boolean") return String(details);
  if (Array.isArray(details)) {
    return details
      .map((value) => detailsSummary(value))
      .filter(Boolean)
      .join(", ");
  }
  if (typeof details === "object") {
    return Object.entries(details)
      .map(([key, value]) => [key, detailsSummary(value)] as const)
      .filter(([, value]) => value !== "")
      .map(([key, value]) => `${key}: ${value}`)
      .join("; ");
  }
  return "";
}
