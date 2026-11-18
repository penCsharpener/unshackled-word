import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { AdminLogItem } from "@/lib/admin/log";
import { json, stubApi } from "@/test/authHelpers";
import { UserLogSection } from "./UserLogSection";

const entry = (over: Partial<AdminLogItem> = {}): AdminLogItem => ({
  id: 1,
  createdAt: "2026-09-02T10:15:00Z",
  action: "role_granted",
  actorUserId: 1,
  actorDisplayName: "Ada",
  targetUserId: 2,
  targetDisplayName: "Maria",
  details: { role: "maintainer" },
  ...over,
});

const log = (items: AdminLogItem[]) => ({ items, total: items.length, page: 1, pageSize: 20 });

const LOG_URL = "GET /admin/log?userId=2&page=1&pageSize=20";

function renderSection(refreshKey: unknown = 1) {
  return render(
    <MemoryRouter>
      <UserLogSection userId={2} refreshKey={refreshKey} />
    </MemoryRouter>,
  );
}

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("UserLogSection", () => {
  it("lists the user's last entries and links to the full log filtered by the user", async () => {
    stubApi({ [LOG_URL]: () => json(log([entry()])) });
    renderSection();
    expect(await screen.findByText("Role granted")).toBeInTheDocument();
    expect(screen.getByText("Ada")).toBeInTheDocument();
    expect(screen.getByText("role: maintainer")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Show all" })).toHaveAttribute(
      "href",
      "/admin/log?userId=2",
    );
  });

  it("shows a note instead of the table when the user has no entries", async () => {
    stubApi({ [LOG_URL]: () => json(log([])) });
    renderSection();
    expect(await screen.findByText("No admin actions for this user yet.")).toBeInTheDocument();
    expect(screen.queryByRole("table")).toBeNull();
  });

  it("shows an alert when the log cannot be loaded", async () => {
    stubApi({ [LOG_URL]: () => new Response("", { status: 500 }) });
    renderSection();
    expect(await screen.findByRole("alert")).toHaveTextContent("The log could not be loaded.");
  });

  it("fetches again when the refresh key changes", async () => {
    const fetchMock = stubApi({ [LOG_URL]: () => json(log([])) });
    const { rerender } = renderSection(1);
    await screen.findByText("No admin actions for this user yet.");
    rerender(
      <MemoryRouter>
        <UserLogSection userId={2} refreshKey={2} />
      </MemoryRouter>,
    );
    await screen.findByText("No admin actions for this user yet.");
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});
