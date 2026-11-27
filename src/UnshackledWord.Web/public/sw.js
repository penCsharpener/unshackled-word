// Minimal service worker: only here so browsers treat the app as installable.
// It caches nothing and never calls respondWith, so every request (API, /media/) goes straight to the network.
self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", (event) => event.waitUntil(self.clients.claim()));
self.addEventListener("fetch", () => {});
