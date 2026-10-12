"use strict";

/* ==========================================================================
   Shell: routes, sidebar, bot card, mobile bars, theme and the render loop.
   ========================================================================== */

/** The menu: what to buy (Shop) first, then how the bot works (Bot). */
const VIEWS = [
  { id: "products", label: "Products", icon: "grid", key: "1", section: "Shop" },
  { id: "radar", label: "Deals", icon: "radar", key: "2" },
  { id: "releases", label: "Releases", icon: "calendar", key: "3" },
  { id: "watching", label: "Watching", icon: "eye", key: "4", section: "Bot" },
  { id: "alerts", label: "Alerts", icon: "bell", key: "5" },
  { id: "stores", label: "Stores", icon: "store", key: "6" },
  { id: "settings", label: "Settings", icon: "sliders", key: "7" },
];

/** Routes that are not in the menu: they light up their parent entry. */
const SUB_ROUTES = { watched: "watching", watch: "watching", product: "products", "test-store": "stores" };

/** The home page. */
const HOME = "products";

/** Hashes of earlier versions (v3 English names, v4 Portuguese names), so old bookmarks keep working. */
const OLD_ROUTES = {
  deals: "radar", overview: "radar", tasks: "watching", compare: "products", probe: "test-store",
  "a-vigiar": "watching", produto: "watched", vigiar: "watch", alertas: "alerts", lojas: "stores", definicoes: "settings",
  testar: "test-store", produtos: "products", ficha: "product", lancamentos: "releases", comparar: "products",
};

/** Routes re-rendered by the poll; the others hold forms and only render on navigation. */
const LIVE_VIEWS = new Set(["radar", "watching", "watched", "alerts", "stores"]);

function parseHash() {
  const match = location.hash.match(/^#\/([\w-]+)(?:\/(.*))?$/);
  let view = match?.[1] || HOME;
  view = OLD_ROUTES[view] || view;
  const param = match?.[2] ? decodeURIComponent(match[2]) : "";
  const known = VIEWS.some((item) => item.id === view) || SUB_ROUTES[view];
  return known ? { view, param } : { view: HOME, param: "" };
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
  if (changed && document.getElementById("drawer").classList.contains("open")) closeDrawer();
  if (route.view === "settings" || route.view === "alerts") refreshNotification();
  if (route.view === "products" || (route.view === "product" && changed)) loadCatalog();
  if (route.view === "product" && changed) { state.fichaHidden = []; state.fichaShowSoldOut = false; if (state.ficha?.key !== route.param) state.ficha = null; }
  if (route.view === "releases") { loadReleases(); if (state.compare === null) loadCatalog(); }
  if (route.view !== "watch") state.wizard = null;
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
  document.getElementById("mobile-title").textContent = state.route.view === "watch" ? "Watch a product"
    : state.route.view === "test-store" ? "Test a store" : view?.label || "Holo";
}

const activeMenu = () => SUB_ROUTES[state.route.view] || state.route.view;

function navCount(id) {
  if (id === "radar") {
    const offers = currentOffers().filter((offer) => inGameTab(offer.task.game));
    return offers.length ? [offers.length, offers.some((offer) => offer.isNew) ? "accent" : ""] : null;
  }
  if (id === "watching") return state.tasks ? [state.tasks.length, ""] : null;
  if (id === "alerts") { const today = alertsToday(); return today > 0 ? [today, "accent"] : null; }
  if (id === "stores") return state.stores ? [enabledStores().length, ""] : null;
  if (id === "releases") {
    const open = (state.releases || []).filter((release) => releasePhase(release) === "preorder").length;
    return open ? [open, "accent"] : null;
  }
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
        count ? h("span", { class: `nav-count ${count[1]}`, title: item.id === "alerts" ? "Alerts today" : item.id === "releases" ? "Pre-orders open" : item.id === "radar" ? "Offers at your price" : null }, count[0]) : null,
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
  if (!monitor) return ["off", "Connecting to the bot…"];
  if (monitor.isChecking) return ["busy", "Checking stores…"];
  if (monitor.paused) return ["paused", "Bot paused"];
  return ["on", "Bot running"];
}

function renderBotCard() {
  const card = document.getElementById("bot-card");
  const monitor = state.monitor;
  const [tone, label] = botState();
  if (!monitor) { card.replaceChildren(h("div", { class: "bot-state" }, h("span", { class: `bot-dot ${tone}` }), label)); return; }

  const last = monitor.lastCycle;
  const duration = last ? Math.round((new Date(last.finishedAt) - new Date(last.startedAt)) / 1000) : null;
  card.replaceChildren(
    h("div", { class: "bot-state", title: label }, h("span", { class: `bot-dot ${tone}` }), label),
    h("div", { class: "bot-meta" },
      h("span", { class: "bot-next" }, monitor.isChecking ? "Searching every active store."
        : monitor.paused ? "Not searching until you resume."
          : monitor.nextCheckAt ? ["Next check in ", h("span", { class: "mono strong", id: "bot-countdown" }, countdown(monitor.nextCheckAt))] : "—"),
      h("span", { class: "bot-last" }, last ? `Last cycle: ${duration} s · ${last.searches} searches` : "No cycles yet")),
    h("div", { class: "bar" }, h("span", { id: "bot-progress", style: `width:${monitor.isChecking ? 100 : Math.round(cycleProgress() * 100)}%`, class: monitor.isChecking ? "busy" : "" })),
    monitor.paused
      ? button("Resume", { iconName: "play", kind: "primary block", title: "Resume the bot", onClick: () => setPaused(false) })
      : button("Pause", { iconName: "pause", kind: "block", title: "Pause the bot", onClick: () => setPaused(true) }));
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
  if (!monitor) { chip.replaceChildren(h("span", { class: `bot-dot ${tone}` }), "Connecting…"); return; }
  chip.replaceChildren();
  append(chip, [h("span", { class: `bot-dot ${tone}` }),
    monitor.isChecking ? "Checking…" : monitor.paused ? "Paused"
      : monitor.nextCheckAt ? ["Running · ", h("span", { class: "mono", id: "status-countdown" }, countdown(monitor.nextCheckAt))] : "Running"]);
}

function renderTabbar() {
  const tabs = [
    ["products", "Products", "grid"], ["radar", "Deals", "radar"], ["watch", "Watch", "plus"], ["watching", "Watching", "eye"], ["alerts", "Alerts", "bell"],
  ];
  document.getElementById("tabbar").replaceChildren(...tabs.map(([id, label, iconName]) => {
    const on = id === "watch" ? state.route.view === "watch" : activeMenu() === id && state.route.view !== "watch";
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
  azul: ["Holo", "#9AA9FF", "#4E5FD9"],
  coral: ["Coral", "#F08A73", "#C94A33"],
  lavanda: ["Lavender", "#A891F5", "#7C5CE0"],
  oceano: ["Ocean", "#5BB4D6", "#1C7FA6"],
};
const MODES = { dark: "Dark", light: "Light", auto: "Automatic" };

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
    state.game, state.watchStatus, state.productOnlyCandidates, state.alertFilter, state.alertStore,
    state.storeCountry, state.storeTier, state.storeType, state.storeTag, state.storeTest]);
  if (!force && signature === state.renderedSignature) return;
  state.renderedSignature = signature;

  const container = document.getElementById("view");
  const scrollers = [...container.querySelectorAll("[data-keep-scroll]")].map((element) => [element.dataset.keepScroll, element.scrollTop]);
  const renderer = RENDERERS[view] || RENDERERS[HOME];
  container.dataset.view = view;
  keepFocus(container, () => container.replaceChildren(...[renderer()].flat(Infinity).filter(Boolean)));

  for (const [key, top] of scrollers) {
    const element = container.querySelector(`[data-keep-scroll="${key}"]`);
    if (element) element.scrollTop = top;
  }
}
