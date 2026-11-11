import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AdminInvitationsPage } from "@/pages/AdminInvitationsPage";
import { json, me, renderWithAuth, stubApi } from "@/test/authHelpers";

const future = new Date(Date.now() + 7 * 86_400_000).toISOString();
const code = {
  id: 5,
  code: "ABC123",
  maxSeats: 3,
  seatsUsed: 1,
  expiresAt: future,
  note: "for friends",
  createdAt: future,
  createdByDisplayName: "Ada",
  redeemedByDisplayNames: [],
};

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("AdminInvitationsPage", () => {
  it("shows a forbidden note to non-admins", async () => {
    stubApi({ "GET /auth/me": () => json({ ...me, roles: ["user"] }) });
    renderWithAuth(<AdminInvitationsPage />);
    expect(await screen.findByRole("alert")).toHaveTextContent("do not have access");
  });

  it("lists codes, creates and revokes", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["admin"] }),
      "GET /admin/invite-codes": () => json({ inviteCodes: [code] }),
      "POST /admin/invite-codes": () => json({ id: 6, code: "NEW" }),
      "DELETE /admin/invite-codes/5": () => new Response(null, { status: 204 }),
    });
    renderWithAuth(<AdminInvitationsPage />);
    expect(await screen.findByText("ABC123")).toBeInTheDocument();
    expect(screen.getByText("1 / 3")).toBeInTheDocument();
    expect(screen.getByText("for friends")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Create code" }));
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        "/api/admin/invite-codes",
        expect.objectContaining({ method: "POST" }),
      ),
    );

    await userEvent.click(screen.getByRole("button", { name: "Revoke" }));
    await userEvent.click(screen.getByRole("button", { name: "Confirm revoke" }));
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        "/api/admin/invite-codes/5",
        expect.objectContaining({ method: "DELETE" }),
      ),
    );
  });

  it("copies the registration link", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["admin"] }),
      "GET /admin/invite-codes": () => json({ inviteCodes: [code] }),
    });
    renderWithAuth(<AdminInvitationsPage />);
    await screen.findByText("ABC123");
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
    await userEvent.click(screen.getByRole("button", { name: "Copy link" }));
    expect(writeText).toHaveBeenCalledWith(`${location.origin}/register?invite=ABC123`);
  });
});
