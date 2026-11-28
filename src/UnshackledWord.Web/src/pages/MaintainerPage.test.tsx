import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { json, me, stubApi } from "@/test/authHelpers";
import { MaintainerPage } from "./MaintainerPage";

beforeEach(() => {
  i18n.changeLanguage("en");
  vi.stubGlobal(
    "URL",
    Object.assign(URL, { createObjectURL: () => "blob:x", revokeObjectURL: () => {} }),
  );
});
afterEach(() => vi.unstubAllGlobals());

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location">{`${location.pathname}${location.search}`}</output>;
}

function renderPage(route = "/edit/maintainer") {
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[route]}>
        <AuthProvider>
          <MaintainerPage />
          <LocationProbe />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("MaintainerPage", () => {
  it("downloads the chosen kind", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "POST /maintainer/exportSeed?kind=morphology": () =>
        new Response("zip", {
          headers: { "Content-Disposition": 'attachment; filename="seed.zip"' },
        }),
    });
    renderPage();
    await userEvent.selectOptions(await screen.findByLabelText("Kind:"), "morphology");
    await userEvent.click(screen.getByRole("button", { name: "Export seed" }));
    expect(await screen.findByText("seed.zip was downloaded.")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/maintainer/exportSeed?kind=morphology",
      expect.objectContaining({ method: "POST", credentials: "include", body: "{}" }),
    );
  });

  it("explains a 409", async () => {
    stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["admin"] }),
      "POST /maintainer/exportSeed?kind=all": () => new Response("{}", { status: 409 }),
    });
    renderPage();
    await userEvent.click(await screen.findByRole("button", { name: "Export seed" }));
    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("already running"));
  });

  it("shows a forbidden note to plain users", async () => {
    stubApi({ "GET /auth/me": () => json({ ...me, roles: ["user"] }) });
    renderPage();
    expect(await screen.findByRole("alert")).toHaveTextContent("do not have access");
  });

  it("shows the AI runs and access token sections to maintainers only", async () => {
    stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/agent-runs?limit=50": () => json({ runs: [] }),
      "GET /maintainer/api-tokens": () => json({ tokens: [] }),
      "GET /maintainer/api-tokens/users": () => json({ users: [] }),
    });
    renderPage("/edit/maintainer?tab=ai");
    expect(await screen.findByRole("heading", { name: "AI runs" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "AI access tokens" })).toBeInTheDocument();
  });

  it("hides them from reviewers", async () => {
    stubApi({ "GET /auth/me": () => json({ ...me, roles: ["reviewer"] }) });
    renderPage("/edit/maintainer?tab=ai");
    await screen.findByRole("heading", { level: 1 });
    expect(screen.queryByRole("heading", { name: "AI runs" })).toBeNull();
    expect(screen.queryByRole("heading", { name: "AI access tokens" })).toBeNull();
    const tabs = screen.getAllByRole("tab");
    expect(tabs.map((tab) => tab.textContent)).toEqual(["Revision"]);
    expect(tabs[0]).toHaveAttribute("aria-selected", "true");
  });

  it("shows every tab to maintainers and starts on the seed tab", async () => {
    const fetchMock = stubApi({ "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }) });
    renderPage();
    const tablist = await screen.findByRole("tablist", { name: "Maintenance areas" });
    const tabs = screen.getAllByRole("tab");
    expect(tablist).toBeInTheDocument();
    expect(tabs.map((tab) => tab.textContent)).toEqual([
      "Seed",
      "Revision",
      "Derived data",
      "LXX",
      "Vocabulary",
      "AI",
    ]);
    expect(screen.getByRole("tab", { name: "Seed" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tabpanel")).toHaveAccessibleName("Seed");
    expect(screen.getByRole("heading", { name: "Export seed" })).toBeInTheDocument();
    // Inactive tabs are not rendered, so they fire no requests.
    expect(screen.queryByRole("heading", { name: "AI runs" })).toBeNull();
    expect(fetchMock).not.toHaveBeenCalledWith(
      "/api/maintainer/agent-runs?limit=50",
      expect.anything(),
    );
  });

  it("switches tabs by click and keyboard and keeps the tab in the URL", async () => {
    stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/agent-runs?limit=50": () => json({ runs: [] }),
      "GET /maintainer/api-tokens": () => json({ tokens: [] }),
      "GET /maintainer/api-tokens/users": () => json({ users: [] }),
    });
    renderPage();
    await userEvent.click(await screen.findByRole("tab", { name: "AI" }));
    expect(await screen.findByRole("heading", { name: "AI runs" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Export seed" })).toBeNull();
    expect(screen.getByTestId("location")).toHaveTextContent("/edit/maintainer?tab=ai");

    await userEvent.keyboard("{ArrowRight}");
    expect(screen.getByRole("tab", { name: "Seed" })).toHaveFocus();
    expect(screen.getByRole("tab", { name: "Seed" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByTestId("location")).toHaveTextContent("/edit/maintainer?tab=seed");
  });

  it("falls back to the first tab for an unknown tab param", async () => {
    stubApi({ "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }) });
    renderPage("/edit/maintainer?tab=nope");
    expect(await screen.findByRole("tab", { name: "Seed" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
  });
});
