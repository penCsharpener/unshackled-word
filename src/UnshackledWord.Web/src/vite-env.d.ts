/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL?: string;
  readonly VITE_WORD_DETAIL_SOURCE?: "mock" | "api";
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
