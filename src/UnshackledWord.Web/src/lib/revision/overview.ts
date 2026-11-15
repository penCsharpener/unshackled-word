import { keepPreviousData, queryOptions, useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { Finding } from "@/lib/quality/types";

export type RevisionWorkListName = "unreviewed" | "incomplete" | "stale";

export const REVISION_WORK_LISTS: RevisionWorkListName[] = ["unreviewed", "incomplete", "stale"];

export interface RevisionBookCount {
  bibleBookId: number;
  revisedVerses: number;
}

export interface RevisionLevelOverview {
  level: number;
  code: string;
  name: string;
  revisedVerses: number;
  totalVerses: number;
  books: RevisionBookCount[];
}

export interface RevisionWorkItem {
  revisionVerseId: number;
  level: number;
  version: number;
  bibleBookId: number;
  chapter: number;
  verse: number;
  createdAt: string;
  unlinkedGermanWords: number;
  unlinkedOriginalWords: number;
}

export interface RevisionWorkList {
  name: RevisionWorkListName;
  total: number;
  page: number;
  pageSize: number;
  items: RevisionWorkItem[];
}

export interface RevisionOverview {
  levels: RevisionLevelOverview[];
  counts: Record<RevisionWorkListName, number>;
  list?: RevisionWorkList;
}

export interface RevisionOverviewParams {
  list?: RevisionWorkListName;
  level?: number;
  page: number;
  pageSize: number;
}

export const revisionOverviewQuery = (params: RevisionOverviewParams) =>
  queryOptions({
    queryKey: ["revision", "overview", params] as const,
    queryFn: ({ signal }) => {
      const query = new URLSearchParams({
        page: String(params.page),
        pageSize: String(params.pageSize),
      });
      if (params.list) query.set("list", params.list);
      if (params.level) query.set("level", String(params.level));
      return api.get<RevisionOverview>(`/revision/overview?${query}`, signal);
    },
    staleTime: 0,
    placeholderData: keepPreviousData,
  });

export function useRevisionOverview(params: RevisionOverviewParams, enabled: boolean) {
  return useQuery({ ...revisionOverviewQuery(params), enabled });
}

/** Reader link of a work item: chapter at the verse with the revision panel of its level (`?revise=`), see ChapterPage. */
export function revisionVerseHref(item: RevisionWorkItem): string {
  const { bibleBookId, chapter, verse, level } = item;
  return `/bible/${bibleBookId}/${chapter}?map=${verse}&revise=${level}#v${verse}`;
}

const SEVERITY: Record<RevisionWorkListName, Finding["severity"]> = {
  unreviewed: "info",
  incomplete: "warning",
  stale: "warning",
};

/** A work item as a data-quality finding, so `FindingsTable` can show it; `detail` is filled by the caller (translated). */
export function toFinding(
  list: RevisionWorkListName,
  item: RevisionWorkItem,
  detail: string,
): Finding {
  return {
    check: `revision-${list}`,
    severity: SEVERITY[list],
    bibleBookId: item.bibleBookId,
    chapter: item.chapter,
    verse: item.verse,
    detail,
  };
}
