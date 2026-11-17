import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { StatusBadge } from "@/components/StatusBadge";
import { TextField } from "@/components/TextField";
import { mcpConfigSnippet } from "@/lib/agent/mcp";
import {
  useApiTokens,
  useApiTokenUsers,
  useCreateApiToken,
  useRevokeApiToken,
} from "@/lib/agent/queries";
import { type ApiToken, type ApiTokenState, apiTokenState } from "@/lib/agent/types";
import { describeApiErrors } from "@/lib/api";
import { formatStamp } from "@/lib/format";

const DAYS = [7, 30, 90, 365] as const;
const DEFAULT_DAYS = 30;
const NEW_MODEL = "new-model";
const TOKEN_TONE = { active: "ok", revoked: "neutral", expired: "warning" } as const;

/** What the maintainer sees once after creating a token; lives only in component state. */
interface IssuedToken {
  secret: string;
  name: string;
}

function SecretDialog({ issued, onClose }: { issued: IssuedToken; onClose: () => void }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [copyFailed, setCopyFailed] = useState(false);

  async function copy(text: string) {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      setCopyFailed(false);
    } catch {
      setCopyFailed(true);
    }
  }

  const done = copied || confirmed;
  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={done}
      title={t("agent.tokens.secret.title", { name: issued.name })}
      description={t("agent.tokens.secret.once")}
      footer={
        <div className="flex justify-end">
          <Button variant="primary" disabled={!done} onClick={onClose}>
            {t("agent.tokens.secret.done")}
          </Button>
        </div>
      }
    >
      <div className="flex flex-col gap-4">
        <TextField
          label={t("agent.tokens.secret.token")}
          value={issued.secret}
          onChange={() => {}}
          readOnly
          onFocus={(event) => event.currentTarget.select()}
        />
        <div className="flex flex-wrap items-center gap-3">
          <Button variant="primary" onClick={() => void copy(issued.secret)}>
            {copied ? t("agent.tokens.secret.copied") : t("agent.tokens.secret.copy")}
          </Button>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={confirmed}
              onChange={(event) => setConfirmed(event.target.checked)}
            />
            {t("agent.tokens.secret.confirm")}
          </label>
        </div>
        <FormError message={copyFailed ? t("agent.tokens.secret.copyFailed") : ""} />
        <div className="flex flex-col gap-2">
          <h3 className="font-medium">{t("agent.tokens.secret.configTitle")}</h3>
          <p className="text-sm text-fg-muted">{t("agent.tokens.secret.configHint")}</p>
          <pre className="overflow-x-auto rounded-md border border-border bg-surface-muted p-3 font-mono text-xs">
            {mcpConfigSnippet(issued.secret)}
          </pre>
          <div>
            <Button size="sm" onClick={() => void copy(mcpConfigSnippet(issued.secret))}>
              {t("agent.tokens.secret.copyConfig")}
            </Button>
          </div>
        </div>
      </div>
    </Dialog>
  );
}

function CreateForm({ onIssued }: { onIssued: (token: IssuedToken) => void }) {
  const { t } = useTranslation();
  const users = useApiTokenUsers();
  const create = useCreateApiToken();
  const [name, setName] = useState("");
  const [days, setDays] = useState(String(DEFAULT_DAYS));
  const [chosen, setChosen] = useState<string>();
  const [model, setModel] = useState("");

  const list = users.data ?? [];
  const target = chosen ?? (list[0] ? `user:${list[0].id}` : NEW_MODEL);
  const isNewModel = target === NEW_MODEL;
  const valid = name.trim().length > 0 && (!isNewModel || model.trim().length > 0);
  const errors = create.isError ? describeApiErrors(create.error) : [];

  function submit() {
    create.mutate(
      {
        name: name.trim(),
        expiresInDays: Number(days),
        ...(isNewModel ? { model: model.trim() } : { userId: Number(target.slice(5)) }),
      },
      {
        onSuccess: (response) => {
          onIssued({ secret: response.secret, name: response.token.name });
          create.reset();
          setName("");
          setModel("");
          setChosen(undefined);
        },
      },
    );
  }

  return (
    <form
      className="flex flex-col gap-3"
      onSubmit={(event) => {
        event.preventDefault();
        if (valid && !create.isPending) submit();
      }}
    >
      <div className="flex flex-wrap items-end gap-3">
        <TextField
          label={t("agent.tokens.name")}
          size="sm"
          maxLength={100}
          value={name}
          onChange={setName}
        />
        <Select
          label={t("agent.tokens.user")}
          value={target}
          onChange={setChosen}
          disabled={users.isPending}
          options={[
            ...list.map((u) => ({
              value: `user:${u.id}`,
              label: `${u.name} (${u.roles.join(", ")})`,
            })),
            { value: NEW_MODEL, label: t("agent.tokens.newModel") },
          ]}
        />
        {isNewModel ? (
          <TextField
            label={t("agent.tokens.model")}
            size="sm"
            maxLength={200}
            placeholder="gemma4:31b"
            value={model}
            onChange={setModel}
          />
        ) : null}
        <Select
          label={t("agent.tokens.validFor")}
          value={days}
          onChange={setDays}
          options={DAYS.map((d) => ({
            value: String(d),
            label: t("agent.tokens.days", { count: d }),
          }))}
        />
        <Button type="submit" variant="primary" disabled={!valid || create.isPending}>
          {create.isPending ? t("agent.tokens.creating") : t("agent.tokens.create")}
        </Button>
      </div>
      {users.isError ? <FormError message={t("agent.tokens.usersFailed")} /> : null}
      {errors.map((message) => (
        <FormError key={message} message={message} />
      ))}
      {create.isError && errors.length === 0 ? (
        <FormError message={t("agent.tokens.createFailed")} />
      ) : null}
    </form>
  );
}

function TokenRow({ token, userName }: { token: ApiToken; userName: string }) {
  const { t, i18n } = useTranslation();
  const revoke = useRevokeApiToken();
  const [confirming, setConfirming] = useState(false);
  const state: ApiTokenState = apiTokenState(token);
  const error = revoke.isError
    ? describeApiErrors(revoke.error)[0] || t("agent.tokens.revokeFailed")
    : "";
  return (
    <tr className="border-b border-border align-top">
      <td className="px-2 py-2">
        {token.name}
        <p className="font-mono text-xs text-fg-muted">{token.prefix}…</p>
      </td>
      <td className="px-2 py-2">{userName}</td>
      <td className="px-2 py-2">
        <StatusBadge tone={TOKEN_TONE[state]}>{t(`agent.tokens.state.${state}`)}</StatusBadge>
      </td>
      <td className="px-2 py-2">{formatStamp(token.expiresAt, i18n.language)}</td>
      <td className="px-2 py-2">
        {token.lastUsedAt
          ? formatStamp(token.lastUsedAt, i18n.language)
          : t("agent.tokens.neverUsed")}
      </td>
      <td className="px-2 py-2">
        {state === "revoked" ? null : (
          <div className="flex flex-col items-start gap-1">
            {confirming ? (
              <div className="flex gap-2">
                <Button
                  size="sm"
                  variant="primary"
                  disabled={revoke.isPending}
                  onClick={() => revoke.mutate(token.id, { onSettled: () => setConfirming(false) })}
                >
                  {t("agent.tokens.revokeConfirm", { name: token.name })}
                </Button>
                <Button size="sm" variant="ghost" onClick={() => setConfirming(false)}>
                  {t("agent.tokens.revokeKeep")}
                </Button>
              </div>
            ) : (
              <Button size="sm" onClick={() => setConfirming(true)}>
                {t("agent.tokens.revoke")}
              </Button>
            )}
            <FormError message={error} />
          </div>
        )}
      </td>
    </tr>
  );
}

/** "KI-Zugriffstoken" section of the maintenance page: issue MCP tokens for an AI user (shown once), list and revoke them. */
export function ApiTokensSection() {
  const { t } = useTranslation();
  const tokens = useApiTokens();
  const users = useApiTokenUsers();
  const [issued, setIssued] = useState<IssuedToken>();
  const userName = (id: number) => users.data?.find((u) => u.id === id)?.name ?? `#${id}`;
  const columns = ["name", "user", "status", "expires", "lastUsed", "actions"];
  return (
    <div className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">{t("agent.tokens.title")}</h2>
      <p className="text-fg-muted">{t("agent.tokens.intro")}</p>
      <CreateForm onIssued={setIssued} />
      {tokens.isError ? <FormError message={t("agent.tokens.loadFailed")} /> : null}
      {tokens.isSuccess && tokens.data.length === 0 ? (
        <p className="text-sm text-fg-muted">{t("agent.tokens.empty")}</p>
      ) : null}
      {tokens.data && tokens.data.length > 0 ? (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <caption className="sr-only">{t("agent.tokens.title")}</caption>
            <thead className="text-fg-muted">
              <tr className="border-b border-border">
                {columns.map((column) => (
                  <th key={column} scope="col" className="px-2 py-1 font-medium">
                    {t(`agent.tokens.columns.${column}`)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {tokens.data.map((token) => (
                <TokenRow key={token.id} token={token} userName={userName(token.userId)} />
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
      {issued ? <SecretDialog issued={issued} onClose={() => setIssued(undefined)} /> : null}
    </div>
  );
}
