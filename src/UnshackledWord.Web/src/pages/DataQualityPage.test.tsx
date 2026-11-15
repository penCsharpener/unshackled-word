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

  it("shows no seed line when the overview has no seedImports", async () => {
    stubApi({ ...handlers, "GET /auth/me": () => json({ ...me, roles: ["reviewer"] }) });
    renderPage();
    await screen.findByRole("button", { name: /U1 Deutsch ohne Grundtext/ });
    expect(screen.queryByText(/^Seed /)).toBeNull();
  });

  it("shows the seed import line with its orphans, and 0 orphans as such", async () => {
    const seed = (orphaned: number) => ({
      kind: "morphology",
      seedVersion: "0123456789abcdef",
      exportedAt: "2026-11-13T09:00:00Z",
      importedAt: "2026-11-13T10:00:00Z",
      rows: 721054,
      orphaned,
      reportPath: "temp/seed-report/morphology-0123456789ab.csv",
    });
    stubApi({
      ...handlers,
      "GET /auth/me": () => json({ ...me, roles: ["reviewer"] }),
      "GET /dataQuality/overview": () => json({ ...overview, seedImports: [seed(12)] }),
    });
    const first = renderPage();
    expect(
      await screen.findByText(/Seed Morphologie: Stand .*, 12 Zeilen nicht zuordenbar/),
    ).toBeInTheDocument();
    first.unmount();

    stubApi({
      ...handlers,
      "GET /auth/me": () => json({ ...me, roles: ["reviewer"] }),
      "GET /dataQuality/overview": () => json({ ...overview, seedImports: [seed(0)] }),
    });
    renderPage();
    expect(await screen.findByText(/0 Zeilen nicht zuordenbar/)).toBeInTheDocument();
  });

  it("summarises a mapping seed update with the kept verses and the report path", async () => {
    stubApi({
      ...handlers,
      "GET /auth/me": () => json({ ...me, roles: ["reviewer"] }),
      "GET /dataQuality/overview": () =>
        json({
          ...overview,
          seedImports: [
            {
              kind: "mappings",
              seedVersion: "fedcba9876543210",
              importedAt: "2026-11-14T10:00:00Z",
              rows: 725513,
              orphaned: 974,
              versesUpdated: 2,
              versesKeptLocal: 1,
              reportPath: "temp/seed-report/mappings-update-fedcba987654.csv",
            },
          ],
        }),
    });
    renderPage();
    expect(
      await screen.findByText(
        "Zuordnungs-Seed aktualisiert: 2 Verse, 1 lokal geändert behalten (Bericht: temp/seed-report/mappings-update-fedcba987654.csv)",
      ),
    ).toBeInTheDocument();
  });

  it("shows the revision seed import with and without orphans and its update summary", async () => {
    const revision = (extra: object) => ({
      kind: "revision",
      seedVersion: "0a1b2c3d4e5f6789",
      exportedAt: "2026-11-15T09:00:00Z",
      importedAt: "2026-11-15T10:00:00Z",
      rows: 880,
      orphaned: 0,
      reportPath: "temp/seed-report/revision-0a1b2c3d4e5f.csv",
      ...extra,
    });
    const render = (seedImports: object[]) => {
      stubApi({
        ...handlers,
        "GET /auth/me": () => json({ ...me, roles: ["reviewer"] }),
        "GET /dataQuality/overview": () => json({ ...overview, seedImports }),
      });
      return renderPage();
    };

    const first = render([revision({ orphaned: 3 })]);
    expect(
      await screen.findByText(/Seed Revision: Stand .*, 3 Zeilen nicht zuordenbar/),
    ).toBeInTheDocument();
    first.unmount();

    const second = render([revision({})]);
    expect(
      await screen.findByText(/Seed Revision: .*, 0 Zeilen nicht zuordenbar/),
    ).toBeInTheDocument();
    second.unmount();

    render([
      revision({
        versesUpdated: 4,
        versesKeptLocal: 1,
        reportPath: "temp/seed-report/revision-update-0a1b2c3d4e5f.csv",
      }),
    ]);
    expect(
      await screen.findByText(
        "Revisions-Seed aktualisiert: 4 Verse, 1 lokal geändert behalten (Bericht: temp/seed-report/revision-update-0a1b2c3d4e5f.csv)",
      ),
    ).toBeInTheDocument();
  });

  it("shows the STTS tiles and an L7 finding with its tag and part of speech", async () => {
    stubApi({
      ...handlers,
      "GET /auth/me": () => json({ ...me, roles: ["reviewer"] }),
      "GET /dataQuality/overview": () =>
        json({
          ...overview,
          checks: [
            ...overview.checks,
            { code: "L6", tier: 1, count: 0, errorCount: 0 },
            { code: "L7", tier: 1, count: 6809, errorCount: 0 },
          ],
        }),
      "GET /dataQuality/checks/L7?page=1&pageSize=50": () =>
        json({
          code: "L7",
          total: 1,
          page: 1,
          pageSize: 50,
          matrix: {
            rows: ["ADV"],
            columns: ["NN"],
            cells: [{ row: "ADV", column: "NN", count: 1, expected: false }],
          },
          items: [
            {
              check: "L7",
              severity: "warning",
              bibleBookId: 1,
              chapter: 1,
              verse: 5,
              elbWordId: 77,
              german: "Morgen",
              pos: "ADV",
              stts: "NN",
              detail: "STTS „NN“ passt nicht zur Wortart ADV",
            },
          ],
        }),
    });
    renderPage("/edit/quality?check=L7");
    expect(
      await screen.findByRole("button", { name: /L6 STTS unbekannt oder leer.*Unauffällig/ }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /L7 STTS passt nicht zur Wortart/ }),
    ).toBeInTheDocument();
    expect(await screen.findByRole("columnheader", { name: "STTS" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Wortart" })).toBeInTheDocument();
    expect(await screen.findAllByText("STTS „NN“ passt nicht zur Wortart ADV")).not.toHaveLength(0);
  });
});
