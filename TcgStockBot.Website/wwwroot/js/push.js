"use strict";

/* ==========================================================================
   Push — notifications on this phone or computer, straight from the bot (Web Push). The browser makes the
   subscription with the bot's public key; the bot encrypts every alert for it and sends it through the browser's
   own push service. Needs HTTPS; on iPhone/iPad Holo must be added to the Home Screen first.
   ========================================================================== */

state.push = null;          // PushDTO from /push/get
state.pushHere = null;      // this browser's device id when it is subscribed, else null

const pushSupported = () => window.isSecureContext && "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;
const isAppleMobile = () => /iPhone|iPad|iPod/.test(navigator.userAgent) || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
const isInstalledApp = () => window.matchMedia("(display-mode: standalone)").matches || navigator.standalone === true;

/** Why this browser can't subscribe, in words the person can act on; null when it can. */
function pushProblem() {
  if (!window.isSecureContext) {
    return `This page isn't HTTPS (${location.origin}). Open Holo at your https address${isInstalledApp() ? ", remove this Home Screen icon and add it again from there" : ""}.`;
  }
  if (!("serviceWorker" in navigator)) return "This browser has service workers turned off (private mode or a setting), so it can't get push notifications.";
  if (!("PushManager" in window) || !("Notification" in window)) {
    if (isAppleMobile()) {
      return isInstalledApp()
        ? "iOS doesn't offer push to this Home Screen icon. It needs iOS 16.4 or later, and the icon must be added from Safari on the https address: remove it, open Holo in Safari, tap Share → Add to Home Screen, and open it from the new icon."
        : null;   // handled by the "Add to Home Screen" hint
    }
    return "This browser doesn't support web push notifications. Try Chrome, Edge, Firefox or Safari.";
  }
  return null;
}

function base64UrlBytes(text) {
  const base64 = text.replace(/-/g, "+").replace(/_/g, "/");
  const raw = atob(base64 + "=".repeat((4 - (base64.length % 4)) % 4));
  return Uint8Array.from(raw, (character) => character.charCodeAt(0));
}

const bytesBase64Url = (buffer) => btoa(String.fromCharCode(...new Uint8Array(buffer))).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");

/** The device id the bot gives an endpoint (first 12 hex characters of its SHA-256). */
async function pushIdOf(endpoint) {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(endpoint));
  return [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, "0")).join("").slice(0, 12);
}

/** "iPhone · Safari", "Windows · Edge"… so the device list says which is which. */
function deviceLabel() {
  const agent = navigator.userAgent;
  const device = /iPad/.test(agent) || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1) ? "iPad"
    : /iPhone/.test(agent) ? "iPhone"
      : /Android/.test(agent) ? "Android"
        : /Mac OS X/.test(agent) ? "Mac"
          : /Windows/.test(agent) ? "Windows"
            : /Linux/.test(agent) ? "Linux" : "Device";
  const browser = /Edg\//.test(agent) ? "Edge"
    : /SamsungBrowser/.test(agent) ? "Samsung Internet"
      : /Firefox\//.test(agent) ? "Firefox"
        : /OPR\//.test(agent) ? "Opera"
          : /Chrome\//.test(agent) ? "Chrome"
            : /Safari\//.test(agent) ? "Safari" : "Browser";
  return `${device} · ${isInstalledApp() ? "Holo app" : browser}`;
}

async function pushRegistration() {
  await navigator.serviceWorker.register("/sw.js");
  return navigator.serviceWorker.ready;
}

/** Loads the push state and whether this browser is one of the devices. */
async function loadPush() {
  try {
    state.push = await api("/push/get");
  } catch {
    state.push = { enabled: false, publicKey: "", devices: [], failed: true };
  }
  state.pushHere = null;
  if (pushSupported() && Notification.permission === "granted") {
    try {
      const registration = await navigator.serviceWorker.getRegistration();
      const subscription = registration ? await registration.pushManager.getSubscription() : null;
      if (subscription) {
        const id = await pushIdOf(subscription.endpoint);
        if (state.push.devices.some((device) => device.id === id)) state.pushHere = id;
      }
    } catch { /* not this browser */ }
  }
  if (state.route.view === "settings") renderView(true);
}

/** Subscribes this browser (asks for permission first) and registers it with the bot. */
async function enablePush({ quiet = false } = {}) {
  if (!pushSupported()) {
    if (!quiet) toast(isAppleMobile() && !isInstalledApp() ? "On iPhone, add Holo to the Home Screen first, then turn notifications on from the app." : (pushProblem() || "This browser can't receive push notifications here."), "error");
    return;
  }
  if (!state.push?.publicKey) await loadPush();
  if (!state.push?.publicKey) { if (!quiet) toast("Couldn't get the bot's push key.", "error"); return; }

  const permission = Notification.permission === "granted" ? "granted" : await Notification.requestPermission();
  if (permission !== "granted") {
    if (!quiet) toast("Notifications are blocked for Holo. Allow them in the browser's site settings and try again.", "error");
    return;
  }

  try {
    const registration = await pushRegistration();
    let subscription = await registration.pushManager.getSubscription();
    const key = base64UrlBytes(state.push.publicKey);
    const current = subscription?.options?.applicationServerKey;
    if (subscription && current && bytesBase64Url(current) !== state.push.publicKey) {
      await subscription.unsubscribe();   // made with another key (the bot's data was reset)
      subscription = null;
    }
    subscription = subscription || await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key });
    const json = subscription.toJSON();
    await post("/push/subscribe", { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth, device: deviceLabel() });
    if (!quiet) toast("Notifications are on for this device", "success");
  } catch (error) {
    if (!quiet) {
      toast(error?.name === "NotAllowedError" || error?.name === "AbortError"
        ? "The browser refused push here. Private windows can't get it; in a normal window, allow notifications for Holo and try again."
        : errorText(error), "error");
    }
  }
  await loadPush();
  refreshNotification();
}

/** Unsubscribes this browser and removes it from the bot. */
async function disablePush() {
  try {
    const registration = await navigator.serviceWorker.getRegistration();
    const subscription = registration ? await registration.pushManager.getSubscription() : null;
    if (subscription) {
      await post("/push/unsubscribe", { endpoint: subscription.endpoint });
      await subscription.unsubscribe();
    } else if (state.pushHere) {
      await post("/push/unsubscribe", { id: state.pushHere });
    }
    toast("Notifications are off for this device", "success");
  } catch (error) {
    toast(errorText(error), "error");
  }
  await loadPush();
  refreshNotification();
}

async function removePushDevice(device) {
  if (!await confirmDialog({ title: "Remove this device?", message: `${device.device || "This device"} (${device.userName || "no account"}) stops getting notifications.`, confirmLabel: "Remove" })) return;
  try {
    await post("/push/unsubscribe", { id: device.id });
    toast("Device removed", "success");
  } catch (error) {
    toast(errorText(error), "error");
  }
  await loadPush();
  refreshNotification();
}

/** The Push tile of Settings → Alert channels: this device on/off, the other devices, and a test. */
function pushTile() {
  const push = state.push;
  if (!push) {
    loadPush();
    return h("div", { class: "channel" }, h("p", { class: "muted small" }, "Loading push…"));
  }
  const devices = push.devices || [];
  const here = state.pushHere;
  const on = push.enabled && devices.length > 0;
  const needsHomeScreen = isAppleMobile() && !isInstalledApp();

  let action;
  if (!push.enabled) action = h("p", { class: "muted small" }, "Turned off with WebPush__Enabled=false in .env.");
  else if (here) action = button("Turn off on this device", { kind: "small", onClick: disablePush });
  else if (needsHomeScreen) action = h("p", { class: "info-box small" }, "On iPhone and iPad: tap Share → ", h("strong", {}, "Add to Home Screen"), ", open Holo from there and turn notifications on.");
  else if (!pushSupported()) action = h("p", { class: "info-box small" }, pushProblem() || "This browser can't get push notifications here.");
  else action = button("Turn on for this device", { kind: "primary small", iconName: "bell", onClick: () => enablePush() });

  return h("div", { class: `channel ${on ? "on" : ""}` },
    h("div", { class: "channel-head" },
      h("span", { class: "channel-icon" }, icon("bell")),
      h("div", {}, h("strong", {}, "Push notifications"),
        h("div", { class: "muted small" }, !push.enabled ? "Off" : here ? "On for this device" : devices.length ? `On for ${devices.length} ${devices.length === 1 ? "device" : "devices"}` : "No device yet")),
      h("span", { class: `bot-dot ${on ? "on" : "off"}`, "aria-hidden": "true" })),
    devices.length ? h("ul", { class: "push-devices" }, devices.map((device) => h("li", {},
      h("div", { class: "push-device-name" },
        h("strong", {}, device.device || "Device"), device.id === here ? h("span", { class: "best-badge" }, "This device") : null,
        h("div", { class: "muted small" }, [device.userName, device.lastSentAt ? `last push ${relative(device.lastSentAt)}` : `added ${relative(device.createdAt)}`].filter(Boolean).join(" · ")),
        device.lastError ? h("div", { class: "amber small", title: device.lastError }, `Last push failed: ${device.lastError}`) : null),
      h("button", { type: "button", class: "text-link small", onclick: () => removePushDevice(device) }, "Remove")))) : null,
    h("p", { class: "muted small" }, "Alerts arrive as notifications on your phone or computer — tap one to open the store page. Nothing to set up in .env."),
    h("div", { class: "toolbar" }, action,
      button("Send a test message", { kind: "small", disabled: !on, onClick: () => sendTest("Push") })));
}

// The service worker asks for a new subscription when the browser rotates it.
if ("serviceWorker" in navigator) {
  navigator.serviceWorker.addEventListener("message", (event) => {
    if (event.data?.type === "push-resubscribe") enablePush({ quiet: true });
  });
}
