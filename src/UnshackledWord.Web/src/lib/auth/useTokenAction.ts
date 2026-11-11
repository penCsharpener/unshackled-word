import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router";

export type TokenActionStatus = "running" | "success" | "failure";

/**
 * Runs a one-shot action with the `?token=` query parameter (verify email, magic link). The token is single
 * use, so the action fires once even when StrictMode mounts the effect twice.
 */
export function useTokenAction(action: (token: string) => Promise<unknown>): TokenActionStatus {
  const [params] = useSearchParams();
  const token = params.get("token");
  const [status, setStatus] = useState<TokenActionStatus>(token ? "running" : "failure");
  const started = useRef(false);
  const latest = useRef(action);
  latest.current = action;

  useEffect(() => {
    if (!token || started.current) return;
    started.current = true;
    latest.current(token).then(
      () => setStatus("success"),
      () => setStatus("failure"),
    );
  }, [token]);

  return status;
}
