/** Short local date and time of an ISO timestamp. */
export const formatStamp = (iso: string, language: string): string =>
  new Intl.DateTimeFormat(language, { dateStyle: "short", timeStyle: "short" }).format(
    new Date(iso),
  );
