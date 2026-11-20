import { useTranslation } from "react-i18next";

const SECTIONS = ["analytics", "logs", "monitoring"] as const;

export function PrivacyPage() {
  const { t } = useTranslation();
  const operatorName = import.meta.env.VITE_OPERATOR_NAME || t("privacy.operator.placeholder");
  const operatorContact =
    import.meta.env.VITE_OPERATOR_CONTACT || t("privacy.operator.placeholder");
  const events = t("privacy.analytics.events", { returnObjects: true }) as string[];

  return (
    <article className="max-w-page">
      <title>{t("privacy.title")}</title>
      <h1 className="font-scripture text-4xl font-semibold">{t("privacy.title")}</h1>
      <p className="mt-4 text-lg text-fg-muted">{t("privacy.lead")}</p>
      {SECTIONS.map((section) => (
        <section key={section} className="mt-8">
          <h2 className="font-scripture text-2xl font-semibold">{t(`privacy.${section}.title`)}</h2>
          <p className="mt-2">{t(`privacy.${section}.intro`)}</p>
          {section === "analytics" ? (
            <ul className="mt-2 list-disc space-y-1 pl-6">
              {events.map((event) => (
                <li key={event}>{event}</li>
              ))}
            </ul>
          ) : null}
          <p className="mt-2 text-fg-muted">{t(`privacy.${section}.note`)}</p>
        </section>
      ))}
      <section className="mt-8">
        <h2 className="font-scripture text-2xl font-semibold">{t("privacy.operator.title")}</h2>
        <dl className="mt-2 grid gap-1">
          <dt className="mt-1 text-sm text-fg-muted">{t("privacy.operator.name")}</dt>
          <dd>{operatorName}</dd>
          <dt className="mt-1 text-sm text-fg-muted">{t("privacy.operator.contact")}</dt>
          <dd>{operatorContact}</dd>
        </dl>
      </section>
    </article>
  );
}
