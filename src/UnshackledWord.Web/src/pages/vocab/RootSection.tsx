import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { StatusBadge } from "@/components/StatusBadge";
import { Switch } from "@/components/Switch";
import { TextField } from "@/components/TextField";
import { describeApiError } from "@/lib/api";
import type { VocabTrack } from "@/lib/vocab/api";
import {
  getVocabPrepositions,
  type SetVocabEntryRootBody,
  setVocabEntryRoot,
  setVocabRootParent,
  type VocabEditorRoot,
  type VocabRootCandidate,
  vocabEditorKeys,
} from "@/lib/vocab/editorApi";
import { RootSearch, rootLabel } from "./RootSearch";

/** A root the maintainer picked: an existing row (`rootId`) or a Greek word that gets its root row on save (`strongsKey`). */
interface PickedRoot {
  rootId?: number;
  strongsKey?: string;
  root: string;
  homograph: number;
}

function pickOf(candidate: VocabRootCandidate): PickedRoot {
  return {
    rootId: candidate.rootId ?? undefined,
    strongsKey: candidate.strongsKey ?? undefined,
    root: candidate.root,
    homograph: candidate.homograph,
  };
}

export interface RootSectionProps {
  entryId: number;
  track: VocabTrack;
  /** The root of the entry's main key; absent while none is known. */
  root: VocabEditorRoot | null | undefined;
  /** Unsaved changes elsewhere on the page: the reload after a root save would drop them. */
  blocked: boolean;
}

/**
 * "Wurzel" section of the vocab editor (roadmap 0030.5f): ordered prefixes from the closed preposition
 * list, the root (search, or Hebrew letters with an optional verb key), "Einfaches Wort", and the
 * parent root with its path. A save sets the row to `manual` (the KI badge goes away) and does not bump the
 * content version, so learners hear nothing.
 */
export function RootSection({ entryId, track, root, blocked }: RootSectionProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const hebrew = track === "H";
  const lang = hebrew ? "he" : "grc";

  const [simple, setSimple] = useState(root?.isSimple ?? false);
  const [prefixKeys, setPrefixKeys] = useState<string[]>(root?.prefixKeys ?? []);
  const [picked, setPicked] = useState<PickedRoot | null>(
    root && !root.isSimple
      ? {
          rootId: root.rootId,
          strongsKey: root.strongsKey ?? undefined,
          root: root.root,
          homograph: root.homograph,
        }
      : null,
  );
  const [text, setText] = useState("");
  const [verbKey, setVerbKey] = useState("");
  const [homograph, setHomograph] = useState("");

  const prepositions = useQuery({
    queryKey: vocabEditorKeys.prepositions,
    queryFn: ({ signal }) => getVocabPrepositions(signal),
    enabled: !hebrew,
    staleTime: Number.POSITIVE_INFINITY,
  });
  const lemmaOf = (key: string) =>
    prepositions.data?.items.find((item) => item.key === key)?.lemma ?? key;

  const save = useMutation({
    mutationFn: (body: SetVocabEntryRootBody) => setVocabEntryRoot(entryId, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: vocabEditorKeys.all }),
  });

  const creating = hebrew && !simple && picked === null;
  const body = (): SetVocabEntryRootBody | null => {
    if (simple) return { simple: true };
    if (hebrew) {
      if (picked?.rootId !== undefined) return { rootId: picked.rootId };
      const letters = text.trim();
      if (!letters) return null;
      const number = Number(homograph);
      return {
        rootLetters: letters,
        rootKey: verbKey.trim() || undefined,
        homograph: Number.isInteger(number) && number > 0 ? number : undefined,
      };
    }
    if (picked === null) return null;
    return picked.rootId !== undefined
      ? { prefixKeys, rootId: picked.rootId }
      : { prefixKeys, rootKey: picked.strongsKey };
  };
  const request = body();

  const addPrefix = (key: string) => {
    if (key) setPrefixKeys((keys) => [...keys, key]);
  };
  const movePrefixUp = (index: number) =>
    setPrefixKeys((keys) => {
      const previous = keys[index - 1];
      const current = keys[index];
      if (previous === undefined || current === undefined) return keys;
      return keys.map((key, i) => (i === index - 1 ? current : i === index ? previous : key));
    });

  return (
    <section className="grid gap-3 rounded-lg border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-semibold">{t("vocab.editor.root.title")}</h2>
        {root ? (
          <StatusBadge tone={root.origin === "agent" ? "info" : "neutral"}>
            {root.origin === "agent"
              ? t("vocab.root.agent")
              : t(`vocab.editor.root.origin.${root.origin}`)}
          </StatusBadge>
        ) : (
          <span className="text-xs text-fg-muted">{t("vocab.editor.root.none")}</span>
        )}
      </div>

      <Switch checked={simple} onCheckedChange={setSimple}>
        {t("vocab.editor.root.simple")}
      </Switch>

      {simple ? (
        <p className="text-sm text-fg-muted">{t("vocab.editor.root.simpleHint")}</p>
      ) : (
        <>
          {hebrew ? null : (
            <div className="grid gap-2">
              <h3 className="text-sm font-medium">{t("vocab.editor.root.prefixes")}</h3>
              {prefixKeys.length > 0 ? (
                <ol className="grid gap-1" aria-label={t("vocab.editor.root.prefixes")}>
                  {prefixKeys.map((key, index) => (
                    // the same preposition may appear twice (ἀπό + ἀπό), so the position is part of the key
                    // biome-ignore lint/suspicious/noArrayIndexKey: ordered list of keys, edited in place
                    <li key={`${key}-${index}`} className="flex items-center gap-2 text-sm">
                      <span lang="grc" className="font-original text-base">
                        {lemmaOf(key)}
                      </span>
                      <span className="text-fg-muted">{key}</span>
                      <span className="ml-auto flex gap-1">
                        {index > 0 ? (
                          <Button
                            variant="ghost"
                            size="sm"
                            aria-label={t("vocab.editor.root.prefixUp", { prefix: lemmaOf(key) })}
                            onClick={() => movePrefixUp(index)}
                          >
                            ↑
                          </Button>
                        ) : null}
                        <Button
                          variant="ghost"
                          size="sm"
                          aria-label={t("vocab.editor.root.prefixRemove", { prefix: lemmaOf(key) })}
                          onClick={() =>
                            setPrefixKeys((keys) => keys.filter((_, i) => i !== index))
                          }
                        >
                          ×
                        </Button>
                      </span>
                    </li>
                  ))}
                </ol>
              ) : (
                <p className="text-sm text-fg-muted">{t("vocab.editor.root.noPrefixes")}</p>
              )}
              <Select
                label={t("vocab.editor.root.prefixAdd")}
                value=""
                onChange={addPrefix}
                options={[
                  { value: "", label: "…" },
                  ...(prepositions.data?.items ?? []).map((item) => ({
                    value: item.key,
                    label: `${item.lemma} (${item.key})`,
                  })),
                ]}
              />
            </div>
          )}

          <div className="grid gap-2">
            {picked ? (
              <div className="flex flex-wrap items-center gap-2 text-sm">
                <span>{t("vocab.editor.root.picked")}</span>
                <span lang={lang} className="font-original text-base">
                  {rootLabel(picked.root, picked.homograph)}
                </span>
                {picked.strongsKey ? (
                  <span className="text-fg-muted">{picked.strongsKey}</span>
                ) : null}
                <Button variant="ghost" size="sm" onClick={() => setPicked(null)}>
                  {t("vocab.editor.root.unpick")}
                </Button>
              </div>
            ) : null}
            <RootSearch
              track={track}
              label={hebrew ? t("vocab.editor.root.letters") : t("vocab.editor.root.search")}
              placeholder={hebrew ? undefined : t("vocab.editor.root.searchPlaceholder")}
              text={text}
              onTextChange={(value) => {
                setText(value);
                if (hebrew) setPicked(null);
              }}
              isPicked={(item) =>
                picked !== null &&
                (picked.rootId !== undefined
                  ? picked.rootId === item.rootId
                  : picked.strongsKey === item.strongsKey)
              }
              onPick={(item) => setPicked(pickOf(item))}
            />
            {creating && text.trim() ? (
              <div className="grid gap-2 sm:grid-cols-2">
                <TextField
                  required={false}
                  size="sm"
                  label={t("vocab.editor.root.verbKey")}
                  placeholder="H3789"
                  value={verbKey}
                  onChange={setVerbKey}
                />
                <TextField
                  required={false}
                  size="sm"
                  type="number"
                  min={1}
                  label={t("vocab.editor.root.homograph")}
                  value={homograph}
                  onChange={setHomograph}
                />
              </div>
            ) : null}
          </div>
        </>
      )}

      <FormError
        message={save.isError ? describeApiError(save.error, t("vocab.editor.root.failed")) : ""}
      />
      <div className="flex flex-wrap items-center gap-3">
        <Button
          variant="primary"
          size="sm"
          disabled={request === null || save.isPending || blocked}
          onClick={() => request && save.mutate(request)}
        >
          {creating ? t("vocab.editor.root.create") : t("vocab.editor.root.save")}
        </Button>
        {blocked ? (
          <span className="text-xs text-fg-muted">{t("vocab.editor.root.saveFirst")}</span>
        ) : null}
      </div>

      <ParentRoot track={track} root={root} />
    </section>
  );
}

interface ParentRootProps {
  track: VocabTrack;
  root: VocabEditorRoot | null | undefined;
}

/** "Übergeordnete Wurzel": the path of the current root and a picker for its parent. */
function ParentRoot({ track, root }: ParentRootProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const lang = track === "H" ? "he" : "grc";
  const [text, setText] = useState("");
  const [picked, setPicked] = useState<VocabRootCandidate | null>(null);

  const mutation = useMutation({
    mutationFn: (parent: { parentRootId?: number; parentStrongsKey?: string }) =>
      setVocabRootParent(root?.rootId ?? 0, parent),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: vocabEditorKeys.all }),
  });

  const path = root ? [root.root, ...root.ancestors.map((ancestor) => ancestor.root)] : [];

  return (
    <div className="grid gap-2 border-t border-border pt-3">
      <h3 className="text-sm font-medium">{t("vocab.editor.root.parent.title")}</h3>
      {root === null || root === undefined ? (
        <p className="text-sm text-fg-muted">{t("vocab.editor.root.parent.needRoot")}</p>
      ) : (
        <>
          {root.ancestors.length > 0 ? (
            <p lang={lang} className="font-original text-lg">
              {path.join(" → ")}
            </p>
          ) : (
            <p className="text-sm text-fg-muted">{t("vocab.editor.root.parent.none")}</p>
          )}
          <RootSearch
            track={track}
            label={t("vocab.editor.root.parent.search")}
            placeholder={t("vocab.editor.root.searchPlaceholder")}
            text={text}
            onTextChange={setText}
            isPicked={(item) =>
              picked !== null &&
              picked.rootId === item.rootId &&
              picked.strongsKey === item.strongsKey
            }
            onPick={setPicked}
          />
          <FormError
            message={
              mutation.isError
                ? describeApiError(mutation.error, t("vocab.editor.root.parent.failed"))
                : ""
            }
          />
          <div className="flex flex-wrap items-center gap-2">
            <Button
              variant="secondary"
              size="sm"
              disabled={picked === null || mutation.isPending}
              onClick={() =>
                picked &&
                mutation.mutate(
                  picked.rootId != null
                    ? { parentRootId: picked.rootId }
                    : { parentStrongsKey: picked.strongsKey ?? undefined },
                )
              }
            >
              {t("vocab.editor.root.parent.set")}
            </Button>
            {root.ancestors.length > 0 ? (
              <Button
                variant="ghost"
                size="sm"
                disabled={mutation.isPending}
                onClick={() => mutation.mutate({})}
              >
                {t("vocab.editor.root.parent.clear")}
              </Button>
            ) : null}
          </div>
        </>
      )}
    </div>
  );
}
