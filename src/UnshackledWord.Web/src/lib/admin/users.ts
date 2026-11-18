import type { TFunction } from "i18next";
import { ApiError, api } from "@/lib/api";

export type UserStatus = "active" | "deactivated" | "ai";

/** Mirrors `Domain.WebApi.UserAdmin.UserListItem` (roadmap 0020, 05-api.md). */
export interface UserListItem {
  id: number;
  email: string;
  displayName: string;
  roles: string[];
  status: UserStatus;
  createdAt: string;
  lastSignInAt?: string | null;
}

export interface UserListResponse {
  items: UserListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface DeactivationInfo {
  at: string;
  byUserId: number;
  byDisplayName: string;
  reason?: string | null;
}

/** Mirrors `UserDetail`; `isSelf` and `isLastActiveAdmin` only drive disabled controls. */
export interface UserDetail extends UserListItem {
  emailVerified: boolean;
  hasPassword: boolean;
  passkeyCount: number;
  deactivation?: DeactivationInfo | null;
  isSelf: boolean;
  isLastActiveAdmin: boolean;
}

export interface UserListQuery {
  search: string;
  role: string;
  status: string;
  page: number;
  pageSize?: number;
}

/** Roles an admin can grant; `user` is implicit for everyone. */
export const ASSIGNABLE_ROLES = ["admin", "maintainer", "reviewer"] as const;
export type AssignableRole = (typeof ASSIGNABLE_ROLES)[number];

export const USER_PAGE_SIZE = 50;

export function listQueryString(query: UserListQuery): string {
  const params = new URLSearchParams();
  if (query.search.trim()) params.set("search", query.search.trim());
  if (query.role) params.set("role", query.role);
  if (query.status) params.set("status", query.status);
  params.set("page", String(query.page));
  params.set("pageSize", String(query.pageSize ?? USER_PAGE_SIZE));
  return params.toString();
}

/** Typed wrappers for `/api/admin/users` (admin role required). */
export const adminUsersApi = {
  list: (query: UserListQuery, signal?: AbortSignal) =>
    api.get<UserListResponse>(`/admin/users?${listQueryString(query)}`, signal),
  get: (id: number, signal?: AbortSignal) => api.get<UserDetail>(`/admin/users/${id}`, signal),
  setRoles: (id: number, roles: string[]) =>
    api.put<UserDetail>(`/admin/users/${id}/roles`, { roles }),
  create: (request: CreateUserRequest) => api.post<UserDetail>("/admin/users", request),
  update: (id: number, request: UpdateUserRequest) =>
    api.put<UserDetail>(`/admin/users/${id}`, request),
  deactivate: (id: number, reason: string) =>
    api.post<UserDetail>(`/admin/users/${id}/deactivate`, { reason: reason.trim() || null }),
  reactivate: (id: number) => api.post<UserDetail>(`/admin/users/${id}/reactivate`, {}),
  sendPasswordReset: (id: number) => api.post<void>(`/admin/users/${id}/password-reset`, {}),
};

export interface CreateUserRequest {
  email: string;
  displayName: string;
  roles: string[];
}

export interface UpdateUserRequest {
  displayName: string;
  email: string;
}

function errorBody(error: unknown): { code?: unknown; existingUserId?: unknown } {
  if (!(error instanceof ApiError)) return {};
  try {
    return JSON.parse(error.body) as { code?: unknown; existingUserId?: unknown };
  } catch {
    return {};
  }
}

/** The `code` of a 409 problem-details body (`own_admin_role`, `last_admin`, `ai_user`, ...), if any. */
export function apiErrorCode(error: unknown): string | undefined {
  const { code } = errorBody(error);
  return typeof code === "string" ? code : undefined;
}

/** The `existingUserId` of a 409 `email_taken` body, if any. */
export function existingUserIdOf(error: unknown): number | undefined {
  const { existingUserId } = errorBody(error);
  return typeof existingUserId === "number" ? existingUserId : undefined;
}

/** Localised text of a 409 `code` (`admin.users.errors.<code>`), or `fallback`. */
export function adminErrorText(error: unknown, t: TFunction, fallback: string): string {
  const code = apiErrorCode(error);
  return code ? t(`admin.users.errors.${code}`, { defaultValue: fallback }) : fallback;
}

/** Assignable roles held by a user (case-insensitive), in display order. */
export function assignableRolesOf(roles: readonly string[]): AssignableRole[] {
  const held = roles.map((role) => role.toLowerCase());
  return ASSIGNABLE_ROLES.filter((role) => held.includes(role));
}
