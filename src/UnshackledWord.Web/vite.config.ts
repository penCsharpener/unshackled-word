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
    host: true, // listen on all interfaces so the LAN can reach the dev server
    allowedHosts: true,
    // Same-origin /api so LAN clients don't need to reach the API host directly.
    proxy: { "/api": process.env.API_PROXY_TARGET ?? "http://host.docker.internal:5142" },
    watch: { usePolling: true },
  },
  test: {
    environment: "jsdom",
    env: { VITE_WORD_DETAIL_SOURCE: "mock" },
    setupFiles: ["./src/test/setup.ts"],
  },
});
