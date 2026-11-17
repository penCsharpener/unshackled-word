import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
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

function renderPage() {
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <AuthProvider>
          <MaintainerPage />
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
    renderPage();
    expect(await screen.findByRole("heading", { name: "AI runs" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "AI access tokens" })).toBeInTheDocument();
  });

  it("hides them from reviewers", async () => {
    stubApi({ "GET /auth/me": () => json({ ...me, roles: ["reviewer"] }) });
    renderPage();
    await screen.findByRole("heading", { level: 1 });
    expect(screen.queryByRole("heading", { name: "AI runs" })).toBeNull();
    expect(screen.queryByRole("heading", { name: "AI access tokens" })).toBeNull();
  });
});
