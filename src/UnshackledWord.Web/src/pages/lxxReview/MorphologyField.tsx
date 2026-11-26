import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { FormError } from "@/components/FormError";
import { TextField } from "@/components/TextField";
import { useLxxMorphologyCheck } from "@/lib/maintainer/lxxReview";
import { useDebouncedValue } from "@/lib/useDebouncedValue";

export interface MorphologyFieldProps {
  value: string;
  onChange: (value: string) => void;
  /** Reports the live check's answer for the (debounced, trimmed) code; `undefined` while it loads. */
  onValidity?: (code: string, valid: boolean | undefined) => void;
  inputRef?: React.Ref<HTMLInputElement>;
}

const MORPHOLOGY_DEBOUNCE_MS = 300;

/**
 * Robinson code with live check through the parser (06a "Parse"); upper-cased on blur only (crasis codes keep spaces
 * and `+`, typing is never rewritten).
 */
export function MorphologyField({ value, onChange, onValidity, inputRef }: MorphologyFieldProps) {
  const { t, i18n } = useTranslation();
  const debounced = useDebouncedValue(value.trim(), MORPHOLOGY_DEBOUNCE_MS);
  const check = useLxxMorphologyCheck(debounced);
  const result = check.data;
  const description = i18n.language.startsWith("en") ? result?.english : result?.german;
  const valid = debounced === "" ? undefined : result?.valid;

  useEffect(() => {
    if (debounced !== "") onValidity?.(debounced, valid);
  }, [debounced, valid, onValidity]);

  return (
    <div className="flex flex-col gap-1">
      <TextField
        size="sm"
        label={t("lxxReview.edit.code")}
        value={value}
        onChange={onChange}
        onBlur={() => {
          const upper = value.trim().toUpperCase();
          if (upper !== value) onChange(upper);
        }}
        invalid={valid === false}
        aria-invalid={valid === false}
        inputRef={inputRef}
        required={false}
      />
      {valid ? <p className="text-sm text-fg-muted">{description}</p> : null}
      <FormError
        message={
          result && !result.valid && debounced !== ""
            ? `${t("lxxReview.error.morphology")} ${result.error ?? ""}`.trim()
            : ""
        }
      />
    </div>
  );
}
