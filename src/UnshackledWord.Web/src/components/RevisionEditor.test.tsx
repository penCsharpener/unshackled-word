import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { RevisionVariant } from "@/lib/revision/types";
import { RevisionEditor } from "./RevisionEditor";

const levels = [
  { level: 100, code: "elb1871", name: "Elb 1871", isBase: true, isDefault: false },
  { level: 200, code: "revision", name: "Revision", isBase: false, isDefault: true },
  { level: 300, code: "easy", name: "Leicht", isBase: false, isDefault: false },
];
const variant: RevisionVariant = {
  revisionVerseId: 9,
  level: 200,
  version: 3,
  text: "Revidierter Text",
  createdAt: "2026-01-02T00:00:00Z",
};

const carry = { same: 2, changed: 1, new: 1, lostOriginalWordIds: [30] };
const mapping = {
  revisionVerseId: 10,
  level: 200,
  version: 4,
  bibleBookId: 43,
  chapter: 3,
  verse: 16,
  language: "greek",
  germanWords: [
    { id: 1, positionInVerse: 1, text: "Denn", change: "same" },
    { id: 2, positionInVerse: 2, text: "einzigen", change: "changed" },
    { id: 3, positionInVerse: 3, text: "sehr", change: "new" },
  ],
  originalWords: [
    { id: 10, position: 1, original: "ὁ" },
    { id: 20, position: 2, original: "μονογενῆ" },
    { id: 30, position: 3, original: "υἱόν" },
  ],
  links: [
    { elbWordId: 1, stepWordId: 10, isAddedWord: false, origin: "carried" },
    { elbWordId: 2, stepWordId: 20, isAddedWord: false, origin: "carried" },
  ],
};

const history = {
  level: 200,
  versions: [
    {
      auditId: 9,
      action: "text",
      version: 3,
      text: "Revidierter Text",
      isActive: true,
      at: "2026-01-02T00:00:00Z",
    },
    {
      auditId: 8,
      action: "text",
      version: 2,
      text: "Zweiter Text",
      isActive: false,
      at: "2026-01-01T00:00:00Z",
      endedAt: "2026-01-02T00:00:00Z",
    },
    {
      auditId: 7,
      action: "text",
      version: 1,
      text: "Erster Text",
      isActive: false,
      at: "2025-12-01T00:00:00Z",
      endedAt: "2026-01-01T00:00:00Z",
    },
  ],
};

function stub(variants: RevisionVariant[], postStatus = 200) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    if (init?.method === "POST" && url.endsWith("/revision/levels")) {
      return new Response(
        JSON.stringify({
          level: 400,
          code: "neu",
          nameDe: "Neu",
          nameEn: "New",
          isBase: false,
          isDefault: false,
          variantCount: 0,
        }),
      );
    }
    if (url.endsWith("/revision/levels")) return new Response(JSON.stringify(levels));
    if (url.includes("/history?level=")) return new Response(JSON.stringify(history));
    if (init?.method === "DELETE") return new Response(null, { status: 204 });
    if (url.includes("/revision/verseMapping/10")) {
      return new Response(
        JSON.stringify(
          init?.method === "PUT"
            ? { ...mapping, mappingReviewedAt: "2026-11-13T00:00:00Z" }
            : mapping,
        ),
      );
    }
    if (init?.method === "POST") {
      return postStatus === 200
        ? new Response(JSON.stringify({ revisionVerseId: 10, level: 200, version: 4, carry }))
        : new Response("", { status: postStatus });
    }
    return new Response(JSON.stringify({ baseText: "Basistext", variants }));
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderEditor() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <RevisionEditor
        verseRef={{ bookId: 43, chapter: 3, verse: 16 }}
        reference="Joh 3,16"
        onClose={() => {}}
      />
    </QueryClientProvider>,
  );
}

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

const variant300: RevisionVariant = {
  ...variant,
  revisionVerseId: 11,
  level: 300,
  version: 1,
  text: "Leichter Text",
};

describe("RevisionEditor", () => {
  it("prefills the Elb1871 text when the level has no variant", async () => {
    stub([]);
    renderEditor();
    expect(await screen.findByRole("textbox", { name: "Text" })).toHaveValue("Basistext");
    expect(screen.queryByRole("option", { name: "Elb 1871" })).toBeNull();
  });

  it("prefills the active variant and sends its version", async () => {
    const fetchMock = stub([variant]);
    renderEditor();
    const field = await screen.findByRole("textbox", { name: "Text" });
    expect(field).toHaveValue("Revidierter Text");
    await userEvent.type(field, "!");
    await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
    expect(await screen.findByText(/Version 4/)).toBeInTheDocument();
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    expect(JSON.parse(String(post?.[1]?.body))).toMatchObject({
      level: 200,
      text: "Revidierter Text!",
      expectedVersion: 3,
    });
  });

  it("shows the conflict message on 409", async () => {
    stub([variant], 409);
    renderEditor();
    await userEvent.type(await screen.findByRole("textbox", { name: "Text" }), "!");
    await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
    expect(await screen.findByText(/inzwischen geändert, neu laden/)).toBeInTheDocument();
  });

  it("opens the mapping form after the save with the carry report and a marker per word to check", async () => {
    stub([variant]);
    renderEditor();
    await userEvent.type(await screen.findByRole("textbox", { name: "Text" }), "!");
    await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
    expect(
      await screen.findByText("2 übernommen, 1 geändert, 1 neu, 1 Originalwort ohne Zuordnung"),
    ).toBeInTheDocument();
    const changed = await screen.findByRole("button", { name: /einzigen.*prüfen/ });
    expect(changed).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /sehr.*prüfen/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /υἱόν.*prüfen/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^Denn(?!.*prüfen)/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /μονογενῆ(?!.*prüfen)/ })).toBeInTheDocument();
  });

  it("saves an edited mapping through the revision endpoint", async () => {
    const fetchMock = stub([variant]);
    renderEditor();
    await userEvent.type(await screen.findByRole("textbox", { name: "Text" }), "!");
    await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
    await userEvent.click(await screen.findByRole("button", { name: /sehr.*prüfen/ }));
    await userEvent.click(screen.getByRole("button", { name: /υἱόν/ }));
    await userEvent.click(screen.getByRole("button", { name: /Speichern \(1\)/ }));
    await vi.waitFor(() => {
      const put = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT");
      expect(put?.[0]).toContain("/revision/verseMapping/10");
      expect(JSON.parse(String(put?.[1]?.body)).links).toContainEqual({
        elbWordId: 3,
        stepWordId: 30,
        isAddedWord: false,
      });
    });
  });

  it("lists levels with a variant first and offers a new level", async () => {
    stub([variant300]);
    renderEditor();
    await screen.findByRole("textbox", { name: "Text" });
    const options = within(screen.getByRole("combobox", { name: "Stufe:" })).getAllByRole("option");
    expect(options.map((o) => o.textContent)).toEqual(["Leicht · v1", "Revision", "Neue Stufe …"]);
  });

  it("refills the text from another variant and carries from it, asking first when the field was edited", async () => {
    const fetchMock = stub([variant, variant300]);
    renderEditor();
    const field = await screen.findByRole("textbox", { name: "Text" });
    await userEvent.type(field, "!");
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Ausgangstext:" }), "300");
    expect(await screen.findByText("Text ersetzen?")).toBeInTheDocument();
    expect(field).toHaveValue("Revidierter Text!");
    await userEvent.click(screen.getByRole("button", { name: "Ersetzen" }));
    expect(field).toHaveValue("Leichter Text");
    await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
    await vi.waitFor(() => {
      const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
      expect(JSON.parse(String(post?.[1]?.body))).toMatchObject({
        level: 200,
        text: "Leichter Text",
        basedOnLevel: 300,
      });
    });
  });

  it("switches the source without asking while the field is untouched", async () => {
    stub([variant, variant300]);
    renderEditor();
    const field = await screen.findByRole("textbox", { name: "Text" });
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Ausgangstext:" }), "0");
    expect(screen.queryByText("Text ersetzen?")).toBeNull();
    expect(field).toHaveValue("Basistext");
  });

  it("sends the base level when the level has no variant", async () => {
    const fetchMock = stub([]);
    renderEditor();
    await userEvent.type(await screen.findByRole("textbox", { name: "Text" }), "!");
    await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
    await vi.waitFor(() => {
      const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
      expect(JSON.parse(String(post?.[1]?.body)).basedOnLevel).toBe(0);
    });
  });

  it("shows the history newest first and restores an old version into the field", async () => {
    stub([variant]);
    renderEditor();
    const field = await screen.findByRole("textbox", { name: "Text" });
    await userEvent.click(await screen.findByText(/Versionsverlauf \(3\)/));
    expect(screen.getByText("v3")).toBeInTheDocument();
    await userEvent.click(
      screen.getAllByRole("button", { name: "wiederherstellen" })[2] as HTMLElement,
    );
    expect(field).toHaveValue("Erster Text");
  });

  it("removes the revision of the level after the confirm", async () => {
    const fetchMock = stub([variant]);
    renderEditor();
    await screen.findByRole("textbox", { name: "Text" });
    await userEvent.click(await screen.findByText(/Versionsverlauf/));
    await userEvent.click(screen.getByRole("button", { name: "Revision dieser Stufe entfernen" }));
    await userEvent.click(screen.getByRole("button", { name: "Entfernen" }));
    await vi.waitFor(() => {
      const del = fetchMock.mock.calls.find(([, init]) => init?.method === "DELETE");
      expect(del?.[0]).toContain("/revision/verse/9");
    });
  });

  it("adds a new level from the dialog and selects it", async () => {
    const fetchMock = stub([variant]);
    renderEditor();
    await screen.findByRole("textbox", { name: "Text" });
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Stufe:" }), "new");
    await userEvent.type(await screen.findByRole("spinbutton", { name: "Stufe (1–1000)" }), "400");
    await userEvent.type(screen.getByRole("textbox", { name: "Kürzel" }), "neu");
    await userEvent.type(screen.getByRole("textbox", { name: "Name (Deutsch)" }), "Neu");
    await userEvent.type(screen.getByRole("textbox", { name: "Name (Englisch)" }), "New");
    await userEvent.click(
      within(screen.getByRole("dialog")).getByRole("button", { name: "Speichern" }),
    );
    await vi.waitFor(() => {
      const post = fetchMock.mock.calls.find(
        ([url, init]) => init?.method === "POST" && String(url).endsWith("/revision/levels"),
      );
      expect(JSON.parse(String(post?.[1]?.body))).toMatchObject({
        level: 400,
        code: "neu",
        nameDe: "Neu",
        nameEn: "New",
        isDefault: false,
      });
    });
  });
});
