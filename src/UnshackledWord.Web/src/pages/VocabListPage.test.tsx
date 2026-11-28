import { render, screen, waitFor, within } from "@testing-library/react";
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
  meanings: [{ german: "der, die, das", rank: 1 }],
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

  it("opens the list searched for the root from the root's Strong's link", async () => {
    const searched = `skip=0&take=50&q=${encodeURIComponent("μαρτυρέω")}`;
    const fetchMock = stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50": () =>
        json(
          page("nt-greek", [
            entry({
              id: 21,
              lemma: "συνεπιμαρτυρέω",
              strongsKeys: ["G4901"],
              root: {
                id: 4,
                root: "μαρτυρέω",
                strongsKey: "G3140",
                prefixes: "σύν + ἐπί",
                origin: "agent",
                isSimple: false,
              },
            }),
          ]),
        ),
      [`GET /vocab/courses/nt-greek/entries?${searched}`]: () =>
        json(page("nt-greek", [entry({ id: 22, lemma: "μαρτυρέω", strongsKeys: ["G3140"] })])),
    });
    renderPage();
    const links = await screen.findAllByRole("link", { name: "Open root G3140" });
    expect(screen.getAllByText("σύν + ἐπί").length).toBeGreaterThan(0);
    await userEvent.click(links[0] as HTMLElement);
    expect((await screen.findAllByRole("link", { name: "μαρτυρέω" })).length).toBeGreaterThan(0);
    expect(fetchMock).toHaveBeenCalledWith(
      `/api/vocab/courses/nt-greek/entries?${searched}`,
      expect.anything(),
    );
    expect(screen.getByLabelText("Search")).toHaveValue("μαρτυρέω");
  });

  it("makes the root a link to its family, which shows the chip, the toggle and the parent link", async () => {
    const compound = entry({
      id: 21,
      lemma: "ἐκπορεύω",
      strongsKeys: ["G1607"],
      root: {
        id: 4,
        root: "πορεύω",
        strongsKey: "G4198",
        prefixes: "ἐκ",
        origin: "prefill",
        isSimple: false,
      },
    });
    const fetchMock = stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50": () =>
        json(page("nt-greek", [compound])),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50&root=4": () =>
        json(
          page("nt-greek", [entry({ id: 20, lemma: "πορεύω", strongsKeys: ["G4198"] }), compound]),
        ),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50&root=4&subRoots=false": () =>
        json(page("nt-greek", [entry({ id: 20, lemma: "πορεύω", strongsKeys: ["G4198"] })])),
      "GET /vocab/roots/4": () =>
        json({
          id: 4,
          root: "πορεύω",
          strongsKey: "G4198",
          parent: { id: 2, root: "πόρος" },
          subRoots: [{ id: 5, root: "πορεία" }],
          members: [],
          memberTotal: 2,
        }),
    });
    renderPage();
    const links = await screen.findAllByRole("link", { name: "Show the word family of πορεύω" });
    await userEvent.click(links[0] as HTMLElement);

    const filter = await screen.findByTestId("family-filter");
    expect(within(filter).getByText(/Word family:/)).toHaveTextContent("πορεύω");
    expect(await within(filter).findByRole("link", { name: "↑ πόρος" })).toHaveAttribute(
      "href",
      "/vocab?root=2",
    );
    const toggle = await within(filter).findByRole("switch", { name: "with sub-roots" });
    expect(toggle).toHaveAttribute("aria-checked", "true");
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/vocab/courses/nt-greek/entries?skip=0&take=50&root=4",
      expect.anything(),
    );

    await userEvent.click(toggle);
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        "/api/vocab/courses/nt-greek/entries?skip=0&take=50&root=4&subRoots=false",
        expect.anything(),
      ),
    );

    await userEvent.click(screen.getByRole("button", { name: "Remove the word family filter" }));
    await waitFor(() => expect(screen.queryByTestId("family-filter")).toBeNull());
  });

  it("opens the whole family from a searched list: the search and the page are dropped, the track is kept", async () => {
    stubApi({
      "GET /vocab/courses": () => json(courses),
      [`GET /vocab/courses/nt-greek/entries?skip=50&take=50&q=${encodeURIComponent("πορευ")}`]:
        () =>
          json(
            page("nt-greek", [
              entry({
                id: 20,
                lemma: "πορεύω",
                strongsKeys: ["G4198"],
                root: {
                  id: 4,
                  root: "πορεύω",
                  strongsKey: "G4198",
                  origin: "prefill",
                  isSimple: true,
                },
              }),
            ]),
          ),
    });
    renderPage("/vocab?track=G&page=2&q=πορευ");
    const links = await screen.findAllByRole("link", { name: "Show the word family of πορεύω" });
    expect(links[0]).toHaveAttribute("href", "/vocab?track=G&root=4");
  });

  it("hides the sub-root toggle and the parent link for a root without either", async () => {
    stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50&root=4": () =>
        json(page("nt-greek", [entry({ id: 20, lemma: "πορεύω" })])),
      "GET /vocab/roots/4": () =>
        json({ id: 4, root: "πορεύω", subRoots: [], members: [], memberTotal: 1 }),
    });
    renderPage("/vocab?root=4");
    const filter = await screen.findByTestId("family-filter");
    await waitFor(() =>
      expect(within(filter).getByText(/Word family:/)).toHaveTextContent("πορεύω"),
    );
    expect(within(filter).queryByRole("switch")).toBeNull();
    expect(within(filter).queryByRole("link")).toBeNull();
  });

  it("shows the accepted meanings of each entry in rank order", async () => {
    stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50": () =>
        json(
          page(
            "nt-greek",
            [
              entry({
                reviewed: true,
                meanings: [
                  { german: "Wort", rank: 1 },
                  { german: "Rede", rank: 2 },
                  { german: "Sache", rank: 3 },
                ],
              }),
            ],
            5407,
          ),
        ),
    });
    renderPage();
    expect((await screen.findAllByText("Wort, Rede, Sache")).length).toBeGreaterThan(0);
    expect(screen.queryByText("unreviewed")).not.toBeInTheDocument();
  });

  it("pages forward with the next-page button", async () => {
    const fetchMock = stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50": () =>
        json(page("nt-greek", [entry({})], 120)),
      "GET /vocab/courses/nt-greek/entries?skip=50&take=50": () =>
        json(page("nt-greek", [entry({ id: 51, rank: 51, lemma: "ἐν" })], 120, 50)),
    });
    renderPage();
    await screen.findAllByRole("link", { name: "ὁ" });
    expect(screen.getByRole("button", { name: "Previous page" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Page 1" })).toHaveAttribute("aria-current", "page");
    await userEvent.click(screen.getByRole("button", { name: "Next page" }));
    expect((await screen.findAllByRole("link", { name: "ἐν" })).length).toBeGreaterThan(0);
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/vocab/courses/nt-greek/entries?skip=50&take=50",
      expect.anything(),
    );
    expect(screen.getByRole("button", { name: "Page 2" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByText("51–51 of 120")).toBeInTheDocument();
  });

  it("jumps to a page from the URL page and back to page 1 with the first-page button", async () => {
    const fetchMock = stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50": () =>
        json(page("nt-greek", [entry({})], 2000)),
      "GET /vocab/courses/nt-greek/entries?skip=100&take=50": () =>
        json(page("nt-greek", [entry({ id: 101, rank: 101, lemma: "ἐν" })], 2000, 100)),
      "GET /vocab/courses/nt-greek/entries?skip=1950&take=50": () =>
        json(page("nt-greek", [entry({ id: 1951, rank: 1951, lemma: "λόγος" })], 2000, 1950)),
    });
    renderPage("/vocab?page=3");
    await screen.findAllByRole("link", { name: "ἐν" });
    expect(screen.getByRole("button", { name: "Page 40" })).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText("Go to page (1–40)"), "99{Enter}");
    expect((await screen.findAllByRole("link", { name: "λόγος" })).length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: "Page 40" })).toHaveAttribute("aria-current", "page");
    await userEvent.click(screen.getByRole("button", { name: "First page" }));
    expect((await screen.findAllByRole("link", { name: "ὁ" })).length).toBeGreaterThan(0);
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/vocab/courses/nt-greek/entries?skip=1950&take=50",
      expect.anything(),
    );
  });

  it("shows no page navigation for a single page", async () => {
    stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50": () =>
        json(page("nt-greek", [entry({})], 1)),
    });
    renderPage();
    await screen.findAllByRole("link", { name: "ὁ" });
    expect(screen.getByText("1–1 of 1")).toBeInTheDocument();
    expect(screen.queryByRole("navigation", { name: "Pages" })).not.toBeInTheDocument();
  });

  it("shows the pager above and below a long list", async () => {
    const items = Array.from({ length: 50 }, (_, i) =>
      entry({ id: i + 1, rank: i + 1, lemma: `λ${i}` }),
    );
    stubApi({
      "GET /vocab/courses": () => json(courses),
      "GET /vocab/courses/nt-greek/entries?skip=0&take=50": () =>
        json(page("nt-greek", items, 120)),
    });
    renderPage();
    await screen.findAllByRole("link", { name: "λ0" });
    expect(screen.getAllByRole("navigation", { name: "Pages" })).toHaveLength(2);
  });
});
