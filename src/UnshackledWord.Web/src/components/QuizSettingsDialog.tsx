import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { ApiError } from "@/lib/api";
import {
  getVocabSettings,
  MULTIPLE_CHOICE_TYPES,
  putVocabSettings,
  type VocabSettings,
} from "@/lib/vocab/settingsApi";
import { Button } from "./Button";
import { Dialog } from "./Dialog";
import { FormError } from "./FormError";
import { Switch } from "./Switch";

/** Quiz types in dialog order; a type the API lists but this list lacks is not offered. */
const QUIZ_TYPE_ORDER = [
  "multiple_choice",
  "reverse_multiple_choice",
  "listening",
  "verse_context",
  "typing",
] as const;

type MultipleChoiceType = (typeof MULTIPLE_CHOICE_TYPES)[number];

function isMultipleChoice(type: string): type is MultipleChoiceType {
  return (MULTIPLE_CHOICE_TYPES as readonly string[]).includes(type);
}

export interface QuizSettingsDialogProps {
  open: boolean;
  onClose: () => void;
}

/**
 * Quiz settings (roadmap 0022.9f): a switch per quiz type the API offers. At least one multiple-choice
 * type stays on, so the last one that is on cannot be switched off; the API answers 400 otherwise.
 */
export function QuizSettingsDialog({ open, onClose }: QuizSettingsDialogProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const settings = useQuery({
    queryKey: ["vocab", "settings"],
    queryFn: ({ signal }) => getVocabSettings(signal),
    enabled: open,
    retry: false,
  });
  const save = useMutation({
    mutationFn: (disabledQuizTypes: string[]) => putVocabSettings(disabledQuizTypes),
    onSuccess: (saved: VocabSettings) => {
      queryClient.setQueryData(["vocab", "settings"], saved);
    },
  });

  const available = settings.data?.availableQuizTypes ?? [];
  const disabled = settings.data?.disabledQuizTypes ?? [];
  const types = QUIZ_TYPE_ORDER.filter((type) => available.includes(type));

  /** True for the one multiple-choice type that must stay on: the other one is already off. */
  function isLocked(type: string): boolean {
    if (!isMultipleChoice(type) || disabled.includes(type)) return false;
    const other = MULTIPLE_CHOICE_TYPES.find((candidate) => candidate !== type);
    return other !== undefined && disabled.includes(other);
  }

  function setEnabled(type: string, enabled: boolean) {
    if (enabled) {
      save.mutate(disabled.filter((item) => item !== type));
      return;
    }
    if (isLocked(type) || disabled.includes(type)) return;
    save.mutate([...disabled, type]);
  }

  const saveMessage =
    save.error instanceof ApiError && save.error.status === 400
      ? t("learn.settings.atLeastOne")
      : t("learn.settings.saveFailed");

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t("learn.settings.title")}
      dismissible={!save.isPending}
      footer={
        <Button variant="primary" size="sm" onClick={onClose}>
          {t("learn.settings.done")}
        </Button>
      }
    >
      <div className="grid gap-4">
        {settings.isError ? <FormError message={t("learn.settings.loadFailed")} /> : null}
        {settings.isPending && open ? (
          <p className="text-sm text-fg-muted">{t("learn.settings.loading")}</p>
        ) : null}
        <ul className="grid gap-3 text-sm">
          {types.map((type) => {
            const label = t(`learn.quizTypes.${type}`);
            const locked = isLocked(type);
            return (
              <li key={type} className="flex items-center justify-between gap-3">
                <span>{label}</span>
                <span className="flex items-center gap-3">
                  {locked ? (
                    <span className="text-fg-muted">{t("learn.settings.alwaysOn")}</span>
                  ) : null}
                  <Switch
                    aria-label={label}
                    checked={!disabled.includes(type)}
                    disabled={locked || save.isPending}
                    onCheckedChange={(enabled) => setEnabled(type, enabled)}
                  />
                </span>
              </li>
            );
          })}
        </ul>
        <p className="text-sm text-fg-muted">{t("learn.settings.atLeastOneHint")}</p>
        {save.isError ? <FormError message={saveMessage} /> : null}
      </div>
    </Dialog>
  );
}
