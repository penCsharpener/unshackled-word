import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { json, me } from "@/test/authHelpers";
import { VocabEditorListPage } from "./VocabEditorListPage";

const courses = {
  courses: [
    {
      code: "nt-greek",
      track: "G",
      nameDe: "NT · Griechisch",
      nameEn: "NT · Greek",
      totalEntries: 1,
      totalOccurrences: 58,
    },
    {
      code: "ot-hebrew",
      track: "H",
      nameDe: "AT · Hebräisch",
      nameEn: "OT · Hebrew",
      totalEntries: 1,
      totalOccurrences: 10,
    },
  ],
};

const listPage = {
  total: 1,
  skip: 0,
  take: 50,
  items: [
    {
      id: 56,
      track: "G",
      rank: 241,
      lemma: "μέσος",
      transliteration: "mesos",
      dictionaryForm: "μέσος, -η, -ον",
      dictionaryFormOrigin: "agent",
      occurrences: 58,
      hasAudio: false,
      reviewed: false,
      retired: false,
      contentVersion: 3,
      learners: 12,
      meanings: [{ german: "Mitte", rank: 1 }],
      strongsKeys: ["G3319"],
      root: { id: 7, root: "μέσος", strongsKey: "G3319", origin: "prefill", isSimple: true },
    },
  ],
};

/** Routes fetch calls by path (the query string is read from the URL); unknown paths answer 404. */
function stubRoutes(roles: string[] = ["maintainer"], total = listPage.total) {
  const fetchMock = vi.fn(async (url: string) => {
    const parsed = new URL(url, "http://localhost");
    const path = parsed.pathname.replace(/^\/api/, "");
    if (path === "/auth/me") return json({ ...me, roles });
    if (path === "/vocab/courses") return json(courses);
    if (path === "/maintainer/vocab/entries")
      return json({ ...listPage, total, skip: Number(parsed.searchParams.get("skip") ?? 0) });
    return new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

/** The query strings of every list request, in order. */
function listQueries(fetchMock: ReturnType<typeof stubRoutes>) {
  return fetchMock.mock.calls
    .map(([url]) => new URL(String(url), "http://localhost"))
    .filter((url) => url.pathname === "/api/maintainer/vocab/entries")
    .map((url) => url.searchParams);
}

function renderAt(route = "/edit/vocab") {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[route]}>
        <AuthProvider>
          <Routes>
            <Route path="/edit/vocab" element={<VocabEditorListPage />} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("VocabEditorListPage", () => {
  it("starts with the unreviewed filter and no rank range", async () => {
    const fetchMock = stubRoutes();
    renderAt();
    await screen.findByRole("table");
    const params = listQueries(fetchMock)[0] ?? new URLSearchParams();
    expect(params.get("course")).toBe("nt-greek");
    expect(params.get("unreviewed")).toBe("true");
    expect(params.get("rankFrom")).toBeNull();
    expect(params.get("rankTo")).toBeNull();
    expect(params.has("noAudio")).toBe(false);
  });

  it("sends the chosen filter as its flag instead of the unreviewed one", async () => {
    const fetchMock = stubRoutes();
    renderAt();
    await screen.findByRole("table");
    await userEvent.selectOptions(screen.getByLabelText("Filter:"), "noAudio");
    await waitFor(() => expect(listQueries(fetchMock).at(-1)?.get("noAudio")).toBe("true"));
    const latest = listQueries(fetchMock).at(-1);
    expect(latest?.has("unreviewed")).toBe(false);
  });

  it("offers the filter without root and sends it as missingRoot", async () => {
    const fetchMock = stubRoutes();
    renderAt();
    await screen.findByRole("table");
    await userEvent.selectOptions(screen.getByLabelText("Filter:"), "missingRoot");
    await waitFor(() => expect(listQueries(fetchMock).at(-1)?.get("missingRoot")).toBe("true"));
    expect(listQueries(fetchMock).at(-1)?.has("unreviewed")).toBe(false);
  });

  it("sends the family filter from the URL and shows the chip", async () => {
    const fetchMock = stubRoutes();
    renderAt("/edit/vocab?root=7&filter=all&subRoots=false");
    await screen.findByRole("table");
    const params = listQueries(fetchMock)[0] ?? new URLSearchParams();
    expect(params.get("root")).toBe("7");
    expect(params.get("subRoots")).toBe("false");
    expect(params.has("unreviewed")).toBe(false);
    expect(screen.getByTestId("family-filter")).toHaveTextContent("Word family:");
  });

  it("opens a family from the root text with the filter set to all", async () => {
    stubRoutes();
    renderAt();
    const table = await screen.findByRole("table");
    expect(
      within(table).getByRole("link", { name: "Show the word family of μέσος" }),
    ).toHaveAttribute("href", "/edit/vocab?root=7&filter=all");
  });

  it("opens the whole family: search, rank range and another filter are dropped, the track is kept", async () => {
    stubRoutes();
    renderAt("/edit/vocab?track=G&filter=formMissing&q=mes&rankFrom=10&page=2");
    const table = await screen.findByRole("table");
    expect(
      within(table).getByRole("link", { name: "Show the word family of μέσος" }),
    ).toHaveAttribute("href", "/edit/vocab?track=G&root=7&filter=all");
  });

  it("sends an edited rank bound in the query", async () => {
    const fetchMock = stubRoutes();
    renderAt();
    await screen.findByRole("table");
    const from = screen.getByRole("spinbutton", { name: "Rank from" });
    await userEvent.clear(from);
    await userEvent.type(from, "100");
    await waitFor(() => expect(listQueries(fetchMock).at(-1)?.get("rankFrom")).toBe("100"));
    expect(listQueries(fetchMock).at(-1)?.get("rankTo")).toBeNull();
  });

  it("links each row to its editor page", async () => {
    stubRoutes();
    renderAt();
    const table = await screen.findByRole("table");
    expect(within(table).getByRole("link", { name: "μέσος" })).toHaveAttribute(
      "href",
      "/edit/vocab/56",
    );
    expect(within(table).getByText("Mitte")).toBeInTheDocument();
  });

  it("shows the root columns and links a word that is its own root to itself", async () => {
    stubRoutes();
    renderAt();
    const table = await screen.findByRole("table");
    expect(within(table).getByRole("columnheader", { name: "Root" })).toBeInTheDocument();
    expect(within(table).getByText("μέσος", { selector: "span" })).toHaveAttribute(
      "data-simple",
      "true",
    );
    expect(within(table).getByRole("link", { name: "Open root G3319" })).toHaveAttribute(
      "href",
      "/edit/vocab/56",
    );
  });

  it("says so when no entry matches", async () => {
    const fetchMock = stubRoutes();
    fetchMock.mockImplementation(async (url: string) => {
      const path = new URL(url, "http://localhost").pathname.replace(/^\/api/, "");
      if (path === "/auth/me") return json({ ...me, roles: ["maintainer"] });
      if (path === "/vocab/courses") return json(courses);
      return json({ total: 0, skip: 0, take: 50, items: [] });
    });
    renderAt();
    expect(await screen.findByText("No entries for these filters.")).toBeInTheDocument();
  });

  it("shows the forbidden note to users without the role", async () => {
    stubRoutes(["user"]);
    renderAt();
    expect(await screen.findByText("You do not have access to this page.")).toBeInTheDocument();
  });

  it("pages through the numbered page buttons and the jump field, with the page kept in skip", async () => {
    const fetchMock = stubRoutes(["maintainer"], 600);
    renderAt("/edit/vocab?page=2");
    await screen.findByRole("table");
    expect(listQueries(fetchMock).at(-1)?.get("skip")).toBe("50");
    expect(screen.getByRole("button", { name: "Page 2" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByText("51–51 of 600")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Page 4" }));
    await waitFor(() => expect(listQueries(fetchMock).at(-1)?.get("skip")).toBe("150"));
    await userEvent.type(await screen.findByLabelText("Go to page (1–12)"), "12{Enter}");
    await waitFor(() => expect(listQueries(fetchMock).at(-1)?.get("skip")).toBe("550"));
    expect(await screen.findByRole("button", { name: "Last page" })).toBeDisabled();
    await userEvent.click(screen.getByRole("button", { name: "Previous page" }));
    await waitFor(() => expect(listQueries(fetchMock).at(-1)?.get("skip")).toBe("500"));
  });

  it("shows no page navigation when all hits fit on one page", async () => {
    stubRoutes();
    renderAt();
    await screen.findByRole("table");
    expect(screen.getByText("1–1 of 1")).toBeInTheDocument();
    expect(screen.queryByRole("navigation", { name: "Pages" })).not.toBeInTheDocument();
  });
});
