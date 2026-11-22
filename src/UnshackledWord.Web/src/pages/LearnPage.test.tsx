import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { json, me, stubApi } from "@/test/authHelpers";
import { LearnPage } from "./LearnPage";

const courses = {
  courses: [
    {
      code: "nt-greek",
      track: "G",
      nameDe: "Neues Testament (Griechisch)",
      nameEn: "New Testament (Greek)",
      totalEntries: 5407,
      totalOccurrences: 141774,
    },
    {
      code: "ot-hebrew",
      track: "H",
      nameDe: "Altes Testament (Hebräisch und Aramäisch)",
      nameEn: "Old Testament (Hebrew and Aramaic)",
      totalEntries: 7378,
      totalOccurrences: 239920,
    },
  ],
};

function renderLearn() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={["/learn"]}>
        <AuthProvider>
          <LearnPage />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("LearnPage", () => {
  it("shows the course cards with a sign-in hint to anonymous visitors", async () => {
    stubApi({
      "GET /auth/me": () => json({}, 401),
      "GET /vocab/courses": () => json(courses),
    });
    renderLearn();
    expect(await screen.findByText("Neues Testament (Griechisch)")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "anmelden" })).toHaveAttribute("href", "/login");
    expect(screen.queryByRole("link", { name: "Beginnen" })).toBeNull();
    expect(screen.getAllByRole("link", { name: "Wortliste ansehen" })).toHaveLength(2);
  });

  it("gives a signed-in learner a start link per course", async () => {
    stubApi({
      "GET /auth/me": () => json(me),
      "GET /vocab/courses": () => json(courses),
    });
    renderLearn();
    const starts = await screen.findAllByRole("link", { name: "Beginnen" });
    expect(starts.map((link) => link.getAttribute("href"))).toEqual([
      "/learn/nt-greek",
      "/learn/ot-hebrew",
    ]);
    expect(screen.queryByRole("link", { name: "anmelden" })).toBeNull();
  });
});
