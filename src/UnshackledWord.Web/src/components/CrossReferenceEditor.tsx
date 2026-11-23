import { useQuery } from "@tanstack/react-query";
import { type ReactNode, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { ApiError, describeApiError } from "@/lib/api";
import { passageTextQuery } from "@/lib/bible/queries";
import { passageSpansParam, useReferenceParse } from "@/lib/bible/referenceParse";
import {
  addedKey,
  addRequest,
  curationRequest,
  TSK_COMMENT_MAX,
  useAddTskCuration,
  useDeleteAddedTskCuration,
  useSaveTskCuration,
} from "@/lib/bible/tskCuration";
import type {
  CrossReference,
  ParsedReference,
  ParseReferenceResponse,
  TskRating,
} from "@/lib/bible/types";
import { useDebouncedValue } from "@/lib/useDebouncedValue";
import { Button } from "./Button";
import { FormError } from "./FormError";
import { TextField } from "./TextField";

type Source = { bookId: number; chapter: number; verse: number };

interface CommonProps {
  /** The verse the link starts at (Elberfelder numbering). */
  source: Source;
  /** Viewport position of the card's corner (measured by the caller); with `above` it is the bottom edge. */
  position: { x: number; y: number; above?: boolean };
  /** Called on cancel, Escape, a click outside and after a successful save or removal. */
  onClose: () => void;
}

export type CrossReferenceEditorProps = CommonProps &
  (
    | {
        /** `edit` (default): rating and comment of one link; an added link can also be removed. */
        mode?: "edit";
        /** The link being curated; its `rating` and `comment` are the starting state. */
        reference: CrossReference;
        /** Readable target, e.g. "Psalm 33,6" (heading and dialog name). */
        label: string;
      }
    | {
        /** `add` (roadmap 0024.4f): a new link from `source` to a typed German reference. */
        mode: "add";
        /** Reader level of the target text preview (default: Elb1871). */
        level?: number;
        reference?: never;
        label?: never;
      }
  );

/** Card width in px (`w-80`), for callers that clamp the position. */
export const CROSS_REFERENCE_EDITOR_WIDTH = 320;

type Choice = TskRating | "neutral";
const CHOICES: readonly Choice[] = ["highlight", "neutral", "hide"];
const PREVIEW_VERSES = 5;

/** The popover shell: top layer, measured position, Escape and a click outside close it. */
function EditorCard({
  position,
  onClose,
  label,
  children,
}: Pick<CommonProps, "position" | "onClose"> & { label: string; children: ReactNode }) {
  const root = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const onPointer = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) onClose();
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("pointerdown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [onClose]);

  return (
    <div
      ref={(element) => {
        root.current = element;
        // measured pixel position: the only value that cannot be a design token
        element?.style.setProperty("left", `${position.x}px`);
        element?.style.setProperty("top", `${position.y}px`);
        // top layer: escapes the clipped side panel and stays above the hover card
        try {
          if (element && !element.matches(":popover-open")) element.showPopover();
        } catch {
          // no popover support (jsdom, old browsers): the plain fixed card is shown
        }
      }}
      role="dialog"
      aria-label={label}
      data-testid="cross-reference-editor"
      popover="manual"
      className={`fixed inset-auto z-50 m-0 h-fit w-80 ${position.above ? "-translate-y-full" : ""} flex flex-col gap-3 rounded-lg border border-border bg-surface p-3 font-sans text-sm text-fg shadow-lg`}
    >
      {children}
    </div>
  );
}

function CommentField({
  value,
  onChange,
  onSubmit,
}: {
  value: string;
  onChange: (value: string) => void;
  /** Ctrl/Cmd+Enter; the caller decides whether saving is possible. */
  onSubmit: () => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="flex flex-col gap-1">
      <TextField
        multiline
        rows={3}
        size="sm"
        required={false}
        label={t("bible.tskCuration.comment")}
        value={value}
        maxLength={TSK_COMMENT_MAX}
        onChange={onChange}
        onKeyDown={(event) => {
          if ((event.ctrlKey || event.metaKey) && event.key === "Enter") onSubmit();
        }}
      />
      <p className="text-end text-xs text-fg-muted">
        {t("bible.tskCuration.counter", { length: value.length, max: TSK_COMMENT_MAX })}
      </p>
    </div>
  );
}

/**
 * Popover editor of one TSK link (roadmap 0024.3f): Hervorheben / Neutral / Ausblenden and a comment (max 500).
 * `mode="add"` (0024.4f) adds a link to a typed reference; an added link can be removed in edit mode.
 * Maintainers save directly; suggestions by other users follow in 0024.6f.
 */
export function CrossReferenceEditor(props: CrossReferenceEditorProps) {
  if (props.mode === "add") {
    return <AddForm {...props} />;
  }
  return <EditForm {...props} />;
}

function EditForm({
  source,
  reference,
  label,
  position,
  onClose,
}: CommonProps & { reference: CrossReference; label: string }) {
  const { t } = useTranslation();
  const save = useSaveTskCuration();
  const remove = useDeleteAddedTskCuration();
  const initialChoice: Choice = reference.rating ?? "neutral";
  const [choice, setChoice] = useState<Choice>(initialChoice);
  const [comment, setComment] = useState(reference.comment ?? "");
  const [confirming, setConfirming] = useState(false);
  const body = useRef<HTMLDivElement>(null);
  const changed = choice !== initialChoice || comment.trim() !== (reference.comment ?? "");
  const added = reference.origin === "added";

  useEffect(() => {
    body.current?.querySelector<HTMLElement>("[aria-pressed='true']")?.focus();
  }, []);

  const submit = () => {
    if (!changed || save.isPending) return;
    save.mutate(curationRequest(source, reference, choice === "neutral" ? null : choice, comment), {
      onSuccess: onClose,
    });
  };
  const confirmRemove = () => remove.mutate(addedKey(source, reference), { onSuccess: onClose });

  const removeError = remove.isError
    ? remove.error instanceof ApiError && remove.error.status === 404
      ? t("bible.tskCuration.add.removeNotFound")
      : describeApiError(remove.error, t("bible.tskCuration.add.removeError"))
    : "";

  return (
    <EditorCard
      position={position}
      onClose={onClose}
      label={t("bible.tskCuration.editorLabel", { reference: label })}
    >
      <div ref={body} className="contents">
        <p className="font-medium">
          {label}
          {added && (
            <span className="ms-2 text-xs font-normal text-fg-muted">
              {t("bible.tskCuration.added")}
            </span>
          )}
        </p>
        <fieldset className="flex gap-1">
          <legend className="sr-only">{t("bible.tskCuration.rating")}</legend>
          {CHOICES.map((value) => (
            <Button
              key={value}
              variant="secondary"
              size="sm"
              aria-pressed={choice === value}
              onClick={() => setChoice(value)}
            >
              {t(`bible.tskCuration.choice.${value}`)}
            </Button>
          ))}
        </fieldset>
        <CommentField value={comment} onChange={setComment} onSubmit={submit} />
        <FormError
          message={save.isError ? describeApiError(save.error, t("bible.tskCuration.error")) : ""}
        />
        {added && confirming ? (
          <div className="flex flex-col gap-2 rounded-md border border-border p-2">
            <p>{t("bible.tskCuration.add.removeConfirm")}</p>
            <FormError message={removeError} />
            <div className="flex justify-end gap-2">
              <Button variant="ghost" size="sm" onClick={() => setConfirming(false)}>
                {t("bible.tskCuration.cancel")}
              </Button>
              <Button
                variant="primary"
                size="sm"
                disabled={remove.isPending}
                onClick={confirmRemove}
              >
                {remove.isPending
                  ? t("bible.tskCuration.add.removing")
                  : t("bible.tskCuration.add.removeYes")}
              </Button>
            </div>
          </div>
        ) : (
          <div className="flex flex-wrap justify-end gap-2">
            {added && (
              <Button
                variant="ghost"
                size="sm"
                className="me-auto text-fg-muted"
                onClick={() => setConfirming(true)}
              >
                {t("bible.tskCuration.add.remove")}
              </Button>
            )}
            <Button variant="ghost" size="sm" onClick={onClose}>
              {t("bible.tskCuration.cancel")}
            </Button>
            <Button
              variant="primary"
              size="sm"
              disabled={!changed || save.isPending}
              onClick={submit}
            >
              {save.isPending ? t("bible.tskCuration.saving") : t("bible.tskCuration.save")}
            </Button>
          </div>
        )}
      </div>
    </EditorCard>
  );
}

type AddCheck =
  | { kind: "empty" }
  | { kind: "pending" }
  | { kind: "error"; message: string }
  | { kind: "ok"; reference: ParsedReference; spans: string };

/** The parsed target as one link: exactly one reference with verses inside one chapter. */
function useAddCheck(input: string, debounced: string): AddCheck {
  const { t } = useTranslation();
  const parse = useReferenceParse(debounced);
  if (input.trim() === "") return { kind: "empty" };
  if (debounced.trim() !== input.trim() || !parse.data) {
    if (parse.isError && debounced.trim() === input.trim()) {
      return { kind: "error", message: t("bible.tskCuration.add.parseFailed") };
    }
    return { kind: "pending" };
  }
  return checkParse(parse.data, t);
}

function checkParse(
  data: ParseReferenceResponse,
  t: ReturnType<typeof useTranslation>["t"],
): AddCheck {
  if (data.error) {
    const { code, args } = data.error;
    const message =
      code === "unknownBook" || code === "empty"
        ? t(`bible.tskCuration.add.errors.${code}`)
        : t(`search.reference.errors.${code}`, {
            ...args,
            defaultValue: t("search.reference.errors.generic"),
          });
    return { kind: "error", message };
  }
  if (data.references.length !== 1) {
    return { kind: "error", message: t("bible.tskCuration.add.errors.several") };
  }
  const [reference] = data.references as [ParsedReference];
  const spans = reference.spans ?? [];
  if (reference.bookOnly || spans.length === 0 || spans.some((s) => s.startVerse === 0)) {
    return { kind: "error", message: t("bible.tskCuration.add.errors.verses") };
  }
  if (spans.length > 1 || spans.some((s) => s.startChapter !== s.endChapter)) {
    return { kind: "error", message: t("bible.tskCuration.add.errors.oneRange") };
  }
  const param = passageSpansParam(reference);
  if (!param) return { kind: "error", message: t("bible.tskCuration.add.errors.verses") };
  return { kind: "ok", reference, spans: param };
}

/** Text of the parsed target, up to five verses (Elb1871 or the reader level). */
function TargetPreview({ spans, level }: { spans: string; level?: number }) {
  const { t } = useTranslation();
  const query = useQuery(passageTextQuery(spans, level));
  if (query.isError) {
    return <p className="text-fg-muted">{t("bible.sidePanel.previewError")}</p>;
  }
  if (!query.data) {
    return <p className="text-fg-muted">{t("bible.sidePanel.previewLoading")}</p>;
  }
  const verses = query.data.passages.flatMap((p) => p.verses);
  return (
    <div className="grid gap-1 font-serif" data-testid="cross-reference-add-preview">
      {verses.slice(0, PREVIEW_VERSES).map((v) => (
        <p key={v.verse}>
          <sup className="me-1 font-sans text-xs text-fg-muted">{v.verse}</sup>
          {v.text}
        </p>
      ))}
      {verses.length > PREVIEW_VERSES ? (
        <p className="font-sans text-fg-muted">
          {t("search.reference.moreVerses", { count: verses.length - PREVIEW_VERSES })}
        </p>
      ) : null}
    </div>
  );
}

function AddForm({ source, level, position, onClose }: CommonProps & { level?: number }) {
  const { t } = useTranslation();
  const add = useAddTskCuration();
  const [input, setInput] = useState("");
  const [comment, setComment] = useState("");
  const debounced = useDebouncedValue(input, 300);
  const check = useAddCheck(input, debounced);
  const canSave = check.kind === "ok" && !add.isPending;
  const field = useRef<HTMLInputElement>(null);

  useEffect(() => {
    field.current?.focus();
  }, []);

  const submit = () => {
    if (!canSave) return;
    add.mutate(addRequest(source, input, comment), { onSuccess: onClose });
  };

  const saveError = add.isError
    ? add.error instanceof ApiError && add.error.status === 409
      ? t("bible.tskCuration.add.conflict")
      : add.error instanceof ApiError && add.error.status === 404
        ? t("bible.tskCuration.add.notFound")
        : describeApiError(add.error, t("bible.tskCuration.add.error"))
    : "";

  return (
    <EditorCard position={position} onClose={onClose} label={t("bible.tskCuration.add.title")}>
      <p className="font-medium">{t("bible.tskCuration.add.title")}</p>
      <div className="flex flex-col gap-1">
        <TextField
          size="sm"
          label={t("bible.tskCuration.add.reference")}
          placeholder={t("bible.tskCuration.add.placeholder")}
          autoComplete="off"
          value={input}
          inputRef={field}
          invalid={check.kind === "error"}
          onChange={(value) => {
            setInput(value);
            if (add.isError) add.reset();
          }}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              submit();
            }
          }}
        />
        {check.kind === "pending" && (
          <p className="text-xs text-fg-muted">{t("bible.tskCuration.add.checking")}</p>
        )}
        {check.kind === "error" && <FormError message={check.message} />}
      </div>
      {check.kind === "ok" && (
        <div className="flex flex-col gap-1 rounded-md bg-surface-muted p-2">
          <p className="font-medium">{check.reference.display}</p>
          <TargetPreview spans={check.spans} level={level} />
        </div>
      )}
      <CommentField value={comment} onChange={setComment} onSubmit={submit} />
      <FormError message={saveError} />
      <div className="flex justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={onClose}>
          {t("bible.tskCuration.cancel")}
        </Button>
        <Button variant="primary" size="sm" disabled={!canSave} onClick={submit}>
          {add.isPending ? t("bible.tskCuration.saving") : t("bible.tskCuration.save")}
        </Button>
      </div>
    </EditorCard>
  );
}
