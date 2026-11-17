/** Address of the MCP server in the config snippet: `VITE_MCP_URL`, else `/mcp` on this site (a reverse proxy usually serves it there). */
export const mcpUrl = (): string =>
  (import.meta.env.VITE_MCP_URL ?? `${window.location.origin}/mcp`).replace(/\/$/, "");

/** MCP client config (Claude Code `.mcp.json` shape) with the token, for copying. */
export function mcpConfigSnippet(token: string, url = mcpUrl()): string {
  return JSON.stringify(
    {
      mcpServers: {
        "unshackled-word": {
          type: "http",
          url,
          headers: { Authorization: `Bearer ${token}` },
        },
      },
    },
    null,
    2,
  );
}
