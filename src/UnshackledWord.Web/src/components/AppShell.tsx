import { useTranslation } from "react-i18next";
import { NavLink, Outlet, ScrollRestoration } from "react-router";
import { HeaderInfoProvider } from "@/lib/headerInfo";
import { HeaderWordInfo } from "./HeaderWordInfo";
import { LanguageSwitch } from "./LanguageSwitch";
import { ThemeToggle } from "./ThemeToggle";

export function AppShell() {
  const { t } = useTranslation();

  return (
    <HeaderInfoProvider>
      <div className="flex min-h-dvh flex-col">
        <header className="app-header sticky top-0 z-30 bg-surface">
          <div className="mx-auto flex h-full max-w-page-wide flex-wrap items-center justify-between gap-x-4 px-4 md:px-8">
            <nav className="flex h-14 items-center gap-6 md:h-full">
              <NavLink to="/" className="font-scripture text-lg font-semibold text-accent">
                <svg
                  aria-hidden="true"
                  viewBox="0 0 24 24"
                  className="size-6 md:hidden"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M4 5.5A2.5 2.5 0 0 1 6.5 3H20v15H6.5A2.5 2.5 0 0 0 4 20.5Z" />
                  <path d="M4 20.5A2.5 2.5 0 0 0 6.5 23H20v-5" />
                </svg>
                <span className="sr-only md:not-sr-only">{t("app.name")}</span>
              </NavLink>
              <NavLink
                to="/bible"
                className={({ isActive }) => (isActive ? "text-fg" : "text-fg-muted hover:text-fg")}
              >
                {t("nav.bible")}
              </NavLink>
            </nav>
            {/* Own line below the menu on mobile, centre of the menu from md. */}
            <div className="order-last flex h-12 w-full min-w-0 items-center md:order-none md:h-full md:w-auto md:flex-1">
              <HeaderWordInfo />
            </div>
            <div className="flex h-14 items-center gap-4 md:h-full">
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
    </HeaderInfoProvider>
  );
}
