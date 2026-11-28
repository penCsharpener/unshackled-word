import { useTranslation } from "react-i18next";

const SECTIONS = [
  {
    id: "features",
    items: ["reader", "links", "lxx", "search", "learn", "notes", "revision"],
  },
  { id: "roles", items: ["reader", "user", "maintainer", "admin"] },
  { id: "quality", items: ["mapping", "language", "source", "care"] },
] as const;

export function AboutPage() {
  const { t } = useTranslation();

  return (
    <section className="grid max-w-page gap-8">
      <header>
        <h1 className="font-scripture text-4xl font-semibold">{t("about.title")}</h1>
        <p className="mt-4 text-lg text-fg-muted">{t("about.lead")}</p>
      </header>
      <section aria-labelledby="about-purpose">
        <h2 id="about-purpose" className="text-2xl font-semibold">
          {t("about.purpose.title")}
        </h2>
        <p className="mt-2">{t("about.purpose.text")}</p>
      </section>
      {SECTIONS.map((section) => (
        <section key={section.id} aria-labelledby={`about-${section.id}`}>
          <h2 id={`about-${section.id}`} className="text-2xl font-semibold">
            {t(`about.${section.id}.title`)}
          </h2>
          <ul className="mt-2 grid gap-2">
            {section.items.map((item) => (
              <li key={item}>
                <strong>{t(`about.${section.id}.${item}.name`)}:</strong>{" "}
                {t(`about.${section.id}.${item}.text`)}
              </li>
            ))}
          </ul>
        </section>
      ))}
      <section aria-labelledby="about-join">
        <h2 id="about-join" className="text-2xl font-semibold">
          {t("about.join.title")}
        </h2>
        <p className="mt-2">{t("about.join.text")}</p>
      </section>
    </section>
  );
}
