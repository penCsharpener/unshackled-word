import { useTranslation } from "react-i18next";
import { NavLink, Outlet, ScrollRestoration } from "react-router";
import { LanguageSwitch } from "./LanguageSwitch";
import { ThemeToggle } from "./ThemeToggle";

export function AppShell() {
  const { t } = useTranslation();

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-page-wide flex-wrap items-center justify-between gap-4 px-4 py-3 md:px-8">
          <nav className="flex items-center gap-6">
            <NavLink to="/" className="font-scripture text-lg font-semibold text-accent">
              {t("app.name")}
            </NavLink>
            <NavLink
              to="/bible"
              className={({ isActive }) => (isActive ? "text-fg" : "text-fg-muted hover:text-fg")}
            >
              {t("nav.bible")}
            </NavLink>
          </nav>
          <div className="flex flex-wrap items-center gap-4">
            <LanguageSwitch />
            <ThemeToggle />
          </div>
        </div>
      </header>
      <main className="mx-auto w-full max-w-page-wide flex-1 px-4 py-8 md:px-8">
        <Outlet />
      </main>
      <ScrollRestoration />
    </div>
  );
}
