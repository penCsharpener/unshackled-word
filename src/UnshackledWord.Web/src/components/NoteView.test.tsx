import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import { NoteView } from "./NoteView";

// The real editor has its own test; this stand-in turns `[text](url)` into anchors like the link plugin does.
vi.mock("@mdxeditor/editor", () => ({
  MDXEditor: ({ markdown }: { markdown: string }) => (
    <div>
      {markdown.split(/(\[[^\]]*\]\([^)]*\))/).map((part, index) => {
        const link = /^\[([^\]]*)\]\(([^)]*)\)$/.exec(part);
        const key = `${index}`;
        return link ? (
          <a key={key} href={link[2]}>
            {link[1]}
          </a>
        ) : (
          <span key={key}>{part}</span>
        );
      })}
    </div>
  ),
  headingsPlugin: () => ({}),
  listsPlugin: () => ({}),
  quotePlugin: () => ({}),
  linkPlugin: () => ({}),
}));

function Where() {
  const location = useLocation();
  return <p data-testid="where">{location.pathname + location.search}</p>;
}

function renderNote(markdown: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={["/notes"]}>
        <Routes>
          <Route
            path="*"
            element={
              <>
                <NoteView
                  markdown={markdown}
                  authorDisplayName="Anna"
                  createdAt="2026-11-22T10:00:00Z"
                />
                <Where />
              </>
            }
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => vi.unstubAllGlobals());

describe("NoteView links", () => {
  it("navigates /bible links inside the SPA without a reload", async () => {
    renderNote("Vgl. [Röm 12,1-2](/bible/45/12?v=1-2#v1) hier");
    await userEvent.setup().click(screen.getByRole("link", { name: "Röm 12,1-2" }));
    expect(screen.getByTestId("where")).toHaveTextContent("/bible/45/12?v=1-2");
  });

  it("shows the verses of the hovered reference", async () => {
    const fetchMock = vi.fn(async (_url: string) =>
      Response.json({
        passages: [
          {
            bookId: 45,
            chapter: 12,
            verse: 1,
            endVerse: 2,
            verses: [
              { verse: 1, text: "Ich ermahne euch nun" },
              { verse: 2, text: "und seid nicht gleichförmig" },
            ],
          },
        ],
        truncated: false,
      }),
    );
    vi.stubGlobal("fetch", fetchMock);
    renderNote("Vgl. [Röm 12,1-2](/bible/45/12?v=1-2#v1) hier");
    await userEvent.setup().hover(screen.getByRole("link", { name: "Röm 12,1-2" }));
    expect(await screen.findByText("Ich ermahne euch nun")).toBeInTheDocument();
    expect(screen.getByText("und seid nicht gleichförmig")).toBeInTheDocument();
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain("45012001-45012002");
  });

  it("shows no preview on a coarse pointer", async () => {
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query.includes("coarse"),
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    renderNote("[Röm 12,1-2](/bible/45/12?v=1-2#v1)");
    await userEvent.setup().hover(screen.getByRole("link", { name: "Röm 12,1-2" }));
    expect(screen.queryByTestId("cross-reference-preview")).not.toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("opens external links in a new tab without opener", async () => {
    renderNote("siehe [Quelle](https://example.org/a)");
    const link = screen.getByRole("link", { name: "Quelle" });
    await waitFor(() => expect(link).toHaveAttribute("target", "_blank"));
    expect(link).toHaveAttribute("rel", "noopener noreferrer");
  });

  it("renders links with any other scheme as plain text", () => {
    renderNote("a [böse](javascript:alert(1)) b [daten](data:text/html,x) c");
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
    expect(screen.getByText(/a böse b daten c/)).toBeInTheDocument();
  });
});
