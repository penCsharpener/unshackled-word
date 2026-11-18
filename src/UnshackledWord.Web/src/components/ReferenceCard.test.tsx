import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { ParsedReference } from "@/lib/bible/types";
import { fixtureBooks } from "@/test/fixtures/bibleStructure";
import { ReferenceCard } from "./ReferenceCard";

const passage = (display: string, path: string): ParsedReference => ({
  bookId: 66,
  bookOnly: false,
  display,
  path,
  groups: [{ display, path }],
});

const renderCard = (props: Partial<Parameters<typeof ReferenceCard>[0]> = {}) =>
  render(
    <MemoryRouter>
      <ReferenceCard
        passages={[]}
        books={fixtureBooks}
        language="de"
        onNavigate={() => {}}
        {...props}
      />
    </MemoryRouter>,
  );

describe("ReferenceCard", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it("renders nothing without content", () => {
    const { container } = renderCard();
    expect(container).toBeEmptyDOMElement();
  });

  it("links a passage and reports the navigation", async () => {
    const onNavigate = vi.fn();
    renderCard({ passages: [passage("Off 2,1–5", "/bible/66/2?v=1-5")], onNavigate });
    const link = screen.getByRole("link", { name: "Off 2,1–5" });
    expect(link).toHaveAttribute("href", "/bible/66/2?v=1-5");
    await userEvent.click(link);
    expect(onNavigate).toHaveBeenCalled();
  });

  it("gives several chapter groups one link each", () => {
    renderCard({
      passages: [
        {
          ...passage("Röm 8,28; 12,1–2", "/bible/45/8?v=28"),
          groups: [
            { display: "Röm 8,28", path: "/bible/45/8?v=28" },
            { display: "12,1–2", path: "/bible/45/12?v=1-2" },
          ],
        },
      ],
    });
    expect(screen.getAllByRole("link")).toHaveLength(2);
  });

  it("shows the book-only link row and an error hint", () => {
    renderCard({
      bookRefs: [{ bookId: 19, bookOnly: true, display: "Psalmen", groups: [] }],
      error: { code: "verseOutOfRange", position: 4, args: { value: "99", max: "31" } },
    });
    expect(screen.getByRole("link", { name: "Zu Psalmen 1" })).toHaveAttribute(
      "href",
      "/bible/19/1",
    );
    expect(screen.getByRole("alert")).toHaveTextContent("Vers 99 gibt es nicht (höchstens 31).");
  });

  it("falls back to a generic hint for an unknown error code and picks a candidate", async () => {
    const onPickBook = vi.fn();
    renderCard({
      error: { code: "somethingNew", position: 0, args: {} },
      bookCandidates: [39, 40],
      onPickBook,
    });
    expect(screen.getByRole("alert")).toHaveTextContent("nicht lesbar");
    await userEvent.click(screen.getByRole("button", { name: "Matthäus" }));
    expect(onPickBook).toHaveBeenCalledWith(expect.objectContaining({ id: 40 }));
  });

  describe("verse text", () => {
    afterEach(() => vi.unstubAllGlobals());

    const withText = (verseCount: number) => {
      const fetchMock = vi.fn(async (_url: string) =>
        Response.json({
          passages: [
            {
              bookId: 43,
              chapter: 3,
              verse: 16,
              endVerse: 16 + verseCount - 1,
              verses: Array.from({ length: verseCount }, (_, i) => ({
                verse: 16 + i,
                text: `Text ${16 + i}`,
              })),
            },
          ],
          truncated: false,
        }),
      );
      vi.stubGlobal("fetch", fetchMock);
      render(
        <QueryClientProvider client={new QueryClient()}>
          <MemoryRouter>
            <ReferenceCard
              passages={[
                {
                  ...passage("Joh 3,16", "/bible/43/3?v=16-22"),
                  bookId: 43,
                  spans: [
                    {
                      startChapter: 3,
                      startVerse: 16,
                      endChapter: 3,
                      endVerse: 16 + verseCount - 1,
                    },
                  ],
                },
              ]}
              books={fixtureBooks}
              language="de"
              level={300}
              onNavigate={() => {}}
            />
          </MemoryRouter>
        </QueryClientProvider>,
      );
      return fetchMock;
    };

    it("shows the verse text of the reference", async () => {
      const fetchMock = withText(1);
      expect(await screen.findByText("Text 16")).toBeInTheDocument();
      const url = String(fetchMock.mock.calls[0]?.[0]);
      expect(url).toContain("/passageText?");
      expect(decodeURIComponent(url)).toContain("spans=43003016");
      expect(url).toContain("level=300");
    });

    it("shows five verses and counts the rest", async () => {
      withText(7);
      expect(await screen.findByText("Text 20")).toBeInTheDocument();
      expect(screen.queryByText("Text 21")).not.toBeInTheDocument();
      expect(screen.getByText("… 2 weitere Verse")).toBeInTheDocument();
    });

    it("fetches nothing for a whole chapter", () => {
      const fetchMock = vi.fn();
      vi.stubGlobal("fetch", fetchMock);
      renderCard({
        passages: [
          {
            ...passage("Offb 2", "/bible/66/2"),
            spans: [{ startChapter: 2, startVerse: 0, endChapter: 2, endVerse: 999 }],
          },
        ],
      });
      expect(fetchMock).not.toHaveBeenCalled();
    });
  });
});
