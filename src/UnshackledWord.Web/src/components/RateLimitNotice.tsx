import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { type ApiError, isRateLimited, rateLimitMessage } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { Button, buttonClasses } from "./Button";

interface Limit {
  error: ApiError;
  /** Epoch ms at which the limiter admits the caller again. */
  until: number;
  /** True when a failed query (not only a mutation) can be refetched. */
  refetchable: boolean;
}

const isLimitedQuery = (state: { status: string; error: unknown }) =>
  state.status === "error" && isRateLimited(state.error);

/**
 * Global notice for anonymous visitors that the Web API rate limiter (HTTP 429) rejected a call: shows
 * the wait as a countdown, then offers "Reload", which refetches the failed queries. Signed-in users are
 * not limited, so they never see it.
 */
export function RateLimitNotice() {
  const { t } = useTranslation();
  const { status } = useAuth();
  const client = useQueryClient();
  const [limit, setLimit] = useState<Limit | null>(null);
  const [now, setNow] = useState(() => Date.now());

  // Mirror the caches: the newest 429 among failed queries and mutations drives the notice.
  useEffect(() => {
    const scan = () => {
      let found: Limit | null = null;
      for (const query of client.getQueryCache().getAll()) {
        const { state } = query;
        if (!isLimitedQuery(state)) continue;
        const error = state.error as ApiError;
        const until = state.errorUpdatedAt + (error.retryAfterSeconds ?? 0) * 1000;
        if (!found || until > found.until) found = { error, until, refetchable: true };
      }
      for (const mutation of client.getMutationCache().getAll()) {
        const { state } = mutation;
        if (!isLimitedQuery(state)) continue;
        const error = state.error as ApiError;
        const until = state.submittedAt + (error.retryAfterSeconds ?? 0) * 1000;
        if (!found || until > found.until) {
          found = { error, until, refetchable: found?.refetchable ?? false };
        }
      }
      // Cache events can fire while another component renders, so never set state synchronously.
      queueMicrotask(() => setLimit(found));
    };
    scan();
    const stopQueries = client.getQueryCache().subscribe(scan);
    const stopMutations = client.getMutationCache().subscribe(scan);
    return () => {
      stopQueries();
      stopMutations();
    };
  }, [client]);

  const until = limit?.until;
  useEffect(() => {
    if (until === undefined) return;
    setNow(Date.now());
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, [until]);

  if (status !== "anonymous" || !limit) return null;

  const secondsLeft = Math.max(0, Math.ceil((limit.until - now) / 1000));
  const waiting = secondsLeft > 0;
  // A mutation-only 429 has nothing to reload: the notice just goes away once the wait is over.
  if (!waiting && !limit.refetchable) return null;

  const message = rateLimitMessage({ tier: limit.error.tier, retryAfterSeconds: secondsLeft });

  return (
    <div role="status" className="bg-surface-muted text-sm">
      <div className="mx-auto flex max-w-page-wide flex-wrap items-center gap-x-4 gap-y-2 px-4 py-2 md:px-8">
        <p className="flex-1">{waiting ? message : t("rateLimit.ready")}</p>
        <Link to="/login" className={buttonClasses({ variant: "secondary", size: "sm" })}>
          {t("rateLimit.signIn")}
        </Link>
        {!waiting && (
          <Button
            variant="primary"
            size="sm"
            onClick={() =>
              void client.refetchQueries({
                predicate: (query) => isLimitedQuery(query.state),
              })
            }
          >
            {t("rateLimit.reload")}
          </Button>
        )}
      </div>
    </div>
  );
}
