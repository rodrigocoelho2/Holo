/* Holo service worker: only makes the app installable. It caches nothing — prices and stock are always live,
   and the login cookie is never touched. Registered only on HTTPS (or localhost), where browsers allow it. */
self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", (event) => event.waitUntil(self.clients.claim()));
self.addEventListener("fetch", () => { /* network as usual */ });
