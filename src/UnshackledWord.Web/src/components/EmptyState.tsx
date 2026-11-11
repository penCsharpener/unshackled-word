import type { ReactNode } from "react";

export interface EmptyStateProps {
  title?: ReactNode;
  children: ReactNode;
  /** `comingSoon` adds a skeleton preview of the content that will appear. */
  variant?: "empty" | "comingSoon";
  /** Number of skeleton rows for `comingSoon` (1-5). */
  skeletonRows?: number;
  className?: string;
}

const SKELETON_IDS = [1, 2, 3, 4, 5];

function SkeletonRow() {
  return (
    <div
      aria-hidden="true"
      className="flex items-center gap-3 rounded-md bg-surface-muted px-3 py-2 opacity-55"
    >
      <span className="h-4 w-12 rounded-sm bg-border" />
      <span className="h-3 w-40 max-w-full rounded-sm bg-border" />
    </div>
  );
}

export function EmptyState({
  title,
  children,
  variant = "empty",
  skeletonRows = 2,
  className = "",
}: EmptyStateProps) {
  return (
    <div
      data-variant={variant}
      className={`grid gap-3 rounded-lg border border-dashed border-border p-4 ${className}`}
    >
      {title ? <p className="font-medium text-fg">{title}</p> : null}
      <div className="text-sm text-fg-muted">{children}</div>
      {variant === "comingSoon"
        ? SKELETON_IDS.slice(0, skeletonRows).map((id) => <SkeletonRow key={id} />)
        : null}
    </div>
  );
}
