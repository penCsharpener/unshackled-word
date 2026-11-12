import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { json, me, stubApi } from "@/test/authHelpers";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import { DataQualityPage } from "./DataQualityPage";

function renderPage(route = "/edit/quality") {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter([{ path: "/edit/quality", element: <DataQualityPage /> }], {
    initialEntries: [route],
  });
  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

const overview = {
  refreshedAt: "2026-11-12T14:05:00Z",
  refreshing: false,
  totals: { germanWords: 722418, sourceWords: 448312 },
  checks: [
    {
      code: "U1",
      tier: 1,
      count: 1204,
      errorCount: 0,
      previousCount: 1190,
      sub: { key: "content", count: 900 },
    },
    { code: "L1", tier: 1, count: 0, errorCount: 0 },
  ],
};
const unmapped = (side: string) => ({ side, total: 0, byClass: [], byBook: [], top: [] });
const handlers = {
  "GET /meta-bible/structure": () => json(fixtureStructure),
  "GET /dataQuality/overview": () => json(overview),
  "GET /dataQuality/unmapped/german?content=true&top=10": () => json(unmapped("german")),
  "GET /dataQuality/unmapped/greek?content=true&top=10": () => json(unmapped("greek")),
  "GET /dataQuality/unmapped/hebrew?content=true&top=10": () => json(unmapped("hebrew")),
  "GET /dataQuality/coverage?metric=german": () => json({ metric: "german", books: [] }),
};

describe("DataQualityPage", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });
  afterEach(() => vi.unstubAllGlobals());

  it("asks logged-out users to sign in", async () => {
    stubApi({ ...handlers, "GET /auth/me": () => json({}, 401) });
    renderPage();
    expect(await screen.findByText("Anmeldung erforderlich")).toBeInTheDocument();
  });

  it("shows tiles with severity and hides the refresh button without write role", async () => {
    stubApi({ ...handlers, "GET /auth/me": () => json({ ...me, roles: ["reviewer"] }) });
    renderPage();
    expect(
      await screen.findByRole("button", { name: /U1 Deutsch ohne Grundtext/ }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /L1 Ohne Lemma.*Unauffällig/ })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Neu berechnen" })).toBeNull();
  });

  it("offers the refresh button to maintainers and marks the selected check from the URL", async () => {
    stubApi({
      ...handlers,
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /dataQuality/checks/U1?page=1&pageSize=50": () =>
        json({ code: "U1", total: 0, page: 1, pageSize: 50, items: [] }),
    });
    renderPage("/edit/quality?check=U1");
    expect(await screen.findByRole("button", { name: "Neu berechnen" })).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByRole("button", { name: /U1 Deutsch ohne Grundtext/ })).toHaveAttribute(
        "aria-pressed",
        "true",
      ),
    );
  });
});
