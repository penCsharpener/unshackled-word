import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { TextField } from "@/components/TextField";
import { useToast } from "@/components/Toast";
import { adminErrorText, adminUsersApi, type UserDetail } from "@/lib/admin/users";

interface UserStatusSectionProps {
  user: UserDetail;
  readOnly: boolean;
  onSaved: (user: UserDetail) => void;
}

/** Status section of the admin user page: deactivate (confirm dialog, optional reason), banner, reactivate. */
export function UserStatusSection({ user, readOnly, onSaved }: UserStatusSectionProps) {
  const { t, i18n } = useTranslation();
  const toast = useToast();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const deactivated = user.status === "deactivated";
  const info = user.deactivation;

  async function run(action: () => Promise<UserDetail>, failed: string, done: string) {
    setBusy(true);
    setError("");
    try {
      onSaved(await action());
      toast.show(done);
      setOpen(false);
      setReason("");
    } catch (err) {
      setError(adminErrorText(err, t, failed));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="flex flex-col gap-3" aria-labelledby="user-status-heading">
      <h2 id="user-status-heading" className="text-lg font-semibold">
        {t("admin.users.statusSection.title")}
      </h2>
      {deactivated && (
        <div role="note" className="flex flex-col gap-1 rounded-md border border-sev-warning p-3">
          <p className="font-medium">{t("admin.users.statusSection.bannerTitle")}</p>
          {info && (
            <p className="text-sm">
              {t("admin.users.statusSection.bannerBy", {
                name: info.byDisplayName,
                date: new Date(info.at).toLocaleString(i18n.language),
              })}
            </p>
          )}
          <p className="text-sm text-fg-muted">
            {info?.reason
              ? t("admin.users.statusSection.reason", { reason: info.reason })
              : t("admin.users.statusSection.noReason")}
          </p>
        </div>
      )}
      <FormError message={open ? "" : error} />
      {!readOnly && deactivated && (
        <div>
          <Button
            disabled={busy}
            onClick={() =>
              void run(
                () => adminUsersApi.reactivate(user.id),
                t("admin.users.statusSection.failed"),
                t("admin.users.statusSection.reactivated"),
              )
            }
          >
            {t("admin.users.statusSection.reactivate")}
          </Button>
        </div>
      )}
      {!readOnly && !deactivated && !user.isSelf && (
        <div className="flex flex-col items-start gap-1">
          <Button
            disabled={user.isLastActiveAdmin}
            aria-describedby={user.isLastActiveAdmin ? "deactivate-hint" : undefined}
            onClick={() => {
              setError("");
              setOpen(true);
            }}
          >
            {t("admin.users.statusSection.deactivate")}
          </Button>
          {user.isLastActiveAdmin && (
            <p id="deactivate-hint" className="text-sm text-fg-muted">
              {t("admin.users.statusSection.lastAdmin")}
            </p>
          )}
        </div>
      )}
      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        dismissible={!busy}
        title={t("admin.users.statusSection.confirmTitle", { name: user.displayName })}
        description={t("admin.users.statusSection.confirmText")}
        footer={
          <>
            <Button
              variant="primary"
              disabled={busy}
              onClick={() =>
                void run(
                  () => adminUsersApi.deactivate(user.id, reason),
                  t("admin.users.statusSection.failed"),
                  t("admin.users.statusSection.deactivated"),
                )
              }
            >
              {t("admin.users.statusSection.confirm")}
            </Button>
            <Button disabled={busy} onClick={() => setOpen(false)}>
              {t("admin.users.statusSection.cancel")}
            </Button>
          </>
        }
      >
        <div className="flex flex-col gap-3">
          <TextField
            multiline
            rows={3}
            required={false}
            maxLength={500}
            label={t("admin.users.statusSection.reasonLabel")}
            value={reason}
            onChange={setReason}
          />
          <FormError message={error} />
        </div>
      </Dialog>
    </section>
  );
}
