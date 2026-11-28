import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import { BiblePage } from "./BiblePage";
import { ChapterPage } from "./ChapterPage";

/** Empty database (roadmap 0031.3f): no verses, `hasText: false`, optional admin session. */
function stubEmptyDatabase(roles?: string[], hasText = false) {
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.includes("/meta-bible/structure")) {
        return new Response(JSON.stringify(fixtureStructure));
      }
      if (url.includes("/meta-bible/status")) {
        return new Response(JSON.stringify({ hasText }));
      }
      if (url.includes("/auth/me")) {
        return roles
          ? new Response(JSON.stringify({ userId: 1, email: "a@b.c", displayName: "Ada", roles }))
          : new Response("", { status: 401 });
      }
      if (url.includes("/revision/levels")) return new Response("[]");
      if (url.includes("chapterWords")) {
        return new Response(JSON.stringify({ bibleBookId: 1, chapter: 1, words: [] }));
      }
      return new Response(JSON.stringify({ verses: [], references: [], notes: [] }));
    }),
  );
}

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [
      { path: "/bible", element: <BiblePage /> },
      { path: "/bible/:bookId/:chapter", element: <ChapterPage /> },
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
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  window.HTMLElement.prototype.showPopover ??= vi.fn();
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe.each([
  ["reader home", "/bible"],
  ["chapter page", "/bible/1/1"],
])("empty database on the %s", (_name, path) => {
  it("shows the notice without a setup link to an anonymous visitor", async () => {
    stubEmptyDatabase();
    renderAt(path);
    expect(await screen.findByText("Noch keine Bibeltexte importiert.")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Zur Ersteinrichtung" })).toBeNull();
  });

  it("links an admin to /edit/setup", async () => {
    stubEmptyDatabase(["Admin"]);
    renderAt(path);
    const link = await screen.findByRole("link", { name: "Zur Ersteinrichtung" });
    expect(link).toHaveAttribute("href", "/edit/setup");
  });

  it("gives a maintainer without Admin no link", async () => {
    stubEmptyDatabase(["Maintainer"]);
    renderAt(path);
    await screen.findByText("Noch keine Bibeltexte importiert.");
    expect(screen.queryByRole("link", { name: "Zur Ersteinrichtung" })).toBeNull();
  });

  it("shows no notice once text exists", async () => {
    stubEmptyDatabase(undefined, true);
    renderAt(path);
    await screen.findByRole("heading", { level: 1 });
    await vi.waitFor(() =>
      expect(fetch).toHaveBeenCalledWith(
        expect.stringContaining("/meta-bible/status"),
        expect.anything(),
      ),
    );
    expect(screen.queryByText("Noch keine Bibeltexte importiert.")).toBeNull();
  });
});
