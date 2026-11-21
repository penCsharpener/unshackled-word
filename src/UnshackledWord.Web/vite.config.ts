import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { loadEnv, type Plugin } from "vite";
import { defineConfig } from "vitest/config";
import pkg from "./package.json" with { type: "json" };

/**
 * Injects the Umami tracker tag (roadmap 0021) into the built index.html, and only when
 * VITE_UMAMI_WEBSITE_ID is set at build time; the dev server never gets it.
 */
function umamiTag(websiteId: string | undefined): Plugin {
  return {
    name: "uw-umami-tag",
    apply: "build",
    transformIndexHtml() {
      if (!websiteId) return [];
      return [
        {
          tag: "script",
          attrs: {
            defer: true,
            src: "/stats/script.js",
            "data-website-id": websiteId,
            "data-host-url": "/stats",
            "data-do-not-track": "true",
            "data-before-send": "uwBeforeSend",
          },
          // Body, after the app module: deferred scripts run in document order, so window.uwBeforeSend exists before the first page view.
          injectTo: "body",
        },
      ];
    },
  };
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, ".", "");
  return {
    define: { __APP_VERSION__: JSON.stringify(pkg.version) },
    plugins: [react(), tailwindcss(), umamiTag(env.VITE_UMAMI_WEBSITE_ID)],
    resolve: {
      alias: { "@": new URL("./src", import.meta.url).pathname },
    },
    server: {
      port: 5173,
      strictPort: true,
      host: true, // listen on all interfaces so the LAN can reach the dev server
      allowedHosts: true,
      // Same-origin /api so LAN clients don't need to reach the API host directly.
      proxy: {
        "/api": env.API_PROXY_TARGET ?? "http://host.docker.internal:5142",
        // Presigned media reads (Garage): /media/<bucket>/<key>?... -> /<bucket>/<key>?... byte for byte. No changeOrigin: SigV4 signs the public Host header.
        "/media": {
          target: env.MEDIA_PROXY_TARGET ?? "http://unshackledword.garage:3900",
          rewrite: (path: string) => path.replace(/^\/media/, ""),
        },
        // Same-origin Umami tracker: /stats/script.js -> Umami /script.js, /stats/api/send -> /api/send.
        "/stats": {
          target: env.UMAMI_PROXY_TARGET ?? "http://host.docker.internal:3002",
          changeOrigin: true,
          rewrite: (path: string) => path.replace(/^\/stats/, ""),
        },
      },
      watch: { usePolling: true },
    },
    test: {
      environment: "jsdom",
      env: { VITE_WORD_DETAIL_SOURCE: "mock" },
      setupFiles: ["./src/test/setup.ts"],
    },
  };
});
