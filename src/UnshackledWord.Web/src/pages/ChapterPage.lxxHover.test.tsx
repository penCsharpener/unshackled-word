import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { SIDE_PANEL_KEY } from "@/lib/bible/sidePanel";
import type {
  GetChapterOriginalsResponse,
  GetLxxChapterResponse,
  LxxWord,
} from "@/lib/bible/types";
import { fixtureBooks } from "@/test/fixtures/bibleStructure";
import { ChapterPage } from "./ChapterPage";

vi.mock("@mdxeditor/editor", () => import("@/test/mdxEditorMock"));

const german = (id: number, positionInVerse: number, wordInContext: string) => ({
  id,
  verse: 1,
  positionInVerse,
  wordInContext,
});

const lxxWord = (
  id: number,
  position: number,
  greek: string,
  hebrewWordIds?: number[],
): LxxWord => ({
  id,
  position,
  greek,
  ...(hebrewWordIds && { hebrewWordIds }),
});

/** Ps 10:1 carries the unit Ps 9:22 (Rahlfs counting): the Hebrew links belong to the Elb verse it is shown at. */
const lxxResponse = (chapter: number): GetLxxChapterResponse => ({
  bibleBookId: 19,
  chapter,
  verses: [
    {
      hebRefId: 19000000 + chapter * 1000 + 1,
      verse: 1,
      units: [
        {
          lxxVerseId: 1,
          bibleBookId: 19,
          chapter: 9,
          verse: 22,
          subverse: 0,
          ref: "Ps.9:22",
          textForm: "",
          mapKind: "one",
          isAddition: false,
          differsFromHebrew: true,
          words: [
            lxxWord(50, 1, "ΙΝΑΤΙ", [900]),
            lxxWord(51, 2, "ΚΥΡΙΕ"),
            lxxWord(52, 3, "ΑΠΕΣΤΗΣ", []),
          ],
        },
      ],
    },
  ],
});

const originals: GetChapterOriginalsResponse = {
  bibleBookId: 19,
  chapter: 10,
  language: "hebrew",
  verses: [
    {
      verse: 1,
      words: [
        { id: 900, position: 1, original: "לָמָה" },
        { id: 901, position: 2, original: "יְהוָה" },
      ],
      links: [
        { germanId: 1, originalId: 900 },
        { germanId: 2, originalId: 901 },
      ],
    },
  ],
};

function stubFetch() {
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.includes("/meta-bible/structure"))
        return new Response(JSON.stringify({ books: fixtureBooks }));
      if (url.includes("/auth/me")) return new Response("", { status: 401 });
      if (url.includes("/revision/levels")) return new Response(JSON.stringify([]));
      if (url.includes("/lxx/chapter/")) {
        const chapter = Number(url.split("/").pop());
        return new Response(JSON.stringify(lxxResponse(chapter)));
      }
      if (url.includes("/chapterOriginals/")) return new Response(JSON.stringify(originals));
      if (url.includes("/chapterCrossReferences/") || url.includes("/similarVerses/"))
        return new Response(JSON.stringify({ verses: [] }));
      const chapter = url.includes("chapterId/11") || url.includes("/11") ? 11 : 10;
      return new Response(
        JSON.stringify({
          bibleBookId: 19,
          chapter,
          words: [german(1, 1, "Warum"), german(2, 2, "HERR"), german(3, 3, "stehst")],
        }),
      );
    }),
  );
}

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [{ path: "/bible/:bookId/:chapter", element: <ChapterPage /> }],
    { initialEntries: [path] },
  );
  render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
  return router;
}

beforeEach(async () => {
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  localStorage.setItem(SIDE_PANEL_KEY, "lxx");
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
  await i18n.changeLanguage("de");
});
afterEach(() => {
  vi.unstubAllGlobals();
  localStorage.removeItem(SIDE_PANEL_KEY);
});

const marked = (name: string) => screen.getByRole("button", { name: new RegExp(name) });

describe("ChapterPage LXX hover link (0028.10f)", () => {
  it("marks the German words of the linked Hebrew word and clears on leave", async () => {
    stubFetch();
    renderAt("/bible/19/10");
    const user = userEvent.setup();
    const greek = await screen.findByRole("button", { name: /ΙΝΑΤΙ/ });
    await waitFor(() => expect(marked("Warum")).toBeInTheDocument());

    await user.hover(greek);
    expect(marked("Warum").className).toContain("bg-mark-linked");
    expect(marked("HERR").className).not.toContain("bg-mark-linked");

    await user.unhover(greek);
    expect(marked("Warum").className).not.toContain("bg-mark-linked");
  });

  it("marks nothing for a word without links, also with an empty list", async () => {
    stubFetch();
    renderAt("/bible/19/10");
    const user = userEvent.setup();
    await screen.findByRole("button", { name: /ΙΝΑΤΙ/ });
    await waitFor(() => expect(marked("Warum")).toBeInTheDocument());

    for (const name of [/ΚΥΡΙΕ/, /ΑΠΕΣΤΗΣ/]) {
      await user.hover(screen.getByRole("button", { name }));
      expect(document.querySelectorAll(".bg-mark-linked")).toHaveLength(0);
    }
  });

  it("marks on keyboard focus", async () => {
    stubFetch();
    renderAt("/bible/19/10");
    await waitFor(() => expect(marked("Warum")).toBeInTheDocument());
    const greek = await screen.findByRole("button", { name: /ΙΝΑΤΙ/ });
    act(() => greek.focus());
    expect(marked("Warum").className).toContain("bg-mark-linked");
    act(() => greek.blur());
    expect(marked("Warum").className).not.toContain("bg-mark-linked");
  });

  it("drops the marks when the chapter changes under the hovered word", async () => {
    stubFetch();
    const router = renderAt("/bible/19/10");
    const user = userEvent.setup();
    await user.hover(await screen.findByRole("button", { name: /ΙΝΑΤΙ/ }));
    expect(marked("Warum").className).toContain("bg-mark-linked");

    await act(async () => {
      await router.navigate("/bible/19/11");
    });
    await waitFor(() => expect(marked("Warum")).toBeInTheDocument());
    await screen.findByRole("button", { name: /ΙΝΑΤΙ/ });
    expect(document.querySelectorAll(".bg-mark-linked")).toHaveLength(0);
  });
});
