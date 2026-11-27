import type { Me } from "./api";

/** Roles allowed to write (same as the server's refresh endpoint: Admin, Maintainer). */
const WRITE_ROLES = ["admin", "maintainer"];

export const hasWriteRole = (user: Me | null | undefined): boolean =>
  !!user?.roles?.some((role) => WRITE_ROLES.includes(role.toLowerCase()));

/** Roles that may read the revision overview (the server also lets reviewers in; they cannot write variants). */
const REVISION_OVERVIEW_ROLES = [...WRITE_ROLES, "reviewer"];

export const hasRevisionOverviewRole = (user: Me | null | undefined): boolean =>
  !!user?.roles?.some((role) => REVISION_OVERVIEW_ROLES.includes(role.toLowerCase()));

export const isAdmin = (user: Me | null | undefined): boolean =>
  !!user?.roles?.some((role) => role.toLowerCase() === "admin");
