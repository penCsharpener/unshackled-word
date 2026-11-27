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

function setup(roles: string[] | null, options: FakeOptions = {}) {
  sessionStorage.clear();
  const fake = createSetupFake({ random: () => 0, ...options });
  replaceSetupFake(fake);
  const status = vi.spyOn(fake, "status");
  stubApi({
    "GET /auth/me": () => (roles ? json({ ...me, roles }) : new Response("", { status: 401 })),
  });
  return { fake, status };
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

  it("shows maintainers without admin the 403 note and never reads the fake", async () => {
    const { status } = setup(["user", "maintainer"]);
    renderPage();
    expect(await screen.findByRole("alert")).toHaveTextContent("You do not have access");
    expect(status).not.toHaveBeenCalled();
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
});
