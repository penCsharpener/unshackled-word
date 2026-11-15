import { useState } from "react";
import { useTranslation } from "react-i18next";
import { formatStamp } from "@/lib/format";
import { wordDiff } from "@/lib/revision/wordDiff";
import { Button } from "./Button";
import { Dialog } from "./Dialog";

export interface RevisionVersion {
  version: number;
  text: string;
  createdAt: string;
  createdBy?: string;
  note?: string;
  endedAt?: string;
}

export interface VersionHistoryProps {
  /** Newest first. */
  versions: RevisionVersion[];
  /** Prefills the editor with the text of a version. */
  onRestore: (text: string) => void;
  /** Called after the confirm dialog. */
  onRemove: () => void;
  canRemove: boolean;
}

export function VersionHistory({ versions, onRestore, onRemove, canRemove }: VersionHistoryProps) {
  const { t, i18n } = useTranslation();
  const [confirming, setConfirming] = useState(false);
  return (
    <details className="rounded-md border border-border bg-surface">
      <summary className="cursor-pointer px-3 py-2 text-sm font-medium text-fg">
        {t("versionHistory.title", { count: versions.length })}
      </summary>
      <div className="flex flex-col gap-3 border-t border-border p-3">
        {versions.length === 0 ? (
          <p className="text-sm text-fg-muted">{t("versionHistory.empty")}</p>
        ) : (
          <ol className="flex flex-col gap-3">
            {versions.map((v, i) => {
              const previous = versions[i + 1];
              const parts = previous ? wordDiff(previous.text, v.text) : null;
              return (
                <li key={v.version} className="flex flex-col gap-1 text-sm">
                  <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-fg-muted">
                    <span className="font-medium text-fg">
                      {t("versionHistory.version", { version: v.version })}
                    </span>
                    <span>{formatStamp(v.createdAt, i18n.language)}</span>
                    {v.createdBy ? <span>{v.createdBy}</span> : null}
                    <Button
                      size="sm"
                      variant="ghost"
                      className="ml-auto"
                      onClick={() => onRestore(v.text)}
                    >
                      {t("versionHistory.restore")}
                    </Button>
                  </div>
                  <p className="text-fg">
                    {parts
                      ? parts.map((p, k) => {
                          const key = `${k}-${p.kind}`;
                          if (p.kind === "added")
                            return (
                              <ins key={key} className="bg-mark-same no-underline">
                                {p.text}{" "}
                              </ins>
                            );
                          if (p.kind === "removed")
                            return (
                              <del key={key} className="text-fg-muted line-through">
                                {p.text}{" "}
                              </del>
                            );
                          return <span key={key}>{p.text} </span>;
                        })
                      : v.text}
                  </p>
                  {v.note ? <p className="text-fg-muted italic">{v.note}</p> : null}
                </li>
              );
            })}
          </ol>
        )}
        {canRemove ? (
          <div>
            <Button size="sm" onClick={() => setConfirming(true)}>
              {t("versionHistory.remove")}
            </Button>
          </div>
        ) : null}
      </div>
      <Dialog
        open={confirming}
        onClose={() => setConfirming(false)}
        title={t("versionHistory.confirmTitle")}
        description={t("versionHistory.confirmBody")}
        footer={
          <>
            <Button variant="ghost" onClick={() => setConfirming(false)}>
              {t("versionHistory.cancel")}
            </Button>
            <Button
              variant="primary"
              onClick={() => {
                setConfirming(false);
                onRemove();
              }}
            >
              {t("versionHistory.confirm")}
            </Button>
          </>
        }
      >
        <span />
      </Dialog>
    </details>
  );
}
