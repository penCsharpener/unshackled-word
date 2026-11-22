import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { json, me, stubApi } from "@/test/authHelpers";
import { VocabEditorPage } from "./VocabEditorPage";

const editor = {
  entry: {
    id: 56,
    track: "G",
    rank: 241,
    lemma: "μέσος",
    transliteration: "mesos",
    dictionaryForm: "μέσος",
    dictionaryFormOrigin: "agent",
    pos: "Adjektiv",
    occurrences: 58,
    isProperName: false,
    isMorpheme: false,
    hasAudio: false,
    contentVersion: 3,
    updatedAt: "2026-11-20T10:00:00",
  },
  meanings: [
    {
      id: 101,
      german: "Mitte",
      rank: 1,
      isAccepted: true,
      origin: "derived",
      isPinned: false,
      count: 40,
      share: 69,
    },
    {
      id: 102,
      german: "Wort",
      rank: 2,
      isAccepted: true,
      origin: "derived",
      isPinned: false,
      count: 1,
      share: 1.7,
    },
  ],
  strongsKeys: [{ strongsKey: "G3319", occurrences: 58 }],
  learners: 12,
  renderings: [
    {
      germanKey: "mitte",
      germanDisplay: "Mitte",
      linkCount: 40,
      fromRevisionCount: 30,
      share: 69,
      posTags: ["NN"],
      keyIsForm: false,
    },
  ],
  renderingsTotal: 1,
  totalLinks: 58,
  refreshedAt: "2026-11-21T09:14:00",
  sampleVerses: [],
};

const saved = { entryId: 56, contentVersion: 4, bumped: true, learners: 12 };

function renderAt(route = "/edit/vocab/56") {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[route]}>
        <AuthProvider>
          <Routes>
            <Route path="/edit/vocab/:id" element={<VocabEditorPage />} />
            <Route path="/edit/vocab" element={<p>list page</p>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/** Stubs the editor API for a maintainer; `handlers` add or replace routes ("METHOD /path"). */
function stubEditor(handlers: Record<string, () => Response> = {}, roles = ["maintainer"]) {
  return stubApi({
    "GET /auth/me": () => json({ ...me, roles }),
    "GET /vocab/courses": () =>
      json({
        courses: [
          {
            code: "nt-greek",
            track: "G",
            nameDe: "NT",
            nameEn: "NT",
            totalEntries: 1,
            totalOccurrences: 58,
          },
        ],
      }),
    "GET /maintainer/vocab/entries/56": () => json(editor),
    "PUT /maintainer/vocab/entries/56": () => json(saved),
    "PATCH /maintainer/vocab/entries/56/meanings/102": () => json(saved),
    ...handlers,
  });
}

function callsWith(fetchMock: ReturnType<typeof stubEditor>, method: string) {
  return fetchMock.mock.calls.filter(([, init]) => init?.method === method);
}

function bodyOf(call: [string, RequestInit?] | undefined) {
  return JSON.parse(String(call?.[1]?.body));
}

function meaningItem(german: string) {
  const item = screen
    .getAllByRole("listitem")
    .find((li) => li.textContent?.includes(german) && li.querySelector('[role="switch"]'));
  if (!item) throw new Error(`no meaning row for ${german}`);
  return item;
}

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("VocabEditorPage", () => {
  it("loads the entry with its form, meanings and renderings", async () => {
    stubEditor();
    renderAt();
    const heading = await screen.findByRole("heading", { level: 1 });
    expect(heading).toHaveTextContent("μέσος");
    expect(
      screen.getByRole("textbox", { name: "Study form (typing target, empty = lemma)" }),
    ).toHaveValue("μέσος");
    expect(screen.getByText("from AI")).toBeInTheDocument();
    expect(meaningItem("Mitte")).toBeInTheDocument();
    expect(meaningItem("Wort")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "G3319" })).toHaveAttribute(
      "href",
      "/bible/strongs/G3319",
    );
  });

  it("saves a changed form with a PUT that sends all three fields", async () => {
    const fetchMock = stubEditor();
    renderAt();
    const form = await screen.findByRole("textbox", {
      name: "Study form (typing target, empty = lemma)",
    });
    await userEvent.clear(form);
    await userEvent.type(form, "μέσος, -η, -ον");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(callsWith(fetchMock, "PUT")).toHaveLength(1));
    expect(bodyOf(callsWith(fetchMock, "PUT")[0] as [string, RequestInit])).toEqual({
      dictionaryForm: "μέσος, -η, -ον",
      transliteration: "mesos",
      reviewed: false,
    });
    expect(callsWith(fetchMock, "PATCH")).toHaveLength(0);
  });

  it("unaccepting a meaning sends a PATCH with isAccepted false", async () => {
    const fetchMock = stubEditor();
    renderAt();
    await screen.findByRole("heading", { level: 1 });
    await userEvent.click(within(meaningItem("Wort")).getByRole("switch", { name: "Accepted" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(callsWith(fetchMock, "PATCH")).toHaveLength(1));
    const patch = callsWith(fetchMock, "PATCH")[0] as [string, RequestInit];
    expect(patch[0]).toMatch(/\/maintainer\/vocab\/entries\/56\/meanings\/102$/);
    expect(bodyOf(callsWith(fetchMock, "PATCH")[0] as [string, RequestInit])).toEqual({
      isAccepted: false,
    });
    expect(callsWith(fetchMock, "PUT")).toHaveLength(0);
  });

  it("marking the entry as reviewed saves the reviewed flag through the PUT", async () => {
    const fetchMock = stubEditor();
    renderAt();
    await screen.findByRole("heading", { level: 1 });
    await userEvent.click(screen.getByRole("switch", { name: "Reviewed" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(callsWith(fetchMock, "PUT")).toHaveLength(1));
    expect(bodyOf(callsWith(fetchMock, "PUT")[0] as [string, RequestInit])).toMatchObject({
      reviewed: true,
    });
  });

  it("warns that the last Strong's key retires the entry when it is moved", async () => {
    stubEditor();
    renderAt();
    await screen.findByRole("heading", { level: 1 });
    await userEvent.click(screen.getByRole("button", { name: "Move…" }));
    expect(await screen.findByText("Move G3319")).toBeInTheDocument();
    expect(
      screen.getByText(/This is the last Strong's key: μέσος is retired afterwards\./),
    ).toBeInTheDocument();
  });

  it("says so when the entry does not exist", async () => {
    stubEditor();
    renderAt("/edit/vocab/999");
    expect(await screen.findByText("This entry does not exist.")).toBeInTheDocument();
  });

  it("shows the forbidden note to users without the role", async () => {
    stubEditor({}, ["user"]);
    renderAt();
    expect(await screen.findByText("You do not have access to this page.")).toBeInTheDocument();
  });

  it("accepts an agent form as written: one POST, then the origin reads manual", async () => {
    let current = editor;
    const fetchMock = stubEditor({
      "GET /maintainer/vocab/entries/56": () => json(current),
      "POST /maintainer/vocab/entries/56/accept-form": () => {
        current = { ...editor, entry: { ...editor.entry, dictionaryFormOrigin: "manual" } };
        return json(saved);
      },
    });
    renderAt();
    await screen.findByRole("heading", { level: 1 });
    expect(screen.getByText("from AI")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Accept" }));

    await waitFor(() => expect(callsWith(fetchMock, "POST")).toHaveLength(1));
    expect(callsWith(fetchMock, "POST")[0]?.[0]).toMatch(
      /\/maintainer\/vocab\/entries\/56\/accept-form$/,
    );
    expect(await screen.findByText("manual")).toBeInTheDocument();
    expect(screen.queryByText("from AI")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Accept" })).not.toBeInTheDocument();
  });
});
