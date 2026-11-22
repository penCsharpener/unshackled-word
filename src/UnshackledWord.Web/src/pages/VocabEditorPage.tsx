import {
  type UseMutationResult,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Navigate, useNavigate, useParams } from "react-router";
import { AudioField } from "@/components/AudioField";
import { Button, buttonClasses } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { MeaningListEditor, type MeaningRow, meaningText } from "@/components/MeaningListEditor";
import { RankedBarList, type RankedItem } from "@/components/RankedBarList";
import { SaveBar, type SaveStatus } from "@/components/SaveBar";
import { StatusBadge } from "@/components/StatusBadge";
import { StrongsKeyList } from "@/components/StrongsKeyList";
import { Switch } from "@/components/Switch";
import { TextField } from "@/components/TextField";
import { ApiError, describeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { formatStamp } from "@/lib/format";
import { getCourses, vocabLang } from "@/lib/vocab/api";
import {
  acceptVocabForm,
  deleteVocabAudio,
  deleteVocabEntry,
  getVocabEditorEntry,
  getVocabEditorList,
  retireVocabEntry,
  uploadVocabAudio,
  type VocabEditorEntryResponse,
  type VocabEditorListItem,
  vocabEditorKeys,
} from "@/lib/vocab/editorApi";
import {
  buildSavePlan,
  draftFromEntry,
  type EditorDraft,
  type SavePlan,
  saveVocabEntryPlan,
} from "@/lib/vocab/editorDraft";
import { formatPos } from "@/lib/vocab/pos";
import { KeyMoveDialog } from "./vocab/KeyMoveDialog";
import { MergeDialog } from "./vocab/MergeDialog";

type ConfirmAction = "retire" | "delete";

/** Reads the neighbour entry one rank away (the header's prev / next links); disabled without a rank. */
function useNeighbour(courseCode: string | undefined, rank: number | null, offset: number) {
  const target = rank === null ? 0 : rank + offset;
  return useQuery({
    queryKey: vocabEditorKeys.list({
      course: courseCode,
      rankFrom: target,
      rankTo: target,
      skip: 0,
      take: 1,
    }),
    queryFn: ({ signal }) =>
      getVocabEditorList(
        { course: courseCode, rankFrom: target, rankTo: target, skip: 0, take: 1 },
        signal,
      ),
    enabled: courseCode !== undefined && target >= 1,
  });
}

/**
 * Editor of one vocab entry (screen 6, roadmap 0022.4f): form, meanings, Strong's keys and the
 * danger zone, with the renderings beside them. Edits are kept as a draft and saved together.
 */
export function VocabEditorPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const entryId = Number(id);
  const validId = Number.isInteger(entryId) && entryId > 0;
  const { status, user } = useAuth();
  const canWrite = hasWriteRole(user);
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: vocabEditorKeys.entry(entryId),
    queryFn: ({ signal }) => getVocabEditorEntry(entryId, signal),
    enabled: validId && canWrite,
  });

  const save = useMutation({
    mutationFn: (plan: SavePlan) => saveVocabEntryPlan(entryId, plan),
    // Saved or not: reload, so the draft starts again from what the server holds.
    onSettled: () => queryClient.invalidateQueries({ queryKey: vocabEditorKeys.all }),
  });

  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;
  if (!canWrite) {
    return (
      <section className="mx-auto flex w-full max-w-6xl flex-col gap-4 p-4">
        <p role="alert" className="text-fg-muted">
          {t("admin.forbidden")}
        </p>
      </section>
    );
  }
  if (!validId || query.isError) {
    const notFound = !validId || (query.error instanceof ApiError && query.error.status === 404);
    return (
      <section className="mx-auto flex w-full max-w-6xl flex-col gap-4 p-4">
        <Link to="/edit/vocab" className="text-sm text-accent underline">
          {t("vocab.editor.back")}
        </Link>
        <FormError message={notFound ? t("vocab.editor.notFound") : t("vocab.editor.loadFailed")} />
      </section>
    );
  }
  if (!query.data) return null;

  // A fresh body per loaded version: the draft starts again from what the server holds after each save.
  return <VocabEditorBody key={query.dataUpdatedAt} data={query.data} save={save} />;
}

interface VocabEditorBodyProps {
  data: VocabEditorEntryResponse;
  save: UseMutationResult<void, Error, SavePlan>;
}

function VocabEditorBody({ data, save }: VocabEditorBodyProps) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { entry } = data;
  const lang = vocabLang(entry.track);
  const number = (value: number) => value.toLocaleString(i18n.language);

  const saved = draftFromEntry(data);
  const [draft, setDraft] = useState<EditorDraft>(saved);
  const [ownMeaning, setOwnMeaning] = useState("");
  const [moveKey, setMoveKey] = useState<string | null>(null);
  const [mergeOpen, setMergeOpen] = useState(false);
  const [confirm, setConfirm] = useState<ConfirmAction | null>(null);
  const plan = buildSavePlan(saved, draft);

  // Ctrl+S (Cmd+S) saves the pending changes, also while a field has focus; the browser's own "save page" stays quiet.
  const saveRef = useRef<() => void>(() => {});
  saveRef.current = () => {
    if (plan.count > 0 && !save.isPending) save.mutate(plan);
  };
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && !event.altKey && event.key.toLowerCase() === "s") {
        event.preventDefault();
        saveRef.current();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  const courses = useQuery({
    queryKey: ["vocab", "courses"],
    queryFn: ({ signal }) => getCourses(signal),
  });
  const course = courses.data?.courses.find((item) => item.track === entry.track);
  const rank = entry.rank ?? null;
  const prevQuery = useNeighbour(course?.code, rank, -1);
  const nextQuery = useNeighbour(course?.code, rank, 1);
  const prev: VocabEditorListItem | undefined = prevQuery.data?.items[0];
  const next: VocabEditorListItem | undefined = nextQuery.data?.items[0];

  const reloadEditor = () => queryClient.invalidateQueries({ queryKey: vocabEditorKeys.all });

  // Accepting an agent form keeps its text and only sets the origin to manual; the reload shows the new badge.
  const acceptForm = useMutation({
    mutationFn: () => acceptVocabForm(entry.id),
    onSuccess: () => reloadEditor(),
  });

  const retire = useMutation({
    mutationFn: () => retireVocabEntry(entry.id),
    onSuccess: () => {
      setConfirm(null);
      void queryClient.invalidateQueries({ queryKey: vocabEditorKeys.all });
    },
  });
  const remove = useMutation({
    mutationFn: () => deleteVocabEntry(entry.id),
    onSuccess: () => {
      // Leave the page first: the deleted entry must not be refetched here.
      navigate("/edit/vocab");
      void queryClient.invalidateQueries({ queryKey: vocabEditorKeys.all });
    },
  });

  const set = (patch: Partial<EditorDraft>) => setDraft((current) => ({ ...current, ...patch }));

  const addMeaning = (german: string) => {
    const text = german.trim();
    if (!text) return;
    const same = draft.meanings.findIndex(
      (row) => meaningText(row).trim().toLowerCase() === text.toLowerCase(),
    );
    if (same >= 0) {
      // Already listed (accepted or not): accept it instead of listing it twice.
      set({
        meanings: draft.meanings.map((row, index) =>
          index === same ? { ...row, isAccepted: true } : row,
        ),
      });
      return;
    }
    const row: MeaningRow = {
      german: text,
      count: null,
      share: null,
      isAccepted: true,
      origin: "manual",
      isPinned: false,
    };
    set({ meanings: [...draft.meanings, row] });
  };

  const acceptedGermans = new Set(
    draft.meanings
      .filter((row) => row.isAccepted)
      .map((row) => meaningText(row).trim().toLowerCase()),
  );
  const renderings: RankedItem[] = data.renderings.map((rendering) => {
    const label = (strongsKey: string) =>
      data.strongsKeys.length > 1
        ? t("vocab.editor.renderings.versesKey", { word: rendering.germanDisplay, key: strongsKey })
        : t("vocab.editor.renderings.verses", { word: rendering.germanDisplay });
    const notAccepted = !acceptedGermans.has(rendering.germanDisplay.trim().toLowerCase());
    return {
      key: rendering.germanKey,
      label: rendering.germanDisplay,
      sub:
        rendering.fromRevisionCount > 0
          ? t("vocab.editor.renderings.sub", { count: number(rendering.fromRevisionCount) })
          : undefined,
      value: rendering.linkCount,
      share: rendering.share / 100,
      flags: notAccepted
        ? [
            {
              key: "notAccepted",
              label: t("vocab.editor.renderings.notAccepted"),
              severity: "info" as const,
            },
          ]
        : [],
      muted: notAccepted,
      action: data.strongsKeys.map((strongs) => (
        <a
          key={strongs.strongsKey}
          href={`/bible/strongs/${strongs.strongsKey}?rendering=${encodeURIComponent(rendering.germanKey)}`}
          target="_blank"
          rel="noopener noreferrer"
          title={label(strongs.strongsKey)}
          aria-label={label(strongs.strongsKey)}
          className="inline-flex items-center gap-1 rounded-sm text-accent"
        >
          <svg
            aria-hidden="true"
            viewBox="0 0 16 16"
            className="size-4"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.5"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <path d="M3 8h9M8.5 4.5 12 8l-3.5 3.5" />
          </svg>
          {data.strongsKeys.length > 1 ? (
            <span className="text-xs">{strongs.strongsKey}</span>
          ) : null}
        </a>
      )),
    };
  });

  const meta = [
    rank !== null ? t("vocab.editor.meta.rank", { rank }) : null,
    t("vocab.editor.meta.occurrences", { count: number(entry.occurrences) }),
    formatPos(entry.pos, t),
    t("vocab.editor.learners", { count: number(data.learners) }),
  ].filter((part): part is string => part !== null);

  const origin = entry.dictionaryFormOrigin ?? null;
  // Unsaved text would be lost by the reload that accepting causes: save the form first.
  const formDirty = draft.dictionaryForm !== saved.dictionaryForm;
  const dangerError = retire.error ?? remove.error;
  const confirmBusy = retire.isPending || remove.isPending;

  return (
    <section className="mx-auto flex w-full max-w-6xl flex-col gap-4 p-4 pb-24">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="grid gap-1">
          <Link to="/edit/vocab" className="text-sm text-accent underline">
            {t("vocab.editor.back")}
          </Link>
          <h1 className="flex flex-wrap items-baseline gap-3">
            <span
              lang={lang}
              dir={lang === "he" ? "rtl" : undefined}
              className="font-original text-3xl"
            >
              {entry.lemma}
            </span>
            <span className="text-sm text-fg-muted">{meta.join(" · ")}</span>
          </h1>
          {entry.retiredAt ? (
            <span>
              <StatusBadge tone="warning">{t("vocab.editor.retired")}</StatusBadge>
            </span>
          ) : null}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {prev ? (
            <Link
              to={`/edit/vocab/${prev.id}`}
              lang={vocabLang(prev.track)}
              className={buttonClasses({ variant: "ghost", size: "sm" })}
            >
              ← {prev.lemma}
            </Link>
          ) : null}
          {next ? (
            <Link
              to={`/edit/vocab/${next.id}`}
              lang={vocabLang(next.track)}
              className={buttonClasses({ variant: "ghost", size: "sm" })}
            >
              {next.lemma} →
            </Link>
          ) : null}
          <Switch checked={draft.reviewed} onCheckedChange={(reviewed) => set({ reviewed })}>
            {t("vocab.editor.reviewed")}
          </Switch>
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-5">
        <div className="grid content-start gap-4 lg:col-span-3">
          <section className="grid gap-3 rounded-lg border border-border bg-surface p-4">
            <div className="flex items-center justify-between gap-2">
              <h2 className="text-lg font-semibold">{t("vocab.editor.form.title")}</h2>
              <div className="flex flex-wrap items-center gap-2">
                {origin ? (
                  <StatusBadge tone={origin === "agent" ? "info" : "neutral"}>
                    {t(`vocab.editor.formOrigin.${origin}`)}
                  </StatusBadge>
                ) : null}
                {origin === "agent" ? (
                  <Button
                    variant="secondary"
                    size="sm"
                    disabled={formDirty || acceptForm.isPending}
                    onClick={() => acceptForm.mutate()}
                  >
                    {t("vocab.editor.form.accept")}
                  </Button>
                ) : null}
              </div>
            </div>
            <FormError message={acceptForm.isError ? t("vocab.editor.actionFailed") : ""} />
            <TextField
              required={false}
              label={t("vocab.editor.form.label")}
              value={draft.dictionaryForm}
              onChange={(dictionaryForm) => set({ dictionaryForm })}
              lang={lang}
              dir={lang === "he" ? "rtl" : undefined}
              className="font-original text-lg"
            />
            <TextField
              required={false}
              size="sm"
              label={t("vocab.editor.form.transliteration")}
              value={draft.transliteration}
              onChange={(transliteration) => set({ transliteration })}
            />
          </section>

          <section className="grid gap-3 rounded-lg border border-border bg-surface p-4">
            <div className="flex items-center justify-between gap-2">
              <h2 className="text-lg font-semibold">{t("vocab.editor.meanings.title")}</h2>
              <span className="text-xs text-fg-muted">{t("vocab.editor.meanings.hint")}</span>
            </div>
            <MeaningListEditor rows={draft.meanings} onChange={(meanings) => set({ meanings })} />
            <div className="flex flex-wrap items-end gap-2">
              <TextField
                required={false}
                size="sm"
                label={t("vocab.editor.meanings.own")}
                placeholder={t("vocab.editor.meanings.ownPlaceholder")}
                value={ownMeaning}
                onChange={setOwnMeaning}
              />
              <Button
                variant="secondary"
                size="sm"
                disabled={ownMeaning.trim() === ""}
                onClick={() => {
                  addMeaning(ownMeaning);
                  setOwnMeaning("");
                }}
              >
                {t("vocab.editor.meanings.add")}
              </Button>
            </div>
          </section>

          <section className="grid gap-3 rounded-lg border border-border bg-surface p-4">
            <h2 className="text-lg font-semibold">{t("vocab.editor.strongs.title")}</h2>
            <StrongsKeyList
              keys={data.strongsKeys.map((key) => ({
                key: key.strongsKey,
                occurrences: key.occurrences,
              }))}
              onMove={setMoveKey}
            />
          </section>

          <section className="grid gap-3 rounded-lg border border-border bg-surface p-4">
            <h2 className="text-lg font-semibold">{t("vocab.editor.audio.title")}</h2>
            <AudioField
              src={entry.audioUrl ?? null}
              onUpload={async (file) => {
                await uploadVocabAudio(entry.id, file, file.name);
                await reloadEditor();
              }}
              onRecord={async (mp3) => {
                await uploadVocabAudio(entry.id, mp3);
                await reloadEditor();
              }}
              onDelete={async () => {
                await deleteVocabAudio(entry.id);
                await reloadEditor();
              }}
            />
          </section>

          <section className="grid gap-3 rounded-lg border border-sev-error bg-surface p-4">
            <h2 className="text-lg font-semibold">{t("vocab.editor.danger.title")}</h2>
            <p className="text-sm text-fg-muted">{t("vocab.editor.danger.intro")}</p>
            <div className="flex flex-wrap gap-2">
              <Button variant="secondary" size="sm" onClick={() => setMergeOpen(true)}>
                {t("vocab.editor.danger.merge")}
              </Button>
              <Button
                variant="secondary"
                size="sm"
                disabled={entry.retiredAt != null}
                onClick={() => setConfirm("retire")}
              >
                {t("vocab.editor.danger.retire")}
              </Button>
              <Button
                variant="secondary"
                size="sm"
                disabled={data.strongsKeys.length > 0}
                title={
                  data.strongsKeys.length > 0 ? t("vocab.editor.danger.deleteBlocked") : undefined
                }
                onClick={() => setConfirm("delete")}
              >
                {t("vocab.editor.danger.delete")}
              </Button>
            </div>
            <FormError
              message={
                dangerError ? describeApiError(dangerError, t("vocab.editor.actionFailed")) : ""
              }
            />
          </section>
        </div>

        <aside className="grid content-start gap-3 lg:col-span-2">
          <section className="grid gap-3 rounded-lg border border-border bg-surface p-4 lg:sticky lg:top-4">
            <RankedBarList
              title={t("vocab.editor.renderings.title")}
              items={renderings}
              onSelect={(key) => {
                const rendering = data.renderings.find((item) => item.germanKey === key);
                if (rendering) addMeaning(rendering.germanDisplay);
              }}
            />
            <p className="text-xs text-fg-muted">
              {data.refreshedAt
                ? t("vocab.detail.renderingsAsOf", {
                    time: formatStamp(data.refreshedAt, i18n.language),
                  })
                : t("vocab.detail.renderingsNever")}
            </p>
          </section>
        </aside>
      </div>

      <SaveBar
        count={plan.count}
        status={saveStatusOf(save)}
        message={save.isError ? describeApiError(save.error, t("vocab.editor.saveFailed")) : ""}
        savedCount={save.variables?.count ?? 0}
        hint={
          plan.learningRelevant && data.learners > 0
            ? t("vocab.editor.notify", { count: number(data.learners) })
            : undefined
        }
        saveLabel={t("vocab.editor.save")}
        onSave={() => save.mutate(plan)}
        onDiscard={() => setDraft(saved)}
      />

      {moveKey !== null ? (
        <KeyMoveDialog
          entryId={entry.id}
          lemma={entry.lemma}
          strongsKey={moveKey}
          lastKey={data.strongsKeys.length === 1}
          learners={data.learners}
          course={course?.code}
          onClose={() => setMoveKey(null)}
        />
      ) : null}
      {mergeOpen ? (
        <MergeDialog
          entryId={entry.id}
          lemma={entry.lemma}
          keyCount={data.strongsKeys.length}
          learners={data.learners}
          course={course?.code}
          onClose={() => setMergeOpen(false)}
        />
      ) : null}

      <Dialog
        open={confirm !== null}
        onClose={() => setConfirm(null)}
        title={
          confirm === "delete"
            ? t("vocab.editor.delete.title", { lemma: entry.lemma })
            : t("vocab.editor.retire.title", { lemma: entry.lemma })
        }
        dismissible={!confirmBusy}
        footer={
          <div className="flex justify-end gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setConfirm(null)}
              disabled={confirmBusy}
            >
              {t("vocab.editor.cancel")}
            </Button>
            <Button
              variant="primary"
              size="sm"
              disabled={confirmBusy}
              onClick={() => {
                if (confirm === "delete") remove.mutate();
                else retire.mutate();
              }}
            >
              {confirm === "delete"
                ? t("vocab.editor.delete.submit")
                : t("vocab.editor.retire.submit")}
            </Button>
          </div>
        }
      >
        <p className="text-sm">
          {confirm === "delete" ? t("vocab.editor.delete.text") : t("vocab.editor.retire.text")}
        </p>
      </Dialog>
    </section>
  );
}

function saveStatusOf(save: UseMutationResult<void, Error, SavePlan>): SaveStatus {
  if (save.isPending) return "saving";
  if (save.isError) return "error";
  if (save.isSuccess) return "saved";
  return "idle";
}
