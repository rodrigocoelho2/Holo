"use strict";

/* ==========================================================================
   Shell: routes, sidebar, bot card, mobile bars, theme and the render loop.
   ========================================================================== */

const VIEWS = [
  { id: "radar", label: "Radar", icon: "radar", key: "1" },
  { id: "a-vigiar", label: "A vigiar", icon: "eye", key: "2" },
  { id: "alertas", label: "Alertas", icon: "bell", key: "3" },
  { id: "lojas", label: "Lojas", icon: "store", key: "4" },
  { id: "comparar", label: "Comparar", icon: "compare", key: "5" },
  { id: "definicoes", label: "Definições", icon: "sliders", key: "6" },
  { id: "testar", label: "Testar loja", icon: "flask", key: "7", section: "Ferramentas" },
];

/** Routes that are not in the menu: they light up their parent entry. */
const SUB_ROUTES = { produto: "a-vigiar", vigiar: "a-vigiar" };

/** Hashes of the previous dashboard, so old bookmarks keep working. */
const OLD_ROUTES = { overview: "radar", tasks: "a-vigiar", stores: "lojas", alerts: "alertas", compare: "comparar", probe: "testar", settings: "definicoes" };

/** Routes re-rendered by the poll; the others hold forms and only render on navigation. */
const LIVE_VIEWS = new Set(["radar", "a-vigiar", "produto", "alertas", "lojas"]);

function parseHash() {
  const match = location.hash.match(/^#\/([\w-]+)(?:\/(.*))?$/);
  let view = match?.[1] || "radar";
  view = OLD_ROUTES[view] || view;
  const param = match?.[2] ? decodeURIComponent(match[2]) : "";
  const known = VIEWS.some((item) => item.id === view) || SUB_ROUTES[view];
  return known ? { view, param } : { view: "radar", param: "" };
}

const routeHash = (view, param = "") => `#/${view}${param ? `/${encodeURIComponent(param)}` : ""}`;

function go(view, param = "") {
  const hash = routeHash(view, param);
  if (location.hash === hash) { navigate(); return; }
  location.hash = hash;                                  // → hashchange → navigate()
}

function navigate() {
  const route = parseHash();
  const changed = route.view !== state.route.view || route.param !== state.route.param;
  state.route = route;
  if (location.hash !== routeHash(route.view, route.param)) history.replaceState(null, "", routeHash(route.view, route.param));
  closeSidebar();
  if (route.view === "definicoes" || route.view === "alertas") refreshNotification();
  if (route.view === "comparar") loadCompare();
  if (route.view !== "vigiar") state.wizard = null;
  renderChrome();
  renderView(true);
  if (changed) {
    window.scrollTo({ top: 0 });
    document.getElementById("view").focus({ preventScroll: true });
  }
}

/* ---------------------------------------------------------------- chrome -- */

function renderChrome() {
  renderNav();
  renderBotCard();
  renderTabbar();
  renderStatusChip();
  const view = VIEWS.find((item) => item.id === (SUB_ROUTES[state.route.view] || state.route.view));
  document.getElementById("mobile-title").textContent = state.route.view === "vigiar" ? "Vigiar produto" : view?.label || "TcgStock Bot";
}

const activeMenu = () => SUB_ROUTES[state.route.view] || state.route.view;

function navCount(id) {
  if (id === "a-vigiar") return state.tasks ? [state.tasks.length, ""] : null;
  if (id === "alertas") { const today = alertsToday(); return today > 0 ? [today, "accent"] : null; }
  if (id === "lojas") return state.stores ? [enabledStores().length, ""] : null;
  return null;
}

function renderNav() {
  const nav = document.getElementById("nav");
  nav.replaceChildren(...VIEWS.flatMap((item) => {
    const count = navCount(item.id);
    return [
      item.section ? h("div", { class: "nav-section" }, item.section) : null,
      h("a", { href: routeHash(item.id), class: activeMenu() === item.id ? "on" : "", "aria-current": activeMenu() === item.id ? "page" : null },
        icon(item.icon), h("span", { class: "nav-label" }, item.label),
        count ? h("span", { class: `nav-count ${count[1]}`, title: item.id === "alertas" ? "Alertas hoje" : null }, count[0]) : null,
        h("kbd", { class: "nav-key" }, item.key)),
    ];
  }).filter(Boolean));
}

/** Seconds into the current wait between cycles, as a 0–1 fraction (for the bot card's bar). */
function cycleProgress() {
  const monitor = state.monitor;
  if (!monitor?.nextCheckAt || !state.settings) return 0;
  const total = Math.max(60, state.settings.pollIntervalSeconds + state.settings.pollJitterSeconds / 2);
  const remaining = Math.max(0, (new Date(monitor.nextCheckAt) - Date.now()) / 1000);
  return Math.min(1, Math.max(0, 1 - remaining / total));
}

function botState() {
  const monitor = state.monitor;
  if (!monitor) return ["off", "A ligar ao bot…"];
  if (monitor.isChecking) return ["busy", "A verificar lojas…"];
  if (monitor.paused) return ["paused", "Bot em pausa"];
  return ["on", "Bot a correr"];
}

function renderBotCard() {
  const card = document.getElementById("bot-card");
  const monitor = state.monitor;
  const [tone, label] = botState();
  if (!monitor) { card.replaceChildren(h("div", { class: "bot-state" }, h("span", { class: `bot-dot ${tone}` }), label)); return; }

  const last = monitor.lastCycle;
  const duration = last ? Math.round((new Date(last.finishedAt) - new Date(last.startedAt)) / 1000) : null;
  card.replaceChildren(
    h("div", { class: "bot-state" }, h("span", { class: `bot-dot ${tone}` }), label),
    h("div", { class: "bot-meta" },
      monitor.isChecking ? "A pesquisar em todas as lojas ativas."
        : monitor.paused ? "Não pesquisa até retomares."
          : monitor.nextCheckAt ? ["Próxima verificação em ", h("span", { class: "mono strong", id: "bot-countdown" }, countdown(monitor.nextCheckAt))] : "—",
      h("br"),
      last ? `Último ciclo: ${duration} s · ${last.searches} pesquisas` : "Ainda sem ciclos"),
    h("div", { class: "bar" }, h("span", { id: "bot-progress", style: `width:${monitor.isChecking ? 100 : Math.round(cycleProgress() * 100)}%`, class: monitor.isChecking ? "busy" : "" })),
    monitor.paused
      ? button("Retomar o bot", { iconName: "play", kind: "primary block", onClick: () => setPaused(false) })
      : button("Pausar o bot", { kind: "block", onClick: () => setPaused(true) }));
}

/** Every second: the countdowns only, not the whole chrome. */
function tickCountdowns() {
  const monitor = state.monitor;
  if (!monitor?.nextCheckAt || monitor.isChecking || monitor.paused) return;
  const text = countdown(monitor.nextCheckAt);
  const countdownElement = document.getElementById("bot-countdown");
  if (countdownElement) countdownElement.textContent = text;
  const progress = document.getElementById("bot-progress");
  if (progress) progress.style.width = `${Math.round(cycleProgress() * 100)}%`;
  const chip = document.getElementById("status-countdown");
  if (chip) chip.textContent = text;
}

function renderStatusChip() {
  const chip = document.getElementById("status-chip");
  const monitor = state.monitor;
  const [tone] = botState();
  if (!monitor) { chip.replaceChildren(h("span", { class: `bot-dot ${tone}` }), "A ligar…"); return; }
  chip.replaceChildren();
  append(chip, [h("span", { class: `bot-dot ${tone}` }),
    monitor.isChecking ? "A verificar…" : monitor.paused ? "Em pausa"
      : monitor.nextCheckAt ? ["A correr · ", h("span", { class: "mono", id: "status-countdown" }, countdown(monitor.nextCheckAt))] : "A correr"]);
}

function renderTabbar() {
  const tabs = [
    ["radar", "Radar", "radar"], ["a-vigiar", "A vigiar", "eye"], ["vigiar", "Vigiar", "plus"], ["alertas", "Alertas", "bell"], ["lojas", "Lojas", "store"],
  ];
  document.getElementById("tabbar").replaceChildren(...tabs.map(([id, label, iconName]) => {
    const on = id === "vigiar" ? state.route.view === "vigiar" : activeMenu() === id && state.route.view !== "vigiar";
    return h("a", { href: routeHash(id), class: on ? "on" : "", "aria-current": on ? "page" : null }, icon(iconName), h("span", {}, label));
  }));
}

function openSidebar() {
  document.getElementById("sidebar").classList.add("open");
  document.getElementById("sidebar-backdrop").classList.add("open");
}

function closeSidebar() {
  document.getElementById("sidebar").classList.remove("open");
  document.getElementById("sidebar-backdrop").classList.remove("open");
}

/* ----------------------------------------------------------------- theme -- */

const ACCENTS = {
  azul: ["Azul", "#8C96F0", "#4F5BD5"],
  coral: ["Coral", "#F08A73", "#C94A33"],
  lavanda: ["Lavanda", "#A891F5", "#7C5CE0"],
  oceano: ["Oceano", "#5BB4D6", "#1C7FA6"],
};
const MODES = { dark: "Escuro", light: "Claro", auto: "Automático" };

const theme = { accent: readPreference("accent", "azul"), mode: readPreference("mode", "dark") };
const lightQuery = window.matchMedia ? window.matchMedia("(prefers-color-scheme: light)") : null;

function applyTheme() {
  if (!ACCENTS[theme.accent]) theme.accent = "azul";
  if (!MODES[theme.mode]) theme.mode = "dark";
  const mode = theme.mode === "auto" ? (lightQuery?.matches ? "light" : "dark") : theme.mode;
  document.documentElement.setAttribute("data-accent", theme.accent);
  document.documentElement.setAttribute("data-mode", mode);
}

function setTheme(changes) {
  Object.assign(theme, changes);
  writePreference("accent", theme.accent);
  writePreference("mode", theme.mode);
  applyTheme();
}

lightQuery?.addEventListener?.("change", () => { if (theme.mode === "auto") applyTheme(); });

/* ----------------------------------------------------------- render loop -- */

const RENDERERS = {};                                    // filled by the view scripts: RENDERERS.radar = viewRadar …

function renderView(force = false) {
  if (!state.tasks || !state.stores || !state.monitor || !state.settings) return;
  const view = state.route.view;
  if (!force && !LIVE_VIEWS.has(view)) return;

  const signature = JSON.stringify([state.route, state.monitor, state.alerts.length, state.alerts[0]?.at, state.tasks, state.stores,
    state.watchGame, state.watchStatus, state.productOnlyCandidates, state.alertFilter, state.alertStore,
    state.storeCountry, state.storeTier, state.storeType, state.storeTag, state.storeTest]);
  if (!force && signature === state.renderedSignature) return;
  state.renderedSignature = signature;

  const container = document.getElementById("view");
  const scrollers = [...container.querySelectorAll("[data-keep-scroll]")].map((element) => [element.dataset.keepScroll, element.scrollTop]);
  const renderer = RENDERERS[view] || RENDERERS.radar;
  container.dataset.view = view;
  keepFocus(container, () => container.replaceChildren(...[renderer()].flat(Infinity).filter(Boolean)));

  for (const [key, top] of scrollers) {
    const element = container.querySelector(`[data-keep-scroll="${key}"]`);
    if (element) element.scrollTop = top;
  }
}
