import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { SIDE_PANEL_KEY, TSK_ONLY_HIGHLIGHTED_KEY } from "@/lib/bible/sidePanel";
import type { BibleStructureBook, CrossReference } from "@/lib/bible/types";
import { fixtureBooks, fixtureChapterWords } from "@/test/fixtures/bibleStructure";
import { ChapterPage } from "./ChapterPage";

vi.mock("@mdxeditor/editor", () => import("@/test/mdxEditorMock"));

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

const highlighted: CrossReference = {
  bibleBookId: 19,
  chapter: 33,
  verse: 6,
  rating: "highlight",
  comment: "Schöpfung durch das Wort",
};
const neutral: CrossReference = { bibleBookId: 19, chapter: 33, verse: 9 };
const hidden: CrossReference = { bibleBookId: 19, chapter: 104, verse: 30, rating: "hide" };

/** The API: hidden links only with `includeHidden=true` for curators (the server ignores it for others). */
function stubFetch(roles?: string[]) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.includes("/meta-bible/structure")) return new Response(JSON.stringify({ books }));
    if (url.includes("/auth/me")) {
      return roles
        ? new Response(JSON.stringify({ userId: 1, email: "a@b.c", displayName: "Ada", roles }))
        : new Response("", { status: 401 });
    }
    if (url.includes("/revision/levels")) return new Response(JSON.stringify([]));
    if (url.includes("/chapterCrossReferences/")) {
      const withHidden = url.includes("includeHidden=true") && roles !== undefined;
      return new Response(
        JSON.stringify({
          bibleBookId: 43,
          chapter: 3,
          verses: [
            { verse: 1, references: [highlighted, neutral] },
            { verse: 2, references: withHidden ? [neutral, hidden] : [neutral] },
          ],
        }),
      );
    }
    if (url.includes("/chapterOriginals/")) return new Response(JSON.stringify({ verses: [] }));
    if (url.includes("/notes/chapter/")) return new Response(JSON.stringify({ notes: [] }));
    return new Response(JSON.stringify(fixtureChapterWords));
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [{ path: "/bible/:bookId/:chapter", element: <ChapterPage /> }],
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

/** Every media query matches: the 120rem layout with the left margin. */
function stubWideScreen() {
  vi.stubGlobal(
    "matchMedia",
    (query: string) =>
      ({
        matches: true,
        media: query,
        addEventListener: () => {},
        removeEventListener: () => {},
      }) as unknown as MediaQueryList,
  );
}

const panel = (verse: number) =>
  screen.queryByRole("group", { name: `Querverweise zu Vers ${verse}` });

beforeEach(async () => {
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  await i18n.changeLanguage("de");
  stubWideScreen();
});
afterEach(() => {
  vi.unstubAllGlobals();
  localStorage.removeItem(SIDE_PANEL_KEY);
  localStorage.removeItem(TSK_ONLY_HIGHLIGHTED_KEY);
});

describe("ChapterPage TSK curation (roadmap 0024.3f)", () => {
  it("shows readers the highlighted link starred and first, no pen and no curate switch", async () => {
    stubFetch();
    renderAt("/bible/43/3");
    const verse1 = await screen.findByRole("group", { name: "Querverweise zu Vers 1" });
    const links = within(verse1).getAllByRole("link");
    expect(links[0]).toHaveAccessibleName("Psalmen 33,6 (hervorgehoben)");
    expect(links[0]?.className).toContain("font-medium");
    expect(links[1]).toHaveAccessibleName("Psalmen 33,9");
    expect(screen.queryByRole("button", { name: /^Querverweis .* bearbeiten$/ })).toBeNull();
    expect(screen.getByRole("switch", { name: "Nur hervorgehobene" })).toBeInTheDocument();
    expect(screen.queryByRole("switch", { name: "Ausgeblendete zeigen" })).toBeNull();
  });

  it("'Nur hervorgehobene' lists only highlighted links and survives a remount", async () => {
    stubFetch();
    const user = userEvent.setup();
    const first = renderAt("/bible/43/3");
    await screen.findByRole("group", { name: "Querverweise zu Vers 2" });
    const toggle = screen.getByRole("switch", { name: "Nur hervorgehobene" });
    expect(toggle).toHaveAttribute("aria-checked", "false");
    await user.click(toggle);
    expect(toggle).toHaveAttribute("aria-checked", "true");
    expect(localStorage.getItem(TSK_ONLY_HIGHLIGHTED_KEY)).toBe("true");
    // verse 2 has no highlighted link: its panel is gone, verse 1 keeps only Ps 33,6
    expect(panel(2)).toBeNull();
    expect(within(panel(1) as HTMLElement).getAllByRole("link")).toHaveLength(1);

    first.unmount();
    renderAt("/bible/43/3");
    const verse1 = await screen.findByRole("group", { name: "Querverweise zu Vers 1" });
    expect(screen.getByRole("switch", { name: "Nur hervorgehobene" })).toHaveAttribute(
      "aria-checked",
      "true",
    );
    expect(within(verse1).getAllByRole("link")).toHaveLength(1);
    expect(panel(2)).toBeNull();
  });

  it("curate mode refetches with includeHidden=true and shows the hidden link greyed with a pen", async () => {
    const fetchMock = stubFetch(["Maintainer"]);
    const user = userEvent.setup();
    renderAt("/bible/43/3");
    const curate = await screen.findByRole("switch", { name: "Ausgeblendete zeigen" });
    await screen.findByRole("group", { name: "Querverweise zu Vers 2" });
    expect(screen.queryByRole("link", { name: /Psalmen 104,30/ })).toBeNull();
    // maintainers get a pen per link
    expect(
      screen.getAllByRole("button", { name: "Querverweis Psalmen 33,9 bearbeiten" }).length,
    ).toBeGreaterThan(0);

    await user.click(curate);
    const hiddenLink = await screen.findByRole("link", { name: "Psalmen 104,30 (ausgeblendet)" });
    expect(hiddenLink.className).toContain("line-through");
    expect(hiddenLink.className).toContain("text-fg-muted");
    expect(
      fetchMock.mock.calls.some(([url]) =>
        String(url).endsWith("/chapterCrossReferences/bookId/43/chapterId/3?includeHidden=true"),
      ),
    ).toBe(true);

    // the editor of the hidden link starts at "Ausblenden"
    await user.click(screen.getByRole("button", { name: "Querverweis Psalmen 104,30 bearbeiten" }));
    const editor = screen.getByRole("dialog", { name: "Querverweis Psalmen 104,30 bearbeiten" });
    expect(within(editor).getByRole("button", { name: "Ausblenden" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );

    // curate mode off: the reader list again
    await user.keyboard("{Escape}");
    await user.click(curate);
    await waitFor(() => expect(screen.queryByRole("link", { name: /Psalmen 104,30/ })).toBeNull());
  });

  it("does not send includeHidden for a signed-in non-maintainer", async () => {
    const fetchMock = stubFetch(["User"]);
    renderAt("/bible/43/3");
    await screen.findByRole("group", { name: "Querverweise zu Vers 1" });
    await waitFor(() =>
      expect(screen.getByRole("switch", { name: "Nur hervorgehobene" })).toBeInTheDocument(),
    );
    expect(screen.queryByRole("switch", { name: "Ausgeblendete zeigen" })).toBeNull();
    expect(screen.queryByRole("button", { name: /^Querverweis .* bearbeiten$/ })).toBeNull();
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes("includeHidden"))).toBe(false);
  });
});
