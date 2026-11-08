import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import { BiblePage } from "./BiblePage";

beforeEach(async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => new Response(JSON.stringify(fixtureStructure))),
  );
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("BiblePage", () => {
  it("renders the title and an open OT picker", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter>
          <BiblePage />
        </MemoryRouter>
      </QueryClientProvider>,
    );
    expect(screen.getByRole("heading", { level: 1, name: "Bibel" })).toBeInTheDocument();
    expect(await screen.findByRole("link", { name: "1. Mose 1" })).toBeInTheDocument();
  });
});
