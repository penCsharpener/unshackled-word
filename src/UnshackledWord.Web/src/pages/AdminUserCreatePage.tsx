import { type FormEvent, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { RequireRole } from "@/components/RequireRole";
import { TextField } from "@/components/TextField";
import { useToast } from "@/components/Toast";
import {
  ASSIGNABLE_ROLES,
  type AssignableRole,
  adminErrorText,
  adminUsersApi,
  apiErrorCode,
  existingUserIdOf,
} from "@/lib/admin/users";

/** Admin page `/admin/users/new`: creates an account and mails the welcome link. */
export function AdminUserCreatePage() {
  const { t } = useTranslation();
  return (
    <section className="mx-auto flex w-full max-w-xl flex-col gap-4 p-4">
      <Link to="/admin/users" className="text-sm text-accent hover:underline">
        {t("admin.users.back")}
      </Link>
      <h1 className="text-2xl font-semibold">{t("admin.users.createPage.title")}</h1>
      <RequireRole requires="admin">
        <CreateForm />
      </RequireRole>
    </section>
  );
}

function CreateForm() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const toast = useToast();
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [roles, setRoles] = useState<AssignableRole[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [takenBy, setTakenBy] = useState<number | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setTakenBy(null);
    try {
      const created = await adminUsersApi.create({
        email: email.trim(),
        displayName: displayName.trim(),
        roles,
      });
      toast.show(t("admin.users.createPage.welcomeSent"));
      navigate(`/admin/users/${created.id}`);
    } catch (err) {
      if (apiErrorCode(err) === "email_taken") {
        setTakenBy(existingUserIdOf(err) ?? 0);
      } else {
        setError(adminErrorText(err, t, t("admin.users.createPage.failed")));
      }
      setBusy(false);
    }
  }

  return (
    <form className="flex flex-col gap-4" onSubmit={(event) => void submit(event)}>
      <TextField
        label={t("admin.users.email")}
        type="email"
        autoComplete="off"
        maxLength={254}
        value={email}
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
      <TextField
        label={t("admin.users.displayName")}
        maxLength={100}
        value={displayName}
        onChange={setDisplayName}
      />
      <fieldset className="flex flex-col gap-2">
        <legend className="text-sm">{t("admin.users.rolesSection.title")}</legend>
        <div>
          <label className="flex items-center gap-2">
            <input type="checkbox" checked disabled />
            {t("admin.users.roleNames.user")}
          </label>
          <p className="ml-6 text-sm text-fg-muted">{t("admin.users.roleDescriptions.user")}</p>
        </div>
        {ASSIGNABLE_ROLES.map((role) => (
          <div key={role}>
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={roles.includes(role)}
                onChange={(event) =>
                  setRoles((current) =>
                    event.target.checked ? [...current, role] : current.filter((r) => r !== role),
                  )
                }
              />
              {t(`admin.users.roleNames.${role}`)}
            </label>
            <p className="ml-6 text-sm text-fg-muted">
              {t(`admin.users.roleDescriptions.${role}`)}
            </p>
          </div>
        ))}
      </fieldset>
      <p className="text-sm text-fg-muted">{t("admin.users.createPage.hint")}</p>
      <FormError message={error} />
      <div>
        <Button type="submit" variant="primary" disabled={busy}>
          {busy ? t("admin.users.createPage.creating") : t("admin.users.createPage.submit")}
        </Button>
      </div>
    </form>
  );
}
