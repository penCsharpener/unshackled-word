import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { useAuth } from "@/lib/auth/AuthContext";
import { Button, buttonClasses } from "./Button";

/** Header slot: "Sign in" while logged out, else an avatar button with a menu (passkeys, sign out). */
export function UserMenu() {
  const { t } = useTranslation();
  const { status, user, logout } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onPointer = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("pointerdown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  if (status === "loading") return null;

  if (status === "anonymous" || !user) {
    return (
      <Link to="/login" className={buttonClasses({ variant: "secondary", size: "sm" })}>
        {t("auth.signIn")}
      </Link>
    );
  }

  const initial = (user.displayName || user.email).trim().charAt(0).toUpperCase();

  return (
    <div ref={root} className="relative">
      <Button
        variant="primary"
        size="sm"
        className="size-8 rounded-full"
        aria-label={t("auth.userMenu", { name: user.displayName || user.email })}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((value) => !value)}
      >
        {initial}
      </Button>
      {open && (
        <div
          role="menu"
          className="absolute right-0 top-full z-40 mt-2 flex min-w-48 flex-col gap-1 rounded-md border border-border bg-surface p-2 text-sm"
        >
          <p className="truncate px-2 py-1 text-fg-muted">{user.email}</p>
          <Link
            role="menuitem"
            to="/account/passkeys"
            className="rounded-md px-2 py-1 hover:bg-surface-muted"
            onClick={() => setOpen(false)}
          >
            {t("auth.passkeys.title")}
          </Link>
          {user.roles?.includes("admin") && (
            <Link
              role="menuitem"
              to="/admin/invitations"
              className="rounded-md px-2 py-1 hover:bg-surface-muted"
              onClick={() => setOpen(false)}
            >
              {t("admin.invitations.menu")}
            </Link>
          )}
          <Button
            role="menuitem"
            variant="ghost"
            size="sm"
            className="justify-start"
            onClick={async () => {
              setOpen(false);
              await logout();
              navigate("/");
            }}
          >
            {t("auth.signOut")}
          </Button>
        </div>
      )}
    </div>
  );
}
