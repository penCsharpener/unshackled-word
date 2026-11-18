import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "@/components/Toast";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { json, me } from "@/test/authHelpers";
import { AdminUserCreatePage } from "./AdminUserCreatePage";
import { AdminUserDetailPage } from "./AdminUserDetailPage";

function setup(post: () => Response) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const key = `${init?.method ?? "GET"} ${url.replace(/^\/api/, "")}`;
    if (key === "GET /auth/me") return json({ ...me, roles: ["user", "admin"] });
    if (key === "POST /admin/users") return post();
    if (key === "GET /admin/users/9") return json(created);
    return new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  render(
    <MemoryRouter initialEntries={["/admin/users/new"]}>
      <ToastProvider>
        <AuthProvider>
          <Routes>
            <Route path="/admin/users/new" element={<AdminUserCreatePage />} />
            <Route path="/admin/users/:id" element={<AdminUserDetailPage />} />
          </Routes>
        </AuthProvider>
      </ToastProvider>
    </MemoryRouter>,
  );
  return fetchMock;
}

const created = {
  id: 9,
  email: "anna@example.org",
  displayName: "Anna",
  roles: ["user", "reviewer"],
  status: "active",
  createdAt: "2026-01-02T00:00:00Z",
  emailVerified: true,
  hasPassword: false,
  passkeyCount: 0,
  isSelf: false,
  isLastActiveAdmin: false,
};

async function fill() {
  await userEvent.type(await screen.findByLabelText("Email"), "anna@example.org");
  await userEvent.type(screen.getByLabelText("Display name"), "Anna");
  await userEvent.click(screen.getByRole("checkbox", { name: "Reviewer" }));
  await userEvent.click(screen.getByRole("button", { name: "Create user" }));
}

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("AdminUserCreatePage", () => {
  it("creates the user and lands on their page with the welcome notice", async () => {
    const fetchMock = setup(() => json(created, 201));
    await fill();
    expect(await screen.findByText("Welcome mail sent.")).toBeInTheDocument();
    expect(await screen.findByRole("heading", { name: "Anna" })).toBeInTheDocument();
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({
      email: "anna@example.org",
      displayName: "Anna",
      roles: ["reviewer"],
    });
  });

  it("shows a field error linking to the existing account", async () => {
    setup(
      () =>
        new Response(JSON.stringify({ code: "email_taken", existingUserId: 4 }), { status: 409 }),
    );
    await fill();
    expect(await screen.findByRole("alert")).toHaveTextContent("already belongs");
    expect(screen.getByRole("link", { name: "Open the other account" })).toHaveAttribute(
      "href",
      "/admin/users/4",
    );
  });
});
