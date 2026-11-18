import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { DefinitionSegment } from "@/lib/bible/types";
import "@/i18n";
import { ScriptureText } from "./ScriptureText";

const segments: DefinitionSegment[] = [
  { text: "right hand, see H1234 and " },
  {
    text: "Mat.5:29",
    ref: {
      bookId: 40,
      path: "/bible/40/5?v=29#v29",
      spans: [{ startChapter: 5, startVerse: 29, endChapter: 5, endVerse: 29 }],
    },
  },
];

function setup(coarse = false) {
  vi.stubGlobal("matchMedia", (query: string) => ({
    matches: coarse && query.includes("coarse"),
    addEventListener: () => {},
    removeEventListener: () => {},
  }));
  vi.stubGlobal(
    "fetch",
    vi.fn(async () =>
      Response.json({
        passages: [
          {
            bookId: 40,
            chapter: 5,
            verse: 29,
            verses: [{ verse: 29, text: "Wenn aber dein rechtes Auge" }],
          },
        ],
        truncated: false,
      }),
    ),
  );
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <ScriptureText text="x" segments={segments} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => vi.unstubAllGlobals());

describe("ScriptureText", () => {
  it("links references and Strong's numbers in plain segments", () => {
    setup();
    expect(screen.getByRole("link", { name: "Mat.5:29" }).getAttribute("href")).toBe(
      "/bible/40/5?v=29#v29",
    );
    expect(screen.getByRole("link", { name: "H1234" }).getAttribute("href")).toBe(
      "/bible/strongs/H1234",
    );
  });

  it("falls back to the plain text with Strong's links", () => {
    render(
      <MemoryRouter>
        <ScriptureText text="see G1188 now" />
      </MemoryRouter>,
    );
    expect(screen.getByRole("link", { name: "G1188" })).toBeTruthy();
    expect(screen.queryByRole("tooltip")).toBeNull();
  });

  it("shows the passage preview on hover and on focus", async () => {
    setup();
    const link = screen.getByRole("link", { name: "Mat.5:29" });
    await userEvent.hover(link);
    await waitFor(() => expect(screen.getByRole("tooltip").textContent).toContain("rechtes Auge"));
    await userEvent.unhover(link);
    expect(screen.queryByRole("tooltip")).toBeNull();
    link.focus();
    await waitFor(() => expect(screen.getByRole("tooltip")).toBeTruthy());
  });

  it("shows no preview on a coarse pointer", async () => {
    setup(true);
    await userEvent.hover(screen.getByRole("link", { name: "Mat.5:29" }));
    expect(screen.queryByRole("tooltip")).toBeNull();
  });
});
