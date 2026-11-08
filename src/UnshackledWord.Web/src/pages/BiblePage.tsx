import { useTranslation } from "react-i18next";
import { BibleChapterPicker } from "@/components/BibleChapterPicker";
import { Button } from "@/components/Button";
import { useBibleStructure } from "@/lib/bible/queries";

export function BiblePage() {
  const { t } = useTranslation();
  const structure = useBibleStructure();

  return (
    <>
      <title>{t("bible.title")}</title>
      <h1 className="font-scripture text-4xl font-semibold">{t("bible.title")}</h1>
      <p className="mt-2 mb-6 text-fg-muted">{t("bible.lead")}</p>
      {structure.isError ? (
        <div role="alert" className="flex flex-wrap items-center gap-4">
          <p>{t("bible.structureError")}</p>
          <Button variant="secondary" onClick={() => structure.refetch()}>
            {t("bible.retry")}
          </Button>
        </div>
      ) : (
        <BibleChapterPicker books={structure.data} defaultTestament="ot" />
      )}
    </>
  );
}
