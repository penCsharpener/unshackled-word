const baseUrl = (import.meta.env.VITE_API_URL ?? "/api").replace(/\/$/, "");

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    /** Raw response text of a failed call (FastEndpoints validation JSON), see `describeApiError`. */
    readonly body = "",
  ) {
    super(message);
    this.name = "ApiError";
  }
}

async function request<T>(method: string, path: string, body?: unknown, signal?: AbortSignal) {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    signal,
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
  get: <T>(path: string, signal?: AbortSignal) => request<T>("GET", path, undefined, signal),
  post: <T>(path: string, body: unknown, signal?: AbortSignal) =>
    request<T>("POST", path, body, signal),
  put: <T>(path: string, body: unknown, signal?: AbortSignal) =>
    request<T>("PUT", path, body, signal),
  delete: <T>(path: string, signal?: AbortSignal) => request<T>("DELETE", path, undefined, signal),
};

/** Pulls a user-facing sentence out of a FastEndpoints validation body, else returns `fallback`. */
export function describeApiError(error: unknown, fallback: string): string {
  if (!(error instanceof ApiError) || !error.body) return fallback;
  try {
    const body = JSON.parse(error.body) as { errors?: Record<string, string[]>; message?: string };
    const first = body.errors && Object.values(body.errors)[0]?.[0];
    return first || body.message || fallback;
  } catch {
    return fallback;
  }
}
