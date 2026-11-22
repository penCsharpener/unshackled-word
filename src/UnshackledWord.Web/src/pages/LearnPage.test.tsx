import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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
    expect(screen.queryByRole("button", { name: "⚙ Quiz-Einstellungen" })).toBeNull();
  });

  it("opens the quiz settings from the header of a signed-in learner", async () => {
    stubApi({
      "GET /auth/me": () => json(me),
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/settings": () =>
        json({
          disabledQuizTypes: [],
          availableQuizTypes: ["multiple_choice", "reverse_multiple_choice"],
        }),
    });
    const user = userEvent.setup();
    renderLearn();
    await user.click(await screen.findByRole("button", { name: "⚙ Quiz-Einstellungen" }));
    expect(
      await screen.findByRole("switch", { name: "Deutsch → Lemma (Auswahl)" }),
    ).toBeInTheDocument();
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

  it("shows the learner's numbers and coverage on the course cards", async () => {
    stubApi({
      "GET /auth/me": () => json(me),
      "GET /vocab/courses": () =>
        json({
          changedCount: 0,
          courses: [
            {
              ...courses.courses[0],
              known: 142,
              due: 18,
              newAvailable: 5270,
              coverage: 62.4,
            },
          ],
        }),
    });
    renderLearn();
    expect(await screen.findByText("62,4 % des NT-Textes kennst du schon.")).toBeInTheDocument();
    expect(screen.getByText("142")).toBeInTheDocument();
    expect(screen.getByText("18")).toBeInTheDocument();
    expect(screen.getByText("5.270")).toBeInTheDocument();
    expect(screen.queryByRole("status")).toBeNull();
  });

  it("shows the Geändert notice when words were revised", async () => {
    stubApi({
      "GET /auth/me": () => json(me),
      "GET /vocab/courses": () => json({ changedCount: 3, courses: courses.courses }),
      "GET /vocab/changes": () => json({ count: 3, items: [] }),
    });
    renderLearn();
    expect(await screen.findByText("3 deiner Wörter wurden überarbeitet.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ansehen" })).toBeInTheDocument();
    // The notice never replaces the course cards.
    expect(screen.getAllByRole("link", { name: "Beginnen" })).toHaveLength(2);
  });

  it("hides the notice when no word was revised", async () => {
    stubApi({
      "GET /auth/me": () => json(me),
      "GET /vocab/courses": () => json({ changedCount: 0, courses: courses.courses }),
    });
    renderLearn();
    expect(await screen.findByText("Neues Testament (Griechisch)")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Ansehen" })).toBeNull();
  });
});
