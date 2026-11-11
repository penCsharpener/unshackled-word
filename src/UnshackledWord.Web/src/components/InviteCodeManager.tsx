import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { type InviteCodeSummary, inviteCodeStatus, inviteCodesApi } from "@/lib/admin/api";
import { describeApiError } from "@/lib/api";
import { Button } from "./Button";
import { FormError } from "./FormError";
import { TextField } from "./TextField";

/** Default expiry is 14 days out, as a yyyy-mm-dd string for a date input. */
function defaultExpiry(): string {
  const date = new Date();
  date.setDate(date.getDate() + 14);
  return date.toISOString().slice(0, 10);
}

/** Admin list of invitation codes with create, copy-registration-link and revoke. */
export function InviteCodeManager() {
  const { t, i18n } = useTranslation();
  const [codes, setCodes] = useState<InviteCodeSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [maxSeats, setMaxSeats] = useState("1");
  const [expiresAt, setExpiresAt] = useState(defaultExpiry);
  const [note, setNote] = useState("");
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState("");
  const [copiedId, setCopiedId] = useState<number | null>(null);
  const [confirmId, setConfirmId] = useState<number | null>(null);
  const [revokingId, setRevokingId] = useState<number | null>(null);

  const load = useCallback(async () => {
    try {
      setCodes((await inviteCodesApi.list()).inviteCodes);
      setLoadError("");
    } catch {
      setLoadError(t("admin.invitations.loadFailed"));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const date = (value: string) => new Date(value).toLocaleDateString(i18n.language);

  async function create() {
    setError("");
    setCreating(true);
    try {
      // A date input has no time of day: expire at the end of the chosen day.
      await inviteCodesApi.create({
        maxSeats: Number(maxSeats),
        expiresAt: new Date(`${expiresAt}T23:59:59`).toISOString(),
        note: note.trim() || undefined,
      });
      setNote("");
      setMaxSeats("1");
      setExpiresAt(defaultExpiry());
      await load();
    } catch (err) {
      setError(describeApiError(err, t("admin.invitations.createFailed")));
    } finally {
      setCreating(false);
    }
  }

  async function copy(code: InviteCodeSummary) {
    try {
      await navigator.clipboard.writeText(`${location.origin}/register?invite=${code.code}`);
      setCopiedId(code.id);
      setTimeout(() => setCopiedId((id) => (id === code.id ? null : id)), 2000);
    } catch {
      setError(t("admin.invitations.copyFailed"));
    }
  }

  async function revoke(code: InviteCodeSummary) {
    if (confirmId !== code.id) {
      setConfirmId(code.id);
      return;
    }
    setConfirmId(null);
    setError("");
    setRevokingId(code.id);
    try {
      await inviteCodesApi.revoke(code.id);
      await load();
    } catch (err) {
      setError(describeApiError(err, t("admin.invitations.revokeFailed")));
    } finally {
      setRevokingId(null);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <p className="text-sm text-fg-muted">{t("admin.invitations.intro")}</p>
      <form
        className="flex flex-wrap items-end gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          void create();
        }}
      >
        <TextField
          label={t("admin.invitations.seats")}
          type="number"
          min={1}
          value={maxSeats}
          onChange={setMaxSeats}
        />
        <TextField
          label={t("admin.invitations.expires")}
          type="date"
          value={expiresAt}
          onChange={setExpiresAt}
        />
        <TextField
          label={t("admin.invitations.note")}
          required={false}
          value={note}
          onChange={setNote}
        />
        <Button type="submit" variant="primary" disabled={creating}>
          {creating ? t("admin.invitations.creating") : t("admin.invitations.create")}
        </Button>
      </form>
      <FormError message={error} />
      {loading ? (
        <p className="text-sm text-fg-muted">{t("auth.loading")}</p>
      ) : loadError ? (
        <FormError message={loadError} />
      ) : codes.length === 0 ? (
        <p className="text-sm text-fg-muted">{t("admin.invitations.empty")}</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="text-fg-muted">
              <tr className="border-b border-border">
                <th className="py-2 pr-4 font-medium">{t("admin.invitations.code")}</th>
                <th className="py-2 pr-4 font-medium">{t("admin.invitations.status")}</th>
                <th className="py-2 pr-4 font-medium">{t("admin.invitations.seats")}</th>
                <th className="py-2 pr-4 font-medium">{t("admin.invitations.expires")}</th>
                <th className="py-2 font-medium">{t("admin.invitations.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {codes.map((code) => {
                const status = inviteCodeStatus(code);
                return (
                  <tr key={code.id} className="border-b border-border align-top">
                    <td className="py-2 pr-4">
                      <span className={`font-mono ${status === "revoked" ? "line-through" : ""}`}>
                        {code.code}
                      </span>
                      {code.boundEmail && <p className="text-fg-muted">{code.boundEmail}</p>}
                      {code.note && <p className="text-fg-muted">{code.note}</p>}
                    </td>
                    <td className="py-2 pr-4">{t(`admin.invitations.statusLabel.${status}`)}</td>
                    <td className="py-2 pr-4">
                      {code.seatsUsed} / {code.maxSeats}
                    </td>
                    <td className="py-2 pr-4">{date(code.expiresAt)}</td>
                    <td className="py-2">
                      <div className="flex gap-2">
                        <Button size="sm" onClick={() => void copy(code)}>
                          {copiedId === code.id
                            ? t("admin.invitations.copied")
                            : t("admin.invitations.copyLink")}
                        </Button>
                        {status !== "revoked" && (
                          <Button
                            size="sm"
                            disabled={revokingId === code.id}
                            onClick={() => void revoke(code)}
                          >
                            {confirmId === code.id
                              ? t("admin.invitations.confirmRevoke")
                              : t("admin.invitations.revoke")}
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
