import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { AdminLogItem } from "@/lib/admin/log";
import { json, me, renderWithAuth, stubApi } from "@/test/authHelpers";
import { AdminLogPage } from "./AdminLogPage";

const admin = { ...me, roles: ["user", "admin"] };

const entry = (over: Partial<AdminLogItem>): AdminLogItem => ({
  id: 1,
  createdAt: "2026-09-02T10:15:00Z",
  action: "user_deactivated",
  actorUserId: 1,
  actorDisplayName: "Ada",
  targetUserId: 4,
  targetDisplayName: "Paul",
  details: { reason: "Spam" },
  ...over,
});

const log = (items: AdminLogItem[], total = items.length) => ({
  items,
  total,
  page: 1,
  pageSize: 50,
});

const userList = {
  items: [
    {
      id: 4,
      email: "paul@example.org",
      displayName: "Paul",
      roles: ["user"],
      status: "deactivated",
      createdAt: "2026-06-01T00:00:00Z",
    },
    {
      id: 6,
      email: "ai@local",
      displayName: "AI · Anthropic",
      roles: ["ai"],
      status: "ai",
      createdAt: "2026-09-12T00:00:00Z",
    },
  ],
  total: 2,
  page: 1,
  pageSize: 200,
};

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("AdminLogPage", () => {
  it("lists time, actor, action, target and details with a link to the target", async () => {
    stubApi({
      "GET /auth/me": () => json(admin),
      "GET /admin/users?page=1&pageSize=200": () => json(userList),
      "GET /admin/log?page=1&pageSize=50": () =>
        json(
          log([
            entry({}),
            entry({
              id: 2,
              action: "invite_created",
              targetUserId: undefined,
              targetDisplayName: undefined,
              inviteCodeId: 3,
              details: undefined,
            }),
          ]),
        ),
    });
    renderWithAuth(<AdminLogPage />);
    const table = await screen.findByRole("table");
    expect(table).toHaveTextContent("Ada");
    expect(table).toHaveTextContent("Deactivated");
    expect(table).toHaveTextContent("reason: Spam");
    expect(table).toHaveTextContent("Invite code created");
    expect(table).toHaveTextContent("Invite code #3");
    expect(screen.getByRole("link", { name: "Paul" })).toHaveAttribute("href", "/admin/users/4");
  });

  it("reads the userId filter from the query string and sends it to the API", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => json(admin),
      "GET /admin/users?page=1&pageSize=200": () => json(userList),
      "GET /admin/log?userId=4&page=1&pageSize=50": () => json(log([entry({})])),
    });
    renderWithAuth(<AdminLogPage />, "/admin/log?userId=4");
    await screen.findByRole("link", { name: "Paul" });
    expect(screen.getByRole("combobox", { name: /^Affected user/ })).toHaveValue("4");
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/admin/log?userId=4&page=1&pageSize=50",
      expect.anything(),
    );
  });

  it("refetches with the chosen action filter", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => json(admin),
      "GET /admin/users?page=1&pageSize=200": () => json(userList),
      "GET /admin/log?page=1&pageSize=50": () => json(log([entry({})])),
      "GET /admin/log?action=role_granted&page=1&pageSize=50": () => json(log([])),
    });
    renderWithAuth(<AdminLogPage />);
    await screen.findByRole("link", { name: "Paul" });
    await userEvent.selectOptions(
      screen.getByRole("combobox", { name: /^Action/ }),
      "role_granted",
    );
    expect(await screen.findByText("No entries for these filters.")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/admin/log?action=role_granted&page=1&pageSize=50",
      expect.anything(),
    );
  });

  it("shows the forbidden note to non-admins without calling the log API", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["user", "maintainer"] }),
    });
    renderWithAuth(<AdminLogPage />);
    expect(await screen.findByRole("alert")).toHaveTextContent("You do not have access");
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes("/admin/log"))).toBe(false);
  });
});
