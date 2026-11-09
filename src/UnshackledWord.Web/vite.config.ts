import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { "@": new URL("./src", import.meta.url).pathname },
  },
  server: {
    port: 5173,
    strictPort: true,
    watch: { usePolling: true },
  },
  test: {
    environment: "jsdom",
    env: { VITE_WORD_DETAIL_SOURCE: "mock" },
    setupFiles: ["./src/test/setup.ts"],
  },
});
