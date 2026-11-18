import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { useToast } from "@/components/Toast";
import {
  ASSIGNABLE_ROLES,
  type AssignableRole,
  adminUsersApi,
  apiErrorCode,
  assignableRolesOf,
  type UserDetail,
} from "@/lib/admin/users";

interface UserRolesSectionProps {
  user: UserDetail;
  readOnly: boolean;
  onSaved: (user: UserDetail) => void;
}

/** Rollen section of the admin user page: `user` is fixed, the guard hints mirror the server's checks. */
export function UserRolesSection({ user, readOnly, onSaved }: UserRolesSectionProps) {
  const { t } = useTranslation();
  const toast = useToast();
  const [selected, setSelected] = useState<AssignableRole[]>(() => assignableRolesOf(user.roles));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const disabledReason = (role: AssignableRole): string | null => {
    if (role !== "admin") return null;
    if (user.isSelf) return t("admin.users.rolesSection.ownAdmin");
    if (user.isLastActiveAdmin && selected.includes("admin")) {
      return t("admin.users.rolesSection.lastAdmin");
    }
    return null;
  };

  const toggle = (role: AssignableRole, checked: boolean) => {
    setSelected((current) => (checked ? [...current, role] : current.filter((r) => r !== role)));
  };

  async function save() {
    setSaving(true);
    setError("");
    try {
      const updated = await adminUsersApi.setRoles(user.id, selected);
      setSelected(assignableRolesOf(updated.roles));
      toast.show(t("admin.users.rolesSection.saved"));
      onSaved(updated);
    } catch (err) {
      const code = apiErrorCode(err);
      setError(
        code
          ? t(`admin.users.errors.${code}`, {
              defaultValue: t("admin.users.rolesSection.saveFailed"),
            })
          : t("admin.users.rolesSection.saveFailed"),
      );
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="flex flex-col gap-3" aria-labelledby="user-roles-heading">
      <h2 id="user-roles-heading" className="text-lg font-semibold">
        {t("admin.users.rolesSection.title")}
      </h2>
      <ul className="flex flex-col gap-2">
        <li>
          <label className="flex items-center gap-2">
            <input type="checkbox" checked disabled />
            {t("admin.users.roleNames.user")}
          </label>
          <p className="ml-6 text-sm text-fg-muted">{t("admin.users.roleDescriptions.user")}</p>
          <p className="ml-6 text-sm text-fg-muted">{t("admin.users.rolesSection.userFixed")}</p>
        </li>
        {ASSIGNABLE_ROLES.map((role) => {
          const reason = disabledReason(role);
          const hintId = `role-hint-${role}`;
          return (
            <li key={role}>
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={selected.includes(role)}
                  disabled={readOnly || reason !== null}
                  aria-describedby={reason ? hintId : undefined}
                  onChange={(event) => toggle(role, event.target.checked)}
                />
                {t(`admin.users.roleNames.${role}`)}
              </label>
              <p className="ml-6 text-sm text-fg-muted">
                {t(`admin.users.roleDescriptions.${role}`)}
              </p>
              {reason && (
                <p id={hintId} className="ml-6 text-sm text-fg-muted">
                  {reason}
                </p>
              )}
            </li>
          );
        })}
      </ul>
      <FormError message={error} />
      {!readOnly && (
        <div className="flex items-center gap-3">
          <Button variant="primary" disabled={saving} onClick={() => void save()}>
            {saving ? t("admin.users.rolesSection.saving") : t("admin.users.rolesSection.save")}
          </Button>
        </div>
      )}
    </section>
  );
}
