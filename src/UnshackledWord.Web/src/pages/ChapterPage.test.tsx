import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { BibleStructureBook } from "@/lib/bible/types";
import { fixtureBooks, fixtureChapterWords } from "@/test/fixtures/bibleStructure";
import { ChapterPage } from "./ChapterPage";

const john: BibleStructureBook = {
  id: 43,
  testament: "nt",
  nameDe: "Johannes",
  nameEn: "John",
  abbreviationDe: "Joh",
  abbreviationEn: "John",
  versesPerChapter: Array<number>(21).fill(20),
};
const books = [...fixtureBooks, john];

function Probe() {
  return <output data-testid="loc">{useLocation().pathname}</output>;
}

function stubFetch(chapterStatus = 200) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.includes("/meta-bible/structure")) {
      return new Response(JSON.stringify({ books }), { status: 200 });
    }
    return new Response(JSON.stringify(fixtureChapterWords), { status: chapterStatus });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

const chapterCalls = (fetchMock: ReturnType<typeof stubFetch>) =>
  fetchMock.mock.calls.filter(([url]) => String(url).includes("chapterWords"));

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [
      {
        path: "/bible/:bookId/:chapter",
        element: (
          <>
            <ChapterPage />
            <Probe />
          </>
        ),
      },
    ],
    { initialEntries: [path] },
  );
  return render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(async () => {
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("ChapterPage", () => {
  it("renders heading and verses", async () => {
    stubFetch();
    const { container } = renderAt("/bible/43/3");
    expect(
      await screen.findByRole("heading", { level: 1, name: "Johannes 3" }),
    ).toBeInTheDocument();
    await screen.findByText("Dieser");
    expect(container.querySelector("#v1")).not.toBeNull();
    expect(container.querySelector("#v3")).not.toBeNull();
    expect(container.querySelector("article")?.getAttribute("lang")).toBe("de");
  });

  it("highlights words sharing a Strong's number on hover", async () => {
    stubFetch();
    renderAt("/bible/43/3");
    const [war] = await screen.findAllByText("war");
    await userEvent.hover(war as HTMLElement);
    const same = screen.getAllByText("war");
    expect(same.every((el) => el.className.includes("bg-mark-same"))).toBe(true);
    expect(screen.getByText("ist").className).toContain("bg-mark-related");
    expect(screen.getByText("Mensch").className).not.toContain("bg-mark");
    await userEvent.unhover(war as HTMLElement);
    expect(screen.getByText("ist").className).not.toContain("bg-mark");
  });

  it("links to previous and next chapter", async () => {
    stubFetch();
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    const prev = screen.getAllByRole("link", { name: /Vorheriges Kapitel/ });
    const next = screen.getAllByRole("link", { name: /Nächstes Kapitel/ });
    expect(prev[0]?.getAttribute("href")).toBe("/bible/43/2");
    expect(next[0]?.getAttribute("href")).toBe("/bible/43/4");
  });

  it("disables previous at Genesis 1", async () => {
    stubFetch();
    renderAt("/bible/1/1");
    await screen.findByRole("heading", { level: 1, name: "1. Mose 1" });
    expect(screen.queryByRole("link", { name: /Vorheriges Kapitel/ })).toBeNull();
    expect(document.querySelector('span[aria-disabled="true"]')).not.toBeNull();
  });

  it.each(["/bible/1/51", "/bible/abc/1"])(
    "shows not-found for %s without fetching",
    async (path) => {
      const fetchMock = stubFetch();
      renderAt(path);
      expect(await screen.findByText("Dieses Kapitel gibt es nicht.")).toBeInTheDocument();
      expect(chapterCalls(fetchMock)).toHaveLength(0);
    },
  );

  it("shows an error and retries", async () => {
    const fetchMock = stubFetch(500);
    renderAt("/bible/43/3");
    expect(await screen.findByText("Das Kapitel konnte nicht geladen werden.")).toBeInTheDocument();
    expect(chapterCalls(fetchMock)).toHaveLength(1);
    await userEvent.click(screen.getByRole("button", { name: "Erneut versuchen" }));
    await waitFor(() => expect(chapterCalls(fetchMock).length).toBeGreaterThanOrEqual(2));
  });

  it("navigates with ArrowRight, but not from an input", async () => {
    stubFetch();
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    const input = document.createElement("input");
    document.body.appendChild(input);
    input.focus();
    await userEvent.keyboard("{ArrowRight}");
    expect(screen.getByTestId("loc").textContent).toBe("/bible/43/3");
    input.remove();
    await userEvent.keyboard("{ArrowRight}");
    await waitFor(() => expect(screen.getByTestId("loc").textContent).toBe("/bible/43/4"));
  });
});
