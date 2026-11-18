import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "@/components/Toast";
import i18n from "@/i18n";
import type { UserDetail } from "@/lib/admin/users";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { json, me } from "@/test/authHelpers";
import { AdminUserDetailPage } from "./AdminUserDetailPage";

const detail = (over: Partial<UserDetail> = {}): UserDetail => ({
  id: 2,
  email: "maria@example.org",
  displayName: "Maria",
  roles: ["user", "maintainer"],
  status: "active",
  createdAt: "2026-01-02T00:00:00Z",
  emailVerified: true,
  hasPassword: true,
  passkeyCount: 0,
  isSelf: false,
  isLastActiveAdmin: false,
  ...over,
});

const emptyLog = { items: [], total: 0, page: 1, pageSize: 20 };

function setup(
  user: UserDetail,
  putResponse: () => Response = () => json(detail({ roles: ["user", "maintainer", "reviewer"] })),
) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const key = `${init?.method ?? "GET"} ${url.replace(/^\/api/, "")}`;
    if (key === "GET /auth/me") return json({ ...me, roles: ["user", "admin"] });
    if (key === `GET /admin/users/${user.id}`) return json(user);
    if (key.startsWith("GET /admin/log")) return json(emptyLog);
    if (key === `PUT /admin/users/${user.id}/roles`) return putResponse();
    return new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  render(
    <MemoryRouter initialEntries={[`/admin/users/${user.id}`]}>
      <ToastProvider>
        <AuthProvider>
          <Routes>
            <Route path="/admin/users/:id" element={<AdminUserDetailPage />} />
          </Routes>
        </AuthProvider>
      </ToastProvider>
    </MemoryRouter>,
  );
  return fetchMock;
}

function setupCalls(user: UserDetail, handlers: Record<string, () => Response>) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const key = `${init?.method ?? "GET"} ${url.replace(/^\/api/, "")}`;
    if (key === "GET /auth/me") return json({ ...me, roles: ["user", "admin"] });
    if (key === `GET /admin/users/${user.id}`) return json(user);
    if (key.startsWith("GET /admin/log") && !handlers[key]) return json(emptyLog);
    return handlers[key]?.() ?? new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  render(
    <MemoryRouter initialEntries={[`/admin/users/${user.id}`]}>
      <ToastProvider>
        <AuthProvider>
          <Routes>
            <Route path="/admin/users/:id" element={<AdminUserDetailPage />} />
          </Routes>
        </AuthProvider>
      </ToastProvider>
    </MemoryRouter>,
  );
  return fetchMock;
}

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("AdminUserDetailPage roles", () => {
  it("saves the ticked roles via PUT roles", async () => {
    const fetchMock = setup(detail());
    const reviewer = await screen.findByRole("checkbox", { name: "Reviewer" });
    expect(screen.getByRole("checkbox", { name: "User" })).toBeDisabled();
    expect(screen.getByRole("checkbox", { name: "Maintainer" })).toBeChecked();
    await userEvent.click(reviewer);
    await userEvent.click(screen.getByRole("button", { name: "Save roles" }));
    expect(await screen.findByText("Roles saved.")).toBeInTheDocument();
    const put = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT");
    expect(JSON.parse(String(put?.[1]?.body))).toEqual({ roles: ["maintainer", "reviewer"] });
    expect(screen.getByText(/signs the affected user out/)).toBeInTheDocument();
  });

  it("disables the own admin checkbox with an explanation", async () => {
    setup(detail({ roles: ["user", "admin"], isSelf: true }));
    const admin = await screen.findByRole("checkbox", { name: "Admin" });
    expect(admin).toBeDisabled();
    expect(admin).toBeChecked();
    expect(screen.getByText(/cannot remove your own admin role/)).toBeInTheDocument();
  });

  it("disables the last admin's checkbox, but not an unticked one", async () => {
    setup(detail({ roles: ["user", "admin"], isLastActiveAdmin: true }));
    expect(await screen.findByRole("checkbox", { name: "Admin" })).toBeDisabled();
    expect(screen.getByText(/the admin role cannot be removed/)).toBeInTheDocument();
  });

  it("maps a 409 code to an inline error", async () => {
    setup(detail(), () => new Response(JSON.stringify({ code: "last_admin" }), { status: 409 }));
    await userEvent.click(await screen.findByRole("checkbox", { name: "Reviewer" }));
    await userEvent.click(screen.getByRole("button", { name: "Save roles" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("last active admin");
  });

  it("shows an AI user read-only without save or editable boxes", async () => {
    setup(detail({ status: "ai", roles: ["user"] }));
    expect(await screen.findByText(/AI user/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Save roles" })).toBeNull();
    for (const box of screen.getAllByRole("checkbox")) expect(box).toBeDisabled();
  });
});

describe("AdminUserDetailPage details and status", () => {
  it("saves name and email via PUT", async () => {
    const fetchMock = setupCalls(detail(), {
      "PUT /admin/users/2": () => json(detail({ displayName: "Maria K." })),
    });
    const name = await screen.findByLabelText("Display name");
    await userEvent.clear(name);
    await userEvent.type(name, "Maria K.");
    await userEvent.click(screen.getByRole("button", { name: "Save details" }));
    expect(await screen.findByText("Details saved.")).toBeInTheDocument();
    const put = fetchMock.mock.calls.find(
      ([url, init]) => init?.method === "PUT" && url.endsWith("/2"),
    );
    expect(JSON.parse(String(put?.[1]?.body))).toEqual({
      displayName: "Maria K.",
      email: "maria@example.org",
    });
  });

  it("shows a field error linking to the account that owns the email", async () => {
    setupCalls(detail(), {
      "PUT /admin/users/2": () =>
        new Response(JSON.stringify({ code: "email_taken", existingUserId: 7 }), { status: 409 }),
    });
    const email = await screen.findByLabelText("Email");
    await userEvent.clear(email);
    await userEvent.type(email, "other@example.org");
    await userEvent.click(screen.getByRole("button", { name: "Save details" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("already belongs");
    expect(screen.getByRole("link", { name: "Open the other account" })).toHaveAttribute(
      "href",
      "/admin/users/7",
    );
  });

  it("sends the password reset mail and confirms", async () => {
    const fetchMock = setupCalls(detail(), {
      "POST /admin/users/2/password-reset": () => new Response(null, { status: 204 }),
    });
    await userEvent.click(await screen.findByRole("button", { name: "Send password reset mail" }));
    expect(await screen.findByText("Password reset mail sent.")).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([url]) => url.endsWith("/2/password-reset"))).toBe(true);
  });

  it("deactivates after confirming with a reason", async () => {
    const fetchMock = setupCalls(detail(), {
      "POST /admin/users/2/deactivate": () =>
        json(
          detail({
            status: "deactivated",
            deactivation: {
              at: "2026-03-01T10:00:00Z",
              byUserId: 1,
              byDisplayName: "Ada",
              reason: "Left project",
            },
          }),
        ),
    });
    await userEvent.click(await screen.findByRole("button", { name: "Deactivate" }));
    await userEvent.type(await screen.findByLabelText("Reason (optional)"), "Left project");
    const buttons = screen.getAllByRole("button", { name: "Deactivate" });
    await userEvent.click(buttons[buttons.length - 1] as HTMLElement);
    expect(await screen.findByText("This account is deactivated.")).toBeInTheDocument();
    expect(screen.getByText("Reason: Left project")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reactivate" })).toBeInTheDocument();
    const post = fetchMock.mock.calls.find(([url]) => url.endsWith("/deactivate"));
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ reason: "Left project" });
  });

  it("hides Deactivate for oneself and disables it for the last admin", async () => {
    setupCalls(detail({ isSelf: true }), {});
    await screen.findByLabelText("Display name");
    expect(screen.queryByRole("button", { name: "Deactivate" })).toBeNull();
  });

  it("reactivates a deactivated user", async () => {
    setupCalls(detail({ status: "deactivated" }), {
      "POST /admin/users/2/reactivate": () => json(detail()),
    });
    await userEvent.click(await screen.findByRole("button", { name: "Reactivate" }));
    expect(await screen.findByRole("button", { name: "Deactivate" })).toBeInTheDocument();
  });
});

describe("AdminUserDetailPage protocol", () => {
  it("shows the user's admin-log entries, refreshed after a role save", async () => {
    let logCalls = 0;
    setupCalls(detail(), {
      "PUT /admin/users/2/roles": () => json(detail({ roles: ["user", "maintainer", "reviewer"] })),
      "GET /admin/log?userId=2&page=1&pageSize=20": () => {
        logCalls += 1;
        return json(
          logCalls === 1
            ? { items: [], total: 0, page: 1, pageSize: 20 }
            : {
                items: [
                  {
                    id: 7,
                    createdAt: "2026-11-18T09:00:00Z",
                    action: "role_granted",
                    actorUserId: 1,
                    actorDisplayName: "Ada",
                    targetUserId: 2,
                    targetDisplayName: "Maria",
                    details: { role: "reviewer" },
                  },
                ],
                total: 1,
                page: 1,
                pageSize: 20,
              },
        );
      },
    });
    expect(await screen.findByText("No admin actions for this user yet.")).toBeInTheDocument();
    await userEvent.click(await screen.findByRole("checkbox", { name: "Reviewer" }));
    await userEvent.click(screen.getByRole("button", { name: "Save roles" }));
    expect(await screen.findByText("Role granted")).toBeInTheDocument();
    expect(screen.getByText("role: reviewer")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Show all" })).toHaveAttribute(
      "href",
      "/admin/log?userId=2",
    );
  });
});
