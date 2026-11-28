import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import type { FakeOptions } from "@/lib/setup/setupFake";
import { createSetupFake, replaceSetupFake } from "@/lib/setup/setupFake";
import { json, me, stubApi } from "@/test/authHelpers";
import { SetupPage } from "./SetupPage";

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={["/edit/setup"]}>
        <AuthProvider>
          <Routes>
            <Route path="/edit/setup" element={<SetupPage />} />
            <Route path="/login" element={<p>login page</p>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/**
 * Stands in for the server: `GET /admin/setup` answers the fake's steps, counts and job (with `wired` set per test) and
 * `POST /admin/setup/run` starts a fake run. The page itself only talks HTTP.
 */
function setup(roles: string[] | null, options: FakeOptions = {}, unwired: string[] = []) {
  sessionStorage.clear();
  const fake = createSetupFake({ random: () => 0, ...options });
  replaceSetupFake(fake);
  const status = vi.spyOn(fake, "status");
  const setupBody = vi.fn(async () => {
    const { steps, checkedAt, job } = await fake.status();
    return json({
      checkedAt,
      job,
      steps: steps.map((step) => (unwired.includes(step.key) ? { ...step, wired: false } : step)),
    });
  });
  const runBody = vi.fn(async (init?: RequestInit) =>
    json(await fake.run(JSON.parse(String(init?.body))), 202),
  );
  const fetchMock = stubApi({
    "GET /auth/me": () => (roles ? json({ ...me, roles }) : new Response("", { status: 401 })),
  });
  const base = fetchMock.getMockImplementation();
  fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
    const method = init?.method ?? "GET";
    if (url.endsWith("/admin/setup") && method === "GET") return setupBody();
    if (url.endsWith("/admin/setup/run") && method === "POST") return runBody(init);
    return base?.(url, init) ?? new Response("", { status: 404 });
  });
  return { fake, status, fetchMock, setupBody, runBody };
}

const rowOf = (label: string) => {
  const row = screen.getByText(label, { selector: "span.font-medium" }).closest("li");
  if (!row) throw new Error(label);
  return row;
};

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
  replaceSetupFake(undefined);
});

describe("SetupPage", () => {
  it("shows four groups and 22 rows, the books done without a run button", async () => {
    setup(["user", "admin"]);
    renderPage();
    expect(await screen.findByText(/steps done/)).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Initial setup" })).toBeInTheDocument();
    for (const name of ["Base text Elb1871", "STEP Bible", "Other sources", "Derived data"]) {
      expect(screen.getByRole("heading", { name: name })).toBeInTheDocument();
    }
    expect(screen.getAllByRole("listitem").filter((li) => li.tagName === "LI")).toHaveLength(22);
    const books = rowOf("Bible books");
    expect(within(books).getByText("done", { selector: "p span" })).toBeInTheDocument();
    expect(within(books).queryByRole("button")).toBeNull();
    expect(screen.getByText(/1 of 22 steps done/)).toBeInTheDocument();
  });

  it("disables a step whose prerequisite is open and says why", async () => {
    setup(["user", "admin"]);
    renderPage();
    await screen.findByText(/steps done/);
    const row = rowOf("Elb1871 verses");
    const button = within(row).getByRole("button", { name: "Run" });
    expect(button).toBeDisabled();
    expect(button).toHaveAccessibleDescription(/needs: Elb1871 text/);
    expect(within(rowOf("STTS tags")).getByRole("button", { name: "Run" })).toBeEnabled();
  });

  it("runs everything: rows turn green one after another, log grows, ends at 22 of 22", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    setup(["user", "admin"]);
    renderPage();
    await screen.findByText(/steps done/);
    await act(async () => {
      screen.getByRole("button", { name: "Run all" }).click();
    });
    await act(() => vi.advanceTimersByTimeAsync(2100));
    expect(screen.getByRole("button", { name: "Run all" })).toBeDisabled();
    for (const button of screen.getAllByRole("button", { name: "Run" })) {
      expect(button).toBeDisabled();
    }
    expect(screen.getAllByRole("status").some((el) => el.textContent === "running")).toBe(true);
    expect(screen.getByText(/stts: lese/)).toBeInTheDocument();
    expect(screen.getByText(/2 of 22 steps done/)).toBeInTheDocument();
    await act(() => vi.advanceTimersByTimeAsync(21 * 1500));
    await waitFor(() => expect(screen.getByText(/22 of 22 steps done/)).toBeInTheDocument());
    expect(screen.getByRole("button", { name: "All done" })).toBeDisabled();
    expect(screen.getAllByRole("status").some((el) => el.textContent === "done")).toBe(true);
  });

  it("shows the failure, the log tail and where the chain stopped", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    setup(["user", "admin"], { failKey: "tsk" });
    renderPage();
    await screen.findByText(/steps done/);
    await act(async () => {
      screen.getByRole("button", { name: "Run all" }).click();
    });
    await act(() => vi.advanceTimersByTimeAsync(21 * 1500 + 4000));
    const alert = await screen.findByText("Error: Fake-Fehler in tsk");
    expect(alert).toHaveAttribute("role", "alert");
    expect(screen.getByText("Stopped at: TSK cross references")).toBeInTheDocument();
    expect(within(rowOf("TSK cross references")).getByText("failed")).toBeInTheDocument();
  });

  it("shows maintainers without admin the 403 note and never reads the setup endpoint", async () => {
    const { setupBody } = setup(["user", "maintainer"]);
    renderPage();
    expect(await screen.findByRole("alert")).toHaveTextContent("You do not have access");
    expect(setupBody).not.toHaveBeenCalled();
  });

  it("reads the real endpoint, says 'not wired yet' on unwired rows and keeps them disabled", async () => {
    const { setupBody } = setup(["user", "admin"], {}, ["tsk", "step-text"]);
    renderPage();
    await screen.findByText(/steps done/);
    expect(setupBody).toHaveBeenCalled();
    const button = within(rowOf("TSK cross references")).getByRole("button", {
      name: "not wired yet",
    });
    expect(button).toBeDisabled();
    expect(within(rowOf("STTS tags")).getByRole("button", { name: "Run" })).toBeEnabled();
  });

  it("links the other-group rows to the imports page and the LXX review", async () => {
    setup(["user", "admin"]);
    renderPage();
    await screen.findByText(/steps done/);
    const tags = rowOf("Septuagint tags");
    expect(within(tags).getByRole("link", { name: "Updates under Imports" })).toHaveAttribute(
      "href",
      "/edit/imports",
    );
    expect(within(tags).getByRole("link", { name: "Review LXX" })).toHaveAttribute(
      "href",
      "/edit/lxx",
    );
    expect(
      within(rowOf("Septuagint text")).getByRole("link", { name: "Review LXX" }),
    ).toHaveAttribute("href", "/edit/lxx");
    expect(within(rowOf("STTS tags")).queryByRole("link")).toBeNull();
  });

  it("re-reads the counts when refresh is clicked", async () => {
    const { setupBody } = setup(["user", "admin"]);
    renderPage();
    await screen.findByText(/steps done/);
    const before = setupBody.mock.calls.length;
    await act(async () => {
      screen.getByRole("button", { name: "Reload state" }).click();
    });
    await waitFor(() => expect(setupBody.mock.calls.length).toBeGreaterThan(before));
  });

  it("sends anonymous visitors to the login page", async () => {
    setup(null);
    renderPage();
    expect(await screen.findByText("login page")).toBeInTheDocument();
  });

  it("speaks German with the thousands separator of the language", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    await i18n.changeLanguage("de");
    const { fake } = setup(["user", "admin"]);
    await fake.run({ keys: ["elb1871-text"] });
    await vi.advanceTimersByTimeAsync(2000);
    renderPage();
    expect(await screen.findByText(/Schritten fertig/)).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Ersteinrichtung" })).toBeInTheDocument();
    expect(screen.getByText(/2 von 22 Schritten fertig/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Alle ausführen" })).toBeInTheDocument();
    expect(rowOf("Elb1871-Text")).toHaveTextContent("722.743");
  });

  it("posts the run request of a row to the server and shows nothing fake", async () => {
    const { runBody } = setup(["user", "admin"]);
    renderPage();
    await screen.findByText(/steps done/);
    await act(async () => {
      within(rowOf("STTS tags")).getByRole("button", { name: "Run" }).click();
    });
    await waitFor(() => expect(runBody).toHaveBeenCalled());
    expect(JSON.parse(String(runBody.mock.calls[0]?.[0]?.body))).toEqual({ keys: ["stts"] });
    expect(screen.queryByRole("note")).toBeNull();
  });

  it("says a job is running when the server answers 409", async () => {
    const { fetchMock } = setup(["user", "admin"]);
    const base = fetchMock.getMockImplementation();
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) =>
      url.endsWith("/admin/setup/run")
        ? json(
            { errors: { generalErrors: ["Another seed job or export is already running."] } },
            409,
          )
        : (base?.(url, init) ?? new Response("", { status: 404 })),
    );
    renderPage();
    await screen.findByText(/steps done/);
    await act(async () => {
      within(rowOf("STTS tags")).getByRole("button", { name: "Run" }).click();
    });
    expect(
      await screen.findByText("A job is already running.", { selector: "p,div" }),
    ).toBeInTheDocument();
  });

  it("ticks the elapsed time of a running job every second and shows the duration once it ended", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const { fake, fetchMock } = setup(["user", "admin"]);
    const base = fetchMock.getMockImplementation();
    const startedAt = new Date().toISOString();
    let finishedAt: string | undefined;
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
      if (!url.endsWith("/admin/setup") || (init?.method ?? "GET") !== "GET")
        return base?.(url, init) ?? new Response("", { status: 404 });
      const { steps, checkedAt } = await fake.status();
      return json({
        checkedAt,
        currentStep: finishedAt ? undefined : "step-text",
        steps,
        job: {
          id: "j",
          operation: "setup",
          kind: "step-text",
          status: finishedAt ? "succeeded" : "running",
          startedAt,
          finishedAt,
          log: ["10:00:00 [1/1] step-text: start"],
        },
      });
    });
    renderPage();
    await screen.findByText(/steps done/);
    expect(
      screen.getByRole("heading", { name: /Job: STEP original text · for 00:0\d/ }),
    ).toBeInTheDocument();
    await act(() => vi.advanceTimersByTimeAsync(5000));
    expect(screen.getByRole("heading", { name: /for 00:0[4-9]/ })).toBeInTheDocument();
    finishedAt = new Date(Date.parse(startedAt) + 125_000).toISOString();
    await act(() => vi.advanceTimersByTimeAsync(2100));
    expect(await screen.findByRole("heading", { name: /took 02:05/ })).toBeInTheDocument();
  });

  it("follows a running server job: spinner on the current step, poll every 2 s, counts re-read", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const { fake, fetchMock } = setup(["user", "admin"]);
    const base = fetchMock.getMockImplementation();
    let polls = 0;
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
      if (!url.endsWith("/admin/setup") || (init?.method ?? "GET") !== "GET")
        return base?.(url, init) ?? new Response("", { status: 404 });
      polls += 1;
      const { steps, checkedAt } = await fake.status();
      const finished = polls >= 3;
      return json({
        checkedAt,
        currentStep: finished ? undefined : "stts",
        steps: steps.map((step) =>
          finished && step.key === "stts"
            ? { ...step, status: "green", tables: step.tables.map((t) => ({ ...t, rows: 54 })) }
            : step,
        ),
        job: {
          id: "j",
          operation: "setup",
          kind: "group:base",
          status: finished ? "succeeded" : "running",
          startedAt: new Date().toISOString(),
          log: ["10:00:00 Started setup (group:base)", "10:00:01 [1/4] stts: start"],
        },
      });
    });
    renderPage();
    await screen.findByText(/steps done/);
    expect(
      within(rowOf("STTS tags")).getByText("running …", { selector: "p span" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/running: STTS tags/)).toBeInTheDocument();
    for (const button of screen.getAllByRole("button", { name: "Run" }))
      expect(button).toBeDisabled();
    await act(() => vi.advanceTimersByTimeAsync(2100));
    expect(polls).toBe(2);
    await act(() => vi.advanceTimersByTimeAsync(2100));
    await waitFor(() => expect(polls).toBe(3));
    await act(() => vi.advanceTimersByTimeAsync(5000));
    expect(polls).toBe(3);
    expect(
      within(rowOf("STTS tags")).getByText("done", { selector: "p span" }),
    ).toBeInTheDocument();
    expect(screen.queryByText(/running: /)).toBeNull();
  });

  it("shows a failed server job on the row of the step that failed, with the log tail", async () => {
    const { fake, fetchMock } = setup(["user", "admin"]);
    const base = fetchMock.getMockImplementation();
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
      if (!url.endsWith("/admin/setup") || (init?.method ?? "GET") !== "GET")
        return base?.(url, init) ?? new Response("", { status: 404 });
      const { steps, checkedAt } = await fake.status();
      return json({
        checkedAt,
        steps: steps.map((step) =>
          step.key === "elb1871-text" ? { ...step, failed: true } : step,
        ),
        job: {
          id: "j",
          operation: "setup",
          kind: "elb1871-text",
          status: "failed",
          startedAt: new Date().toISOString(),
          finishedAt: new Date().toISOString(),
          error: "file missing",
          log: [
            "10:00:00 Started",
            "10:00:01 [1/1] elb1871-text: start",
            "10:00:02 [1/1] elb1871-text: failed after 0.1 s",
          ],
        },
      });
    });
    renderPage();
    await screen.findByText(/steps done/);
    const row = rowOf("Elb1871 text");
    expect(within(row).getByText("failed", { selector: "p span" })).toBeInTheDocument();
    expect(within(row).getByText(/elb1871-text: failed after/)).toBeInTheDocument();
    expect(screen.getByText("Error: file missing")).toBeInTheDocument();
    expect(within(row).getByRole("button", { name: "Run" })).toBeEnabled();
  });
});
