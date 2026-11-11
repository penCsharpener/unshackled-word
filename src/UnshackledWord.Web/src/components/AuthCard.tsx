import type { ReactNode } from "react";

/** Centered card with a title for the auth pages; the title is the page's h1. */
export function AuthCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="mx-auto w-full max-w-popover rounded-lg border border-border bg-surface p-6">
      <h1 className="mb-4 font-scripture text-2xl font-semibold">{title}</h1>
      <div className="flex flex-col gap-4">{children}</div>
    </section>
  );
}
