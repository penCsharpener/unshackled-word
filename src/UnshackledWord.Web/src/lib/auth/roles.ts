import type { Me } from "./api";

/** Roles allowed to write (same as the server's refresh endpoint: Admin, Maintainer). */
const WRITE_ROLES = ["admin", "maintainer"];

export const hasWriteRole = (user: Me | null | undefined): boolean =>
  !!user?.roles?.some((role) => WRITE_ROLES.includes(role.toLowerCase()));
