import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { BibleChapterPicker } from "@/components/BibleChapterPicker";
import { Button } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { useAuth } from "@/lib/auth/AuthContext";
import { isAdmin } from "@/lib/auth/roles";
import { useBibleStructure, useNoBibleText } from "@/lib/bible/queries";

export function BiblePage() {
  const { t } = useTranslation();
  const structure = useBibleStructure();
  const noText = useNoBibleText();
  const { user } = useAuth();

  return (
    <>
      <title>{t("bible.title")}</title>
      <h1 className="font-scripture text-4xl font-semibold">{t("bible.title")}</h1>
      <p className="mt-2 mb-6 text-fg-muted">{t("bible.lead")}</p>
      {noText && (
        <EmptyState className="mb-6">
          <p role="status">{t("bible.noText.message")}</p>
          {isAdmin(user) && (
            <Link to="/edit/setup" className="text-accent underline">
              {t("bible.noText.setupLink")}
            </Link>
          )}
        </EmptyState>
      )}
      {structure.isError ? (
        <div role="alert" className="flex flex-wrap items-center gap-4">
          <p>{t("bible.structureError")}</p>
          <Button variant="secondary" onClick={() => structure.refetch()}>
            {t("bible.retry")}
          </Button>
        </div>
      ) : (
        <BibleChapterPicker books={structure.data} defaultTestament="ot" panel="inline" />
      )}
    </>
  );
}
