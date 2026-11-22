import i18n from "i18next";

/**
 * MDXEditor's own labels (toolbar, link dialog) through i18next under `notes.mdx.<key>`; keys we do not translate
 * keep the editor's English default. Signature of MDXEditor's `translation` prop.
 */
export function mdxTranslation(
  key: string,
  defaultValue: string,
  interpolations?: Record<string, unknown>,
): string {
  return i18n.t(`notes.mdx.${key}`, { ...interpolations, defaultValue });
}
