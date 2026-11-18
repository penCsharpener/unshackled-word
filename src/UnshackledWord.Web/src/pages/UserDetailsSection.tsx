import { type FormEvent, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { TextField } from "@/components/TextField";
import { useToast } from "@/components/Toast";
import {
  adminErrorText,
  adminUsersApi,
  apiErrorCode,
  existingUserIdOf,
  type UserDetail,
} from "@/lib/admin/users";

interface UserDetailsSectionProps {
  user: UserDetail;
  readOnly: boolean;
  onSaved: (user: UserDetail) => void;
}

/** Details section of the admin user page: name and email form plus the password-reset mail. */
export function UserDetailsSection({ user, readOnly, onSaved }: UserDetailsSectionProps) {
  const { t } = useTranslation();
  const toast = useToast();
  const [displayName, setDisplayName] = useState(user.displayName);
  const [email, setEmail] = useState(user.email);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [takenBy, setTakenBy] = useState<number | null>(null);
  const [mailBusy, setMailBusy] = useState(false);
  const [mailError, setMailError] = useState("");

  async function save(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    setError("");
    setTakenBy(null);
    try {
      const updated = await adminUsersApi.update(user.id, {
        displayName: displayName.trim(),
        email: email.trim(),
      });
      setDisplayName(updated.displayName);
      setEmail(updated.email);
      toast.show(t("admin.users.detailsSection.saved"));
      onSaved(updated);
    } catch (err) {
      if (apiErrorCode(err) === "email_taken") {
        setTakenBy(existingUserIdOf(err) ?? 0);
      } else {
        setError(adminErrorText(err, t, t("admin.users.detailsSection.saveFailed")));
      }
    } finally {
      setSaving(false);
    }
  }

  async function sendReset() {
    setMailBusy(true);
    setMailError("");
    try {
      await adminUsersApi.sendPasswordReset(user.id);
      toast.show(
        t(
          user.hasPassword
            ? "admin.users.detailsSection.resetSent"
            : "admin.users.detailsSection.welcomeSent",
        ),
      );
    } catch (err) {
      setMailError(adminErrorText(err, t, t("admin.users.detailsSection.resetFailed")));
    } finally {
      setMailBusy(false);
    }
  }

  const unchanged = displayName.trim() === user.displayName && email.trim() === user.email;
  return (
    <section className="flex flex-col gap-3" aria-labelledby="user-details-heading">
      <h2 id="user-details-heading" className="text-lg font-semibold">
        {t("admin.users.detailsSection.title")}
      </h2>
      <form className="flex flex-col gap-3" onSubmit={(event) => void save(event)}>
        <TextField
          label={t("admin.users.displayName")}
          value={displayName}
          maxLength={100}
          disabled={readOnly}
          onChange={(value) => {
            setDisplayName(value);
          }}
        />
        <TextField
          label={t("admin.users.email")}
          type="email"
          value={email}
          maxLength={254}
          disabled={readOnly}
          invalid={takenBy !== null}
          onChange={(value) => {
            setTakenBy(null);
            setEmail(value);
          }}
        />
        {takenBy !== null && (
          <p role="alert" className="text-sm font-medium text-accent">
            {t("admin.users.errors.email_taken")}{" "}
            {takenBy > 0 && (
              <Link to={`/admin/users/${takenBy}`} className="underline">
                {t("admin.users.openOtherAccount")}
              </Link>
            )}
          </p>
        )}
        <FormError message={error} />
        {!readOnly && (
          <div className="flex items-center gap-3">
            <Button type="submit" variant="primary" disabled={saving || unchanged}>
              {saving
                ? t("admin.users.detailsSection.saving")
                : t("admin.users.detailsSection.save")}
            </Button>
          </div>
        )}
      </form>
      {!readOnly && (
        <div className="flex flex-col items-start gap-2">
          <Button
            disabled={mailBusy || user.status === "deactivated"}
            onClick={() => void sendReset()}
          >
            {t("admin.users.detailsSection.sendReset")}
          </Button>
          {user.status === "deactivated" && (
            <p className="text-sm text-fg-muted">{t("admin.users.errors.deactivated")}</p>
          )}
          <FormError message={mailError} />
        </div>
      )}
    </section>
  );
}
