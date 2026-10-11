"use strict";

/* ==========================================================================
   Actions, loading and wiring. Loaded last.
   ========================================================================== */

/* ------------------------------------------------------------- actions -- */

/** Runs a mutation, reloads the configuration and reports the outcome. */
async function mutate(action, successMessage) {
  try {
    await action();
    await reloadConfiguration();
    if (successMessage) toast(successMessage, "success");
    return true;
  } catch (error) {
    toast(errorText(error), "error");
    return false;
  }
}

function setTaskEnabled(id, enabled) {
  const message = id
    ? (enabled ? "Watching again" : "Product paused")
    : (enabled ? "Watching every product" : "Every product paused");
  return mutate(() => post("/stocktask/enable", { id, enabled }), message);
}

function setStoreEnabled(name, enabled) {
  return mutate(() => post("/store/enable", { name, enabled }), `${name} ${enabled ? "turned on" : "turned off"}`);
}

async function deleteTask(task) {
  if (!await confirmDialog({ title: "Stop watching?", message: `“${task.name || task.id}” is deleted and no longer searched. Past alerts stay in the list.`, confirmLabel: "Delete", danger: true })) return;
  const ok = await mutate(() => post(`/stocktask/delete?identifier=${encodeURIComponent(task.id)}`), "Product deleted");
  if (ok && state.route.view === "watched") go("watching");
}

async function deleteAllTasks() {
  const tasks = state.tasks || [];
  if (tasks.length === 0) return;
  if (!await confirmDialog({
    title: `Delete all ${tasks.length} products?`,
    message: "None of them will be watched any more. A copy (JSON) you can import again is downloaded first.",
    confirmLabel: "Delete all", danger: true,
  })) return;

  exportTasks();
  const failed = [];
  for (const task of tasks) {
    try {
      await post(`/stocktask/delete?identifier=${encodeURIComponent(task.id)}`);
    } catch {
      failed.push(task.name || task.id);
    }
  }
  await reloadConfiguration();
  if (failed.length === 0) toast(`${tasks.length} product(s) deleted`, "success");
  else toast(`Could not delete: ${failed.join(", ")}`, "error");
}

async function deleteStore(store) {
  if (!await confirmDialog({ title: "Delete store?", message: `“${store.name}” is no longer searched and is removed from the products that use it.`, confirmLabel: "Delete", danger: true })) return;
  closeDrawer();
  await mutate(() => post(`/store/delete?identifier=${encodeURIComponent(store.name)}`), "Store deleted");
}

async function setPaused(paused) {
  try {
    await post(paused ? "/monitor/pause" : "/monitor/resume");
    toast(paused ? "Bot paused" : "Bot resumed — checking now", "success");
    await refresh();
  } catch (error) {
    toast(error.message, "error");
  }
}

async function runNow() {
  try {
    await post("/monitor/run");
    toast("Check requested", "success");
    setTimeout(refresh, 700);
  } catch (error) {
    toast(error.message, "error");
  }
}

async function clearAlertList() {
  if (!await confirmDialog({ title: "Clear the alert list?", message: "Deletes the alert history. Products still in stock are not alerted again because of this.", confirmLabel: "Clear" })) return;
  try {
    await post("/alert/clear");
    toast("Alert list cleared", "success");
    state.alerts = await api("/alert/list");
    renderChrome();
    renderView(true);
  } catch (error) {
    toast(error.message, "error");
  }
}

async function resetAlertMemory() {
  if (!await confirmDialog({ title: "Clear the alert memory?", message: "On the next check you get alerted again about everything in stock at your price.", confirmLabel: "Clear" })) return;
  try {
    await post("/alert/reset");
    toast("Alert memory cleared", "success");
  } catch (error) {
    toast(error.message, "error");
  }
}

async function sendTest(channel) {
  try {
    await post("/notification/test", { channel });
    toast(channel === "Email" ? "Test email sent" : `Test message sent — check ${channel}`, "success");
  } catch (error) {
    toast(errorText(error), "error");
  }
}

/* ------------------------------------------------------------- loading -- */

async function reloadConfiguration() {
  [state.tasks, state.stores, state.settings] = await Promise.all([api("/stocktask/list"), api("/store/list"), api("/setting/get")]);
  renderChrome();
  renderView(true);
}

async function refreshNotification() {
  try {
    state.notification = await api("/notification/get");
    if (state.route.view === "settings" || state.route.view === "alerts") renderView(true);
  } catch {
    // The pages show "A carregar canais…" until it works.
  }
}

async function refresh() {
  try {
    state.monitor = await api("/monitor/get");
    const cycleAt = state.monitor.lastCycle?.finishedAt || null;
    if (cycleAt !== state.lastCycleAt) {
      state.lastCycleAt = cycleAt;
      state.alerts = await api("/alert/list");
      if (state.route.view === "products") loadCatalog();
      if (state.route.view === "product") loadFicha(state.route.param, { quiet: true });
    }
    renderChrome();
    renderView();
  } catch (error) {
    document.getElementById("status-chip").replaceChildren(h("span", { class: "bot-dot error" }), error.message || "Can't reach the bot");
    document.getElementById("bot-card").replaceChildren(
      h("div", { class: "bot-state" }, h("span", { class: "bot-dot error" }), "Can't reach the bot"),
      h("div", { class: "bot-meta" }, error.message || "Trying again…"));
  }
}

async function start() {
  applyTheme();
  try {
    [state.tasks, state.stores, state.settings, state.monitor, state.alerts] = await Promise.all([
      api("/stocktask/list"), api("/store/list"), api("/setting/get"), api("/monitor/get"), api("/alert/list"),
    ]);
    state.lastCycleAt = state.monitor.lastCycle?.finishedAt || null;
    state.msrp = await api("/msrp/list").catch(() => ({ kinds: [], entries: [] }));
    state.releases = await api("/release/list").catch(() => null);
  } catch (error) {
    document.getElementById("view").replaceChildren(h("div", { class: "card soft" }, emptyState("alert", "Could not connect to the bot", `${error.message} — trying again…`)));
    setTimeout(start, 3000);
    return;
  }

  navigate();
  refreshNotification();
  if ("serviceWorker" in navigator && window.isSecureContext) navigator.serviceWorker.register("/sw.js").catch(() => { /* optional */ });
  setInterval(refresh, POLL_MS);
  setInterval(tickCountdowns, 1000);
}

/* -------------------------------------------------------------- wiring -- */

window.addEventListener("hashchange", navigate);

document.getElementById("drawer-backdrop").addEventListener("click", closeDrawer);
document.getElementById("menu-button").addEventListener("click", openSidebar);
document.getElementById("sidebar-backdrop").addEventListener("click", closeSidebar);

// "More" menus close when clicking elsewhere.
document.addEventListener("click", (event) => {
  for (const menu of document.querySelectorAll("details.menu[open]")) if (!menu.contains(event.target)) menu.open = false;
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") {
    const menu = document.querySelector("details.menu[open]");
    if (menu) { menu.open = false; menu.querySelector("summary")?.focus(); return; }
    closeDrawer();
    closeSidebar();
    return;
  }
  const typing = /^(INPUT|TEXTAREA|SELECT)$/.test(event.target.tagName) || event.target.isContentEditable;
  if (typing || event.ctrlKey || event.metaKey || event.altKey) return;
  if (document.getElementById("drawer").classList.contains("open") || document.getElementById("modal").classList.contains("open")) return;

  const view = VIEWS.find((item) => item.key === event.key);
  if (view) { go(view.id); return; }
  const key = event.key.toLowerCase();
  if (key === "r") runNow();
  else if (key === "n") go("watch");
  else if (key === "/") {
    const search = document.querySelector("#view input[type=search]");
    if (search) { event.preventDefault(); search.focus(); }
  }
});

start();
