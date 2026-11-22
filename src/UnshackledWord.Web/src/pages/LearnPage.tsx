import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { CourseCard } from "@/components/CourseCard";
import { FormError } from "@/components/FormError";
import { useAuth } from "@/lib/auth/AuthContext";
import { getCourses } from "@/lib/vocab/api";

/** `/learn` (roadmap 0022.8f): one card per course. Anonymous visitors see the cards with a sign-in hint. */
export function LearnPage() {
  const { t, i18n } = useTranslation();
  const { status } = useAuth();
  const courses = useQuery({
    queryKey: ["vocab", "courses"],
    queryFn: ({ signal }) => getCourses(signal),
  });
  if (status === "loading") return null;
  const signedIn = status === "authenticated";
  return (
    <section className="grid w-full max-w-4xl gap-4">
      <h1 className="text-2xl font-semibold">{t("learn.title")}</h1>
      {signedIn ? null : (
        <p className="rounded-lg border border-border bg-surface-muted p-3 text-sm">
          {t("learn.anonHint.lead")}{" "}
          <Link to="/login" className="text-accent underline">
            {t("learn.anonHint.link")}
          </Link>
          {t("learn.anonHint.tail")}
        </p>
      )}
      {courses.isError ? <FormError message={t("learn.loadFailed")} /> : null}
      <div className="grid gap-4 md:grid-cols-2">
        {courses.data?.courses.map((course) => (
          <CourseCard
            key={course.code}
            title={i18n.language.startsWith("en") ? course.nameEn : course.nameDe}
            subtitle={t("learn.courses.subtitle", {
              language: t(`learn.courses.languages.${course.track}`),
              count: course.totalEntries.toLocaleString(i18n.language),
            })}
            signedIn={signedIn}
            to={`/learn/${course.code}`}
            listTo={`/vocab?track=${course.track}`}
          />
        ))}
      </div>
    </section>
  );
}
