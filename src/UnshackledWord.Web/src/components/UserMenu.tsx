import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { Button, buttonClasses } from "./Button";
import { HeaderMenu, type HeaderMenuItem } from "./HeaderMenu";

/** Header slot: "Sign in" while logged out, else an avatar button with a menu (passkeys, sign out). */
export function UserMenu() {
  const { t } = useTranslation();
  const { status, user, logout } = useAuth();
  const navigate = useNavigate();

  if (status === "loading") return null;

  if (status === "anonymous" || !user) {
    return (
      <Link to="/login" className={buttonClasses({ variant: "secondary", size: "sm" })}>
        {t("auth.signIn")}
      </Link>
    );
  }

  const initial = (user.displayName || user.email).trim().charAt(0).toUpperCase();
  const items: HeaderMenuItem[] = [
    { key: "notes", to: "/notes", label: t("notes.mine.nav") },
    { key: "passkeys", to: "/account/passkeys", label: t("auth.passkeys.title") },
    { key: "lemmas", to: "/edit/lemmas", label: t("lemmas.menu") },
  ];
  if (hasWriteRole(user)) {
    items.push(
      { key: "vocab", to: "/edit/vocab", label: t("vocab.editor.nav") },
      { key: "quality", to: "/edit/quality", label: t("quality.nav") },
      { key: "maintainer", to: "/edit/maintainer", label: t("maintainer.nav") },
      { key: "proposals", to: "/edit/proposals", label: t("agent.proposals.nav") },
      { key: "replace", to: "/edit/revision/replace", label: t("bulkReplace.nav") },
      { key: "noteReview", to: "/edit/notes", label: t("notes.review.nav") },
      { key: "tskReview", to: "/edit/tsk", label: t("tskReview.nav") },
      { key: "lxxReview", to: "/edit/lxx", label: t("lxxReview.nav") },
      { key: "imports", to: "/edit/imports", label: t("imports.nav") },
    );
  }

  return (
    <HeaderMenu
      trigger={initial}
      label={t("auth.userMenu", { name: user.displayName || user.email })}
      heading={user.email}
      items={items}
      triggerVariant="primary"
      triggerClassName="size-8 rounded-full"
      footer={(close) => (
        <Button
          role="menuitem"
          variant="ghost"
          size="sm"
          className="justify-start"
          onClick={async () => {
            close();
            await logout();
            navigate("/");
          }}
        >
          {t("auth.signOut")}
        </Button>
      )}
    />
  );
}
