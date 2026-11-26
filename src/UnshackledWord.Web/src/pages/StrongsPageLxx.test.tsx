import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { SIDE_PANEL_KEY } from "@/lib/bible/sidePanel";
import type { GetStrongsOccurrencesResponse } from "@/lib/bible/types";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import { fixtureSummary, fixtureWordStats } from "@/test/fixtures/strongsStats";
import { StrongsPage } from "./StrongsPage";

const lxxPage: GetStrongsOccurrencesResponse = {
  strongs: "G0746",
  language: "greek",
  corpus: "lxx",
  page: 1,
  pageSize: 50,
  totalItems: 2,
  totalPages: 1,
  items: [],
  lxxItems: [
    {
      lxxWordId: 486320,
      lxxVerseId: 1,
      ref: "Gen.1:1",
      textForm: "default",
      bibleBookId: 1,
      chapter: 1,
      verse: 1,
      subverse: 0,
      words: [
        { text: "ἐν", matched: false },
        { text: "ἀρχῇ", matched: true },
        { text: "ἐποίησεν", matched: false },
      ],
      elb: {
        hebRefId: 1001001,
        bibleBookId: 1,
        chapter: 1,
        verse: 1,
        text: "Im Anfang schuf Gott",
      },
    },
    {
      lxxWordId: 5,
      lxxVerseId: 2,
      ref: "Est.1:1.1",
      textForm: "default",
      bibleBookId: 17,
      chapter: 1,
      verse: 1,
      subverse: 1,
      words: [{ text: "ἀρχή", matched: true }],
      elb: { hebRefId: 1002004, bibleBookId: 1, chapter: 2, verse: 4, elbIsAnchor: true },
    },
  ],
};

function stubFetch(lxx: GetStrongsOccurrencesResponse, roles?: string[]) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.includes("/auth/me")) {
      return roles
        ? new Response(JSON.stringify({ userId: 9, email: "m@b.c", displayName: "Max", roles }))
        : new Response("", { status: 401 });
    }
    if (url.includes("/meta-bible/structure"))
      return new Response(JSON.stringify(fixtureStructure));
    if (url.includes("/strongsSummary/")) return new Response(JSON.stringify(fixtureSummary));
    if (url.includes("/wordStats")) return new Response(JSON.stringify(fixtureWordStats));
    if (url.includes("corpus=lxx")) return new Response(JSON.stringify(lxx));
    return new Response(
      JSON.stringify({ ...lxxPage, corpus: undefined, lxxItems: undefined, items: [] }),
    );
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function Where() {
  const location = useLocation();
  return <p data-testid="where">{`${location.pathname}${location.search}${location.hash}`}</p>;
}

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [
      { path: "/bible/strongs/:strongs", element: <StrongsPage /> },
      { path: "/bible/:book/:chapter", element: <Where /> },
    ],
    { initialEntries: [path] },
  );
  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

beforeEach(async () => {
  await i18n.changeLanguage("de");
  localStorage.removeItem(SIDE_PANEL_KEY);
});
afterEach(() => {
  vi.unstubAllGlobals();
  localStorage.removeItem(SIDE_PANEL_KEY);
});

describe("StrongsPage LXX corpus", () => {
  it("shows the switch for a Greek number and no switch for a Hebrew one", async () => {
    stubFetch(lxxPage);
    renderAt("/bible/strongs/G746");
    const group = await screen.findByRole("group", { name: "Textkorpus" });
    expect(within(group).getByRole("button", { name: "NT" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(within(group).getByRole("button", { name: "LXX" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
  });

  it("has no corpus switch on a Hebrew Strong's page", async () => {
    stubFetch(lxxPage);
    renderAt("/bible/strongs/H1254");
    await screen.findByText("Alle Stellen");
    expect(screen.queryByRole("group", { name: "Textkorpus" })).not.toBeInTheDocument();
  });

  it("switches to the LXX list with the word marked and the Elb1871 verse", async () => {
    const fetchMock = stubFetch(lxxPage);
    renderAt("/bible/strongs/G746");
    await userEvent.setup().click(await screen.findByRole("button", { name: "LXX" }));

    const list = await screen.findByRole("list", { name: "Vorkommen in der LXX" });
    expect(within(list).getAllByRole("listitem")).toHaveLength(2);
    const marked = within(list).getAllByText("ἀρχῇ")[0];
    expect(marked?.tagName).toBe("MARK");
    expect(marked).toHaveClass("bg-mark-linked");
    expect(within(list).getByText("ἐν").tagName).toBe("SPAN");
    expect(within(list).getByText("ἐν").closest("p")).toHaveAttribute("lang", "el");
    expect(within(list).getByText("Im Anfang schuf Gott")).toHaveAttribute("lang", "de");
    expect(within(list).getByText("Elb1871 1. Mose 1,1")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "LXX" })).toHaveAttribute("aria-pressed", "true");
    expect(
      fetchMock.mock.calls.some(([url]) =>
        String(url).includes("/strongsOccurrences/strongs/G746?corpus=lxx&page=1&pageSize=50"),
      ),
    ).toBe(true);
  });

  it("shows an addition with its anchor verse and no Elb text", async () => {
    stubFetch(lxxPage);
    renderAt("/bible/strongs/G746?corpus=lxx");
    const list = await screen.findByRole("list", { name: "Vorkommen in der LXX" });
    expect(
      within(list).getByText(/Zusatz der LXX, steht bei Elb1871 1. Mose 2,4/),
    ).toBeInTheDocument();
  });

  it("opens the reader at the Elb verse with the LXX side panel", async () => {
    stubFetch(lxxPage);
    renderAt("/bible/strongs/G746?corpus=lxx");
    await userEvent
      .setup()
      .click(await screen.findByRole("link", { name: "Gen.1:1 im Leser mit der LXX öffnen" }));

    expect(await screen.findByTestId("where")).toHaveTextContent("/bible/1/1#v1");
    expect(localStorage.getItem(SIDE_PANEL_KEY)).toBe("lxx");
  });

  it("shows the unreviewed line to maintainers only", async () => {
    stubFetch({ ...lxxPage, unconfirmedCount: 7 }, ["Maintainer"]);
    renderAt("/bible/strongs/G746?corpus=lxx");
    expect(await screen.findByText("7 weitere Vorkommen noch ungeprüft")).toBeInTheDocument();
  });

  it("shows no unreviewed line without the count", async () => {
    stubFetch(lxxPage);
    renderAt("/bible/strongs/G746?corpus=lxx");
    await screen.findByRole("list", { name: "Vorkommen in der LXX" });
    expect(screen.queryByText(/noch ungeprüft/)).not.toBeInTheDocument();
  });

  it("shows the empty state when nothing is confirmed", async () => {
    stubFetch({ ...lxxPage, totalItems: 0, totalPages: 0, lxxItems: [], unconfirmedCount: 3 }, [
      "Admin",
    ]);
    renderAt("/bible/strongs/G746?corpus=lxx");
    expect(await screen.findByText("Noch keine geprüften LXX-Vorkommen")).toBeInTheDocument();
    expect(screen.getByText("3 weitere Vorkommen noch ungeprüft")).toBeInTheDocument();
  });

  it("goes back to the NT list without the corpus parameter", async () => {
    const fetchMock = stubFetch(lxxPage);
    renderAt("/bible/strongs/G746?corpus=lxx");
    await userEvent.setup().click(await screen.findByRole("button", { name: "NT" }));
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([url]) =>
          String(url).endsWith("/strongsOccurrences/strongs/G746?page=1&pageSize=50"),
        ),
      ).toBe(true),
    );
    expect(screen.getByRole("button", { name: "NT" })).toHaveAttribute("aria-pressed", "true");
  });
});
