import { ApiError } from "@/lib/api";

export type SeedKind = "all" | "morphology" | "mappings" | "revision" | "vocab";

const baseUrl = (import.meta.env.VITE_API_URL ?? "/api").replace(/\/$/, "");

/** Asks the server to build the seed export and returns the zip with the file name the server suggested. */
export async function exportSeed(kind: SeedKind): Promise<{ blob: Blob; fileName: string }> {
  const path = `/maintainer/exportSeed?kind=${kind}`;
  const response = await fetch(`${baseUrl}${path}`, {
    method: "POST",
    credentials: "include",
    // The endpoint binds a JSON request (kind comes from the query), so it needs a JSON content type.
    headers: { "Content-Type": "application/json" },
    body: "{}",
  });
  if (!response.ok) {
    const text = await response.text().catch(() => "");
    throw new ApiError(response.status, `POST ${path} failed with ${response.status}`, text);
  }
  const disposition = response.headers.get("Content-Disposition") ?? "";
  const fileName =
    /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1] ?? `seed-export-${kind}.zip`;
  return { blob: await response.blob(), fileName };
}

/** Hands the blob to the browser as a download. */
export function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
