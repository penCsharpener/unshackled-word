import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { GrammarVocabulary } from "@/lib/search/grammarQuery";
import {
  foldGrammar,
  type GrammarFacetsResponse,
  type GrammarSlot,
} from "@/lib/search/grammarSearch";

/** The `grammar` filter of the Strong's page: whitespace-separated terms `Key:value[,value]`, optionally `-Key:value`. */
export interface GrammarTerm {
  /** The term as written, e.g. `-Kasus:nom,gen`. */
  raw: string;
  negated: boolean;
  key: string;
  values: string[];
}

const MAX_GRAMMAR_LENGTH = 200;

/** `?grammar=` as a trimmed, single-spaced term list of at most 200 characters; anything else is no filter. */
export function parseGrammarFilter(value: string | null): string | undefined {
  const text = value?.trim().replace(/\s+/g, " ");
  return text && text.length <= MAX_GRAMMAR_LENGTH ? text : undefined;
}

/** The terms of a grammar filter; a token without `key:value` is ignored. */
export function grammarTerms(filter: string | undefined): GrammarTerm[] {
  if (!filter) return [];
  return filter.split(" ").flatMap((raw) => {
    const negated = raw.startsWith("-");
    const colon = raw.indexOf(":");
    const key = raw.slice(negated ? 1 : 0, colon);
    const values =
      colon < 0
        ? []
        : raw
            .slice(colon + 1)
            .split(",")
            .filter(Boolean);
    return key && values.length > 0 ? [{ raw, negated, key, values }] : [];
  });
}

const joinTerms = (terms: GrammarTerm[]): string | undefined =>
  terms.length === 0 ? undefined : terms.map((term) => term.raw).join(" ");

/**
 * The value as the server reads it in a term: the German label without spaces and brackets
 * (`Perfekt (Qatal)` → `PerfektQatal`, `1. Person` → `1.Person`). `Code` patterns keep their characters.
 */
export const grammarValueToken = (key: string, label: string): string =>
  key === "Code" ? label.replace(/\s+/g, "") : label.replace(/[\s()]+/g, "");

/**
 * Sets the value of `key` in the filter, or removes the key's term when `value` is null. One value
 * per key: another value of the same key replaces its term in place (the "Formen" block narrows,
 * never widens), a new key adds a term. Negated and OR terms stay untouched.
 */
export function setGrammarValue(
  filter: string | undefined,
  key: string,
  value: string | null,
): string | undefined {
  const terms = grammarTerms(filter);
  const sameKey = (term: GrammarTerm) =>
    !term.negated && foldGrammar(term.key) === foldGrammar(key);
  const existing = terms.find(sameKey);
  if (value === null) return joinTerms(terms.filter((term) => term !== existing));
  const next: GrammarTerm = { raw: `${key}:${value}`, negated: false, key, values: [value] };
  if (!existing) return joinTerms([...terms, next]);
  return joinTerms(terms.map((term) => (term === existing ? next : term)));
}

/** The filter without one term. */
export function removeGrammarTerm(
  filter: string | undefined,
  term: GrammarTerm,
): string | undefined {
  return joinTerms(grammarTerms(filter).filter((t) => t.raw !== term.raw));
}

/** The single value currently chosen for `key` as written in the filter (may be an alias), else undefined. */
export function activeGrammarValue(filter: string | undefined, key: string): string | undefined {
  const term = grammarTerms(filter).find(
    (t) => !t.negated && foldGrammar(t.key) === foldGrammar(key) && t.values.length === 1,
  );
  return term?.values[0];
}

/** The `grammar` filter of an overlay slot: its non-German filters (the German side has no original forms). */
export function slotGrammarFilter(slot: Pick<GrammarSlot, "filters">): string | undefined {
  const terms = slot.filters
    .filter((filter) => !filter.german && filter.values.length > 0)
    .map((filter) => {
      const values = filter.values.map((value) => grammarValueToken(filter.key, value)).join(",");
      return `${filter.negated ? "-" : ""}${filter.key}:${values}`;
    });
  return terms.length === 0 ? undefined : terms.join(" ");
}

/** The link target of a slot chip with a lemma, or null when the slot has no lemma. */
export function slotStrongsLink(slot: GrammarSlot): string | null {
  const lemma = slot.readAs === "lemma" ? slot.lemmas[0] : undefined;
  if (!lemma) return null;
  const grammar = slotGrammarFilter(slot);
  const base = `/bible/strongs/${encodeURIComponent(lemma.strongs)}`;
  return grammar ? `${base}?${new URLSearchParams({ grammar })}` : base;
}

/**
 * The label of one value of a term in the language of the UI, from the loaded vocabulary
 * (matched by label, canonical key or alias); the written value when it is unknown.
 */
export function grammarValueLabel(
  vocabulary: GrammarVocabulary | undefined,
  key: string,
  value: string,
  language: string,
): string {
  const folded = foldGrammar(value);
  const entry = vocabulary?.keys
    .filter(
      (k) =>
        foldGrammar(k.name) === foldGrammar(key) ||
        k.aliases.some((a) => foldGrammar(a) === foldGrammar(key)),
    )
    .flatMap((k) => k.values)
    .find((v) =>
      [v.labelDe, v.key, ...v.aliases].some(
        (candidate) =>
          foldGrammar(candidate) === folded ||
          foldGrammar(grammarValueToken(key, candidate)) === folded,
      ),
    );
  if (!entry) return value;
  return language.startsWith("en") ? entry.labelEn : entry.labelDe;
}

export const strongsFormsKeys = {
  forms: (strongs: string) => ["bible", "strongsForms", strongs] as const,
};

/** Counts of every grammar value that occurs for a (normalized) Strong's number: the facets of its slot 0. */
export function useStrongsForms(strongs: string | undefined) {
  return useQuery({
    queryKey: strongsFormsKeys.forms(strongs ?? ""),
    queryFn: ({ signal }) =>
      api.get<GrammarFacetsResponse>(
        `/elberfelder/search/grammar/facets?${new URLSearchParams({ q: strongs ?? "", slot: "0" })}`,
        signal,
      ),
    enabled: strongs !== undefined,
    staleTime: Number.POSITIVE_INFINITY,
  });
}
