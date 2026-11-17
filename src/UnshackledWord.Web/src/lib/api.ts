import i18n from "i18next";

const baseUrl = (import.meta.env.VITE_API_URL ?? "/api").replace(/\/$/, "");

export type RateLimitTier = "general" | "search" | "auth";

export class ApiError extends Error {
  /** Seconds until the limiter admits the caller again; only set on a 429 with a readable body. */
  readonly retryAfterSeconds?: number;
  /** Which limiter tier rejected the call; only set on a 429 with a readable body. */
  readonly tier?: RateLimitTier;

  constructor(
    readonly status: number,
    message: string,
    /** Raw response text of a failed call (FastEndpoints validation JSON), see `describeApiError`. */
    readonly body = "",
  ) {
    super(message);
    this.name = "ApiError";
    if (status === 429) {
      try {
        const parsed = JSON.parse(body) as { tier?: RateLimitTier; retryAfterSeconds?: number };
        if (typeof parsed.retryAfterSeconds === "number") {
          this.retryAfterSeconds = parsed.retryAfterSeconds;
        }
        this.tier = parsed.tier;
      } catch {
        // Not JSON (a proxy's own 429): status alone says "rate limited".
      }
    }
  }
}

/** True when the Web API's rate limiter (HTTP 429) rejected the call. */
export const isRateLimited = (error: unknown): error is ApiError =>
  error instanceof ApiError && error.status === 429;

export interface RequestOptions {
  /** Pass `no-cache` to revalidate against the server instead of reusing a fresh HTTP cache entry. */
  cache?: RequestCache;
}

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  signal?: AbortSignal,
  options: RequestOptions = {},
) {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    signal,
    cache: options.cache,
    credentials: "include",
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    const text = await response.text().catch(() => "");
    throw new ApiError(response.status, `${method} ${path} failed with ${response.status}`, text);
  }
  // Several auth endpoints answer 200/202 with an empty body, so parse only when there is one.
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

/** Typed fetch helpers for the Web API. Responses are camelCase JSON with nulls omitted. */
export const api = {
  get: <T>(path: string, signal?: AbortSignal, options?: RequestOptions) =>
    request<T>("GET", path, undefined, signal, options),
  post: <T>(path: string, body: unknown, signal?: AbortSignal) =>
    request<T>("POST", path, body, signal),
  put: <T>(path: string, body: unknown, signal?: AbortSignal) =>
    request<T>("PUT", path, body, signal),
  delete: <T>(path: string, signal?: AbortSignal) => request<T>("DELETE", path, undefined, signal),
};

/** "Too many requests, try again in N s" in the tier's wording; the wait is left out when the body had none. */
export function rateLimitMessage(error: Pick<ApiError, "tier" | "retryAfterSeconds">): string {
  const wait =
    error.retryAfterSeconds === undefined
      ? i18n.t("rateLimit.soon")
      : i18n.t("rateLimit.inSeconds", { seconds: Math.ceil(error.retryAfterSeconds) });
  return i18n.t(`rateLimit.${error.tier ?? "general"}`, { wait });
}

/** Pulls a user-facing sentence out of a FastEndpoints validation body, else returns `fallback`. */
export function describeApiError(error: unknown, fallback: string): string {
  if (isRateLimited(error)) return rateLimitMessage(error);
  if (!(error instanceof ApiError) || !error.body) return fallback;
  try {
    const body = JSON.parse(error.body) as { errors?: Record<string, string[]>; message?: string };
    const first = body.errors && Object.values(body.errors)[0]?.[0];
    return first || body.message || fallback;
  } catch {
    return fallback;
  }
}

/**
 * Every message of a FastEndpoints validation body (one entry per problem, e.g. the hard-rule messages of a rejected save),
 * the rate-limit sentence for a 429, else the body's `message`; empty when there is nothing readable (use your own fallback).
 */
export function describeApiErrors(error: unknown): string[] {
  if (isRateLimited(error)) return [rateLimitMessage(error)];
  if (!(error instanceof ApiError) || !error.body) return [];
  try {
    const body = JSON.parse(error.body) as { errors?: Record<string, unknown>; message?: unknown };
    const messages = Object.values(body.errors ?? {})
      .flatMap((value) => (Array.isArray(value) ? value : [value]))
      .filter((value): value is string => typeof value === "string" && value.length > 0);
    if (messages.length > 0) return messages;
    return typeof body.message === "string" && body.message ? [body.message] : [];
  } catch {
    return [];
  }
}
