import { render } from "@testing-library/react";
import type { ReactElement } from "react";
import { MemoryRouter } from "react-router";
import { vi } from "vitest";
import { AuthProvider } from "@/lib/auth/AuthContext";

export const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status });

/** Stubs fetch with route handlers keyed by "METHOD /path" (path without the /api prefix). */
export function stubApi(handlers: Record<string, () => Response>) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const key = `${init?.method ?? "GET"} ${url.replace(/^\/api/, "")}`;
    return handlers[key]?.() ?? new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

export function renderWithAuth(ui: ReactElement, route = "/") {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <AuthProvider>{ui}</AuthProvider>
    </MemoryRouter>,
  );
}

export const me = { userId: 1, email: "ada@example.org", displayName: "Ada" };
