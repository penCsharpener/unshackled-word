import { useTranslation } from "react-i18next";

export function AboutPage() {
  const { t } = useTranslation();

  return (
    <section className="max-w-page">
      <h1 className="font-scripture text-4xl font-semibold">{t("about.title")}</h1>
      <p className="mt-4 text-lg text-fg-muted">{t("about.lead")}</p>
    </section>
  );
}
