import { useTranslation } from "react-i18next";
import { useAuth } from "@/lib/auth/AuthContext";
import { HeaderMenu } from "./HeaderMenu";

/** Header gear menu with the administration pages; renders nothing unless the user has the admin role. */
export function AdminMenu() {
  const { t } = useTranslation();
  const { user } = useAuth();

  if (!user?.roles?.some((role) => role.toLowerCase() === "admin")) return null;

  return (
    <HeaderMenu
      label={t("admin.menu.label")}
      items={[
        { key: "users", to: "/admin/users", label: t("admin.users.menu") },
        { key: "new", to: "/admin/users/new", label: t("admin.users.create") },
        { key: "invitations", to: "/admin/invitations", label: t("admin.invitations.menu") },
        { key: "log", to: "/admin/log", label: t("admin.log.menu") },
      ]}
      trigger={
        <svg
          aria-hidden="true"
          viewBox="0 0 24 24"
          className="size-5"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6Z" />
          <path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1Z" />
        </svg>
      }
    />
  );
}
