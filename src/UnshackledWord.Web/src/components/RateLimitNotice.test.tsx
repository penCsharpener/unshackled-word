import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { ApiError } from "@/lib/api";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { retryUnlessRateLimited } from "@/lib/queryClient";
import { json, me, stubApi } from "@/test/authHelpers";
import { RateLimitNotice } from "./RateLimitNotice";

const limited = (seconds = 30, tier = "general") =>
  new ApiError(
    429,
    "limited",
    JSON.stringify({ code: "rateLimited", tier, retryAfterSeconds: seconds }),
  );

function Probe({ client }: { client: QueryClient }) {
  return (
    <QueryClientProvider client={client}>
      <AuthProvider>
        <MemoryRouter>
          <RateLimitNotice />
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>
  );
}

async function fail(client: QueryClient, error: unknown, key = ["k"]) {
  await client
    .fetchQuery({ queryKey: key, queryFn: () => Promise.reject(error), retry: false })
    .catch(() => {});
}

describe("ApiError and retry rule", () => {
  it("parses the 429 body", () => {
    const error = limited(41, "search");
    expect(error.retryAfterSeconds).toBe(41);
    expect(error.tier).toBe("search");
  });

  it("tolerates a 429 without a JSON body", () => {
    expect(new ApiError(429, "x", "Too Many Requests").retryAfterSeconds).toBeUndefined();
  });

  it("never retries a 429, retries other failures once", () => {
    expect(retryUnlessRateLimited(0, limited())).toBe(false);
    expect(retryUnlessRateLimited(0, new ApiError(500, "x"))).toBe(true);
    expect(retryUnlessRateLimited(1, new ApiError(500, "x"))).toBe(false);
  });
});

describe("RateLimitNotice", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it("counts down for anonymous users, then offers a reload of the failed queries", async () => {
    stubApi({ "GET /auth/me": () => json({}, 401) });
    const client = new QueryClient();
    render(<Probe client={client} />);
    await fail(client, limited(2));
    expect(
      await screen.findByText(/Zu viele Anfragen\. Bitte in [12] s erneut/),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Anmelden" })).toHaveAttribute("href", "/login");
    expect(screen.queryByRole("button", { name: "Neu laden" })).toBeNull();

    const reload = await screen.findByRole("button", { name: "Neu laden" }, { timeout: 4000 });
    const refetch = vi.spyOn(client, "refetchQueries");
    await userEvent.click(reload);
    expect(refetch).toHaveBeenCalled();
  });

  it("stays hidden for signed-in users", async () => {
    stubApi({ "GET /auth/me": () => json({ ...me, roles: ["user"] }) });
    const client = new QueryClient();
    render(<Probe client={client} />);
    await fail(client, limited());
    await act(async () => {});
    expect(screen.queryByRole("status")).toBeNull();
  });

  it("ignores other failures", async () => {
    stubApi({ "GET /auth/me": () => json({}, 401) });
    const client = new QueryClient();
    render(<Probe client={client} />);
    await fail(client, new ApiError(500, "boom"));
    await act(async () => {});
    expect(screen.queryByRole("status")).toBeNull();
  });

  it("uses the tier's wording in English", async () => {
    await i18n.changeLanguage("en");
    stubApi({ "GET /auth/me": () => json({}, 401) });
    const client = new QueryClient();
    render(<Probe client={client} />);
    await fail(client, limited(40, "search"));
    expect(
      await screen.findByText(/Too many searches\. Please try again in (39|40) s/),
    ).toBeInTheDocument();
  });
});
