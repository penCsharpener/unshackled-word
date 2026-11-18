/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL?: string;
  readonly VITE_MCP_URL?: string;
  readonly VITE_WORD_DETAIL_SOURCE?: "mock" | "api";
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

/** App version from package.json, kept in sync with Directory.Build.props. */
declare const __APP_VERSION__: string;
