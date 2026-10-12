/* Holo service worker: makes the app installable and shows the push notifications. It caches nothing — prices and
   stock are always live, and the login cookie is never touched. Registered only on HTTPS (or localhost), where
   browsers allow it. */
self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", (event) => event.waitUntil(self.clients.claim()));
self.addEventListener("fetch", () => { /* network as usual */ });

/* A push from the bot: { title, body, url, tag }. Same tag = the newer one replaces the older one. */
self.addEventListener("push", (event) => {
  let data = {};
  try {
    data = event.data ? event.data.json() : {};
  } catch {
    data = { body: event.data ? event.data.text() : "" };
  }
  const title = data.title || "Holo";
  event.waitUntil(self.registration.showNotification(title, {
    body: data.body || "",
    tag: data.tag || undefined,
    renotify: Boolean(data.tag),
    icon: "/img/icon-192.png",
    badge: "/img/badge-96.png",
    data: { url: data.url || "/" },
  }));
});

/* A tap opens the store page of the product (or Holo), reusing an open Holo tab for Holo links. */
self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const target = new URL(event.notification.data?.url || "/", self.location.origin);
  event.waitUntil((async () => {
    if (target.origin === self.location.origin) {
      const tabs = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
      const tab = tabs.find((client) => new URL(client.url).origin === self.location.origin);
      if (tab) {
        await tab.focus();
        if (tab.url !== target.href && "navigate" in tab) await tab.navigate(target.href).catch(() => {});
        return;
      }
    }
    await self.clients.openWindow(target.href);
  })());
});

/* The browser replaced the subscription (keys rotated): tell the bot about the new one. */
self.addEventListener("pushsubscriptionchange", (event) => {
  event.waitUntil((async () => {
    const tabs = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
    for (const tab of tabs) tab.postMessage({ type: "push-resubscribe" });
  })());
});
