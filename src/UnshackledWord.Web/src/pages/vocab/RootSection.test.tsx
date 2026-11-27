import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { VocabEditorRoot } from "@/lib/vocab/editorApi";
import { RootSection } from "./RootSection";

const prepositions = {
  items: [
    { key: "G1909", lemma: "ἐπί" },
    { key: "G4862", lemma: "σύν" },
  ],
};

const agentRoot: VocabEditorRoot = {
  rootId: 7,
  root: "μαρτυρέω",
  strongsKey: "G3140",
  homograph: 1,
  prefixKeys: ["G4862"],
  prefixes: "σύν",
  origin: "agent",
  isSimple: false,
  ancestors: [],
};

interface Call {
  method: string;
  url: string;
  body?: unknown;
}

/** Stubs fetch: the lists are fixed, every PUT answers 200; the calls are recorded. */
function stubFetch(search: Record<string, unknown> = {}) {
  const calls: Call[] = [];
  const mock = vi.fn(async (url: string, init?: RequestInit) => {
    const method = init?.method ?? "GET";
    const path = url.replace(/^\/api/, "");
    calls.push({ method, url: path, body: init?.body ? JSON.parse(String(init.body)) : undefined });
    if (path.startsWith("/maintainer/vocab/roots/prepositions")) {
      return new Response(JSON.stringify(prepositions), { status: 200 });
    }
    if (path.startsWith("/maintainer/vocab/roots/search")) {
      return new Response(JSON.stringify(search), { status: 200 });
    }
    if (method === "PUT" && path.endsWith("/parent") && path.includes("/roots/99/")) {
      return new Response(
        JSON.stringify({
          errors: { generalErrors: ["μαρτυρέω already sits below μαρτυρία; cycle"] },
        }),
        { status: 400 },
      );
    }
    if (method === "PUT") return new Response("{}", { status: 200 });
    return new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", mock);
  return calls;
}

function renderSection(props: {
  track?: "G" | "H";
  root?: VocabEditorRoot | null;
  blocked?: boolean;
}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <RootSection
        entryId={56}
        track={props.track ?? "G"}
        root={props.root === undefined ? agentRoot : props.root}
        blocked={props.blocked ?? false}
      />
    </QueryClientProvider>,
  );
}

const puts = (calls: Call[]) => calls.filter((call) => call.method === "PUT");

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("RootSection", () => {
  it("shows the AI badge and the current prefixes and root", async () => {
    stubFetch();
    renderSection({});
    expect(screen.getByText("AI")).toBeInTheDocument();
    expect(await screen.findByText("σύν")).toBeInTheDocument();
    expect(screen.getByText("μαρτυρέω")).toBeInTheDocument();
  });

  it("saves the ordered prefixes with the picked root id", async () => {
    const calls = stubFetch();
    const user = userEvent.setup();
    renderSection({});
    await screen.findByRole("option", { name: "ἐπί (G1909)" });
    await user.selectOptions(screen.getByRole("combobox", { name: /Add prefix/ }), "G1909");
    await user.click(screen.getByRole("button", { name: "Save root" }));
    await waitFor(() => expect(puts(calls)).toHaveLength(1));
    expect(puts(calls)[0]).toMatchObject({
      url: "/maintainer/vocab/entries/56/root",
      body: { prefixKeys: ["G4862", "G1909"], rootId: 7 },
    });
  });

  it("reorders and removes prefixes", async () => {
    const calls = stubFetch();
    const user = userEvent.setup();
    renderSection({ root: { ...agentRoot, prefixKeys: ["G4862", "G1909"] } });
    await screen.findByText("ἐπί");
    await user.click(screen.getByRole("button", { name: "Move ἐπί forward" }));
    await user.click(screen.getByRole("button", { name: "Remove σύν" }));
    await user.click(screen.getByRole("button", { name: "Save root" }));
    await waitFor(() => expect(puts(calls)).toHaveLength(1));
    expect(puts(calls)[0]?.body).toEqual({ prefixKeys: ["G1909"], rootId: 7 });
  });

  it("saves a simple word without prefixes", async () => {
    const calls = stubFetch();
    const user = userEvent.setup();
    renderSection({});
    await user.click(screen.getByRole("switch", { name: "Simple word" }));
    await user.click(screen.getByRole("button", { name: "Save root" }));
    await waitFor(() => expect(puts(calls)).toHaveLength(1));
    expect(puts(calls)[0]?.body).toEqual({ simple: true });
  });

  it("picks a Greek word without a root row by its Strong's key", async () => {
    const calls = stubFetch({
      items: [{ root: "μαρτυρέω", strongsKey: "G3140", homograph: 1, meaning: "bezeugen" }],
    });
    const user = userEvent.setup();
    renderSection({ root: null });
    expect(screen.getByRole("button", { name: "Save root" })).toBeDisabled();
    await user.type(screen.getByRole("textbox", { name: "Search root" }), "μαρ");
    await user.click(await screen.findByRole("button", { name: /μαρτυρέω/ }));
    await user.click(screen.getByRole("button", { name: "Save root" }));
    await waitFor(() => expect(puts(calls)).toHaveLength(1));
    expect(puts(calls)[0]?.body).toEqual({ prefixKeys: [], rootKey: "G3140" });
  });

  it("creates a Hebrew root from letters with a homograph and no verb key", async () => {
    const calls = stubFetch({ items: [] });
    const user = userEvent.setup();
    renderSection({ track: "H", root: null });
    await user.type(screen.getByRole("textbox", { name: "Root letters" }), "ענה");
    await user.type(screen.getByRole("spinbutton", { name: /Homograph/ }), "2");
    await user.click(screen.getByRole("button", { name: "Create new root" }));
    await waitFor(() => expect(puts(calls)).toHaveLength(1));
    expect(puts(calls)[0]?.body).toEqual({ rootLetters: "ענה", homograph: 2 });
  });

  it("uses an existing Hebrew root by id", async () => {
    const calls = stubFetch({
      items: [{ rootId: 31, root: "ענה", homograph: 2, meaning: "antworten" }],
    });
    const user = userEvent.setup();
    renderSection({ track: "H", root: null });
    await user.type(screen.getByRole("textbox", { name: "Root letters" }), "ענה");
    await user.click(await screen.findByRole("button", { name: /ענה II/ }));
    await user.click(screen.getByRole("button", { name: "Save root" }));
    await waitFor(() => expect(puts(calls)).toHaveLength(1));
    expect(puts(calls)[0]?.body).toEqual({ rootId: 31 });
  });

  it("blocks the save while the page has unsaved changes", async () => {
    stubFetch();
    renderSection({ blocked: true });
    expect(screen.getByRole("button", { name: "Save root" })).toBeDisabled();
    expect(screen.getByText("Save the changes above first.")).toBeInTheDocument();
  });

  it("shows the path of the parents and sets a parent by id", async () => {
    const calls = stubFetch({
      items: [{ rootId: 5, root: "πόρος", strongsKey: "G4197", homograph: 1 }],
    });
    const user = userEvent.setup();
    renderSection({
      root: { ...agentRoot, root: "μαρτυρία", ancestors: [{ id: 8, root: "μαρτυρέω" }] },
    });
    expect(screen.getByText("μαρτυρία → μαρτυρέω")).toBeInTheDocument();
    await user.type(screen.getByRole("textbox", { name: "Search parent root" }), "πορ");
    await user.click(await screen.findByRole("button", { name: /πόρος/ }));
    await user.click(screen.getByRole("button", { name: "Set as parent root" }));
    await waitFor(() => expect(puts(calls)).toHaveLength(1));
    expect(puts(calls)[0]).toMatchObject({
      url: "/maintainer/vocab/roots/7/parent",
      body: { parentRootId: 5 },
    });
  });

  it("clears the parent with an empty body and shows a refusal", async () => {
    const calls = stubFetch();
    const user = userEvent.setup();
    renderSection({
      root: { ...agentRoot, rootId: 99, ancestors: [{ id: 8, root: "μαρτυρία" }] },
    });
    await user.click(screen.getByRole("button", { name: "Remove parent root" }));
    await waitFor(() => expect(puts(calls)).toHaveLength(1));
    expect(puts(calls)[0]).toMatchObject({ url: "/maintainer/vocab/roots/99/parent", body: {} });
    expect(await screen.findByRole("alert")).toHaveTextContent("cycle");
  });
});
