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
    ? (enabled ? "A vigiar outra vez" : "Produto em pausa")
    : (enabled ? "Todos os produtos a vigiar" : "Todos os produtos em pausa");
  return mutate(() => post("/stocktask/enable", { id, enabled }), message);
}

function setStoreEnabled(name, enabled) {
  return mutate(() => post("/store/enable", { name, enabled }), `${name} ${enabled ? "ativada" : "desligada"}`);
}

async function deleteTask(task) {
  if (!await confirmDialog({ title: "Deixar de vigiar?", message: `“${task.name || task.id}” é apagado e deixa de ser pesquisado. Os alertas antigos ficam na lista.`, confirmLabel: "Apagar", danger: true })) return;
  const ok = await mutate(() => post(`/stocktask/delete?identifier=${encodeURIComponent(task.id)}`), "Produto apagado");
  if (ok && state.route.view === "produto") go("a-vigiar");
}

async function deleteAllTasks() {
  const tasks = state.tasks || [];
  if (tasks.length === 0) return;
  if (!await confirmDialog({
    title: `Apagar os ${tasks.length} produtos?`,
    message: "Deixam todos de ser vigiados. Antes de apagar é descarregada uma cópia (JSON) que podes voltar a importar.",
    confirmLabel: "Apagar todos", danger: true,
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
  if (failed.length === 0) toast(`${tasks.length} produto(s) apagado(s)`, "success");
  else toast(`Não foi possível apagar: ${failed.join(", ")}`, "error");
}

async function deleteStore(store) {
  if (!await confirmDialog({ title: "Apagar loja?", message: `“${store.name}” deixa de ser pesquisada e sai dos produtos que a usam.`, confirmLabel: "Apagar", danger: true })) return;
  closeDrawer();
  await mutate(() => post(`/store/delete?identifier=${encodeURIComponent(store.name)}`), "Loja apagada");
}

async function setPaused(paused) {
  try {
    await post(paused ? "/monitor/pause" : "/monitor/resume");
    toast(paused ? "Bot em pausa" : "Bot retomado — a verificar já", "success");
    await refresh();
  } catch (error) {
    toast(error.message, "error");
  }
}

async function runNow() {
  try {
    await post("/monitor/run");
    toast("Verificação pedida", "success");
    setTimeout(refresh, 700);
  } catch (error) {
    toast(error.message, "error");
  }
}

async function clearAlertList() {
  if (!await confirmDialog({ title: "Limpar a lista de alertas?", message: "Apaga o histórico de alertas. Os produtos que continuam em stock não voltam a ser avisados por causa disto.", confirmLabel: "Limpar" })) return;
  try {
    await post("/alert/clear");
    toast("Lista de alertas limpa", "success");
    state.alerts = await api("/alert/list");
    renderChrome();
    renderView(true);
  } catch (error) {
    toast(error.message, "error");
  }
}

async function resetAlertMemory() {
  if (!await confirmDialog({ title: "Limpar memória de alertas?", message: "Na próxima verificação voltas a receber aviso de tudo o que estiver em stock ao preço.", confirmLabel: "Limpar" })) return;
  try {
    await post("/alert/reset");
    toast("Memória de alertas limpa", "success");
  } catch (error) {
    toast(error.message, "error");
  }
}

async function sendTest(channel) {
  try {
    await post("/notification/test", { channel });
    toast(channel === "WhatsApp" ? "Mensagem de teste enviada — vê o WhatsApp" : "Email de teste enviado", "success");
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
    if (state.route.view === "definicoes" || state.route.view === "alertas") renderView(true);
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
      if (state.route.view === "comparar") loadCompare();
    }
    renderChrome();
    renderView();
  } catch (error) {
    document.getElementById("status-chip").replaceChildren(h("span", { class: "bot-dot error" }), error.message || "Sem ligação ao bot");
    document.getElementById("bot-card").replaceChildren(
      h("div", { class: "bot-state" }, h("span", { class: "bot-dot error" }), "Sem ligação ao bot"),
      h("div", { class: "bot-meta" }, error.message || "A tentar outra vez…"));
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
  } catch (error) {
    document.getElementById("view").replaceChildren(h("div", { class: "card soft" }, emptyState("alert", "Não foi possível ligar ao bot", `${error.message} — a tentar outra vez…`)));
    setTimeout(start, 3000);
    return;
  }

  navigate();
  refreshNotification();
  setInterval(refresh, POLL_MS);
  setInterval(tickCountdowns, 1000);
}

/* -------------------------------------------------------------- wiring -- */

window.addEventListener("hashchange", navigate);

document.getElementById("drawer-backdrop").addEventListener("click", closeDrawer);
document.getElementById("menu-button").addEventListener("click", openSidebar);
document.getElementById("sidebar-backdrop").addEventListener("click", closeSidebar);

// "Mais" menus close when clicking elsewhere.
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
  else if (key === "n") go("vigiar");
  else if (key === "/") {
    const search = document.querySelector("#view input[type=search]");
    if (search) { event.preventDefault(); search.focus(); }
  }
});

start();
