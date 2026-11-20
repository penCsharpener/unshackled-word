/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL?: string;
  readonly VITE_MCP_URL?: string;
  /** Umami website id; the tracker tag is only built into index.html when set. */
  readonly VITE_UMAMI_WEBSITE_ID?: string;
  /** Operator name / contact shown on the privacy page; a placeholder text is used when unset. */
  readonly VITE_OPERATOR_NAME?: string;
  readonly VITE_OPERATOR_CONTACT?: string;
  readonly VITE_WORD_DETAIL_SOURCE?: "mock" | "api";
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

/** App version from package.json, kept in sync with Directory.Build.props. */
declare const __APP_VERSION__: string;
