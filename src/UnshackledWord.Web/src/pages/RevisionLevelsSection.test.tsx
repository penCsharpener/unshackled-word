import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { RevisionLevelsSection } from "./RevisionLevelsSection";

const details = [
  {
    level: 100,
    code: "elb1871",
    nameDe: "Elberfelder 1871",
    nameEn: "Elberfelder 1871",
    isBase: true,
    isDefault: false,
    variantCount: 0,
  },
  {
    level: 200,
    code: "revision",
    nameDe: "Revision",
    nameEn: "Revision",
    isBase: false,
    isDefault: true,
    variantCount: 4,
  },
  {
    level: 400,
    code: "modern",
    nameDe: "Modern",
    nameEn: "Modern",
    isBase: false,
    isDefault: false,
    variantCount: 0,
  },
];

function stub() {
  const fetchMock = vi.fn(async (_url: string, init?: RequestInit) => {
    if (init?.method === "PUT") return new Response(JSON.stringify(details[2]));
    return new Response(JSON.stringify(details));
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderSection() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <RevisionLevelsSection />
    </QueryClientProvider>,
  );
}

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("RevisionLevelsSection", () => {
  it("lists the presets, marks the default and offers no edit for the base level", async () => {
    stub();
    renderSection();
    expect(await screen.findByText("Modern")).toBeInTheDocument();
    expect(screen.getByText("Standard")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Bearbeiten" })).toHaveLength(2);
    expect(screen.getByText(/Stufe 200 · revision · 4 Versionen/)).toBeInTheDocument();
  });

  it("moves the default to another preset with a PUT", async () => {
    const fetchMock = stub();
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "Als Standard setzen" }));
    await vi.waitFor(() => {
      const put = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT");
      expect(put?.[0]).toContain("/revision/levels/400");
      expect(JSON.parse(String(put?.[1]?.body))).toMatchObject({
        level: 400,
        code: "modern",
        isDefault: true,
      });
    });
  });

  it("lets the level number of a preset that variants use change", async () => {
    stub();
    renderSection();
    await userEvent.click(
      (await screen.findAllByRole("button", { name: "Bearbeiten" }))[0] as HTMLElement,
    );
    expect(await screen.findByRole("spinbutton", { name: "Stufe (1–1000)" })).toBeEnabled();
  });
});
