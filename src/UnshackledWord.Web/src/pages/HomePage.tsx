import { useTranslation } from "react-i18next";

export function HomePage() {
  const { t } = useTranslation();

  return (
    <section className="max-w-page">
      <h1 className="font-scripture text-4xl font-semibold">{t("home.title")}</h1>
      <p className="mt-4 text-lg text-fg-muted">{t("home.lead")}</p>
    </section>
  );
}
