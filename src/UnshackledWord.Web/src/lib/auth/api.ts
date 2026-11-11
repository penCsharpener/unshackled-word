import { api } from "@/lib/api";
import type { PasskeyCredentialJson } from "./webauthn";

export interface Me {
  userId: number;
  email: string;
  displayName: string;
  roles?: string[];
}

export interface AuthUser {
  userId: number;
  email: string;
  displayName: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
  inviteCode?: string;
}

export interface RegistrationPolicy {
  mode: "open" | "restricted";
  inviteCodeRequired: boolean;
}

export interface PasskeySummary {
  id: number;
  name: string;
  createdAt: string;
  lastUsedAt?: string | null;
}

export interface PasskeyLoginOptions {
  flowId: string;
  options: PublicKeyCredentialRequestOptionsJSON;
}

/** Typed wrappers for `/api/auth/*`; request and response shapes mirror the lexio API 1:1. */
export const authApi = {
  me: (signal?: AbortSignal) => api.get<Me>("/auth/me", signal),
  login: (email: string, password: string) =>
    api.post<AuthUser>("/auth/login", { email, password }),
  logout: () => api.post<void>("/auth/logout", {}),
  register: (body: RegisterRequest) => api.post<AuthUser>("/auth/register", body),
  registrationPolicy: () => api.get<RegistrationPolicy>("/auth/registration-policy"),
  verifyEmail: (token: string) => api.post<void>("/auth/verify-email", { token }),
  requestPasswordReset: (email: string) =>
    api.post<void>("/auth/request-password-reset", { email }),
  resetPassword: (token: string, newPassword: string) =>
    api.post<void>("/auth/reset-password", { token, newPassword }),
  requestMagicLink: (email: string) => api.post<void>("/auth/request-magic-link", { email }),
  magicLinkLogin: (token: string) => api.post<AuthUser>("/auth/magic-link-login", { token }),
};

export const passkeysApi = {
  getRegisterOptions: () =>
    api.post<PublicKeyCredentialCreationOptionsJSON>("/auth/passkeys/register/options", {}),
  verifyRegistration: (credential: PasskeyCredentialJson, name: string) =>
    api.post<PasskeySummary>("/auth/passkeys/register/verify", { credential, name }),
  list: () => api.get<{ passkeys: PasskeySummary[] }>("/auth/passkeys"),
  remove: (id: number) => api.delete<void>(`/auth/passkeys/${id}`),
  getLoginOptions: (email?: string) =>
    api.post<PasskeyLoginOptions>("/auth/passkeys/login/options", { email: email || null }),
  login: (flowId: string, credential: PasskeyCredentialJson) =>
    api.post<AuthUser>("/auth/passkeys/login/verify", { flowId, credential }),
};
