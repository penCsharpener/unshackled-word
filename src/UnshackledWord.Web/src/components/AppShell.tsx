import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { NavLink, Outlet, ScrollRestoration, useLocation, useMatches } from "react-router";
import { HeaderInfoProvider } from "@/lib/headerInfo";
import { PickerSlotProvider, useSetPickerSlot } from "@/lib/pickerSlot";
import { SEARCH_ICON_PATHS } from "@/lib/search/icon";
import { useSearchHotkey } from "@/lib/search/useSearchHotkey";
import { Button } from "./Button";
import { HeaderWordInfo } from "./HeaderWordInfo";
import { LanguageSwitch } from "./LanguageSwitch";
import { RateLimitNotice } from "./RateLimitNotice";
import { SearchOverlay } from "./SearchOverlay";
import { ThemeToggle } from "./ThemeToggle";
import { UserMenu } from "./UserMenu";

export function AppShell() {
  return (
    <PickerSlotProvider>
      <AppShellContent />
    </PickerSlotProvider>
  );
}

function AppShellContent() {
  const { t } = useTranslation();
  const setPickerSlot = useSetPickerSlot();
  // A route opts into the app-like layout (page does not scroll, its content fills the height) with
  // `handle: { layout: "fill" }`; below md the page scrolls normally.
  const fill = useMatches().some(
    (match) => (match.handle as { layout?: string } | undefined)?.layout === "fill",
  );

  // The search belongs to the reading area: reader, book picker and Strong's page.
  const { pathname } = useLocation();
  const searchAvailable = pathname === "/bible" || pathname.startsWith("/bible/");
  const [searchOpen, setSearchOpen] = useState(false);
  const searchButton = useRef<HTMLButtonElement>(null);
  const wasOpen = useRef(false);
  useSearchHotkey(searchAvailable && !searchOpen, () => setSearchOpen(true));
  // Closing returns focus to the magnifier (the dialog restores the element focused before it opened).
  useEffect(() => {
    if (wasOpen.current && !searchOpen) searchButton.current?.focus({ preventScroll: true });
    wasOpen.current = searchOpen;
  }, [searchOpen]);

  return (
    <HeaderInfoProvider>
      <div className={`flex min-h-dvh flex-col ${fill ? "md:h-dvh" : ""}`}>
        <header className="app-header sticky top-0 z-30 bg-surface">
          <div className="mx-auto flex h-full max-w-page-wide flex-wrap items-center justify-between gap-x-4 px-4 md:px-8">
            <nav className="flex h-14 items-center gap-6 md:h-full laptop:flex-1">
              <NavLink to="/bible" className="font-scripture text-lg font-semibold text-accent">
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
                to="/about"
                className={({ isActive }) => (isActive ? "text-fg" : "text-fg-muted hover:text-fg")}
              >
                {t("nav.about")}
              </NavLink>
            </nav>
            {/* Own line below the menu on mobile, centre of the menu from md. */}
            <div className="order-last flex h-12 w-full min-w-0 items-center md:order-none md:h-full md:w-auto md:flex-1 laptop:flex-none">
              <HeaderWordInfo />
            </div>
            {/* From 1200px the chapter picker (HeaderPickerBar) is rendered here: one slim menu row. */}
            <div ref={setPickerSlot} className="hidden h-full items-center laptop:flex" />
            <div className="flex h-14 items-center gap-4 md:h-full laptop:flex-1 laptop:justify-end">
              {searchAvailable && (
                <Button
                  ref={searchButton}
                  variant="ghost"
                  size="sm"
                  aria-label={t("search.open")}
                  title={t("search.open")}
                  aria-keyshortcuts="f"
                  aria-haspopup="dialog"
                  onClick={() => setSearchOpen(true)}
                >
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
                    {SEARCH_ICON_PATHS.map((d) => (
                      <path key={d} d={d} />
                    ))}
                  </svg>
                </Button>
              )}
              <LanguageSwitch />
              <ThemeToggle />
              <UserMenu />
            </div>
          </div>
        </header>
        <RateLimitNotice />
        <main
          className={`mx-auto flex w-full max-w-page-wide flex-1 flex-col px-4 md:px-8 ${
            fill ? "py-4 md:min-h-0 md:pb-2" : "py-8"
          }`}
        >
          <Outlet />
        </main>
        {searchAvailable && (
          <SearchOverlay open={searchOpen} onClose={() => setSearchOpen(false)} />
        )}
        <ScrollRestoration />
      </div>
    </HeaderInfoProvider>
  );
}
