import { useState } from "react";
import { useTranslation } from "react-i18next";
import { ApiError, describeApiError, describeApiErrors } from "@/lib/api";
import { ELB1871_ONLY_LEVEL } from "@/lib/bible/api";
import { reviewMarkers, toMappingData } from "@/lib/revision/mapping";
import {
  useRevisionHistory,
  useRevisionLevels,
  useRevisionMapping,
  useRevisionVerse,
  useSaveRevision,
  useSaveRevisionMapping,
  useWithdrawRevision,
} from "@/lib/revision/queries";
import type { RevisionVerseRef, SaveRevisionResponse } from "@/lib/revision/types";
import { RevisionLevelDialog } from "@/pages/RevisionLevelDialog";
import { Button } from "./Button";
import { Dialog } from "./Dialog";
import { FormError } from "./FormError";
import { MappingEditor, type MappingSaveError } from "./MappingEditor";
import { Select } from "./Select";
import { TextField } from "./TextField";
import { VersionHistory } from "./VersionHistory";

/** Value of the "New level …" entry in the level select. */
const NEW_LEVEL = "new";

export interface RevisionEditorProps {
  verseRef: RevisionVerseRef;
  /** Verse reference shown in the panel label, e.g. "Joh 3,16". */
  reference: string;
  onClose: () => void;
}

interface SavedState extends SaveRevisionResponse {
  text: string;
}

/** Panel below a verse: write a new version of the text at one revision level (Admin / Maintainer). */
export function RevisionEditor({ verseRef, reference, onClose }: RevisionEditorProps) {
  const { t } = useTranslation();
  const levels = useRevisionLevels();
  const verse = useRevisionVerse(verseRef);
  const save = useSaveRevision(verseRef);
  const [level, setLevel] = useState<number>();
  /** Edited texts per level; a level without an entry shows its prefill. */
  const [drafts, setDrafts] = useState<Record<number, string>>({});
  const [note, setNote] = useState("");
  const [saved, setSaved] = useState<SavedState>();
  /** The mapping form opens right after a text save; closing it keeps the summary. */
  const [mappingOpen, setMappingOpen] = useState(false);
  const mapping = useRevisionMapping(saved && mappingOpen ? saved.revisionVerseId : null);
  const saveMapping = useSaveRevisionMapping();
  const withdraw = useWithdrawRevision(verseRef);
  /** Starting text per level: 100 = Elberfelder 1871, else the level of an existing variant of this verse. */
  const [sources, setSources] = useState<Record<number, number>>({});
  const [newLevelOpen, setNewLevelOpen] = useState(false);
  /** Source waiting for the confirm because the field holds unsaved edits. */
  const [pendingSource, setPendingSource] = useState<number>();
  const [removed, setRemoved] = useState(false);

  const variants = verse.data?.variants ?? [];
  const writable = (levels.data ?? []).filter((l) => !l.isBase);
  const versionOf = (value: number) => variants.find((v) => v.level === value)?.version;
  const nameOf = (value: number) => writable.find((l) => l.level === value)?.name ?? `${value}`;
  const labelOf = (l: { level: number; name: string }) => {
    const version = versionOf(l.level);
    return version === undefined
      ? l.name
      : t("bible.revision.levelOption", { name: l.name, version });
  };
  // levels that already have a variant first, then the others
  const options = [
    ...writable.filter((l) => versionOf(l.level) !== undefined),
    ...writable.filter((l) => versionOf(l.level) === undefined),
  ];
  const activeLevel = level ?? (options.find((l) => l.isDefault) ?? options[0])?.level;
  const variant = variants.find((v) => v.level === activeLevel);
  const sourceLevel =
    activeLevel === undefined
      ? ELB1871_ONLY_LEVEL
      : (sources[activeLevel] ?? (variant ? activeLevel : ELB1871_ONLY_LEVEL));
  const sourceText = (value: number) =>
    value === ELB1871_ONLY_LEVEL
      ? (verse.data?.baseText ?? "")
      : (variants.find((v) => v.level === value)?.text ?? verse.data?.baseText ?? "");
  const prefill = sourceText(sourceLevel);
  const stored = variant?.text ?? verse.data?.baseText ?? "";
  const text = activeLevel === undefined ? "" : (drafts[activeLevel] ?? prefill);
  const changed = text !== stored && text.trim().length > 0;
  const history = useRevisionHistory(verseRef, activeLevel, variant !== undefined && !saved);

  function applySource(value: number) {
    if (activeLevel === undefined) return;
    setSources({ ...sources, [activeLevel]: value });
    const { [activeLevel]: _discarded, ...rest } = drafts;
    setDrafts(rest);
  }

  function chooseSource(value: number) {
    if (
      activeLevel !== undefined &&
      drafts[activeLevel] !== undefined &&
      drafts[activeLevel] !== prefill
    ) {
      setPendingSource(value);
      return;
    }
    applySource(value);
  }
  const conflict = save.error instanceof ApiError && save.error.status === 409;

  function reload() {
    setDrafts({});
    setNote("");
    save.reset();
    void verse.refetch();
  }

  function submit() {
    if (activeLevel === undefined || !changed) return;
    const body = {
      level: activeLevel,
      text: text.trim(),
      expectedVersion: variant?.version,
      note: note.trim() || undefined,
      basedOnLevel: sourceLevel,
    };
    save.mutate(body, {
      onSuccess: (response) => {
        setSaved({ ...response, text: body.text });
        setMappingOpen(true);
        saveMapping.reset();
        setDrafts({});
        setSources({});
        setRemoved(false);
        setNote("");
      },
    });
  }

  function mappingSaveError(): MappingSaveError | null {
    if (!saveMapping.isError) return null;
    const status = saveMapping.error instanceof ApiError ? saveMapping.error.status : 0;
    if (status === 400) return "validation";
    return status === 403 ? "forbidden" : "failed";
  }

  const loading = levels.isLoading || verse.isLoading;
  const failed = levels.isError || verse.isError;
  const levelName = nameOf;

  return (
    <section
      aria-label={t("bible.revision.panelLabel", { reference })}
      className="map-panel flex flex-col gap-3 font-sans text-sm"
    >
      {loading && (
        <p role="status" className="text-fg-muted">
          {t("bible.revision.loading")}
        </p>
      )}
      {failed && (
        <div role="alert" className="flex flex-wrap items-center gap-4">
          <p>{t("bible.revision.loadError")}</p>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => {
              void levels.refetch();
              void verse.refetch();
            }}
          >
            {t("bible.retry")}
          </Button>
          <Button variant="ghost" size="sm" onClick={onClose}>
            {t("bible.revision.close")}
          </Button>
        </div>
      )}
      {verse.data && activeLevel !== undefined && saved && (
        <div className="flex flex-col gap-2">
          <p role="status" className="font-medium">
            {t("bible.revision.saved", { level: levelName(saved.level), version: saved.version })}
          </p>
          <p className="whitespace-pre-wrap font-serif text-base">{saved.text}</p>
          <p role="status" className="text-fg-muted">
            {t("bible.revision.carry", {
              same: saved.carry.same,
              changed: saved.carry.changed,
              new: saved.carry.new,
              lost: saved.carry.lostOriginalWordIds.length,
            })}
          </p>
          {mappingOpen && mapping.isLoading && (
            <p role="status" className="text-fg-muted">
              {t("bible.mapping.loading")}
            </p>
          )}
          {mappingOpen && mapping.isError && (
            <div role="alert" className="flex flex-wrap items-center gap-4">
              <p>{t("bible.mapping.loadError")}</p>
              <Button variant="secondary" size="sm" onClick={() => mapping.refetch()}>
                {t("bible.retry")}
              </Button>
            </div>
          )}
          {mappingOpen && mapping.data && (
            <MappingEditor
              data={toMappingData(mapping.data, true)}
              reference={reference}
              review={reviewMarkers(mapping.data, saved.carry)}
              saving={saveMapping.isPending}
              saveError={mappingSaveError()}
              saveErrorDetails={describeApiErrors(saveMapping.error)}
              onCancel={() => setMappingOpen(false)}
              onSave={(links) =>
                saveMapping.mutate(
                  { id: saved.revisionVerseId, links },
                  { onSuccess: () => setMappingOpen(false) },
                )
              }
            />
          )}
          <div className="flex gap-2">
            {!mappingOpen && (
              <Button size="sm" variant="secondary" onClick={() => setMappingOpen(true)}>
                {t("bible.revision.openMapping")}
              </Button>
            )}
            <Button size="sm" variant="secondary" onClick={() => setSaved(undefined)}>
              {t("bible.revision.editAgain")}
            </Button>
            <Button size="sm" variant="ghost" onClick={onClose}>
              {t("bible.revision.close")}
            </Button>
          </div>
        </div>
      )}
      {verse.data && activeLevel !== undefined && !saved && (
        <>
          <Select
            label={t("bible.revision.level")}
            value={String(activeLevel)}
            onChange={(value) => {
              if (value === NEW_LEVEL) setNewLevelOpen(true);
              else {
                setLevel(Number(value));
                setRemoved(false);
              }
            }}
            options={[
              ...options.map((l) => ({ value: String(l.level), label: labelOf(l) })),
              { value: NEW_LEVEL, label: t("bible.revision.newLevel") },
            ]}
            size="md"
          />
          <Select
            label={t("bible.revision.source")}
            value={String(sourceLevel)}
            onChange={(value) => chooseSource(Number(value))}
            options={[
              { value: String(ELB1871_ONLY_LEVEL), label: t("bible.revision.sourceBase") },
              ...variants.map((v) => ({
                value: String(v.level),
                label: t("bible.revision.levelOption", {
                  name: nameOf(v.level),
                  version: v.version,
                }),
              })),
            ]}
          />
          <p className="text-fg-muted">
            {variant
              ? t("bible.revision.activeVersion", {
                  version: variant.version,
                  date: new Date(variant.createdAt).toLocaleDateString(),
                })
              : t("bible.revision.noVariant")}
          </p>
          <TextField
            multiline
            rows={4}
            label={t("bible.revision.text")}
            value={text}
            maxLength={2000}
            onChange={(value) => setDrafts({ ...drafts, [activeLevel]: value })}
          />
          {removed && !variant ? (
            <p role="status" className="text-fg-muted">
              {t("bible.revision.removed")}
            </p>
          ) : null}
          <TextField
            label={t("bible.revision.note")}
            value={note}
            required={false}
            onChange={setNote}
          />
          {save.isError &&
            (conflict ? (
              <div role="alert" className="flex flex-wrap items-center gap-3">
                <p className="font-medium text-accent">{t("bible.revision.conflict")}</p>
                <Button size="sm" variant="secondary" onClick={reload}>
                  {t("bible.revision.reload")}
                </Button>
              </div>
            ) : (
              <FormError message={describeApiError(save.error, t("bible.revision.saveError"))} />
            ))}
          <div className="flex gap-2">
            <Button
              size="sm"
              variant="primary"
              disabled={!changed || save.isPending || conflict}
              onClick={submit}
            >
              {save.isPending ? t("bible.revision.saving") : t("bible.revision.save")}
            </Button>
            <Button size="sm" variant="ghost" onClick={onClose}>
              {t("bible.revision.cancel")}
            </Button>
          </div>
          {variant && history.data ? (
            <VersionHistory
              versions={history.data.versions}
              canRemove
              onRestore={(restored) => setDrafts({ ...drafts, [activeLevel]: restored })}
              onRemove={() =>
                withdraw.mutate(variant.revisionVerseId, {
                  onSuccess: () => {
                    setRemoved(true);
                    setDrafts({});
                    setSources({});
                  },
                })
              }
            />
          ) : null}
          {withdraw.isError ? (
            <FormError
              message={describeApiError(withdraw.error, t("bible.revision.removeError"))}
            />
          ) : null}
        </>
      )}
      <RevisionLevelDialog
        key={newLevelOpen ? "open" : "closed"}
        open={newLevelOpen}
        onClose={() => setNewLevelOpen(false)}
        onSaved={setLevel}
      />
      <Dialog
        open={pendingSource !== undefined}
        onClose={() => setPendingSource(undefined)}
        title={t("bible.revision.replaceTitle")}
        description={t("bible.revision.replaceBody")}
        footer={
          <>
            <Button variant="ghost" onClick={() => setPendingSource(undefined)}>
              {t("bible.revision.replaceCancel")}
            </Button>
            <Button
              variant="primary"
              onClick={() => {
                if (pendingSource !== undefined) applySource(pendingSource);
                setPendingSource(undefined);
              }}
            >
              {t("bible.revision.replaceConfirm")}
            </Button>
          </>
        }
      >
        <span />
      </Dialog>
    </section>
  );
}
