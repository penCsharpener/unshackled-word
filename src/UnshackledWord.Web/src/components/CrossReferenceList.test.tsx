import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { describe, expect, it, vi } from "vitest";
import type { BibleStructureBook } from "@/lib/bible/types";
import "@/i18n";
import { CrossReferenceList, cardLeft } from "./CrossReferenceList";

const books = [
  { id: 1, nameDe: "1. Mose", nameEn: "Genesis", abbreviationDe: "1Mo", abbreviationEn: "Gen" },
  { id: 19, nameDe: "Psalm", nameEn: "Psalms", abbreviationDe: "Ps", abbreviationEn: "Ps" },
] as BibleStructureBook[];

const references = [
  { bibleBookId: 1, chapter: 1, verse: 10 },
  { bibleBookId: 19, chapter: 33, verse: 6, endVerse: 9 },
  { bibleBookId: 1, chapter: 2, verse: 4 },
];

function renderInline() {
  return render(
    <MemoryRouter>
      <CrossReferenceList variant="inline" references={references} books={books} />
    </MemoryRouter>,
  );
}

describe("CrossReferenceList inline", () => {
  it("links abbreviated labels to the verse and names the full book for screen readers", () => {
    renderInline();
    const link = screen.getByRole("link", { name: /1\. Mose 1,10|Genesis 1,10/ });
    expect(link).toHaveTextContent(/^(1Mo|Gen) 1,10$/);
    expect(link).toHaveAttribute("href", "/bible/1/1#v10");
    expect(screen.getByRole("link", { name: /33,6–9/ })).toHaveAttribute("href", "/bible/19/33#v6");
  });

  it("is one Tab stop and moves with the arrow keys", async () => {
    const user = userEvent.setup();
    renderInline();
    const links = screen.getAllByRole("link");
    expect(links.map((l) => l.tabIndex)).toEqual([0, -1, -1]);
    links[0]?.focus();
    await user.keyboard("{ArrowRight}");
    expect(links[1]).toHaveFocus();
    expect(links.map((l) => l.tabIndex)).toEqual([-1, 0, -1]);
    await user.keyboard("{End}");
    expect(links[2]).toHaveFocus();
    await user.keyboard("{Home}");
    expect(links[0]).toHaveFocus();
  });
});

describe("CrossReferenceList preview", () => {
  it("loads all texts of the verse once and shows the hovered target", async () => {
    const fetchMock = vi.fn(async () =>
      Response.json({
        references: [
          {
            bibleBookId: 1,
            chapter: 1,
            verse: 10,
            verses: [{ verse: 10, text: "Und Gott nannte" }],
          },
          { bibleBookId: 1, chapter: 2, verse: 4, verses: [{ verse: 4, text: "Dies ist die" }] },
        ],
      }),
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <MemoryRouter>
          <CrossReferenceList
            variant="inline"
            references={[
              { bibleBookId: 1, chapter: 1, verse: 10 },
              { bibleBookId: 1, chapter: 2, verse: 4 },
            ]}
            books={books}
            preview={{ bookId: 43, chapter: 3, verse: 1, level: 200 }}
          />
        </MemoryRouter>
      </QueryClientProvider>,
    );
    const [first, second] = screen.getAllByRole("link") as [HTMLElement, HTMLElement];
    await user.hover(first);
    expect(await screen.findByText("Und Gott nannte")).toBeInTheDocument();
    await user.unhover(first);
    await user.hover(second);
    expect(await screen.findByText("Dies ist die")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);
    vi.unstubAllGlobals();
  });

  it("widens the card for originals from md and keeps it inside the viewport at the right edge", async () => {
    vi.stubGlobal("innerWidth", 1000);
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query === "(min-width: 48rem)",
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          references: [
            {
              bibleBookId: 1,
              chapter: 1,
              verse: 10,
              verses: [
                {
                  verse: 10,
                  text: "Und Gott nannte",
                  original: { language: "hebrew", text: "וַיִּקְרָא" },
                },
              ],
            },
            { bibleBookId: 1, chapter: 2, verse: 4, verses: [{ verse: 4, text: "Dies ist die" }] },
          ],
        }),
      ),
    );
    const user = userEvent.setup();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <MemoryRouter>
          <CrossReferenceList
            variant="inline"
            references={[
              { bibleBookId: 1, chapter: 1, verse: 10 },
              { bibleBookId: 1, chapter: 2, verse: 4 },
            ]}
            books={books}
            preview={{ bookId: 1, chapter: 1, verse: 1, level: 200 }}
          />
        </MemoryRouter>
      </QueryClientProvider>,
    );
    const [first, second] = screen.getAllByRole("link") as [HTMLElement, HTMLElement];
    const at = (element: HTMLElement, left: number) => {
      const rect = {
        left,
        right: left + 40,
        top: 100,
        bottom: 120,
        width: 40,
        height: 20,
      } as DOMRect;
      element.getBoundingClientRect = () => rect;
      element.getClientRects = () => [rect] as unknown as DOMRectList;
    };
    at(first, 900);
    at(second, 900);
    await user.hover(first);
    expect(await screen.findByText("וַיִּקְרָא")).toBeInTheDocument();
    // wide card: 1000 - 640 - 8
    expect(screen.getByRole("tooltip").style.left).toBe("352px");
    await user.unhover(first);
    await user.hover(second);
    expect(await screen.findByText("Dies ist die")).toBeInTheDocument();
    // no originals: the narrow card, 1000 - 320 - 8
    expect(screen.getByRole("tooltip").style.left).toBe("672px");
    vi.unstubAllGlobals();
  });
});

describe("cardLeft", () => {
  it("clamps to the viewport with the narrow and the wide width", () => {
    expect(cardLeft(100, false, 1000)).toBe(100);
    expect(cardLeft(900, false, 1000)).toBe(672);
    expect(cardLeft(900, true, 1000)).toBe(352);
    expect(cardLeft(2, true, 1000)).toBe(8);
    // phone narrower than the card: the left gap wins
    expect(cardLeft(200, false, 300)).toBe(8);
  });
});
