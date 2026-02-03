// @ts-check
import { defineConfig, envField } from 'astro/config';

import tailwindcss from '@tailwindcss/vite';

// https://astro.build/config
export default defineConfig({
  server: {
      port: 5000,
      host: true
  },

  vite: {
    server: {
      watch: { usePolling: true },
    },
    plugins: [tailwindcss()],
  },

  env: {
    schema: {
      PUBLIC_API_DOMAIN: envField.string({ 
        context: "client", 
        access: "public", 
        default: "https://api.example.com" 
      }),
    }
  }
});