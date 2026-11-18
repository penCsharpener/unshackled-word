import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { UserListItem } from "@/lib/admin/users";
import { json, me, renderWithAuth, stubApi } from "@/test/authHelpers";
import { AdminUsersPage } from "./AdminUsersPage";

const admin = { ...me, roles: ["user", "admin"] };
const item = (over: Partial<UserListItem>): UserListItem => ({
  id: 2,
  email: "maria@example.org",
  displayName: "Maria",
  roles: ["user", "maintainer"],
  status: "active",
  createdAt: "2026-01-02T00:00:00Z",
  lastSignInAt: null,
  ...over,
});
const list = (items: UserListItem[], total = items.length) => ({
  items,
  total,
  page: 1,
  pageSize: 50,
});

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("AdminUsersPage", () => {
  it("lists users with role and status badges and links to the detail page", async () => {
    stubApi({
      "GET /auth/me": () => json(admin),
      "GET /admin/users?page=1&pageSize=50": () =>
        json(list([item({}), item({ id: 3, displayName: "Bot", status: "ai", roles: ["user"] })])),
    });
    renderWithAuth(<AdminUsersPage />);
    const link = await screen.findByRole("link", { name: "Maria" });
    expect(link).toHaveAttribute("href", "/admin/users/2");
    const table = screen.getByRole("table");
    expect(within(table).getByText("Maintainer")).toBeInTheDocument();
    expect(within(table).getByText("AI")).toBeInTheDocument();
  });

  it("sends the debounced search and the status filter to the API", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => json(admin),
      "GET /admin/users?page=1&pageSize=50": () => json(list([item({})])),
      "GET /admin/users?search=mar&page=1&pageSize=50": () => json(list([item({})])),
      "GET /admin/users?search=mar&status=deactivated&page=1&pageSize=50": () => json(list([])),
    });
    renderWithAuth(<AdminUsersPage />);
    await screen.findByRole("link", { name: "Maria" });
    await userEvent.type(screen.getByLabelText("Search by name or email"), "mar");
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        "/api/admin/users?search=mar&page=1&pageSize=50",
        expect.anything(),
      ),
    );
    await userEvent.selectOptions(screen.getByRole("combobox", { name: /^Status/ }), "deactivated");
    expect(await screen.findByText("No users match these filters.")).toBeInTheDocument();
  });

  it("pages through the list", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => json(admin),
      "GET /admin/users?page=1&pageSize=50": () => json(list([item({})], 120)),
      "GET /admin/users?page=2&pageSize=50": () =>
        json({ ...list([item({ id: 9, displayName: "Zed" })], 120), page: 2 }),
    });
    renderWithAuth(<AdminUsersPage />);
    await screen.findByText("Page 1 of 3");
    expect(screen.getByRole("button", { name: "Previous" })).toBeDisabled();
    await userEvent.click(screen.getByRole("button", { name: "Next" }));
    expect(await screen.findByRole("link", { name: "Zed" })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/admin/users?page=2&pageSize=50",
      expect.anything(),
    );
  });

  it("shows the forbidden note to non-admins without calling the users API", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["user", "maintainer"] }),
    });
    renderWithAuth(<AdminUsersPage />);
    expect(await screen.findByRole("alert")).toHaveTextContent("You do not have access");
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes("/admin/users"))).toBe(false);
  });
});
