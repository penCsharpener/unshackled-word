import { api } from "@/lib/api";

/** Mirrors `GET`/`PUT api/vocab/settings`: the quiz types the learner turned off and the ones the API offers. */
export interface VocabSettings {
  disabledQuizTypes: string[];
  availableQuizTypes: string[];
}

/** Both multiple-choice types; the API refuses to disable both (400), so the dialog keeps one on. */
export const MULTIPLE_CHOICE_TYPES = ["multiple_choice", "reverse_multiple_choice"] as const;

/** The learner's quiz settings (roadmap 0022.9f). */
export function getVocabSettings(signal?: AbortSignal) {
  return api.get<VocabSettings>("/vocab/settings", signal);
}

/** Replaces the disabled quiz types; returns the settings as saved. */
export function putVocabSettings(disabledQuizTypes: string[]) {
  return api.put<VocabSettings>("/vocab/settings", { disabledQuizTypes });
}
