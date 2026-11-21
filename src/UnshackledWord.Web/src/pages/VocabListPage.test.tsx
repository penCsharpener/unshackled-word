import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { json, stubApi } from "@/test/authHelpers";
import { VocabListPage } from "./VocabListPage";

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

const entry = (over: Record<string, unknown>) => ({
  id: 1,
  rank: 1,
  lemma: "ὁ",
  transliteration: "ho",
  meanings: ["der, die, das"],
  occurrences: 20754,
  hasAudio: false,
  reviewed: false,
  strongsKeys: [],
  ...over,
});

const page = (code: string, items: unknown[], total = items.length, skip = 0) => ({
  code,
  total,
  skip,
  take: 50,
  items,
});

function renderPage(route = "/vocab") {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <VocabListPage />
    </MemoryRouter>,
  );
}

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("VocabListPage", () => {
  it("lists the New Testament course by default with its totals", async () => {
    stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50": () =>
        json(page("nt-greek", [entry({})], 5407)),
    });
    renderPage();
    expect((await screen.findAllByRole("link", { name: "ὁ" })).length).toBeGreaterThan(0);
    expect(screen.getByText("5,407 entries · 141,774 occurrences")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "NT · Greek" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("switches to the Hebrew course and marks its lemmas rtl", async () => {
    const fetchMock = stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50": () =>
        json(page("nt-greek", [entry({})], 5407)),
      "GET /vocab/courses/ot-hebrew/entries?skip=0&take=50": () =>
        json(page("ot-hebrew", [entry({ id: 9, rank: 1, lemma: "בָּרָא", transliteration: "bara" })])),
    });
    renderPage();
    await screen.findAllByRole("link", { name: "ὁ" });
    await userEvent.click(screen.getByRole("button", { name: "OT · Hebrew/Aramaic" }));
    const [link] = await screen.findAllByRole("link", { name: "בָּרָא" });
    expect(link).toHaveAttribute("dir", "rtl");
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/vocab/courses/ot-hebrew/entries?skip=0&take=50",
      expect.anything(),
    );
  });

  it("sends the debounced search to the API", async () => {
    const fetchMock = stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50": () =>
        json(page("nt-greek", [entry({})], 5407)),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50&q=lo": () => json(page("nt-greek", [])),
    });
    renderPage();
    await screen.findAllByRole("link", { name: "ὁ" });
    await userEvent.type(screen.getByLabelText("Search"), "lo");
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        "/api/vocab/courses/nt-greek/entries?skip=0&take=50&q=lo",
        expect.anything(),
      ),
    );
    expect(await screen.findByText("No entries for this search.")).toBeInTheDocument();
  });

  it("shows the accepted meanings of each entry in rank order", async () => {
    stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50": () =>
        json(
          page("nt-greek", [entry({ reviewed: true, meanings: ["Wort", "Rede", "Sache"] })], 5407),
        ),
    });
    renderPage();
    expect((await screen.findAllByText("Wort, Rede, Sache")).length).toBeGreaterThan(0);
    expect(screen.queryByText("unreviewed")).not.toBeInTheDocument();
  });

  it("pages forward with the Next button", async () => {
    const fetchMock = stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50": () =>
        json(page("nt-greek", [entry({})], 120)),
      "GET /vocab/courses/nt-greek/entries?skip=50&take=50": () =>
        json(page("nt-greek", [entry({ id: 51, rank: 51, lemma: "ἐν" })], 120, 50)),
    });
    renderPage();
    await screen.findAllByRole("link", { name: "ὁ" });
    expect(screen.getByRole("button", { name: "Previous" })).toBeDisabled();
    await userEvent.click(screen.getByRole("button", { name: "Next" }));
    expect((await screen.findAllByRole("link", { name: "ἐν" })).length).toBeGreaterThan(0);
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/vocab/courses/nt-greek/entries?skip=50&take=50",
      expect.anything(),
    );
  });
});
