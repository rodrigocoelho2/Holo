"use strict";

/* ==========================================================================
   TcgStockBot dashboard — vanilla JS, no build step.
   Talks only to the Website controllers (which call the WebApi through the Sdk).
   Everything coming from stores (titles, URLs) is inserted as text, never HTML.
   ========================================================================== */

const POLL_MS = Number(document.getElementById("app")?.dataset.pollMs) || 3000;

/** Per-browser view preferences (never data): localStorage can be unavailable, so always fall back. */
function readPreference(name, fallback) {
  try {
    const value = localStorage.getItem(`tcgstockbot.${name}`);
    return value === null ? fallback : JSON.parse(value);
  } catch {
    return fallback;
  }
}

function writePreference(name, value) {
  try { localStorage.setItem(`tcgstockbot.${name}`, JSON.stringify(value)); } catch { /* ignore */ }
}
const ANTIFORGERY_TOKEN = document.querySelector('meta[name="request-verification-token"]')?.content || "";

const state = {
  monitor: null,
  tasks: null,
  stores: null,
  settings: null,
  notification: null,
  alerts: [],
  view: "overview",
  taskFilter: "all",
  gameFilter: "all",
  compare: null,                                        // compared products (Comparar page)
  compareFilter: "",
  compareMode: "all",                                   // all | stock | drops
  compareSingles: false,                                // also show products found in one store only
  storeQuery: "",                                       // Lojas page filters
  storeCountry: "all",
  storeTier: "all",
  storeType: "all",
  storeTag: "",
  storeTest: "all",                                     // all | never | ok | failed | auto
  expandedTasks: new Set(),
  onlyProducts: readPreference("onlyProducts", true),   // global "Só produtos" switch
  storeFilter: new Map(),                               // per task/store override of the global switch
  probe: { storeName: "", query: "elite trainer box", results: null, errors: null, loading: false },
  lastCycleAt: null,
  renderedSignature: "",
};

/* ---------------------------------------------------------------- icons -- */

const ICONS = {
  home: '<svg viewBox="0 0 24 24"><path d="M3 11l9-7 9 7v9a1 1 0 0 1-1 1h-5v-6h-6v6H4a1 1 0 0 1-1-1z"/></svg>',
  tasks: '<svg viewBox="0 0 24 24"><path d="M9 6h11M9 12h11M9 18h11"/><path d="M4 6l1 1 2-2M4 12l1 1 2-2M4 18l1 1 2-2"/></svg>',
  store: '<svg viewBox="0 0 24 24"><path d="M4 9l1.5-5h13L20 9"/><path d="M4 9h16v2a3 3 0 0 1-5.3 2 3 3 0 0 1-5.4 0A3 3 0 0 1 4 11z"/><path d="M5 13v7h14v-7"/></svg>',
  bell: '<svg viewBox="0 0 24 24"><path d="M6 16V11a6 6 0 1 1 12 0v5l2 2H4z"/><path d="M10 20a2 2 0 0 0 4 0"/></svg>',
  flask: '<svg viewBox="0 0 24 24"><path d="M9 3h6M10 3v6L4.5 18.5A1.7 1.7 0 0 0 6 21h12a1.7 1.7 0 0 0 1.5-2.5L14 9V3"/><path d="M7.5 15h9"/></svg>',
  settings: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/></svg>',
  play: '<svg viewBox="0 0 24 24"><path d="M7 5l12 7-12 7z"/></svg>',
  stop: '<svg viewBox="0 0 24 24"><rect x="6" y="6" width="12" height="12" rx="2"/></svg>',
  pause: '<svg viewBox="0 0 24 24"><path d="M8 5v14M16 5v14"/></svg>',
  edit: '<svg viewBox="0 0 24 24"><path d="M4 20h4L19 9l-4-4L4 16z"/><path d="M13.5 6.5l4 4"/></svg>',
  trash: '<svg viewBox="0 0 24 24"><path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3"/></svg>',
  plus: '<svg viewBox="0 0 24 24"><path d="M12 5v14M5 12h14"/></svg>',
  close: '<svg viewBox="0 0 24 24"><path d="M6 6l12 12M18 6L6 18"/></svg>',
  external: '<svg viewBox="0 0 24 24"><path d="M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5"/></svg>',
  chevron: '<svg viewBox="0 0 24 24"><path d="M9 6l6 6-6 6"/></svg>',
  check: '<svg viewBox="0 0 24 24"><path d="M5 12l5 5 9-10"/></svg>',
  alert: '<svg viewBox="0 0 24 24"><path d="M12 9v4M12 17h.01"/><path d="M10.3 3.9L2 18a2 2 0 0 0 1.7 3h16.6a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/></svg>',
  box: '<svg viewBox="0 0 24 24"><path d="M21 8l-9-5-9 5 9 5z"/><path d="M3 8v8l9 5 9-5V8M12 13v8"/></svg>',
  search: '<svg viewBox="0 0 24 24"><circle cx="11" cy="11" r="7"/><path d="M20 20l-3.5-3.5"/></svg>',
  clock: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>',
  mail: '<svg viewBox="0 0 24 24"><rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 7l9 6 9-6"/></svg>',
  chat: '<svg viewBox="0 0 24 24"><path d="M4 20l1.4-4.2A8 8 0 1 1 8.2 18.6z"/><path d="M9 10h.01M12 10h.01M15 10h.01"/></svg>',
  refresh: '<svg viewBox="0 0 24 24"><path d="M20 11a8 8 0 0 0-14.9-3M4 5v4h4M4 13a8 8 0 0 0 14.9 3M20 19v-4h-4"/></svg>',
  arrowUp: '<svg viewBox="0 0 24 24"><path d="M7 17L17 7M9 7h8v8"/></svg>',
  arrowDown: '<svg viewBox="0 0 24 24"><path d="M7 7l10 10M17 9v8H9"/></svg>',
  chart: '<svg viewBox="0 0 24 24"><path d="M4 19h16"/><path d="M5 15l4-5 4 3 6-7"/></svg>',
  compare: '<svg viewBox="0 0 24 24"><path d="M12 4v16M7 20h10M5 7h14"/><path d="M5 7l-3 6a3 3 0 0 0 6 0zM19 7l-3 6a3 3 0 0 0 6 0z"/></svg>',
  copy: '<svg viewBox="0 0 24 24"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V5a1 1 0 0 0-1-1H5a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h3"/></svg>',
};

/** Static, trusted SVG markup only. */
function icon(name) {
  const template = document.createElement("template");
  template.innerHTML = ICONS[name] || ICONS.box;
  return template.content.firstElementChild;
}

/* ------------------------------------------------------------ DOM helper -- */

const PROPERTY_KEYS = new Set(["value", "checked", "disabled", "selected", "readOnly", "type", "htmlFor"]);

function h(tag, props, ...children) {
  const element = document.createElement(tag);
  for (const [key, value] of Object.entries(props || {})) {
    if (value === null || value === undefined || value === false) continue;
    if (key === "class") element.className = value;
    else if (key.startsWith("on") && typeof value === "function") element.addEventListener(key.slice(2), value);
    else if (PROPERTY_KEYS.has(key)) element[key] = value;
    else if (key === "href") { const safe = safeHref(value); if (safe) element.setAttribute("href", safe); }
    else element.setAttribute(key, value === true ? "" : String(value));
  }
  append(element, children);
  return element;
}

function append(parent, children) {
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) continue;
    parent.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return parent;
}

function svg(tag, attributes, ...children) {
  const element = document.createElementNS("http://www.w3.org/2000/svg", tag);
  for (const [key, value] of Object.entries(attributes || {})) element.setAttribute(key, String(value));
  for (const child of children.flat()) {
    if (child === null || child === undefined) continue;
    element.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return element;
}

function safeHref(url) {
  if (typeof url !== "string") return null;
  if (url.startsWith("#")) return url;
  return /^https?:\/\//i.test(url) ? url : null;
}

function externalLink(url, text) {
  const href = safeHref(url);
  return href
    ? h("a", { href, target: "_blank", rel: "noopener noreferrer", title: url }, text)
    : h("span", {}, text);
}

/* ------------------------------------------------------------------ API -- */

class ApiError extends Error {
  constructor(errors) {
    super(errors.join(" "));
    this.errors = errors;
  }
}

/**
 * Calls a Website action. Every action answers an AjaxDataResult { isValid, validationErrors, data }.
 * POSTs carry the anti-forgery token.
 */
async function api(path, options = {}) {
  const method = options.method || "GET";
  const headers = {};
  if (method !== "GET") headers.RequestVerificationToken = ANTIFORGERY_TOKEN;
  if (options.body !== undefined) headers["Content-Type"] = "application/json";

  let response;
  try {
    response = await fetch(path, {
      method,
      headers,
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
      credentials: "same-origin",
    });
  } catch {
    throw new ApiError(["Sem ligação ao dashboard."]);
  }

  const text = await response.text();
  const result = text ? safeJson(text) : null;

  if (!response.ok || !result) {
    throw new ApiError([result?.title || `Erro ${response.status}`]);
  }
  if (!result.isValid) {
    throw new ApiError((result.validationErrors || []).map((error) => error.message).filter(Boolean));
  }
  return result.data;
}

const post = (path, body) => api(path, { method: "POST", body: body === undefined ? {} : body });

function safeJson(text) {
  try { return JSON.parse(text); } catch { return null; }
}

/* ------------------------------------------------------------ formatting -- */

const moneyFormat = new Intl.NumberFormat("pt-PT", { style: "currency", currency: "EUR" });
const timeFormat = new Intl.DateTimeFormat("pt-PT", { hour: "2-digit", minute: "2-digit" });
const dateTimeFormat = new Intl.DateTimeFormat("pt-PT", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" });
const longDateFormat = new Intl.DateTimeFormat("pt-PT", { day: "numeric", month: "long" });
const dayFormat = new Intl.DateTimeFormat("pt-PT", { day: "2-digit", month: "2-digit" });

const money = (value) => (value === null || value === undefined ? "—" : moneyFormat.format(value));
const clock = (iso) => (iso ? timeFormat.format(new Date(iso)) : "—");
const dateTime = (iso) => (iso ? dateTimeFormat.format(new Date(iso)) : "—");

function relative(iso) {
  if (!iso) return "nunca";
  const seconds = Math.round((Date.now() - new Date(iso).getTime()) / 1000);
  if (seconds < 10) return "agora mesmo";
  if (seconds < 60) return `há ${seconds} s`;
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `há ${minutes} min`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `há ${hours} h`;
  return `há ${Math.round(hours / 24)} dias`;
}

function countdown(iso) {
  const seconds = Math.max(0, Math.round((new Date(iso).getTime() - Date.now()) / 1000));
  const minutes = Math.floor(seconds / 60);
  return `${String(minutes).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
}

function slugify(text) {
  return text.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase()
    .replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 40);
}

const splitList = (text) => text.split(",").map((item) => item.trim()).filter(Boolean);

/* ---------------------------------------------------------- vocabulary -- */

const OUTCOMES = {
  Match: ["Corresponde", "green"],
  MatchLanguageToConfirm: ["Corresponde · confirmar idioma", "green"],
  MissingKeyword: ["Não é o produto", "grey"],
  ExcludedKeyword: ["Excluído", "grey"],
  NotEnglish: ["Não é EN", "amber"],
  NotJapanese: ["Não é JP", "amber"],
  LanguageUnknown: ["Idioma ?", "amber"],
  OutOfStock: ["Esgotado", "red"],
  NoPrice: ["Sem preço", "amber"],
  AboveMaxPrice: ["Acima do preço", "amber"],
};

const isMatch = (outcome) => outcome === "Match" || outcome === "MatchLanguageToConfirm";

/** "Não é o produto": the store's search returned something unrelated (hidden while "Só produtos" is on). */
const isIrrelevant = (listing) => listing.outcome === "MissingKeyword";

/** Whether one store of one task shows only products: its own switch if touched, otherwise the global one. */
const showsOnlyProducts = (key) => (state.storeFilter.has(key) ? state.storeFilter.get(key) : state.onlyProducts);

/** A small labelled switch, used for "Só produtos". */
function labelledSwitch(checked, label, onChange) {
  return h("label", { class: "inline-switch" }, switchInput(checked, onChange, label), h("span", {}, label));
}

const LANGUAGES = { English: ["EN", "green"], Japanese: ["JP", "violet"], NonEnglish: ["Outro", "amber"], Unknown: ["?", "grey"] };

const TASK_LANGUAGES = { English: ["EN", "Inglês"], Japanese: ["JP", "Japonês"] };

/** The card games a task can belong to (TcgGame), with their pill colour. */
const GAMES = { Pokemon: ["Pokémon", "amber"], OnePiece: ["One Piece", "red"], Lorcana: ["Lorcana", "violet"], Magic: ["Magic", "blue"], Other: ["Outro", "grey"] };

const gamePill = (game) => {
  const [label, tone] = GAMES[game] || GAMES.Pokemon;
  return h("span", { class: `pill plain ${tone}` }, label);
};

/** The product photo the store returned (ImageUrl), or an empty square of the same size. */
function productThumb(url, size = "") {
  const src = safeHref(url);
  return src
    ? h("img", { class: `thumb ${size}`, src, alt: "", loading: "lazy", decoding: "async", referrerpolicy: "no-referrer" })
    : h("span", { class: `thumb empty ${size}`, "aria-hidden": "true" });
}

const taskLanguagePill = (language) => {
  const [code, label] = TASK_LANGUAGES[language] || TASK_LANGUAGES.English;
  return h("span", { class: `pill plain ${language === "Japanese" ? "violet" : "blue"}`, title: label }, code);
};

/** Two-option switch (Inglês | Japonês). */
function languageSwitch(value, onChange) {
  const container = h("div", { class: "segmented", role: "radiogroup", "aria-label": "Idioma do produto" });
  const render = (current) => container.replaceChildren(...Object.entries(TASK_LANGUAGES).map(([key, [code, label]]) =>
    h("button", {
      type: "button",
      class: current === key ? "active" : "",
      role: "radio",
      "aria-checked": current === key ? "true" : "false",
      onclick: () => { onChange(key); render(key); },
    }, h("span", { class: "code" }, code), label)));
  render(value);
  return container;
}

const UNMARKED_LANGUAGE = {
  Unknown: ["Não avisar", "não avisar (idioma desconhecido)"],
  English: ["Contar como inglês", "conta como inglês"],
  Confirm: ["Avisar com idioma por confirmar", "avisa · confirmar idioma"],
};

const PLATFORMS = {
  Shopify: "Shopify — pesquisa pública em /search/suggest.json",
  WooCommerce: "WooCommerce — Store API /wp-json/wc/store/…",
  Html: "HTML — lê a página de pesquisa/categoria com seletores CSS",
  Amazon: "Amazon — API oficial Creators API (credenciais no .env)",
  Fnac: "FNAC — só com feed de afiliados Awin autorizado",
  Worten: "Worten — feed de afiliados Awin",
  ElCorteIngles: "El Corte Inglés — feed de afiliados Awin",
  Awin: "Awin (genérico) — feed de afiliados de qualquer outra loja",
  Manual: "Sem integração — só na lista (link e etiquetas), nunca pesquisada",
};
const COUNTRIES = { Portugal: ["Portugal", "🇵🇹"], Spain: ["Espanha", "🇪🇸"], Europe: ["Europa / online", "🇪🇺"] };
const TIERS = {
  S: ["Tier S", "amber", "MSRP / grande retalho"],
  A: ["Tier A", "green", "Especialistas TCG"],
  B: ["Tier B", "blue", "Importadores e lojas online"],
  C: ["Tier C", "grey", "Marketplaces — só referência"],
  None: ["Sem tier", "grey", "Por classificar"],
};
const STORE_TEST = {
  Ok: ["OK", "green"], Empty: ["0 produtos", "amber"], Blocked: ["Bloqueia o bot", "red"], RateLimited: ["Limite de pedidos", "amber"],
  Unreachable: ["Sem ligação", "red"], NoIntegration: ["Sem integração", "grey"], NotConfigured: ["Por configurar", "grey"], Error: ["Erro", "red"],
};
/** Test outcomes that mean "this store does not work as configured". */
const TEST_FAILED = new Set(["Blocked", "Unreachable", "Error", "RateLimited"]);
const testPill = (test) => { const [label, tone] = STORE_TEST[test.status] || [test.status, "grey"]; return h("span", { class: `pill ${tone}`, title: test.message || "" }, label); };
const STORE_TYPES = {
  Retail: "Retalho", Specialist: "Especialista TCG", Importer: "Importador", Marketplace: "Marketplace", Official: "Oficial", Unknown: "Sem tipo",
};
const MSRP_BASIS = {
  Official: ["Oficial", "green"], LaunchPrice: ["Preço de lançamento", "blue"], Estimate: ["Estimativa", "amber"],
};

const AWIN_FEED_KEYS = { Fnac: "Fnac", Worten: "Worten", ElCorteIngles: "ElCorteIngles" };

const outcomePill = (outcome) => {
  const [label, tone] = OUTCOMES[outcome] || [outcome, "grey"];
  return h("span", { class: `pill ${tone}` }, label);
};

const languagePill = (language) => {
  const [label, tone] = LANGUAGES[language] || [language, "grey"];
  return h("span", { class: `pill plain ${tone}` }, label);
};

const STORE_COLORS = ["#8b5cf6", "#60a5fa", "#34d399", "#f472b6", "#fbbf24", "#22d3ee", "#fb7185", "#a3e635"];

function storeColor(name) {
  let hash = 0;
  for (const character of name || "") hash = (hash * 31 + character.charCodeAt(0)) >>> 0;
  return STORE_COLORS[hash % STORE_COLORS.length];
}

function storeAvatar(name) {
  const initials = (name || "?").replace(/[^\p{L}\p{N} ]/gu, "").split(" ").filter(Boolean).slice(0, 2).map((word) => word[0]).join("").toUpperCase() || "?";
  return h("span", { class: "store-avatar", style: `background:${storeColor(name)}` }, initials);
}

/* --------------------------------------------------------- derived data -- */

const searches = () => state.monitor?.searches || [];
const enabledStores = () => (state.stores || []).filter((store) => store.enabled);
const sameName = (a, b) => a.toLowerCase() === b.toLowerCase();

function storesForTask(task) {
  const names = task.storeNames || [];
  return enabledStores().filter((store) => names.length === 0 || names.some((name) => sameName(name, store.name)));
}

function searchFor(task, storeName) {
  return searches().find((search) => search.stockTaskId === task.id && search.storeName === storeName);
}

function taskSearches(task) {
  return storesForTask(task).map((store) => searchFor(task, store.name)).filter(Boolean);
}

function taskStatus(task) {
  if (!task.enabled) return { key: "stopped", label: "Parada", tone: "grey" };
  if (storesForTask(task).length === 0) return { key: "stopped", label: "Sem lojas", tone: "grey" };

  const reports = taskSearches(task);
  if (reports.some((report) => report.matchCount > 0)) return { key: "instock", label: "Em stock!", tone: "green pulse" };
  if (state.monitor?.paused) return { key: "running", label: "Em pausa", tone: "amber" };
  if (reports.length === 0) return { key: "running", label: "À espera", tone: "blue" };

  const failed = reports.filter((report) => !report.succeeded).length;
  if (failed === reports.length) return { key: "error", label: "Erro", tone: "red" };
  if (failed > 0) return { key: "error", label: `${failed} loja(s) c/ erro`, tone: "amber" };
  return { key: "running", label: "A monitorizar", tone: "violet pulse" };
}

/* ----------------------------------------------------------- navigation -- */

const VIEWS = [
  { id: "overview", label: "Visão geral", icon: "home", key: "1", section: "Monitor" },
  { id: "tasks", label: "Tarefas", icon: "tasks", key: "2" },
  { id: "stores", label: "Lojas", icon: "store", key: "3" },
  { id: "alerts", label: "Alertas", icon: "bell", key: "4" },
  { id: "compare", label: "Comparar", icon: "compare", key: "5" },
  { id: "probe", label: "Testar loja", icon: "flask", key: "6", section: "Ferramentas" },
  { id: "settings", label: "Definições", icon: "settings", key: "7" },
];

/** Views re-rendered by the poll; the others hold form inputs and only render on navigation. */
const LIVE_VIEWS = new Set(["overview", "tasks", "stores", "alerts"]);

function navigate(view) {
  if (!VIEWS.some((item) => item.id === view)) view = "overview";
  state.view = view;
  if (location.hash !== `#/${view}`) history.replaceState(null, "", `#/${view}`);
  document.getElementById("sidebar").classList.remove("open");
  if (view === "settings") refreshNotification();
  if (view === "compare") loadCompare();
  renderChrome();
  renderView(true);
  window.scrollTo({ top: 0 });
}

function renderChrome() {
  renderNav();
  renderBotCard();
  renderBreadcrumb();
  renderNextCheck();
}

function renderNav() {
  const nav = document.getElementById("nav");
  const newToday = alertsToday();
  nav.replaceChildren(...VIEWS.flatMap((item) => [
    item.section ? h("div", { class: "nav-label" }, item.section) : null,
    h("a", { href: `#/${item.id}`, class: state.view === item.id ? "active" : "" },
      icon(item.icon),
      item.label,
      item.id === "alerts" && newToday > 0 ? h("span", { class: "badge", title: "Alertas hoje" }, newToday) : null,
      h("kbd", {}, item.key)),
  ]).filter(Boolean));
}

function renderBotCard() {
  const card = document.getElementById("bot-card");
  const monitor = state.monitor;
  if (!monitor) { card.replaceChildren(h("div", { class: "bot-card-meta" }, "A ligar ao bot…")); return; }

  const [dotTone, label] = monitor.isChecking ? ["violet", "A verificar…"] : monitor.paused ? ["amber", "Em pausa"] : ["green", "A monitorizar"];
  const running = (state.tasks || []).filter((task) => task.enabled).length;

  card.replaceChildren(
    h("div", { class: "bot-card-state" }, h("span", { class: `dot ${dotTone}` }), label),
    h("div", { class: "bot-card-meta" }, `${running} tarefa(s) · ${enabledStores().length} loja(s) ativas`),
    monitor.paused
      ? h("button", { class: "button primary block small", onclick: () => setPaused(false) }, icon("play"), "Retomar")
      : h("button", { class: "button block small", onclick: () => setPaused(true) }, icon("pause"), "Pausar"),
  );
}

function renderBreadcrumb() {
  const view = VIEWS.find((item) => item.id === state.view);
  document.getElementById("breadcrumb").replaceChildren(
    icon(view.icon), h("span", {}, "Início"), h("span", { class: "separator" }, "/"), h("strong", {}, view.label));
}

function renderNextCheck() {
  const chip = document.getElementById("next-check-chip");
  const monitor = state.monitor;
  if (!monitor) { chip.replaceChildren("A ligar…"); return; }
  if (monitor.isChecking) chip.replaceChildren(icon("refresh"), h("strong", {}, "A verificar lojas…"));
  else if (monitor.paused) chip.replaceChildren(icon("pause"), "Em pausa");
  else if (monitor.nextCheckAt) chip.replaceChildren(icon("clock"), "Próxima em ", h("strong", {}, countdown(monitor.nextCheckAt)));
  else chip.replaceChildren(icon("clock"), "—");
}

/* ---------------------------------------------------------------- views -- */

function renderView(force = false) {
  if (!state.tasks || !state.stores || !state.monitor || !state.settings) return;
  if (!force && !LIVE_VIEWS.has(state.view)) return;

  const signature = JSON.stringify([state.view, state.monitor, state.alerts.length, state.tasks, state.stores, state.taskFilter, [...state.expandedTasks],
    state.storeCountry, state.storeTier, state.storeType, state.storeTag, state.storeTest]);
  if (!force && signature === state.renderedSignature) return;
  state.renderedSignature = signature;

  const renderers = { overview: viewOverview, tasks: viewTasks, stores: viewStores, alerts: viewAlerts, compare: viewCompare, probe: viewProbe, settings: viewSettings };
  const container = document.getElementById("view");
  const scrollers = [...container.querySelectorAll("[data-keep-scroll]")].map((element) => [element.dataset.keepScroll, element.scrollTop]);

  container.replaceChildren(...[renderers[state.view]()].flat());

  for (const [key, top] of scrollers) {
    const element = container.querySelector(`[data-keep-scroll="${key}"]`);
    if (element) element.scrollTop = top;
  }
}

function greeting() {
  const hour = new Date().getHours();
  return hour < 12 ? "Bom dia" : hour < 20 ? "Boa tarde" : "Boa noite";
}

function alertsToday() {
  const today = new Date().toDateString();
  return state.alerts.filter((alert) => new Date(alert.at).toDateString() === today).length;
}

/* ----- Overview ----- */

function viewOverview() {
  const lastCycle = state.monitor.lastCycle;
  const tasks = state.tasks;
  const runningTasks = tasks.filter((task) => task.enabled).length;
  const inStockNow = searches().reduce((total, search) => total + search.matchCount, 0);
  const tasksInStock = tasks.filter((task) => taskStatus(task).key === "instock").length;
  const lastAlert = state.alerts[0];
  const today = alertsToday();
  const failedLast = lastCycle?.failed || 0;
  const durationSeconds = lastCycle ? Math.round((new Date(lastCycle.finishedAt) - new Date(lastCycle.startedAt)) / 1000) : null;

  return [
    h("div", { class: "page-head" },
      h("div", {},
        h("h1", {}, `${greeting()}.`, h("span", { class: "date" }, `  /  ${longDateFormat.format(new Date())}`)),
        h("p", {}, "Stock de Pokémon TCG em inglês, ao preço de tabela, nas lojas que estás a seguir."))),

    h("div", { class: "grid stats" },
      statCard({
        label: "Alertas enviados", value: state.alerts.length, iconName: "bell", tone: "violet",
        delta: today > 0 ? [`+${today} hoje`, "up"] : ["Nenhum hoje", ""],
        foot: [["Último alerta", lastAlert ? lastAlert.storeName : "—"], ["Quando", lastAlert ? relative(lastAlert.at) : "—"]],
      }),
      statCard({
        label: "Em stock agora", value: inStockNow, iconName: "box", tone: inStockNow > 0 ? "green" : "blue",
        delta: tasksInStock > 0 ? [`${tasksInStock} tarefa(s) com stock`, "up"] : ["Nada disponível ao preço", ""],
        foot: [["Tarefas a correr", `${runningTasks} de ${tasks.length}`], ["Lojas ativas", `${enabledStores().length} de ${state.stores.length}`]],
      }),
      statCard({
        label: "Pesquisas no último ciclo", value: lastCycle ? `${lastCycle.searches - lastCycle.failed}/${lastCycle.searches}` : "—",
        iconName: "search", tone: failedLast > 0 ? "red" : "green",
        delta: !lastCycle ? ["Ainda sem ciclos", ""] : failedLast > 0 ? [`${failedLast} com erro`, "down"] : ["Sem erros", "up"],
        foot: [["Última verificação", lastCycle ? relative(lastCycle.finishedAt) : "—"], ["Duração", durationSeconds !== null ? `${durationSeconds} s` : "—"]],
      })),

    h("div", { class: "grid overview-top" },
      alertsChartCard(),
      h("div", { class: "stack" },
        storeDonutCard(),
        h("div", { class: "grid tiles" },
          tile("tasks", "violet", "Tarefas", tasks.length),
          tile("play", "green", "A correr", runningTasks),
          tile("store", "blue", "Lojas ativas", enabledStores().length),
          tile("alert", failedLast > 0 ? "red" : "amber", "Erros (último ciclo)", failedLast)))),

    h("div", { class: "grid overview-bottom" }, recentAlertsCard(8), feedCard()),
  ];
}

function statCard({ label, value, iconName, tone, delta, foot }) {
  const [deltaText, deltaTone] = delta;
  return h("div", { class: "card stat" },
    h("div", { class: "stat-top" },
      h("div", {}, h("div", { class: "stat-label" }, label), h("div", { class: "stat-value" }, value)),
      h("div", { class: `icon-badge ${tone}` }, icon(iconName))),
    h("div", { class: `stat-delta ${deltaTone}` }, deltaTone === "up" ? icon("arrowUp") : deltaTone === "down" ? icon("arrowDown") : null, deltaText),
    h("div", { class: "stat-foot" }, foot.map(([footLabel, footValue]) =>
      h("div", {}, h("div", { class: "label" }, footLabel), h("div", { class: "value", title: footValue }, footValue)))));
}

function tile(iconName, tone, label, value) {
  return h("div", { class: "tile" },
    h("div", { class: `icon-badge ${tone}` }, icon(iconName)),
    h("div", {}, h("div", { class: "label" }, label), h("div", { class: "value" }, value)));
}

function alertsChartCard() {
  const days = 14;
  const buckets = [];
  for (let offset = days - 1; offset >= 0; offset--) {
    const day = new Date();
    day.setHours(0, 0, 0, 0);
    day.setDate(day.getDate() - offset);
    buckets.push({ day, count: 0 });
  }
  for (const alert of state.alerts) {
    const at = new Date(alert.at);
    at.setHours(0, 0, 0, 0);
    const bucket = buckets.find((item) => item.day.getTime() === at.getTime());
    if (bucket) bucket.count++;
  }
  const total = buckets.reduce((sum, bucket) => sum + bucket.count, 0);
  const values = buckets.map((bucket) => bucket.count);
  const labels = buckets.map((bucket) => dayFormat.format(bucket.day));

  // The chart fills whatever height the row gets from the right-hand column, and is redrawn at the
  // box's real pixel size so axis text never stretches.
  const box = h("div", { class: "chart-box" });
  new ResizeObserver(() => {
    const { width, height } = box.getBoundingClientRect();
    if (width > 20 && height > 20) box.replaceChildren(lineChart(values, labels, Math.round(width), Math.round(height)));
  }).observe(box);

  return h("div", { class: "card chart-card" },
    h("div", { class: "card-head" }, h("span", { class: "card-title" }, "Alertas"), h("div", { class: "actions" }, h("span", { class: "chip" }, "Últimos 14 dias"))),
    h("div", { class: "card-body" },
      h("div", { class: "chart-head" },
        h("div", {}, h("div", { class: "chart-value" }, total), h("div", { class: "muted" }, total === 1 ? "alerta" : "alertas")),
        h("div", { class: "muted" }, `${alertsToday()} hoje`)),
      box));
}

/** Draws the line chart at an exact pixel size (width × height). */
function lineChart(values, labels, width, height) {
  const left = 34, right = 14, top = 12, bottom = 26;
  const max = Math.max(1, ...values);
  const steps = max <= 4 ? max : 4;
  const x = (index) => left + (index * (width - left - right)) / (values.length - 1);
  const y = (value) => top + (1 - value / max) * (height - top - bottom);
  const points = values.map((value, index) => [x(index), y(value)]);
  const line = points.map(([px, py], index) => `${index ? "L" : "M"}${px.toFixed(1)},${py.toFixed(1)}`).join(" ");
  const area = `${line} L${x(values.length - 1)},${height - bottom} L${x(0)},${height - bottom} Z`;

  const grid = [];
  for (let step = 0; step <= steps; step++) {
    const value = Math.round((max * step) / steps);
    grid.push(svg("line", { class: "grid-line", x1: left, x2: width - right, y1: y(value), y2: y(value) }));
    grid.push(svg("text", { x: left - 8, y: y(value) + 4, "text-anchor": "end" }, value));
  }

  return svg("svg", { class: "chart", viewBox: `0 0 ${width} ${height}`, role: "img", "aria-label": "Alertas por dia" },
    svg("defs", {},
      svg("linearGradient", { id: "area-fill", x1: 0, x2: 0, y1: 0, y2: 1 },
        svg("stop", { offset: "0%", "stop-color": "#8b5cf6", "stop-opacity": 0.38 }),
        svg("stop", { offset: "100%", "stop-color": "#8b5cf6", "stop-opacity": 0 }))),
    grid,
    svg("path", { d: area, fill: "url(#area-fill)", stroke: "none" }),
    svg("path", { class: "line", d: line }),
    points.map(([px, py], index) => values[index] > 0 ? svg("circle", { class: "point", cx: px, cy: py, r: 3.5 }, svg("title", {}, `${labels[index]}: ${values[index]}`)) : null),
    labels.map((label, index) => index % 2 === 1 || index === labels.length - 1
      ? svg("text", { x: x(index), y: height - 8, "text-anchor": "middle" }, label) : null));
}

function storeDonutCard() {
  let title = "Alertas por loja";
  let centerLabel = "alertas";
  const counts = new Map();
  for (const alert of state.alerts) counts.set(alert.storeName, (counts.get(alert.storeName) || 0) + 1);

  if (counts.size === 0) {
    title = "Resultados por loja";
    centerLabel = "resultados";
    for (const search of searches()) counts.set(search.storeName, (counts.get(search.storeName) || 0) + search.listings.length);
  }

  const entries = [...counts.entries()].filter(([, count]) => count > 0).sort((a, b) => b[1] - a[1]);
  const total = entries.reduce((sum, [, count]) => sum + count, 0);

  const body = total === 0
    ? emptyState("store", "Ainda sem dados — a primeira verificação preenche isto.")
    : h("div", { class: "donut-layout" }, donut(entries, total, centerLabel),
      h("div", { class: "legend" }, entries.slice(0, 6).map(([name, count]) =>
        h("div", { class: "legend-row" },
          h("span", { class: "swatch", style: `background:${storeColor(name)}` }),
          h("span", { class: "name", title: name }, name),
          h("span", { class: "share" }, `${Math.round((count / total) * 100)}%`),
          h("span", { class: "count" }, count)))));

  return h("div", { class: "card" }, h("div", { class: "card-head" }, h("span", { class: "card-title" }, title)), h("div", { class: "card-body" }, body));
}

function donut(entries, total, centerLabel) {
  const radius = 62, stroke = 16, circumference = 2 * Math.PI * radius, gap = entries.length > 1 ? 3 : 0;
  let offset = 0;
  const segments = entries.map(([name, count]) => {
    const length = (count / total) * circumference;
    const segment = svg("circle", {
      cx: 85, cy: 85, r: radius, fill: "none", stroke: storeColor(name), "stroke-width": stroke,
      "stroke-dasharray": `${Math.max(0, length - gap)} ${circumference}`, "stroke-dashoffset": -offset,
      transform: "rotate(-90 85 85)", style: `filter:drop-shadow(0 0 5px ${storeColor(name)}66)`,
    }, svg("title", {}, `${name}: ${count}`));
    offset += length;
    return segment;
  });

  return svg("svg", { class: "donut", viewBox: "0 0 170 170" },
    svg("circle", { class: "track", cx: 85, cy: 85, r: radius, fill: "none", "stroke-width": stroke }),
    segments,
    svg("text", { class: "total", x: 85, y: 88, "text-anchor": "middle" }, total),
    svg("text", { class: "total-label", x: 85, y: 108, "text-anchor": "middle" }, centerLabel));
}

function recentAlertsCard(limit) {
  const rows = state.alerts.slice(0, limit);
  return h("div", { class: "card" },
    h("div", { class: "card-head" }, h("span", { class: "card-title" }, "Alertas recentes"),
      h("div", { class: "actions" }, h("a", { href: "#/alerts", class: "button ghost small" }, "Ver todos", icon("chevron")))),
    h("div", { class: "card-body flush" }, rows.length === 0
      ? emptyState("bell", "Ainda nenhum alerta. Quando um produto EN aparecer ao preço, surge aqui.")
      : alertsTable(rows, false)));
}

function feedCard() {
  const events = state.monitor.events.slice(0, 80);
  return h("div", { class: "card" },
    h("div", { class: "card-head" }, h("span", { class: "card-title" }, "Feed do monitor"),
      h("div", { class: "actions" }, h("span", { class: "pill plain grey" }, `${events.length} eventos`))),
    h("div", { class: "card-body flush" }, events.length === 0
      ? emptyState("clock", "Sem atividade ainda.")
      : h("ul", { class: "feed", "data-keep-scroll": "feed" }, events.map((event) =>
        h("li", { class: event.level },
          h("time", {}, clock(event.at)),
          h("span", { class: "marker" }),
          h("span", { class: "message" }, event.message))))));
}

function alertsTable(rows, full) {
  return h("div", { class: "table-wrap" }, h("table", {},
    h("thead", {}, h("tr", {},
      h("th", {}, "Loja"), h("th", {}, "Produto"), full ? h("th", {}, "Tarefa") : null,
      h("th", { class: "num" }, "Preço"), h("th", { class: "num" }, full ? "Data" : "Hora"), h("th", {}))),
    h("tbody", {}, rows.map((alert) => h("tr", {},
      h("td", { class: "nowrap" }, h("span", { class: "store-link" }, storeAvatar(alert.storeName), alert.storeName)),
      h("td", { class: "title-cell" }, h("div", { class: "with-thumb" }, productThumb(alert.imageUrl),
        h("div", {},
          h("span", { class: "primary-text", title: alert.title }, alert.title),
          h("div", { class: "pill-row" },
            alert.previousPrice !== null && alert.previousPrice !== undefined ? h("span", { class: "pill blue" }, "Baixou de preço") : null,
            alert.preOrder ? h("span", { class: "pill violet" }, "Pré-venda") : null,
            alert.languageToConfirm ? h("span", { class: "pill amber" }, "Confirmar idioma") : null)))),
      full ? h("td", {}, alert.stockTaskName) : null,
      h("td", { class: "num" }, money(alert.price),
        alert.previousPrice !== null && alert.previousPrice !== undefined ? h("div", { class: "was-price" }, money(alert.previousPrice)) : null,
        msrpDelta(alert.price, alert.msrp)),
      h("td", { class: "num nowrap" }, full ? dateTime(alert.at) : clock(alert.at)),
      h("td", { class: "actions" }, safeHref(alert.url)
        ? h("a", { class: "button small", href: alert.url, target: "_blank", rel: "noopener noreferrer" }, "Abrir", icon("external"))
        : null))))));
}

function emptyState(iconName, text) {
  return h("div", { class: "empty" }, icon(iconName), h("div", {}, text));
}

/* ----- Tasks ----- */

function viewTasks() {
  const tasks = state.tasks;
  const withStatus = tasks.map((task) => ({ task, status: taskStatus(task) }));
  const counts = {
    all: tasks.length,
    running: withStatus.filter((item) => item.task.enabled).length,
    stopped: withStatus.filter((item) => !item.task.enabled).length,
    instock: withStatus.filter((item) => item.status.key === "instock").length,
    error: withStatus.filter((item) => item.status.key === "error").length,
  };
  const filters = [["all", "Todas"], ["running", "A correr"], ["instock", "Em stock"], ["error", "Com erros"], ["stopped", "Paradas"]];
  const visible = withStatus.filter(({ task, status }) =>
    (state.gameFilter === "all" || (task.game || "Pokemon") === state.gameFilter)
    && (state.taskFilter === "all" ? true
      : state.taskFilter === "running" ? task.enabled
        : state.taskFilter === "stopped" ? !task.enabled
          : status.key === state.taskFilter));
  const games = Object.keys(GAMES).filter((game) => tasks.some((task) => (task.game || "Pokemon") === game));

  return [
    h("div", { class: "page-head" },
      h("div", {}, h("h1", {}, "Tarefas"), h("p", {}, "Cada tarefa procura um produto nas lojas escolhidas e avisa-te quando aparece em inglês, em stock e ao preço.")),
      h("div", { class: "actions" },
        h("button", { class: "button", onclick: () => setTaskEnabled(null, false) }, icon("stop"), "Parar todas"),
        h("button", { class: "button", onclick: () => setTaskEnabled(null, true) }, icon("play"), "Iniciar todas"),
        h("button", { class: "button", onclick: exportTasks, disabled: tasks.length === 0, title: "Descarrega as tarefas num ficheiro JSON" }, icon("external"), "Exportar JSON"),
        h("button", { class: "button", onclick: openTaskImportDrawer, title: "Cria ou atualiza várias tarefas a partir de JSON" }, icon("copy"), "Importar JSON"),
        h("button", { class: "button", onclick: deleteAllTasks, disabled: tasks.length === 0, title: "Apaga todas as tarefas (descarrega uma cópia JSON antes)" }, icon("trash"), "Apagar todas"),
        h("button", { class: "button primary", onclick: () => openTaskDrawer(null) }, icon("plus"), "Nova tarefa"))),

    h("div", { class: "filters" },
      filters.map(([key, label]) =>
        h("button", { class: `filter ${state.taskFilter === key ? "active" : ""}`, onclick: () => { state.taskFilter = key; renderView(true); } },
          label, h("span", { class: "count" }, counts[key]))),
      games.length > 1 ? h("select", {
        class: "filter-select", "aria-label": "Jogo",
        onchange: (event) => { state.gameFilter = event.target.value; renderView(true); },
      }, h("option", { value: "all", selected: state.gameFilter === "all" }, "Todos os jogos"),
        games.map((game) => h("option", { value: game, selected: state.gameFilter === game }, GAMES[game][0]))) : null,
      h("div", { class: "filters-end", title: "Esconde os resultados que as lojas devolveram mas que não são o produto da tarefa" },
        labelledSwitch(state.onlyProducts, "Só produtos (todas as lojas)", (checked) => {
          state.onlyProducts = checked;
          state.storeFilter.clear();
          writePreference("onlyProducts", checked);
          renderView(true);
        }))),

    h("div", { class: "card" }, h("div", { class: "card-body flush" }, visible.length === 0
      ? emptyState("tasks", tasks.length === 0 ? "Ainda não tens tarefas. Cria a primeira em “Nova tarefa”." : "Nenhuma tarefa neste filtro.")
      : h("div", { class: "table-wrap" }, h("table", {},
        h("thead", {}, h("tr", {},
          h("th", {}), h("th", {}, "Produto"), h("th", {}, "Lojas"), h("th", { class: "num" }, "Preço máx."),
          h("th", {}, "Estado"), h("th", { class: "num" }, "Resultados"), h("th", {}, "Última verificação"), h("th", {}))),
        h("tbody", {}, visible.flatMap(({ task, status }) => taskRows(task, status))))))),
  ];
}

function taskRows(task, status) {
  const expanded = state.expandedTasks.has(task.id);
  const stores = storesForTask(task);
  const reports = taskSearches(task);
  const listingCount = reports.reduce((sum, report) => sum + report.listings.filter((listing) => !isIrrelevant(listing)).length, 0);
  const matchCount = reports.reduce((sum, report) => sum + report.matchCount, 0);
  const lastChecked = reports.map((report) => report.checkedAt).sort().at(-1);
  const toggle = () => { expanded ? state.expandedTasks.delete(task.id) : state.expandedTasks.add(task.id); renderView(true); };
  const stop = (event) => event.stopPropagation();

  const row = h("tr", { class: `clickable ${expanded ? "expanded" : ""}`, onclick: toggle },
    h("td", { style: "width:1%" }, h("span", { style: `display:inline-flex;transition:transform .15s;transform:rotate(${expanded ? 90 : 0}deg);color:var(--dim)` }, icon("chevron"))),
    h("td", {},
      h("div", { class: "primary-text", style: "display:flex;align-items:center;gap:8px" }, gamePill(task.game), taskLanguagePill(task.language), task.name || task.id),
      h("div", { class: "secondary-text" }, `“${task.searchQuery}”`)),
    h("td", {}, stores.length === 0
      ? h("span", { class: "dim" }, "nenhuma")
      : h("span", { style: "display:inline-flex;gap:3px", title: stores.map((store) => store.name).join(", ") },
        stores.slice(0, 5).map((store) => storeAvatar(store.name)),
        stores.length > 5 ? h("span", { class: "dim" }, ` +${stores.length - 5}`) : null)),
    h("td", { class: "num" }, money(task.maxPrice)),
    h("td", {}, h("span", { class: `pill ${status.tone}` }, status.label)),
    h("td", { class: "num" }, reports.length ? h("span", {}, h("span", { class: matchCount > 0 ? "" : "dim", style: matchCount > 0 ? "color:var(--green);font-weight:600" : "" }, matchCount), h("span", { class: "dim" }, ` / ${listingCount}`)) : "—"),
    h("td", { class: "nowrap" }, lastChecked ? relative(lastChecked) : h("span", { class: "dim" }, "—")),
    h("td", { class: "actions", onclick: stop },
      task.enabled
        ? h("button", { class: "icon-button stop", title: "Parar tarefa", onclick: () => setTaskEnabled(task.id, false) }, icon("stop"))
        : h("button", { class: "icon-button play", title: "Iniciar tarefa", onclick: () => setTaskEnabled(task.id, true) }, icon("play")),
      h("button", { class: "icon-button", title: "Editar", onclick: () => openTaskDrawer(task) }, icon("edit")),
      h("button", { class: "icon-button", title: "Duplicar", onclick: () => openTaskDrawer({ ...task, id: "", name: `${task.name} (cópia)` }, true) }, icon("copy")),
      h("button", { class: "icon-button delete", title: "Apagar", onclick: () => deleteTask(task) }, icon("trash"))));

  if (!expanded) return [row];

  const detail = h("tr", { class: "detail-row" }, h("td", { colspan: 8 }, h("div", { class: "detail" },
    stores.length === 0
      ? h("div", { class: "notice" }, "Esta tarefa não tem lojas ativas. Edita a tarefa ou ativa lojas na página Lojas.")
      : stores.map((store) => storeResult(task, store)))));

  return [row, detail];
}

function storeResult(task, store) {
  const report = searchFor(task, store.name);
  const head = h("div", { class: "detail-store-head" }, storeAvatar(store.name), store.name,
    !report ? h("span", { class: "pill blue" }, "À espera")
      : !report.succeeded ? h("span", { class: "pill red" }, "Erro")
        : report.matchCount > 0 ? h("span", { class: "pill green pulse" }, `${report.matchCount} em stock`)
          : h("span", { class: "pill violet" }, "Sem correspondência"),
    report ? h("span", { class: "meta" }, `${report.listings.filter((listing) => !isIrrelevant(listing)).length} relevante(s) de ${report.listings.length} · ${report.durationMs} ms · ${relative(report.checkedAt)}`) : null);

  if (!report) return h("div", { class: "detail-store" }, head);
  if (!report.succeeded) return h("div", { class: "detail-store" }, head, h("div", { class: "card-body", style: "color:var(--red)" }, report.error));

  const key = `${task.id}|${store.name}`;
  const onlyProducts = showsOnlyProducts(key);
  const hiddenCount = onlyProducts ? report.listings.filter(isIrrelevant).length : 0;
  const visible = report.listings.filter((listing) => !onlyProducts || !isIrrelevant(listing));
  const ordered = [...visible].sort((a, b) => (isMatch(a.outcome) ? -1 : 0) - (isMatch(b.outcome) ? -1 : 0)).slice(0, 12);

  head.append(labelledSwitch(onlyProducts, "Só produtos", (checked) => {
    state.storeFilter.set(key, checked);
    renderView(true);
  }));

  const body = report.listings.length === 0
    ? h("div", { class: "card-body dim" }, "A loja não devolveu nada para esta pesquisa.")
    : ordered.length === 0
      ? h("div", { class: "card-body dim" }, `Nenhum resultado desta loja é o produto procurado (${hiddenCount} escondido(s)).`)
      : listingsTable(ordered, (listing) => outcomePill(listing.outcome));

  return h("div", { class: "detail-store" }, head, body);
}

/** Outcomes of products a task recognises: only these have a price history. */
const TRACKED_OUTCOMES = new Set(["Match", "MatchLanguageToConfirm", "LanguageUnknown", "OutOfStock", "NoPrice", "AboveMaxPrice"]);

function listingsTable(listings, outcomeCell) {
  return h("div", { class: "table-wrap" }, h("table", {},
    h("thead", {}, h("tr", {}, h("th", {}, "Produto"), h("th", { class: "num" }, "Preço"), h("th", {}, "Stock"), h("th", {}, "Idioma"), h("th", {}, "Resultado"))),
    h("tbody", {}, listings.map((listing) => h("tr", {},
      h("td", { class: "title-cell" }, h("div", { class: "with-thumb" }, productThumb(listing.imageUrl, "small"),
        h("div", {},
          h("span", { class: "primary-text" }, externalLink(listing.url, listing.title)),
          listing.preOrder ? h("div", { class: "pill-row" }, h("span", { class: "pill violet" }, "Pré-venda")) : null))),
      h("td", { class: "num" },
        h("div", { class: "price-cell" }, money(listing.price),
          listing.priceHistoryKey && TRACKED_OUTCOMES.has(listing.outcome)
            ? h("button", { class: "icon-button tiny", title: "Histórico de preço", "aria-label": `Histórico de preço de ${listing.title}`, onclick: () => openPriceHistory(listing.priceHistoryKey) }, icon("chart"))
            : null),
        listing.lowestPrice !== null && listing.lowestPrice !== undefined && listing.price !== null && listing.lowestPrice < listing.price - 0.005
          ? h("div", { class: "was-price plain" }, `mín. ${money(listing.lowestPrice)}`)
          : null),
      h("td", {}, listing.inStock ? h("span", { class: "pill green" }, "Sim") : h("span", { class: "pill red" }, "Não")),
      h("td", {}, languagePill(listing.language)),
      h("td", {}, outcomeCell(listing)))))));
}

/* ----- Price history of one listing (opened from a task's results) ----- */

const normalizeText = (text) => (text || "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();

async function openPriceHistory(key) {
  let history;
  try {
    history = await api(`/pricehistory/get?identifier=${encodeURIComponent(key)}`);
  } catch (error) {
    toast(error.errors?.[0] || error.message, "error");
    return;
  }

  const summary = history.summary;
  const points = [...history.points].reverse();
  const stat = (label, value, note) => h("div", { class: "history-stat" },
    h("div", { class: "stat-label" }, label), h("div", { class: "history-value" }, value), note ? h("div", { class: "was-price plain" }, note) : null);

  openDrawer({
    title: summary.title,
    subtitle: `${summary.storeName} · a seguir desde ${dateTime(summary.firstSeen)}`,
    body: [
      h("div", { class: "history-stats" },
        stat("Atual", money(summary.currentPrice), summary.currentInStock ? "em stock" : "esgotado"),
        stat("Mínimo", money(summary.lowestPrice), dateTime(summary.lowestPriceAt)),
        stat("Máximo", money(summary.highestPrice)),
        stat("Alterações", String(summary.pointCount))),
      h("div", { class: "drawer-section" }, h("h3", {}, "Preço ao longo do tempo"), priceHistoryChart(history)),
      h("div", { class: "drawer-section" }, h("h3", {}, "Alterações"),
        h("div", { class: "table-wrap" }, h("table", {},
          h("thead", {}, h("tr", {}, h("th", {}, "Desde"), h("th", { class: "num" }, "Preço"), h("th", {}, "Stock"))),
          h("tbody", {}, points.map((point) => h("tr", {},
            h("td", { class: "nowrap" }, dateTime(point.at)),
            h("td", { class: "num" }, money(point.price)),
            h("td", {}, point.inStock ? h("span", { class: "pill green" }, "Sim") : h("span", { class: "pill red" }, "Não")))))))),
    ],
    footer: [
      h("button", { class: "button ghost", onclick: closeDrawer }, "Fechar"),
      safeHref(summary.url) ? h("a", { class: "button primary", href: summary.url, target: "_blank", rel: "noopener noreferrer" }, "Abrir produto", icon("external")) : null,
    ],
  });
}

/** Round step for price ticks (1, 2, 5 × 10^n) so the axis shows 3–5 clean values. */
function niceStep(range) {
  const rough = range / 4;
  const power = 10 ** Math.floor(Math.log10(rough || 1));
  return [1, 2, 5, 10].map((factor) => factor * power).find((step) => step >= rough) || power;
}

/**
 * Step chart of one product's price: the price holds until the next change. In-stock stretches are a solid
 * accent line, sold-out stretches a dashed muted line (and "esgotado" in the tooltip and the table), so stock
 * is never told by colour alone. One series → no legend box; a small key explains the two line styles.
 */
function priceHistoryChart(history) {
  const points = history.points.map((point) => ({ ...point, time: new Date(point.at).getTime() }));
  if (points.length === 0) return emptyState("chart", "Sem pontos.");

  const width = 516, height = 220, left = 56, right = 64, top = 14, bottom = 28;
  const lastSeen = Math.max(new Date(history.summary.lastSeen).getTime(), points[points.length - 1].time);
  const start = points[0].time;
  const end = lastSeen > start ? lastSeen : start + 3600e3;

  const prices = points.map((point) => point.price);
  const low = Math.min(...prices), high = Math.max(...prices);
  const step = niceStep(Math.max(high - low, high * 0.1, 1));
  const yMin = Math.max(0, Math.floor((low - step * 0.5) / step) * step);
  const yMax = Math.ceil((high + step * 0.5) / step) * step;

  const x = (time) => left + ((time - start) / (end - start)) * (width - left - right);
  const y = (price) => top + (1 - (price - yMin) / (yMax - yMin)) * (height - top - bottom);

  const grid = [];
  for (let value = yMin; value <= yMax + 1e-9; value += step) {
    grid.push(svg("line", { class: "history-grid", x1: left, x2: width - right, y1: y(value), y2: y(value) }),
      svg("text", { class: "history-axis", x: left - 8, y: y(value) + 4, "text-anchor": "end" }, money(value).replace(",00", "")));
  }

  const segments = points.map((point, index) => {
    const until = index + 1 < points.length ? points[index + 1].time : end;
    const next = points[index + 1];
    const path = `M${x(point.time)},${y(point.price)} H${x(until)}` + (next ? ` V${y(next.price)}` : "");
    return svg("path", { class: point.inStock ? "history-line" : "history-line sold-out", d: path });
  });

  const lowest = points.reduce((best, point) => (point.price < best.price ? point : best), points[0]);
  const current = points[points.length - 1];
  const markers = points.map((point) => svg("circle", { class: point.inStock ? "history-dot" : "history-dot sold-out", cx: x(point.time), cy: y(point.price), r: 4 }));
  const labels = [svg("text", { class: "history-label", x: x(end) + 8, y: y(current.price) + 4 }, money(current.price))];
  if (lowest !== current && Math.abs(y(lowest.price) - y(current.price)) > 14) {
    labels.push(svg("text", { class: "history-label", x: x(lowest.time), y: y(lowest.price) + 18, "text-anchor": "middle" }, `mín. ${money(lowest.price)}`));
  }

  const dayLabel = (time) => new Intl.DateTimeFormat("pt-PT", { day: "2-digit", month: "short" }).format(new Date(time));
  const axis = [
    svg("line", { class: "history-grid", x1: left, x2: width - right, y1: height - bottom, y2: height - bottom }),
    svg("text", { class: "history-axis", x: left, y: height - 8, "text-anchor": "start" }, dayLabel(start)),
    svg("text", { class: "history-axis", x: width - right, y: height - 8, "text-anchor": "end" }, dayLabel(end)),
  ];

  const crosshair = svg("line", { class: "history-crosshair", x1: 0, x2: 0, y1: top, y2: height - bottom, visibility: "hidden" });
  const focusDot = svg("circle", { class: "history-dot focus", r: 5, visibility: "hidden" });
  const tooltip = h("div", { class: "history-tooltip", role: "status" });
  tooltip.hidden = true;

  let focusedIndex = points.length - 1;
  const show = (index, pointerX) => {
    const point = points[index];
    const px = pointerX ?? x(point.time);
    crosshair.setAttribute("x1", px); crosshair.setAttribute("x2", px); crosshair.setAttribute("visibility", "visible");
    focusDot.setAttribute("cx", px); focusDot.setAttribute("cy", y(point.price)); focusDot.setAttribute("visibility", "visible");
    tooltip.replaceChildren(h("strong", {}, money(point.price)), h("span", {}, point.inStock ? "em stock" : "esgotado"), h("span", { class: "muted" }, `desde ${dateTime(point.at)}`));
    tooltip.hidden = false;
    tooltip.style.left = `${Math.min(Math.max((px / width) * 100, 12), 78)}%`;
  };
  const hide = () => { crosshair.setAttribute("visibility", "hidden"); focusDot.setAttribute("visibility", "hidden"); tooltip.hidden = true; };
  const indexAt = (time) => { let found = 0; points.forEach((point, index) => { if (point.time <= time) found = index; }); return found; };

  const plot = svg("svg", {
    class: "history-chart", viewBox: `0 0 ${width} ${height}`, role: "img", tabindex: 0,
    "aria-label": `Preço de ${history.summary.title}: atual ${money(current.price)}, mínimo ${money(lowest.price)}, máximo ${money(high)}. A tabela abaixo tem todos os valores.`,
  }, grid, axis, segments, markers, labels, crosshair, focusDot,
    svg("rect", { class: "history-hit", x: left, y: top, width: width - left - right, height: height - top - bottom }));

  plot.addEventListener("pointermove", (event) => {
    const box = plot.getBoundingClientRect();
    const px = ((event.clientX - box.left) / box.width) * width;
    const clamped = Math.min(Math.max(px, left), width - right);
    const time = start + ((clamped - left) / (width - left - right)) * (end - start);
    focusedIndex = indexAt(time);
    show(focusedIndex, clamped);
  });
  plot.addEventListener("pointerleave", hide);
  plot.addEventListener("focus", () => show(focusedIndex));
  plot.addEventListener("blur", hide);
  plot.addEventListener("keydown", (event) => {
    if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
    event.preventDefault();
    focusedIndex = Math.min(Math.max(focusedIndex + (event.key === "ArrowRight" ? 1 : -1), 0), points.length - 1);
    show(focusedIndex);
  });

  return h("div", {},
    h("div", { class: "history-box" }, plot, tooltip),
    h("div", { class: "history-key" },
      h("span", {}, svg("svg", { viewBox: "0 0 24 8", class: "key-line" }, svg("path", { class: "history-line", d: "M1,4 H23" })), "em stock"),
      h("span", {}, svg("svg", { viewBox: "0 0 24 8", class: "key-line" }, svg("path", { class: "history-line sold-out", d: "M1,4 H23" })), "esgotado")));
}

/* ----- Compare ----- */

/** Store line colours: the validated dark categorical order (blue, orange, aqua, yellow, magenta, violet). */
const CHART_STORE_COLORS = ["#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#9085e9"];

/**
 * Colours for the stores of one product. Each store keeps its own slot (its position in the store list) so it looks
 * the same in every product; two stores that share a slot inside the same product get the next free one.
 */
function storeColors(storeNames) {
  const order = (state.stores || []).map((store) => store.name);
  const used = new Set();
  const colors = new Map();
  for (const name of [...storeNames].sort((a, b) => (order.indexOf(a) - order.indexOf(b)) || a.localeCompare(b))) {
    let slot = Math.max(order.indexOf(name), 0) % CHART_STORE_COLORS.length;
    for (let tries = 0; used.has(slot) && tries < CHART_STORE_COLORS.length; tries++) slot = (slot + 1) % CHART_STORE_COLORS.length;
    used.add(slot);
    colors.set(name, CHART_STORE_COLORS[slot]);
  }
  return colors;
}

async function loadCompare() {
  try {
    state.compare = await api("/compare/list");
  } catch (error) {
    state.compare = [];
    toast(error.message, "error");
  }
  if (state.view === "compare") renderView(true);
}

/** Cheapest in stock, runner-up, lowest ever and most recent drop of one product. */
function compareSummary(group) {
  const listings = group.listings;
  const inStock = listings.filter((listing) => listing.currentInStock);
  const lowestEver = listings.reduce((best, listing) => (!best || listing.lowestPrice < best.lowestPrice ? listing : best), null);
  const lastDrop = listings.filter((listing) => listing.lastDropAt).sort((a, b) => new Date(b.lastDropAt) - new Date(a.lastDropAt))[0] || null;
  return { best: inStock[0] || null, second: inStock[1] || null, inStockCount: inStock.length, lowestEver, lastDrop };
}

const shortDate = (iso) => (iso ? new Date(iso).toLocaleDateString("pt-PT", { day: "2-digit", month: "2-digit" }) : "—");
const WEEK = 7 * 864e5;

function storeChip(listing, colors, isBest) {
  return h("span", { class: `store-chip ${listing.currentInStock ? "" : "sold-out"} ${isBest ? "best" : ""}`, title: listing.title },
    h("span", { class: "swatch", style: `background:${colors.get(listing.storeName)};color:${colors.get(listing.storeName)}` }),
    listing.storeName, h("strong", {}, money(listing.currentPrice)), listing.currentInStock ? null : "esgotado");
}

function viewCompare() {
  const tableBox = h("div", { class: "card-body flush" });
  const all = state.compare || [];
  const visibleBySize = all.filter((group) => state.compareSingles || group.listings.length >= 2);
  const counts = {
    all: visibleBySize.length,
    stock: visibleBySize.filter((group) => group.listings.some((listing) => listing.currentInStock)).length,
    drops: visibleBySize.filter((group) => group.listings.some((listing) => listing.lastDropAt && Date.now() - new Date(listing.lastDropAt) < WEEK)).length,
  };
  const stores = new Set(all.flatMap((group) => group.listings.map((listing) => listing.storeName)));

  const renderTable = () => {
    const words = normalizeText(state.compareFilter).split(/\s+/).filter(Boolean);
    const rows = visibleBySize.filter((group) => {
      if (state.compareMode === "stock" && !group.listings.some((listing) => listing.currentInStock)) return false;
      if (state.compareMode === "drops" && !group.listings.some((listing) => listing.lastDropAt && Date.now() - new Date(listing.lastDropAt) < WEEK)) return false;
      const haystack = normalizeText(`${group.title} ${group.listings.map((listing) => `${listing.storeName} ${listing.title}`).join(" ")}`);
      return words.every((word) => haystack.includes(word));
    });
    tableBox.replaceChildren(state.compare === null
      ? h("div", { class: "empty" }, h("div", {}, "A carregar…"))
      : rows.length === 0
        ? emptyState("compare", all.length === 0
          ? "Ainda nada para comparar. Os produtos aparecem aqui depois de um ciclo, quando as tarefas os encontram em mais do que uma loja."
          : "Nenhum produto corresponde aos filtros.")
        : compareTable(rows));
  };
  renderTable();

  const modeButton = (mode, label) => h("button", {
    class: `filter ${state.compareMode === mode ? "active" : ""}`,
    onclick: () => { state.compareMode = mode; renderView(true); },
  }, label, h("span", { class: "count" }, String(counts[mode])));

  return [
    h("div", { class: "page-head" },
      h("div", {}, h("h1", {}, "Comparar"),
        h("p", {}, "O mesmo produto em todas as lojas: quem tem o preço mais baixo agora, o mínimo de sempre e as últimas descidas. Os produtos são agrupados por set e tipo; se algum estiver mal agrupado, corrige no detalhe."))),
    h("div", { class: "filters" },
      h("input", {
        type: "text", value: state.compareFilter, placeholder: "Filtrar: pitch black, booster bundle…", style: "max-width:360px",
        "aria-label": "Filtrar produtos", oninput: (event) => { state.compareFilter = event.target.value; renderTable(); },
      }),
      modeButton("all", "Todos"), modeButton("stock", "Com stock"), modeButton("drops", "Baixaram (7 dias)"),
      h("span", { class: "filter-switch" },
        switchInput(state.compareSingles, (checked) => { state.compareSingles = checked; renderView(true); }, "Incluir produtos só numa loja"),
        h("span", {}, "Só numa loja")),
      h("span", { class: "muted filters-end" }, `${visibleBySize.length} produtos · ${stores.size} lojas`)),
    h("div", { class: "card" }, tableBox),
  ];
}

function compareTable(groups) {
  return h("div", { class: "table-wrap" }, h("table", {},
    h("thead", {}, h("tr", {},
      h("th", {}, "Produto"), h("th", {}, "Lojas"), h("th", { class: "num" }, "Mais barato c/ stock"), h("th", { class: "num" }, "MSRP"),
      h("th", { class: "num" }, "Mínimo de sempre"), h("th", { class: "num" }, "Última descida"), h("th", {}))),
    h("tbody", {}, groups.map((group) => {
      const summary = compareSummary(group);
      const colors = storeColors(group.listings.map((listing) => listing.storeName));
      return h("tr", {},
        h("td", {}, h("div", { class: "group-title" },
          h("span", { class: "set" }, group.title),
          h("span", { class: "meta" }, `${group.listings.length} ${group.listings.length === 1 ? "loja" : "lojas"} · ${summary.inStockCount} com stock`))),
        h("td", {}, h("div", { class: "compare-chips" }, group.listings.map((listing) => storeChip(listing, colors, listing === summary.best)))),
        h("td", { class: "num nowrap" }, summary.best
          ? [h("div", {}, money(summary.best.currentPrice)),
            h("div", { class: "delta" }, summary.best.storeName + (summary.second ? ` · 2.ª +${money(summary.second.currentPrice - summary.best.currentPrice)}` : ""))]
          : h("span", { class: "pill red" }, "Sem stock")),
        h("td", { class: "num nowrap", title: group.msrpLabel ? `Referência: ${group.msrpLabel}` : "Sem preço de referência para este tipo de produto" },
          group.msrp ? [h("div", {}, money(group.msrp)), msrpDelta(summary.best?.currentPrice, group.msrp)] : h("span", { class: "muted" }, "—")),
        h("td", { class: "num nowrap" }, money(summary.lowestEver?.lowestPrice),
          summary.lowestEver ? h("div", { class: "delta" }, `${summary.lowestEver.storeName} · ${shortDate(summary.lowestEver.lowestPriceAt)}`) : null),
        h("td", { class: "num nowrap" }, summary.lastDrop
          ? [h("div", {}, `🔻 ${money(summary.lastDrop.lastDropTo)}`),
            h("div", { class: "delta" }, `${summary.lastDrop.storeName} · −${money(summary.lastDrop.lastDropFrom - summary.lastDrop.lastDropTo)} · ${relative(summary.lastDrop.lastDropAt)}`)]
          : h("span", { class: "muted" }, "—")),
        h("td", { class: "actions" }, h("button", {
          class: "icon-button", title: "Comparar lojas", "aria-label": `Comparar ${group.title}`, onclick: () => openCompareGroup(group.key),
        }, icon("chart"))));
    }))));
}

async function openCompareGroup(key) {
  let group;
  try {
    group = await api(`/compare/get?identifier=${encodeURIComponent(key)}`);
  } catch (error) {
    toast(error.errors?.[0] || error.message, "error");
    return;
  }

  const summary = compareSummary(group);
  const colors = storeColors(group.listings.map((listing) => listing.storeName));
  const stat = (label, value, note) => h("div", { class: "history-stat" },
    h("div", { class: "stat-label" }, label), h("div", { class: "history-value" }, value), note ? h("div", { class: "was-price plain" }, note) : null);

  openDrawer({
    title: group.title,
    subtitle: `${group.listings.length} ${group.listings.length === 1 ? "loja" : "lojas"} · ${group.key.startsWith("solo|") ? "separado manualmente" : "agrupado por set e tipo"}`,
    body: [
      h("div", { class: "history-stats" },
        stat("Mais barato agora", summary.best ? money(summary.best.currentPrice) : "—", summary.best ? summary.best.storeName : "sem stock"),
        stat("Mínimo de sempre", money(summary.lowestEver?.lowestPrice), summary.lowestEver ? `${summary.lowestEver.storeName} · ${dateTime(summary.lowestEver.lowestPriceAt)}` : null),
        stat("MSRP", group.msrp ? money(group.msrp) : "—", group.msrp
          ? [msrpDelta(summary.best?.currentPrice, group.msrp, "o mais barato"), h("div", {}, `${group.msrpLabel} · ${(MSRP_BASIS[group.msrpBasis] || MSRP_BASIS.Estimate)[0].toLowerCase()}`)]
          : "sem referência"),
        stat("Com stock", `${summary.inStockCount} de ${group.listings.length}`),
        stat("Última descida", summary.lastDrop ? money(summary.lastDrop.lastDropTo) : "—", summary.lastDrop ? `${summary.lastDrop.storeName} · ${relative(summary.lastDrop.lastDropAt)}` : null)),
      h("div", { class: "drawer-section" }, h("h3", {}, "Preço por loja"), compareChart(group, colors)),
      h("div", { class: "drawer-section" }, h("h3", {}, "Lojas"),
        h("div", { class: "table-wrap" }, h("table", {},
          h("thead", {}, h("tr", {}, h("th", {}, "Loja"), h("th", { class: "num" }, "Atual"), h("th", { class: "num" }, "Mínimo"))),
          h("tbody", {}, group.listings.map((listing) => h("tr", {},
            h("td", { class: "title-cell" },
              h("span", { class: "store-link" }, h("span", { class: "swatch-dot", style: `background:${colors.get(listing.storeName)}` }), listing.storeName,
                listing.isManual ? h("span", { class: "pill grey", title: "Neste produto por uma correção manual" }, "manual") : null),
              h("span", { class: "listing-title", title: listing.title }, externalLink(listing.url, listing.title)),
              h("div", { class: "link-row" },
                h("button", { class: "link-button", title: "Tirar este anúncio deste produto", onclick: () => separateListing(group, listing) }, "Separar deste produto"),
                listing.isManual ? h("button", { class: "link-button", onclick: () => assignListing(listing.historyKey, "", group.key) }, "Repor automático") : null)),
            h("td", { class: "num nowrap" }, money(listing.currentPrice),
              h("div", {}, listing.currentInStock ? h("span", { class: "pill green" }, "Em stock") : h("span", { class: "pill red" }, "Esgotado"))),
            h("td", { class: "num nowrap" }, money(listing.lowestPrice), h("div", { class: "was-price plain" }, dateTime(listing.lowestPriceAt))))))))),
      h("div", { class: "drawer-section" }, h("h3", {}, "Correção"),
        h("p", { class: "muted", style: "margin:0;font-size:12.5px" }, "Falta aqui uma loja com este produto? Junta o anúncio dela a este grupo. A correção fica guardada e o bot respeita-a nos próximos ciclos."),
        h("div", {}, h("button", { class: "button small", onclick: () => joinListing(group) }, icon("plus"), "Juntar anúncio…"))),
    ],
    footer: [h("button", { class: "button ghost", onclick: closeDrawer }, "Fechar")],
  });
}

/**
 * Step chart of one product in every store: one line per store in the store's colour; sold-out stretches are the
 * same colour, dashed and lighter. Stores are named at the line ends, in the legend (hover a store to highlight it)
 * and in the tooltip, which lists every store at the pointer's time — so identity never rests on colour alone.
 */
function compareChart(group, colors) {
  const series = group.listings
    .map((listing) => ({ listing, color: colors.get(listing.storeName), points: listing.points.map((point) => ({ ...point, time: new Date(point.at).getTime() })) }))
    .filter((item) => item.points.length > 0);
  if (series.length === 0) return emptyState("chart", "Sem pontos.");

  const width = 516, height = 240, left = 52, right = 100, top = 14, bottom = 28;
  const all = series.flatMap((item) => item.points);
  const start = Math.min(...all.map((point) => point.time));
  const lastSeen = Math.max(...group.listings.map((listing) => new Date(listing.lastSeen).getTime()), ...all.map((point) => point.time));
  const end = lastSeen > start ? lastSeen : start + 3600e3;
  const msrp = group.msrp || null;
  const prices = all.map((point) => point.price).concat(msrp ? [msrp] : []);
  const low = Math.min(...prices), high = Math.max(...prices);
  const step = niceStep(Math.max(high - low, high * 0.1, 1));
  const yMin = Math.max(0, Math.floor((low - step * 0.5) / step) * step), yMax = Math.ceil((high + step * 0.5) / step) * step;
  const x = (time) => left + ((time - start) / (end - start)) * (width - left - right);
  const y = (price) => top + (1 - (price - yMin) / (yMax - yMin)) * (height - top - bottom);

  const grid = [];
  for (let value = yMin; value <= yMax + 1e-9; value += step) {
    grid.push(svg("line", { class: "history-grid", x1: left, x2: width - right, y1: y(value), y2: y(value) }),
      svg("text", { class: "history-axis", x: left - 8, y: y(value) + 4, "text-anchor": "end" }, money(value).replace(",00", "")));
  }
  const dayLabel = (time) => new Intl.DateTimeFormat("pt-PT", { day: "2-digit", month: "short" }).format(new Date(time));
  grid.push(svg("line", { class: "history-grid", x1: left, x2: width - right, y1: height - bottom, y2: height - bottom }),
    svg("text", { class: "history-axis", x: left, y: height - 8 }, dayLabel(start)),
    svg("text", { class: "history-axis", x: width - right, y: height - 8, "text-anchor": "end" }, dayLabel(end)));

  if (msrp) {
    grid.push(svg("line", { class: "msrp-line", x1: left, x2: width - right, y1: y(msrp), y2: y(msrp) }),
      svg("text", { class: "history-axis msrp-label", x: left + 4, y: y(msrp) - 4 }, `MSRP ${money(msrp).replace(",00", "")}`));
  }

  const lines = series.map((item) => {
    const paths = item.points.map((point, index) => {
      const next = item.points[index + 1];
      const until = next ? next.time : end;
      return svg("path", { class: `history-line store-line ${point.inStock ? "" : "sold-out"}`, style: `stroke:${item.color}`, d: `M${x(point.time)},${y(point.price)} H${x(until)}` + (next ? ` V${y(next.price)}` : "") });
    });
    const dots = item.points.map((point) => svg("circle", { class: `history-dot store-dot ${point.inStock ? "" : "sold-out"}`, style: `fill:${item.color}`, cx: x(point.time), cy: y(point.price), r: 4 }));
    item.endY = y(item.points[item.points.length - 1].price);
    return svg("g", { class: "store-series", "data-store": item.listing.storeName }, paths, dots);
  });

  // End labels, spread just enough not to overlap, with a short leader back to the line end.
  let previous = -Infinity;
  const labels = [...series].sort((a, b) => a.endY - b.endY).flatMap((item) => {
    const labelY = Math.max(item.endY, previous + 13);
    previous = labelY;
    return [
      Math.abs(labelY - item.endY) > 1 ? svg("path", { class: "history-grid", d: `M${x(end)},${item.endY} L${x(end) + 5},${labelY - 4}` }) : null,
      svg("text", { class: "history-label", x: x(end) + 8, y: labelY + 4 }, `${shortStoreName(item.listing.storeName)} ${money(item.points[item.points.length - 1].price).replace(",00", "")}`),
    ];
  });

  const crosshair = svg("line", { class: "history-crosshair", x1: 0, x2: 0, y1: top, y2: height - bottom, visibility: "hidden" });
  const tooltip = h("div", { class: "history-tooltip", role: "status" });
  tooltip.hidden = true;
  const plot = svg("svg", {
    class: "history-chart", viewBox: `0 0 ${width} ${height}`, role: "img", tabindex: 0,
    "aria-label": `Preço de ${group.title} em ${series.length} lojas. A tabela abaixo tem todos os valores.`,
  }, grid, lines, labels, crosshair, svg("rect", { class: "history-hit", x: left, y: top, width: width - left - right, height: height - top - bottom }));

  const showAt = (time, px) => {
    crosshair.setAttribute("x1", px); crosshair.setAttribute("x2", px); crosshair.setAttribute("visibility", "visible");
    const rows = series.map((item) => {
      const seen = item.points.filter((point) => point.time <= time);
      return seen.length ? { item, point: seen[seen.length - 1] } : null;
    }).filter(Boolean).sort((a, b) => (b.point.inStock - a.point.inStock) || (a.point.price - b.point.price));
    tooltip.replaceChildren(h("span", { class: "muted" }, dateTime(new Date(time).toISOString())), ...rows.map(({ item, point }) =>
      h("div", { class: `tip-row ${point.inStock ? "" : "sold-out"}` }, h("span", { class: "line-key", style: `background:${item.color}` }),
        h("strong", {}, money(point.price)), item.listing.storeName, point.inStock ? null : "· esgotado")));
    tooltip.hidden = false;
    tooltip.style.left = `${Math.min(Math.max((px / width) * 100, 16), 72)}%`;
  };
  const hide = () => { crosshair.setAttribute("visibility", "hidden"); tooltip.hidden = true; };
  plot.addEventListener("pointermove", (event) => {
    const box = plot.getBoundingClientRect();
    const px = Math.min(Math.max(((event.clientX - box.left) / box.width) * width, left), width - right);
    showAt(start + ((px - left) / (width - left - right)) * (end - start), px);
  });
  plot.addEventListener("pointerleave", hide);
  plot.addEventListener("focus", () => showAt(end, x(end)));
  plot.addEventListener("blur", hide);

  const focus = (store) => {
    plot.classList.toggle("dim-others", Boolean(store));
    plot.querySelectorAll(".store-series").forEach((element) => element.classList.toggle("focus", element.dataset.store === store));
  };

  return h("div", {},
    h("div", { class: "history-box" }, plot, tooltip),
    h("div", { class: "compare-legend" }, series.map((item) =>
      h("button", {
        onmouseenter: () => focus(item.listing.storeName), onmouseleave: () => focus(null),
        onfocus: () => focus(item.listing.storeName), onblur: () => focus(null),
      }, svg("svg", { viewBox: "0 0 20 8", class: "key-line" }, svg("path", { class: "history-line store-line", style: `stroke:${item.color}`, d: "M1,4 H19" })), item.listing.storeName)),
      h("span", { class: "legend-end" },
        svg("svg", { viewBox: "0 0 20 8", class: "key-line" }, svg("path", { class: "history-line store-line sold-out", style: "stroke:var(--text-soft)", d: "M1,4 H19" })), "esgotado")));
}

/** "Versus Gamecenter" → "Versus": the chart end labels have ~100 px; the legend and tooltip keep the full name. */
function shortStoreName(name) {
  const first = name.split(/\s+/)[0] || name;
  return first.length > 11 ? `${first.slice(0, 10)}…` : first;
}

/** A modal with one select; resolves to the chosen value, or null when cancelled. */
function chooseDialog({ title, message, label, options, confirmLabel }) {
  const backdrop = document.getElementById("modal");
  return new Promise((resolve) => {
    const close = (result) => { backdrop.classList.remove("open"); backdrop.replaceChildren(); resolve(result); };
    const select = h("select", {}, options.map(([value, text]) => h("option", { value }, text)));
    backdrop.replaceChildren(h("div", { class: "modal", role: "dialog", "aria-modal": "true", style: "width:min(520px,92vw)" },
      h("h2", {}, title),
      h("div", { class: "modal-body" }, h("p", {}, message), h("div", { class: "field" }, h("label", {}, label), select)),
      h("div", { class: "buttons" },
        h("button", { class: "button ghost", onclick: () => close(null) }, "Cancelar"),
        h("button", { class: "button primary", onclick: () => close(select.value) }, confirmLabel))));
    backdrop.onclick = (event) => { if (event.target === backdrop) close(null); };
    backdrop.classList.add("open");
    select.focus();
  });
}

async function assignListing(historyKey, groupKey, reopenKey) {
  try {
    await post("/compare/assign", { historyKey, groupKey });
    toast("Correção guardada", "success");
  } catch (error) {
    toast((error.errors || [error.message]).join(" "), "error");
    return;
  }
  await loadCompare();
  if (reopenKey && (state.compare || []).some((group) => group.key === reopenKey)) await openCompareGroup(reopenKey);
  else closeDrawer();
}

async function separateListing(group, listing) {
  const others = (state.compare || []).filter((other) => other.key !== group.key && !other.key.startsWith("solo|"));
  const target = await chooseDialog({
    title: "Separar anúncio?",
    message: `“${listing.title}” (${listing.storeName}) deixa de contar como ${group.title}.`,
    label: "Passa a ser",
    options: [["solo", "Um produto à parte"], ...others.map((other) => [other.key, other.title])],
    confirmLabel: "Separar",
  });
  if (target !== null) await assignListing(listing.historyKey, target, group.key);
}

async function joinListing(group) {
  let listings;
  try {
    listings = await api("/pricehistory/list");
  } catch (error) {
    toast(error.message, "error");
    return;
  }

  const inGroup = new Set(group.listings.map((listing) => listing.historyKey));
  const setWords = normalizeText(group.setName).split(/\s+/).filter(Boolean);
  const candidates = listings
    .filter((item) => !inGroup.has(item.key))
    .map((item) => ({ item, sameSet: setWords.length > 0 && setWords.every((word) => normalizeText(item.title).includes(word)) }))
    .sort((a, b) => (b.sameSet - a.sameSet) || a.item.title.localeCompare(b.item.title))
    .slice(0, 300);
  if (candidates.length === 0) {
    toast("Não há outros anúncios no histórico.", "error");
    return;
  }

  const target = await chooseDialog({
    title: `Juntar a ${group.title}`,
    message: "Escolhe o anúncio de outra loja que é este mesmo produto (os do mesmo set aparecem primeiro):",
    label: "Anúncio",
    options: candidates.map(({ item }) => [item.key, `${item.storeName} — ${item.title} · ${money(item.currentPrice)}${item.currentInStock ? " · em stock" : " · esgotado"}`]),
    confirmLabel: "Juntar",
  });
  if (target !== null) await assignListing(target, group.key, group.key);
}

/* ----- Stores ----- */

function viewStores() {
  const stores = state.stores;
  const tags = [...new Set(stores.flatMap((store) => store.tags || []))].sort((a, b) => a.localeCompare(b, "pt"));
  const listBox = h("div", {});

  const matches = storeMatchesFilters;

  const renderList = () => {
    const visible = stores.filter(matches);
    if (stores.length === 0) { listBox.replaceChildren(h("div", { class: "card" }, emptyState("store", "Sem lojas configuradas."))); return; }
    if (visible.length === 0) { listBox.replaceChildren(h("div", { class: "card" }, emptyState("search", "Nenhuma loja corresponde aos filtros."))); return; }

    // Area (country) first, then tier inside it: S → A → B → C → sem tier.
    const tierOrder = ["S", "A", "B", "C", "None"];
    listBox.replaceChildren(...Object.keys(COUNTRIES).map((country) => {
      const inCountry = visible.filter((store) => (store.country || "Portugal") === country);
      if (inCountry.length === 0) return null;
      const [label, flag] = COUNTRIES[country];
      return h("section", { class: "store-area" },
        h("h2", { class: "store-area-title" }, h("span", { "aria-hidden": "true" }, flag), label,
          h("span", { class: "count" }, `${inCountry.length} lojas · ${inCountry.filter((store) => store.enabled).length} ativas`)),
        tierOrder.map((tier) => {
          const inTier = inCountry.filter((store) => (store.tier || "None") === tier).sort((a, b) => a.name.localeCompare(b.name, "pt"));
          if (inTier.length === 0) return null;
          const [tierLabel, tone, description] = TIERS[tier];
          return h("div", { class: "store-tier" },
            h("div", { class: "store-tier-title" }, h("span", { class: `pill ${tone}` }, tierLabel), h("span", {}, description), h("span", { class: "muted" }, `· ${inTier.length}`)),
            h("div", { class: "grid stores" }, inTier.map(storeCard)));
        }));
    }).filter(Boolean));
  };
  renderList();

  const select = (value, options, onChange, label) => h("select", { "aria-label": label, onchange: (event) => onChange(event.target.value) },
    options.map(([optionValue, optionLabel]) => h("option", { value: optionValue, selected: optionValue === value }, optionLabel)));

  return [
    h("div", { class: "page-head" },
      h("div", {}, h("h1", {}, "Lojas"), h("p", {}, "Agrupadas por área e tier. Só são pesquisadas as lojas ativas com integração; as \"Sem integração\" ficam só como referência.")),
      h("div", { class: "actions" },
        h("button", { class: "button", onclick: exportStores, title: "Descarrega todas as lojas em JSON" }, icon("copy"), "Exportar JSON"),
        h("button", { class: "button", onclick: openStoreImportDrawer, title: "Cria várias lojas a partir de JSON" }, icon("copy"), "Importar JSON"),
        h("button", { class: "button", onclick: openStoreTestDrawer, title: "Testa várias lojas de uma vez e ativa as que funcionam" }, icon("flask"), "Testar lojas"),
        h("button", { class: "button primary", onclick: () => openStoreDrawer(null) }, icon("plus"), "Adicionar loja"))),
    h("div", { class: "filters" },
      h("input", {
        type: "text", value: state.storeQuery, placeholder: "Filtrar: nome, site, etiqueta…", style: "max-width:260px",
        "aria-label": "Filtrar lojas", oninput: (event) => { state.storeQuery = event.target.value; renderList(); },
      }),
      select(state.storeCountry, [["all", "Todas as áreas"], ...Object.entries(COUNTRIES).map(([key, [label]]) => [key, label])],
        (value) => { state.storeCountry = value; renderView(true); }, "Área"),
      select(state.storeTier, [["all", "Todos os tiers"], ...Object.entries(TIERS).map(([key, [label, , description]]) => [key, `${label} — ${description}`])],
        (value) => { state.storeTier = value; renderView(true); }, "Tier"),
      select(state.storeType, [["all", "Todos os tipos"], ...Object.entries(STORE_TYPES)],
        (value) => { state.storeType = value; renderView(true); }, "Tipo de loja"),
      tags.length ? select(state.storeTag, [["", "Todas as etiquetas"], ...tags.map((tag) => [tag, tag])], (value) => { state.storeTag = value; renderView(true); }, "Etiqueta") : null,
      select(state.storeTest, [["all", "Qualquer teste"], ["never", "Nunca testadas"], ["ok", "Último teste OK"], ["failed", "Último teste falhou"], ["auto", "Desligadas pelo bot"]],
        (value) => { state.storeTest = value; renderView(true); }, "Último teste"),
      h("span", { class: "muted filters-end" }, `${stores.length} lojas · ${stores.filter((store) => store.enabled).length} ativas`)),
    listBox,
  ];
}

/** Whether a store passes the Lojas page filters (area, tier, type, tag, last test, text). */
function storeMatchesFilters(store) {
  if (state.storeCountry !== "all" && store.country !== state.storeCountry) return false;
  if (state.storeTier !== "all" && (store.tier || "None") !== state.storeTier) return false;
  if (state.storeType !== "all" && (store.type || "Unknown") !== state.storeType) return false;
  if (state.storeTag && !(store.tags || []).includes(state.storeTag)) return false;
  const test = store.lastTest;
  if (state.storeTest === "never" && test) return false;
  if (state.storeTest === "ok" && test?.status !== "Ok") return false;
  if (state.storeTest === "failed" && !(test && TEST_FAILED.has(test.status))) return false;
  if (state.storeTest === "auto" && !(store.autoDisabledAt && !store.enabled)) return false;
  const words = normalizeText(state.storeQuery).split(/\s+/).filter(Boolean);
  const haystack = normalizeText(`${store.name} ${store.baseUrl} ${(store.tags || []).join(" ")} ${store.notes || ""}`);
  return words.every((word) => haystack.includes(word));
}

/** The classification pills of a store: tier, type and free tags. */
function storeTags(store) {
  const [tierLabel, tone] = TIERS[store.tier || "None"];
  return h("div", { class: "pill-row store-tags" },
    store.tier && store.tier !== "None" ? h("span", { class: `pill ${tone}` }, tierLabel) : null,
    store.type && store.type !== "Unknown" ? h("span", { class: "pill plain grey" }, STORE_TYPES[store.type]) : null,
    (store.tags || []).map((tag) => h("button", {
      class: `pill plain ${tag === state.storeTag ? "violet" : "grey"} tag-button`, title: `Filtrar pela etiqueta "${tag}"`,
      onclick: () => { state.storeTag = tag === state.storeTag ? "" : tag; renderView(true); },
    }, `#${tag}`)));
}

function storeCard(store) {
  const reports = searches().filter((search) => search.storeName === store.name);
  const failed = reports.filter((report) => !report.succeeded);
  const tasks = state.tasks.filter((task) => task.enabled && ((task.storeNames || []).length === 0 || task.storeNames.some((name) => sameName(name, store.name))));
  const lastChecked = reports.map((report) => report.checkedAt).sort().at(-1);
  const health = store.autoDisabledAt && !store.enabled
    ? h("span", { class: "pill red", title: `Desligada ${relative(store.autoDisabledAt)} após ${store.consecutiveFailures} ciclos seguidos com erro. Ligar volta a contar do zero.` }, "Desligada pelo bot")
    : store.platform === "Manual" ? h("span", { class: "pill grey" }, "Sem integração")
    : !store.enabled ? h("span", { class: "pill grey" }, "Desligada")
    : reports.length === 0 ? h("span", { class: "pill blue" }, "À espera")
      : failed.length === reports.length ? h("span", { class: "pill red" }, "Erro")
        : failed.length > 0 ? h("span", { class: "pill amber" }, "Parcial")
          : h("span", { class: "pill green" }, "OK");

  return h("div", { class: `card store-card ${store.enabled ? "" : "disabled"}` },
    h("div", { class: "card-body" },
      h("div", { class: "store-card-top" },
        storeAvatar(store.name),
        h("div", { style: "min-width:0" }, h("div", { class: "name" }, store.name),
          h("div", { class: "url" }, safeHref(store.baseUrl) ? h("a", { href: store.baseUrl, target: "_blank", rel: "noopener noreferrer" }, store.baseUrl) : store.baseUrl)),
        switchInput(store.enabled, (checked) => setStoreEnabled(store.name, checked), `Ativar ${store.name}`)),
      storeTags(store),
      h("dl", { class: "kv" },
        h("dt", {}, "Estado"), h("dd", {}, health),
        h("dt", {}, "Plataforma"), h("dd", {}, store.platform),
        h("dt", {}, "Sem marcador"), h("dd", {}, (UNMARKED_LANGUAGE[store.unmarkedLanguage] || UNMARKED_LANGUAGE.Unknown)[1]),
        h("dt", {}, "Tarefas"), h("dd", {}, tasks.length),
        h("dt", {}, "Verificada"), h("dd", {}, lastChecked ? relative(lastChecked) : "—"),
        h("dt", {}, "Último teste"), h("dd", {}, store.lastTest
          ? [testPill(store.lastTest), " ", h("span", { class: "muted" }, `${relative(store.lastTest.at)} · ${store.lastTest.results} produtos`)]
          : h("span", { class: "muted" }, "nunca")),
        store.enabled && store.consecutiveFailures > 0
          ? [h("dt", {}, "Ciclos c/ erro"), h("dd", { style: "color:var(--amber)" }, `${store.consecutiveFailures} seguidos`)] : null,
        failed.length > 0 ? [h("dt", {}, "Último erro"), h("dd", { style: "color:var(--red)" }, failed[0].error)] : null),
      store.notes ? h("p", { class: "store-notes" }, store.notes) : null),
    h("div", { class: "store-card-actions" },
      h("button", { class: "button small", onclick: () => { state.probe.storeName = store.name; state.probe.results = null; state.probe.errors = null; navigate("probe"); } }, icon("flask"), "Testar"),
      h("button", { class: "button small", onclick: () => openStoreDrawer(store) }, icon("edit"), "Editar"),
      h("span", { class: "spacer" }),
      h("button", { class: "icon-button delete", title: "Apagar loja", onclick: () => deleteStore(store) }, icon("trash"))));
}

function switchInput(checked, onChange, label) {
  return h("label", { class: "switch", title: label },
    h("input", { type: "checkbox", checked, "aria-label": label, onchange: (event) => onChange(event.target.checked) }),
    h("span", {}));
}

/* ----- Alerts ----- */

function viewAlerts() {
  return [
    h("div", { class: "page-head" },
      h("div", {}, h("h1", {}, "Alertas"), h("p", {}, "Tudo o que o bot já te avisou (mais recentes primeiro).")),
      h("div", { class: "actions" },
        h("button", { class: "button", onclick: clearAlertList, disabled: state.alerts.length === 0 }, icon("trash"), "Limpar lista"),
        h("button", { class: "button", onclick: resetAlertMemory, title: "Faz com que tudo o que está em stock seja avisado de novo" }, icon("refresh"), "Limpar memória de alertas"))),
    h("div", { class: "card" }, h("div", { class: "card-body flush" }, state.alerts.length === 0
      ? emptyState("bell", "Ainda nenhum alerta.")
      : alertsTable(state.alerts, true))),
  ];
}

/* ----- Probe ----- */

function viewProbe() {
  const probe = state.probe;
  const stores = state.stores;
  if (!probe.storeName && stores.length) probe.storeName = stores[0].name;

  const storeSelect = h("select", { onchange: (event) => { probe.storeName = event.target.value; } },
    stores.map((store) => h("option", { value: store.name, selected: store.name === probe.storeName }, store.name)));
  const queryInput = h("input", { type: "text", value: probe.query, placeholder: "ex.: elite trainer box", oninput: (event) => { probe.query = event.target.value; } });
  queryInput.addEventListener("keydown", (event) => { if (event.key === "Enter") runProbe(); });

  const tasks = state.tasks;
  let results = null;
  if (probe.loading) results = h("div", { class: "card-body muted" }, "A pesquisar na loja…");
  else if (probe.errors) results = h("div", { class: "card-body" }, errorList(probe.errors));
  else if (probe.results) {
    results = probe.results.length === 0
      ? emptyState("search", "A loja não devolveu resultados.")
      : listingsTable(probe.results, (listing) => h("div", { style: "display:flex;flex-wrap:wrap;gap:4px" },
        tasks.map((task) => {
          const [label, tone] = OUTCOMES[listing.outcomeByTask[task.id]] || ["—", "grey"];
          return h("span", { class: `pill plain ${tone}`, title: `${task.name}: ${label}` }, `${task.name || task.id}: ${label}`);
        })));
  }

  return [
    h("div", { class: "page-head" },
      h("div", {}, h("h1", {}, "Testar loja"), h("p", {}, "Faz uma pesquisa numa loja e mostra, para cada tarefa, porque é que cada produto corresponde ou não."))),
    h("div", { class: "card" },
      h("div", { class: "card-body" },
        h("div", { class: "fields", style: "grid-template-columns:minmax(0,1fr) minmax(0,2fr) auto;align-items:end" },
          h("div", { class: "field" }, h("label", {}, "Loja"), storeSelect),
          h("div", { class: "field" }, h("label", {}, "Pesquisa"), queryInput),
          h("button", { class: "button primary", style: "height:36px", disabled: probe.loading || stores.length === 0, onclick: runProbe }, icon("search"), "Testar")))),
    results ? h("div", { class: "card" },
      h("div", { class: "card-head" }, h("span", { class: "card-title" }, "Resultados"),
        probe.results ? h("div", { class: "actions" }, h("span", { class: "pill plain grey" }, `${probe.results.length} produto(s)`)) : null),
      h("div", { class: "card-body flush" }, results)) : null,
  ];
}

async function runProbe() {
  const store = state.stores.find((item) => item.name === state.probe.storeName);
  if (!store) return;
  state.probe.loading = true;
  state.probe.errors = null;
  renderView(true);
  try {
    state.probe.results = await post("/store/probe", { store: storeToSaveRequest(store, null), query: state.probe.query });
  } catch (error) {
    state.probe.results = null;
    state.probe.errors = error.errors || [error.message];
  } finally {
    state.probe.loading = false;
    if (state.view === "probe") renderView(true);
  }
}

/* ----- Settings ----- */

function viewSettings() {
  const settings = state.settings;
  const email = state.notification?.email;
  const whatsApp = state.notification?.whatsApp;
  const draft = {
    interval: Math.round(settings.pollIntervalSeconds / 60),
    jitter: Math.round(settings.pollJitterSeconds / 60),
    delay: settings.delayBetweenRequestsSeconds,
    userAgent: settings.userAgent,
    autoDisable: settings.autoDisableAfterFailures,
  };
  const errors = h("div", {});

  const numberField = (label, key, hint, min) => h("div", { class: "field" },
    h("label", {}, label),
    h("input", { type: "number", min, value: draft[key], oninput: (event) => { draft[key] = Number(event.target.value); } }),
    h("div", { class: "hint" }, hint));

  const save = async () => {
    errors.replaceChildren();
    try {
      state.settings = await post("/setting/save", {
        pollIntervalSeconds: draft.interval * 60,
        pollJitterSeconds: draft.jitter * 60,
        delayBetweenRequestsSeconds: draft.delay,
        userAgent: draft.userAgent.trim(),
        autoDisableAfterFailures: draft.autoDisable,
      });
      toast("Definições guardadas", "success");
    } catch (error) {
      errors.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  const channelCard = (title, iconName, channel, enabled, rows, note) => h("div", { class: "card" },
    h("div", { class: "card-head" }, h("span", { class: "card-title" }, title),
      h("div", { class: "actions" }, enabled ? h("span", { class: "pill green" }, "Ligado") : h("span", { class: "pill grey" }, "Desligado"))),
    h("div", { class: "card-body stack", style: "gap:12px" },
      h("dl", { class: "kv" }, rows.map(([label, value]) => [h("dt", {}, label), h("dd", {}, value)])),
      h("div", { class: "notice" }, note),
      h("div", {}, h("button", { class: "button", disabled: !enabled, onclick: () => sendTest(channel) }, icon(iconName), "Enviar mensagem de teste"))));

  const missing = (text) => h("span", { style: "color:var(--amber)" }, text);

  return [
    h("div", { class: "page-head" }, h("div", {}, h("h1", {}, "Definições"), h("p", {}, "Frequência das verificações, identificação do bot e canais de alerta."))),
    h("div", { class: "grid overview-bottom" },
      h("div", { class: "stack" },
        h("div", { class: "card" },
          h("div", { class: "card-head" }, h("span", { class: "card-title" }, "Monitorização")),
          h("div", { class: "card-body stack", style: "gap:14px" },
            h("div", { class: "fields" },
              numberField("Intervalo entre verificações (min)", "interval", "Mínimo 1. Entre 5 e 15 é simpático para as lojas.", 1),
              numberField("Variação aleatória (min)", "jitter", "Soma-se ao intervalo para não bater sempre à mesma hora.", 0),
              numberField("Pausa entre pedidos à mesma loja (s)", "delay", "Evita disparar pedidos seguidos.", 0),
              numberField("Desligar loja após N ciclos com erro", "autoDisable", "Quando todas as pesquisas de uma loja falham N ciclos seguidos, o bot desliga-a e avisa-te. 0 = nunca.", 0),
              h("div", { class: "field wide" },
                h("label", {}, "User-Agent"),
                h("input", { type: "text", value: draft.userAgent, oninput: (event) => { draft.userAgent = event.target.value; } }),
                h("div", { class: "hint" }, "Como o bot se identifica às lojas. Mantém-no honesto."))),
            errors,
            h("div", {}, h("button", { class: "button primary", onclick: save }, icon("check"), "Guardar definições")))),
        h("div", { class: "card" },
          h("div", { class: "card-head" }, h("span", { class: "card-title" }, "Memória de alertas")),
          h("div", { class: "card-body stack", style: "gap:12px" },
            h("p", { class: "muted", style: "margin:0" }, "O bot não repete o aviso de um produto que já te avisou enquanto continuar em stock. Limpar faz com que tudo o que está disponível seja avisado de novo."),
            h("div", {}, h("button", { class: "button", onclick: resetAlertMemory }, icon("refresh"), "Limpar memória"))))),
      h("div", { class: "stack" },
        !state.notification
          ? h("div", { class: "card" }, h("div", { class: "card-body muted" }, "A carregar canais…"))
          : [
            channelCard("WhatsApp", "chat", "WhatsApp", whatsApp.enabled, [
              ["Número", whatsApp.maskedPhone || missing("em falta")],
              ["API key", whatsApp.hasApiKey ? "definida" : missing("em falta")],
            ], "Configura-se no .env (WhatsApp__Enabled, WhatsApp__Phone, WhatsApp__ApiKey). A chave nunca passa pelo dashboard."),
            channelCard("Email", "mail", "Email", email.enabled, [
              ["Servidor", email.smtpHost || "—"],
              ["De", email.from || "—"],
              ["Para", email.to.length ? email.to.join(", ") : "—"],
              ["Password", email.hasPassword ? "definida" : missing("em falta")],
            ], "Configura-se no .env (Email__…). A password nunca passa pelo dashboard."),
          ])),
  ];
}

/* -------------------------------------------------------------- drawers -- */

function openDrawer({ title, subtitle, body, footer, wide = false }) {
  const drawer = document.getElementById("drawer");
  drawer.classList.toggle("wide", wide);
  drawer.replaceChildren(
    h("div", { class: "drawer-head" },
      h("div", {}, h("h2", {}, title), subtitle ? h("p", {}, subtitle) : null),
      h("button", { class: "icon-button", "aria-label": "Fechar", onclick: closeDrawer }, icon("close"))),
    h("div", { class: "drawer-body" }, body),
    h("div", { class: "drawer-foot" }, footer));
  drawer.classList.add("open");
  drawer.setAttribute("aria-hidden", "false");
  document.getElementById("drawer-backdrop").classList.add("open");
  setTimeout(() => drawer.querySelector("input, select")?.focus(), 250);
}

function closeDrawer() {
  const drawer = document.getElementById("drawer");
  drawer.classList.remove("open");
  drawer.setAttribute("aria-hidden", "true");
  document.getElementById("drawer-backdrop").classList.remove("open");
}

function field(label, control, hint, wide = false) {
  return h("div", { class: `field ${wide ? "wide" : ""}` }, h("label", {}, label), control, hint ? h("div", { class: "hint" }, hint) : null);
}

function textInput(value, onInput, attributes = {}) {
  return h("input", { type: "text", value: value ?? "", oninput: (event) => onInput(event.target.value), ...attributes });
}

function errorList(errors) {
  return h("div", { class: "errors" }, h("ul", {}, (errors.length ? errors : ["Erro desconhecido."]).map((message) => h("li", {}, message))));
}

function openTaskDrawer(task, isCopy = false) {
  const isNew = !task || isCopy;
  const draft = {
    id: task?.id || "",
    name: task?.name || "",
    enabled: task?.enabled ?? true,
    searchQuery: task?.searchQuery || "",
    requiredKeywords: (task?.requiredKeywords || []).join(", "),
    excludedKeywords: (task?.excludedKeywords || ["sleeves", "binder", "proxy", "break", "acrylic", "acrilico"]).join(", "),
    maxPrice: task?.maxPrice ?? "",
    storeNames: new Set(task?.storeNames || []),
    language: task?.language || "English",
    game: task?.game || "Pokemon",
  };
  const errors = h("div", {});
  const idInput = textInput(draft.id, (value) => { draft.id = value; }, { readOnly: !isNew, placeholder: "gerado a partir do nome" });
  idInput.addEventListener("input", () => { idInput.dataset.touched = "1"; });

  const body = [
    h("div", { class: "drawer-section" },
      h("h3", {}, "Produto"),
      h("div", { class: "fields" },
        field("Jogo", h("select", { onchange: (event) => { draft.game = event.target.value; } },
          Object.entries(GAMES).map(([value, [label]]) => h("option", { value, selected: draft.game === value }, label))),
          "Serve para agrupar e filtrar as tarefas."),
        field("Idioma do produto", languageSwitch(draft.language, (value) => { draft.language = value; }),
          "Japonês: só avisa quando o título diz JP / Japanese / Japonês. Não precisas de pôr “japanese” na pesquisa.", true),
        field("Nome", textInput(draft.name, (value) => {
          draft.name = value;
          if (isNew && !idInput.dataset.touched) { draft.id = slugify(value); idInput.value = draft.id; }
        }, { placeholder: "ex.: Perfect Order ETB" }), null, true),
        field("Texto de pesquisa", textInput(draft.searchQuery, (value) => { draft.searchQuery = value; }, { placeholder: "elite trainer box" }),
          "O que é escrito na pesquisa de cada loja. Evita “pokemon”: há lojas que não o põem nos títulos.", true),
        field("Palavras obrigatórias", textInput(draft.requiredKeywords, (value) => { draft.requiredKeywords = value; }, { placeholder: "perfect order, elite trainer box" }), "Separadas por vírgula; todas têm de estar no título. Alternativas com | (booster box|booster display = uma delas) e * para o início de uma palavra (op* apanha OP-15 e OP10).", true),
        field("Palavras excluídas", textInput(draft.excludedKeywords, (value) => { draft.excludedKeywords = value; }), "Separadas por vírgula; nenhuma pode estar no título. Também aceitam | e *.", true),
        field("Preço máximo (€)", h("input", { type: "number", min: 0, step: "0.01", value: draft.maxPrice, oninput: (event) => { draft.maxPrice = event.target.value; } }), "Acima disto não avisa."),
        field("Id", idInput, isNew ? "Identificador interno (não muda depois)." : "Não pode ser alterado."))),
    h("div", { class: "drawer-section" },
      h("h3", {}, "Lojas"),
      h("div", { class: "hint dim" }, "Nenhuma selecionada = todas as lojas ativas."),
      h("div", { class: "check-list" }, state.stores.map((store) =>
        h("label", { class: "check-pill" },
          h("input", { type: "checkbox", checked: [...draft.storeNames].some((name) => sameName(name, store.name)), onchange: (event) => {
            for (const name of [...draft.storeNames]) if (sameName(name, store.name)) draft.storeNames.delete(name);
            if (event.target.checked) draft.storeNames.add(store.name);
          } }),
          store.name, store.enabled ? null : h("span", { class: "dim" }, "(desligada)"))))),
    h("div", { class: "drawer-section" },
      h("div", { style: "display:flex;align-items:center;gap:12px" },
        switchInput(draft.enabled, (checked) => { draft.enabled = checked; }, "Tarefa ativa"),
        h("span", {}, "Tarefa ativa"))),
    errors,
  ];

  const save = async () => {
    errors.replaceChildren();
    try {
      await post("/stocktask/save", {
        isNew,
        id: draft.id.trim(),
        name: draft.name.trim(),
        enabled: draft.enabled,
        storeNames: [...draft.storeNames],
        searchQuery: draft.searchQuery.trim(),
        requiredKeywords: splitList(draft.requiredKeywords),
        excludedKeywords: splitList(draft.excludedKeywords),
        maxPrice: Number(draft.maxPrice) || 0,
        language: draft.language,
        game: draft.game,
      });
      await reloadConfiguration();
      closeDrawer();
      toast(isNew ? "Tarefa criada" : "Tarefa guardada", "success");
    } catch (error) {
      errors.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  openDrawer({
    title: isNew ? "Nova tarefa" : "Editar tarefa",
    subtitle: "Um produto a vigiar, com o preço máximo que aceitas.",
    body,
    footer: [h("button", { class: "button ghost", onclick: closeDrawer }, "Cancelar"), h("button", { class: "button primary", onclick: save }, icon("check"), isNew ? "Criar tarefa" : "Guardar")],
  });
}

/* ----- Tasks: JSON import / export ----- */

const TASK_IMPORT_EXAMPLE = `[
  {
    "id": "pkm-etb-en",
    "name": "Pokémon ETB (EN)",
    "searchQuery": "elite trainer box",
    "requiredKeywords": ["pokemon", "elite trainer box"],
    "excludedKeywords": ["sleeves", "binder", "proxy"],
    "maxPrice": 60,
    "language": "English",
    "game": "Pokemon",
    "storeNames": [],
    "enabled": true
  }
]`;

/** The task fields the import accepts, in the same shape "Exportar JSON" produces. */
function taskToJson(task) {
  return {
    id: task.id, name: task.name, enabled: task.enabled, storeNames: task.storeNames || [], searchQuery: task.searchQuery,
    requiredKeywords: task.requiredKeywords || [], excludedKeywords: task.excludedKeywords || [], maxPrice: task.maxPrice, language: task.language,
    game: task.game || "Pokemon",
  };
}

function exportTasks() {
  const json = JSON.stringify(state.tasks.map(taskToJson), null, 2);
  const url = URL.createObjectURL(new Blob([json], { type: "application/json" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = `tcgstockbot-tarefas-${new Date().toISOString().slice(0, 10)}.json`;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/**
 * Turns one imported object into a StockTaskSaveRequest (without isNew). Lenient on the shape — keywords as an
 * array or "a, b" text, language as English/Japanese/EN/JP, price as 59.99 or "59,99" — the API still validates.
 */
function normalizeImportedTask(raw) {
  if (!raw || typeof raw !== "object" || Array.isArray(raw)) throw new Error("não é um objeto");
  const list = (value) => Array.isArray(value) ? value.map((item) => String(item).trim()).filter(Boolean) : splitList(String(value ?? ""));
  const name = String(raw.name ?? "").trim();
  const id = String(raw.id ?? "").trim()
    || name.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
  const languageText = String(raw.language ?? "English").trim().toLowerCase();
  const language = ["japanese", "jp", "jpn", "japones", "japonês", "ja"].includes(languageText) ? "Japanese"
    : ["english", "en", "eng", "ingles", "inglês"].includes(languageText) ? "English" : null;
  if (!language) throw new Error(`idioma "${raw.language}" desconhecido (usa English ou Japanese)`);
  const gameText = String(raw.game ?? "Pokemon").trim().toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[^a-z]/g, "");
  const game = { pokemon: "Pokemon", onepiece: "OnePiece", op: "OnePiece", lorcana: "Lorcana", magic: "Magic", mtg: "Magic", magicthegathering: "Magic", other: "Other", outro: "Other" }[gameText];
  if (!game) throw new Error(`jogo "${raw.game}" desconhecido (usa Pokemon, OnePiece, Lorcana, Magic ou Other)`);
  return {
    id, name, enabled: raw.enabled === undefined ? true : Boolean(raw.enabled),
    storeNames: list(raw.storeNames), searchQuery: String(raw.searchQuery ?? "").trim(),
    requiredKeywords: list(raw.requiredKeywords), excludedKeywords: list(raw.excludedKeywords),
    maxPrice: typeof raw.maxPrice === "number" ? raw.maxPrice : Number(String(raw.maxPrice ?? "").replace(",", ".")) || 0,
    language,
    game,
  };
}

function openTaskImportDrawer() {
  let text = "";
  let updateExisting = false;
  let running = false;
  const results = h("div", {});
  const textarea = h("textarea", {
    rows: 18, spellcheck: "false", placeholder: TASK_IMPORT_EXAMPLE, style: "font-family:ui-monospace,Consolas,monospace;font-size:12px",
    oninput: (event) => { text = event.target.value; },
  });
  const fileInput = h("input", {
    type: "file", accept: ".json,application/json",
    onchange: async (event) => { const file = event.target.files?.[0]; if (file) { text = await file.text(); textarea.value = text; } },
  });

  const runImport = async () => {
    if (running) return;
    let items;
    try {
      const parsed = JSON.parse(text.trim() || "null");
      items = Array.isArray(parsed) ? parsed : Array.isArray(parsed?.tasks) ? parsed.tasks : parsed && typeof parsed === "object" ? [parsed] : null;
      if (!items || items.length === 0) throw new Error("esperava uma lista de tarefas: [ { ... }, { ... } ]");
    } catch (error) {
      results.replaceChildren(errorList([`JSON inválido: ${error.message}`]));
      return;
    }

    running = true;
    const existing = new Set(state.tasks.map((task) => task.id.toLowerCase()));
    const rows = [];
    let created = 0, updated = 0, skipped = 0, failed = 0;
    for (const [index, raw] of items.entries()) {
      results.replaceChildren(h("div", { class: "muted" }, `A importar… ${index + 1}/${items.length}`));
      const label = raw?.name || raw?.id || `#${index + 1}`;
      try {
        const task = normalizeImportedTask(raw);
        const exists = existing.has(task.id.toLowerCase());
        if (exists && !updateExisting) { skipped++; rows.push([label, "grey", "já existe — ignorada"]); continue; }
        await post("/stocktask/save", { isNew: !exists, ...task });
        existing.add(task.id.toLowerCase());
        if (exists) { updated++; rows.push([label, "violet", "atualizada"]); } else { created++; rows.push([label, "green", "criada"]); }
      } catch (error) {
        failed++;
        rows.push([label, "red", (error.errors || [error.message]).join("; ")]);
      }
    }
    running = false;

    await reloadConfiguration();
    results.replaceChildren(
      h("div", { class: "notice violet" }, `${created} criada(s), ${updated} atualizada(s), ${skipped} ignorada(s), ${failed} com erro.`),
      h("ul", { class: "import-results" }, rows.map(([label, tone, message]) => h("li", {}, h("span", { class: `pill ${tone}` }, message), h("span", {}, label)))));
    if (failed === 0) toast(`Importação concluída: ${created + updated} tarefa(s)`, "success");
  };

  openDrawer({
    title: "Importar tarefas (JSON)",
    subtitle: "Cola uma lista de tarefas ou escolhe um ficheiro. O formato é o mesmo do \"Exportar JSON\".",
    body: [
      h("div", { class: "drawer-section" },
        field("Ficheiro", fileInput, "Opcional: carrega um .json em vez de colar.", true),
        field("JSON", textarea, "Campos: id, name, searchQuery, requiredKeywords, excludedKeywords, maxPrice, language (English/Japanese), game (Pokemon/OnePiece/Lorcana/Magic/Other), storeNames ([] = todas), enabled. Sem id, é gerado a partir do nome.", true),
        h("div", { style: "display:flex;align-items:center;gap:12px;margin-top:8px" },
          switchInput(updateExisting, (checked) => { updateExisting = checked; }, "Atualizar as que já existem"),
          h("span", {}, "Atualizar as tarefas que já existem (mesmo id)"))),
      results,
    ],
    footer: [h("button", { class: "button ghost", onclick: closeDrawer }, "Fechar"), h("button", { class: "button primary", onclick: runImport }, icon("check"), "Importar")],
  });
}

/** Builds the StoreSaveRequest the API expects from a store draft. */
function storeToSaveRequest(store, originalName) {
  return {
    originalName,
    name: (store.name || "").trim(),
    enabled: store.enabled,
    platform: store.platform,
    baseUrl: (store.baseUrl || "").trim(),
    unmarkedLanguage: store.unmarkedLanguage,
    wooCommerceProductsPath: store.wooCommerceProductsPath,
    shopifyCatalogPaths: store.platform === "Shopify" ? (store.shopifyCatalogPaths || "").trim() : "",
    html: store.platform === "Html" ? store.html : null,
    awinFeedKey: store.platform === "Awin" ? (store.awinFeedKey || "").trim() : "",
    country: store.country || "Portugal",
    tier: store.tier || "None",
    type: store.type || "Unknown",
    tags: Array.isArray(store.tags) ? store.tags : splitList(String(store.tags || "")),
    notes: (store.notes || "").trim(),
  };
}

function openStoreDrawer(store, preset = null) {
  const isNew = !store;
  const originalName = store?.name || null;
  const draft = structuredClone(preset || store || {
    name: "", enabled: true, platform: "Shopify", baseUrl: "https://", unmarkedLanguage: "Unknown",
    wooCommerceProductsPath: "/wp-json/wc/store/v1/products", shopifyCatalogPaths: "", html: null,
    awinFeedKey: "", country: "Portugal", tier: "None", type: "Unknown", tags: [], notes: "",
  });
  draft.html = draft.html || { searchUrlTemplate: "", productSelector: "", titleSelector: "", priceSelector: "", linkSelector: "", outOfStockSelector: "", inStockSelector: "" };

  const errors = h("div", {});
  const platformFields = h("div", { class: "drawer-section" });
  const testResults = h("div", {});
  let testQuery = "elite trainer box";

  const renderPlatformFields = () => {
    if (draft.platform === "WooCommerce") {
      platformFields.replaceChildren(h("h3", {}, "WooCommerce"),
        field("Caminho da Store API", textInput(draft.wooCommerceProductsPath, (value) => { draft.wooCommerceProductsPath = value; }),
          "Normalmente /wp-json/wc/store/v1/products. Algumas lojas só respondem sem o /v1."));
    } else if (draft.platform === "Html") {
      const selector = (label, key, hint) => field(label, textInput(draft.html[key], (value) => { draft.html[key] = value; }), hint);
      platformFields.replaceChildren(h("h3", {}, "Seletores HTML"),
        h("div", { class: "fields" },
          field("URL de pesquisa", textInput(draft.html.searchUrlTemplate, (value) => { draft.html.searchUrlTemplate = value; }, { placeholder: "https://loja.pt/pesquisa?q={query}" }),
            "Usa {query} para o texto de pesquisa, ou o URL fixo de uma categoria.", true),
          selector("Cartão do produto", "productSelector", "ex.: li.product-item"),
          selector("Título", "titleSelector", "vazio = texto do cartão"),
          selector("Preço", "priceSelector", "ex.: .price"),
          selector("Link", "linkSelector", "vazio = primeiro <a>"),
          selector("Marcador de esgotado", "outOfStockSelector", "existe só se esgotado"),
          selector("Marcador de disponível", "inStockSelector", "ex.: botão de comprar")));
    } else if (draft.platform === "Amazon") {
      platformFields.replaceChildren(h("div", { class: "notice violet" },
        "Usa a Creators API oficial da Amazon (sem scraping). Preenche Amazon__CredentialId, Amazon__CredentialSecret e Amazon__PartnerTag no .env. "
        + "URL base: https://www.amazon.es. O texto de pesquisa pode ser palavras-chave ou um ASIN (ex.: B0XXXXXXXX)."));
    } else if (draft.platform === "Awin") {
      platformFields.replaceChildren(h("h3", {}, "Feed Awin"),
        field("Chave do feed", textInput(draft.awinFeedKey, (value) => { draft.awinFeedKey = value; }, { placeholder: "ex.: MediaMarktES" }),
          "Só letras, números e _. O bot lê o URL do Create-a-Feed de AwinFeeds__Feeds__<chave> no .env (o site da loja nunca é contactado). "
          + "Só funciona depois de a loja te aprovar no programa Awin dela.", true));
    } else if (draft.platform === "Manual") {
      platformFields.replaceChildren(h("div", { class: "notice" },
        "Sem integração: a loja fica na lista (link, tier, etiquetas) mas o bot nunca a pesquisa. Escolhe outra plataforma quando souberes como a ler."));
    } else if (AWIN_FEED_KEYS[draft.platform]) {
      platformFields.replaceChildren(h("div", { class: "notice violet" },
        "Lê o feed de produtos Awin autorizado desta loja (o site da loja não é contactado). Põe o URL do Create-a-Feed em "
        + `AwinFeeds__Feeds__${AWIN_FEED_KEYS[draft.platform]} no .env. O feed é descarregado no máximo de 6 em 6 horas; sem feed, a loja mostra "integração não configurada".`));
    } else {
      platformFields.replaceChildren(h("h3", {}, "Shopify"),
        field("Catálogo (opcional)", textInput(draft.shopifyCatalogPaths, (value) => { draft.shopifyCatalogPaths = value; }, { placeholder: "/collections/pokemon, /collections/riftbound" }),
          "Vazio = pesquisa da loja (no máximo 10 produtos por pesquisa). Com coleções (separadas por vírgula) ou /products.json, o bot lê o catálogo "
          + "uma vez por ciclo e filtra-o para todas as tarefas — sem limite de 10. Prefere coleções: /products.json lê a loja inteira.", true));
    }
  };
  renderPlatformFields();

  const runTest = async () => {
    testResults.replaceChildren(h("div", { class: "muted" }, "A pesquisar…"));
    try {
      const results = await post("/store/probe", { store: storeToSaveRequest(draft, null), query: testQuery });
      testResults.replaceChildren(results.length === 0
        ? h("div", { class: "notice" }, "A loja respondeu, mas sem produtos para esta pesquisa.")
        : h("div", { class: "detail-store" },
          h("div", { class: "detail-store-head" }, `${results.length} produto(s) encontrados`),
          listingsTable(results.slice(0, 10), (listing) => {
            const matched = Object.values(listing.outcomeByTask).some(isMatch);
            return matched ? h("span", { class: "pill green" }, "Corresponde") : h("span", { class: "pill plain grey" }, "—");
          })));
    } catch (error) {
      testResults.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  const testQueryInput = textInput(testQuery, (value) => { testQuery = value; });
  testQueryInput.addEventListener("keydown", (event) => { if (event.key === "Enter") runTest(); });

  const body = [
    draft.detect ? detectNotice(draft.detect) : null,
    h("div", { class: "drawer-section" },
      h("h3", {}, "Loja"),
      h("div", { class: "fields" },
        field("Nome", textInput(draft.name, (value) => { draft.name = value; }, { placeholder: "ex.: Versus Gamecenter" })),
        field("Plataforma", h("select", { onchange: (event) => { draft.platform = event.target.value; renderPlatformFields(); } },
          Object.entries(PLATFORMS).map(([value, label]) => h("option", { value, selected: draft.platform === value }, label)))),
        field("URL base", h("div", { class: "input-with-button" },
          textInput(draft.baseUrl, (value) => { draft.baseUrl = value; }, { placeholder: "https://loja.pt" }),
          h("button", { class: "button", onclick: () => detectInDrawer(store, draft), title: "Descobre se é Shopify, WooCommerce ou PrestaShop e preenche a configuração" }, icon("search"), "Detetar")),
          "Cola o site (ou a página da categoria Pokémon) e carrega em Detetar.", true),
        field("Títulos sem indicação de idioma", h("select", { onchange: (event) => { draft.unmarkedLanguage = event.target.value; } },
          Object.entries(UNMARKED_LANGUAGE).map(([value, [label]]) => h("option", { value, selected: draft.unmarkedLanguage === value }, label))),
        "“Contar como inglês” só se a loja marca sempre os PT/ES/JP. “Idioma por confirmar” avisa na mesma, com um aviso para confirmares na página.", true)),
      h("div", { style: "display:flex;align-items:center;gap:12px" },
        switchInput(draft.enabled, (checked) => { draft.enabled = checked; }, "Loja ativa"), h("span", {}, "Loja ativa"))),
    h("div", { class: "drawer-section" },
      h("h3", {}, "Classificação"),
      h("div", { class: "fields" },
        field("Área", h("select", { onchange: (event) => { draft.country = event.target.value; } },
          Object.entries(COUNTRIES).map(([value, [label]]) => h("option", { value, selected: draft.country === value }, label)))),
        field("Tier", h("select", { onchange: (event) => { draft.tier = event.target.value; } },
          Object.entries(TIERS).map(([value, [label, , description]]) => h("option", { value, selected: (draft.tier || "None") === value }, `${label} — ${description}`)))),
        field("Tipo de loja", h("select", { onchange: (event) => { draft.type = event.target.value; } },
          Object.entries(STORE_TYPES).map(([value, label]) => h("option", { value, selected: (draft.type || "Unknown") === value }, label)))),
        field("Etiquetas", textInput((draft.tags || []).join(", "), (value) => { draft.tags = splitList(value); }, { placeholder: "por testar, envia para PT" }),
          "Separadas por vírgula. Aparecem no cartão e servem de filtro."),
        field("Notas", h("textarea", { rows: 3, oninput: (event) => { draft.notes = event.target.value; } }, draft.notes || ""), null, true))),
    platformFields,
    h("div", { class: "drawer-section" },
      h("h3", {}, "Testar antes de guardar"),
      h("div", { style: "display:flex;gap:8px" }, testQueryInput, h("button", { class: "button", onclick: runTest }, icon("flask"), "Testar")),
      testResults),
    errors,
  ];

  const save = async () => {
    errors.replaceChildren();
    try {
      await post("/store/save", storeToSaveRequest(draft, originalName));
      await reloadConfiguration();
      closeDrawer();
      toast(isNew ? "Loja adicionada" : "Loja guardada", "success");
    } catch (error) {
      errors.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  openDrawer({
    title: isNew ? "Adicionar loja" : `Editar ${store.name}`,
    subtitle: "Testa a configuração antes de guardar.",
    body,
    footer: [h("button", { class: "button ghost", onclick: closeDrawer }, "Cancelar"), h("button", { class: "button primary", onclick: save }, icon("check"), isNew ? "Adicionar" : "Guardar")],
  });
}

/* ----- Stores: detect platform ----- */

/** Applies a detection to a store draft (platform setup only; name only when empty). */
function applyDetection(draft, result) {
  if (result.status !== "Detected") return draft;
  return {
    ...draft,
    name: (draft.name || "").trim() || result.suggestedName,
    baseUrl: result.baseUrl,
    platform: result.platform,
    shopifyCatalogPaths: result.platform === "Shopify" ? result.shopifyCatalogPaths : "",
    wooCommerceProductsPath: result.platform === "WooCommerce" ? result.wooCommerceProductsPath : draft.wooCommerceProductsPath,
    html: result.platform === "Html" ? result.html : draft.html,
  };
}

async function detectInDrawer(store, draft) {
  try {
    toast("A detetar a plataforma…", "info");
    const result = await post("/store/detect", { url: draft.baseUrl });
    const next = applyDetection(draft, result);
    if (result.status !== "Detected") { next.baseUrl = result.baseUrl || draft.baseUrl; if (!next.name) next.name = result.suggestedName || ""; }
    next.detect = result;
    openStoreDrawer(store, next);
  } catch (error) {
    toast((error.errors || [error.message]).join(" "), "error");
  }
}

function detectNotice(result) {
  const tone = result.status === "Detected" ? "violet" : result.status === "Unknown" ? "" : "amber";
  return h("div", { class: `notice ${tone} detect-notice` },
    h("div", {}, h("strong", {}, result.message)),
    result.evidence?.length ? h("details", {}, h("summary", {}, "O que foi verificado"),
      h("ul", {}, result.evidence.map((line) => h("li", {}, line)))) : null);
}

/* ----- Stores: test many at once ----- */

const TEST_WORKERS = 4;

function openStoreTestDrawer() {
  const scopes = {
    visible: ["As visíveis com os filtros atuais", () => state.stores.filter(storeMatchesFilters)],
    never: ["Nunca testadas", () => state.stores.filter((store) => !store.lastTest)],
    failed: ["Falharam no último teste", () => state.stores.filter((store) => store.lastTest && TEST_FAILED.has(store.lastTest.status))],
    disabled: ["Desligadas", () => state.stores.filter((store) => !store.enabled)],
    all: ["Todas", () => state.stores],
  };
  let scope = "visible";
  let query = "pokemon";
  let running = false;
  let stopRequested = false;
  let list = [];
  const rows = new Map();                                  // name -> { state: "waiting" | "running" | "done", result, note }

  const table = h("div", {});
  const progress = h("div", { class: "muted" });
  const actions = h("div", { class: "test-actions" });
  const scopeCount = h("span", { class: "muted" });
  const refreshCount = () => { scopeCount.textContent = `${scopes[scope][1]().length} loja(s)`; };

  const render = () => {
    const done = [...rows.values()].filter((row) => row.state === "done").length;
    progress.textContent = rows.size ? `${done} de ${rows.size} testadas${running ? "…" : ""}` : "";
    table.replaceChildren(rows.size === 0 ? h("div", { class: "muted" }, "Escolhe as lojas e carrega em Começar.") : h("div", { class: "table-wrap" }, h("table", {},
      h("thead", {}, h("tr", {}, h("th", {}, "Loja"), h("th", {}, "Resultado"), h("th", { class: "num" }, "Produtos"), h("th", { class: "num" }, "Reconhecidos"), h("th", {}, "Exemplo / motivo"))),
      h("tbody", {}, list.map((listed) => {
        const store = state.stores.find((candidate) => candidate.name === listed.name) || listed;   // enabled / platform may have changed
        const row = rows.get(store.name);
        const result = row?.result;
        return h("tr", {},
          h("td", { class: "nowrap" }, h("span", { class: "store-link" }, storeAvatar(store.name), store.name),
            h("div", { class: "was-price plain" }, `${store.platform}${store.enabled ? " · ativa" : " · desligada"}`)),
          h("td", { class: "nowrap" }, row?.state === "running" ? h("span", { class: "pill violet pulse" }, "A testar…")
            : result ? testPill(result) : h("span", { class: "pill plain grey" }, "À espera")),
          h("td", { class: "num" }, result ? result.results : "—"),
          h("td", { class: "num" }, result ? `${result.relevant}${result.matches ? ` (${result.matches} alerta)` : ""}` : "—"),
          h("td", { class: "title-cell" },
            result ? h("span", { class: "primary-text", title: result.sample || result.message }, result.sample || result.message || "—") : null,
            row?.note ? h("div", { class: "was-price plain" }, row.note) : null));
      })))));
  };

  const testOne = async (store) => {
    const row = rows.get(store.name);
    row.state = "running";
    render();
    try {
      row.result = await post("/store/test", { name: store.name, query });
    } catch (error) {
      row.result = { name: store.name, status: "Error", results: 0, relevant: 0, matches: 0, message: (error.errors || [error.message]).join(" ") };
    }
    row.state = "done";
    render();
  };

  const runAll = async (stores) => {
    running = true;
    stopRequested = false;
    renderActions();
    const queue = [...stores];
    await Promise.all(Array.from({ length: TEST_WORKERS }, async () => {
      while (queue.length && !stopRequested) await testOne(queue.shift());
    }));
    running = false;
    await reloadConfiguration();
    render();
    renderActions();
  };

  const start = () => {
    if (running) return;
    list = scopes[scope][1]().slice().sort((a, b) => a.name.localeCompare(b.name, "pt"));
    rows.clear();
    list.forEach((store) => rows.set(store.name, { state: "waiting" }));
    render();
    runAll(list);
  };

  const byResult = (predicate) => list.filter((store) => {
    const result = rows.get(store.name)?.result;
    const current = state.stores.find((candidate) => candidate.name === store.name) || store;
    return result && predicate(result, current);
  }).map((store) => state.stores.find((candidate) => candidate.name === store.name) || store);

  const bulk = async (stores, action, message) => {
    running = true;
    renderActions();
    let failed = 0;
    for (const store of stores) {
      try { await action(store); } catch (error) { failed++; const row = rows.get(store.name); if (row) row.note = (error.errors || [error.message]).join(" "); }
    }
    running = false;
    await reloadConfiguration();
    render();
    renderActions();
    toast(failed ? `${message}: ${stores.length - failed} ok, ${failed} com erro` : `${message}: ${stores.length}`, failed ? "error" : "success");
  };

  const redetect = async (stores) => {
    running = true;
    renderActions();
    for (const store of stores) {
      const row = rows.get(store.name);
      row.state = "running";
      render();
      try {
        const detection = await post("/store/detect", { url: store.baseUrl });
        if (detection.status === "Detected" && (detection.platform !== store.platform || detection.shopifyCatalogPaths !== (store.shopifyCatalogPaths || ""))) {
          const updated = applyDetection(store, detection);
          await post("/store/save", storeToSaveRequest({ ...updated, tags: (store.tags || []).filter((tag) => tag !== "plataforma por identificar") }, store.name));
          row.note = `Detetado ${detection.platform}${detection.shopifyCatalogPaths ? ` (${detection.shopifyCatalogPaths})` : ""} — configuração atualizada.`;
          row.result = await post("/store/test", { name: store.name, query });
        } else {
          row.note = detection.message;
        }
      } catch (error) {
        row.note = (error.errors || [error.message]).join(" ");
      }
      row.state = "done";
      render();
    }
    running = false;
    await reloadConfiguration();
    render();
    renderActions();
  };

  function renderActions() {
    if (running) {
      actions.replaceChildren(h("button", { class: "button", onclick: () => { stopRequested = true; } }, icon("stop"), "Parar"));
      return;
    }
    if (rows.size === 0) { actions.replaceChildren(); return; }
    const toEnable = byResult((result, store) => result.status === "Ok" && !store.enabled && store.platform !== "Manual");
    const toDisable = byResult((result, store) => TEST_FAILED.has(result.status) && store.enabled);
    const blocked = byResult((result, store) => result.status === "Blocked" && store.platform !== "Manual");
    const toDetect = byResult((result) => ["Unreachable", "Error", "Empty", "NoIntegration"].includes(result.status));
    const action = (stores, label, onClick, primary) => h("button", { class: `button ${primary ? "primary" : ""}`, disabled: stores.length === 0, onclick: onClick }, `${label} (${stores.length})`);
    actions.replaceChildren(
      action(toEnable, "Ativar as que funcionam", () => bulk(toEnable, (store) => post("/store/enable", { name: store.name, enabled: true }), "Lojas ativadas"), true),
      action(toDisable, "Desligar as que falharam", () => bulk(toDisable, (store) => post("/store/enable", { name: store.name, enabled: false }), "Lojas desligadas")),
      action(blocked, "Bloqueadas → Sem integração", () => bulk(blocked, (store) => post("/store/save", storeToSaveRequest({
        ...store, platform: "Manual", enabled: false, tags: [...new Set([...(store.tags || []), "bloqueia bots (403)"])],
      }, store.name)), "Marcadas como Sem integração")),
      action(toDetect, "Voltar a detetar a plataforma", () => redetect(toDetect)),
    );
  }

  const scopeSelect = h("select", { onchange: (event) => { scope = event.target.value; refreshCount(); } },
    Object.entries(scopes).map(([value, [label]]) => h("option", { value, selected: value === scope }, label)));
  refreshCount();
  render();

  openDrawer({
    title: "Testar lojas",
    subtitle: `Faz uma pesquisa em cada loja (${TEST_WORKERS} de cada vez, com os limites por domínio) e guarda o resultado no cartão. Lojas "Sem integração" não são contactadas.`,
    body: [
      h("div", { class: "drawer-section" },
        h("div", { class: "fields" },
          field("Lojas", h("div", { class: "input-with-button" }, scopeSelect, scopeCount)),
          field("Texto de pesquisa", textInput(query, (value) => { query = value; }), "\"pokemon\" apanha quase tudo; usa \"elite trainer box\" para algo mais fino.")),
        h("div", { style: "display:flex;align-items:center;gap:10px" }, h("button", { class: "button primary", onclick: start }, icon("play"), "Começar"), progress)),
      h("div", { class: "drawer-section" }, table),
      h("div", { class: "drawer-section" }, h("h3", {}, "Depois do teste"), actions),
    ],
    footer: [h("button", { class: "button ghost", onclick: () => { stopRequested = true; closeDrawer(); } }, "Fechar")],
    wide: true,
  });
}

/* ----- Stores: JSON import / export ----- */

function exportStores() {
  const json = JSON.stringify(state.stores.map((store) => ({ ...storeToSaveRequest(store, null), originalName: undefined })), null, 2);
  downloadJson(json, `tcgstockbot-lojas-${new Date().toISOString().slice(0, 10)}.json`);
}

function downloadJson(json, fileName) {
  const url = URL.createObjectURL(new Blob([json], { type: "application/json" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** The site of a store URL ("loja.pt"); a URL with a query (a search link) keeps it, so two searches are not one site. */
const hostOf = (url) => {
  try {
    const parsed = new URL(url);
    return (parsed.host.replace(/^www\./, "") + parsed.search).toLowerCase();
  } catch {
    return "";
  }
};

/**
 * Imports stores in the "Exportar JSON" shape. A store that already exists (same name, or same site) is not replaced:
 * with "Atualizar classificação" only its area, tier, type, tags and notes change — its platform setup is kept.
 */
function openStoreImportDrawer() {
  let text = "";
  let updateClassification = true;
  let running = false;
  const results = h("div", {});
  const textarea = h("textarea", {
    rows: 16, spellcheck: "false", style: "font-family:ui-monospace,Consolas,monospace;font-size:12px",
    placeholder: '[\n  { "name": "Loja", "platform": "Shopify", "baseUrl": "https://loja.pt", "country": "Portugal", "tier": "A", "type": "Specialist", "tags": ["por testar"], "enabled": false }\n]',
    oninput: (event) => { text = event.target.value; },
  });
  const fileInput = h("input", {
    type: "file", accept: ".json,application/json",
    onchange: async (event) => { const file = event.target.files?.[0]; if (file) { text = await file.text(); textarea.value = text; } },
  });

  const runImport = async () => {
    if (running) return;
    let items;
    try {
      const parsed = JSON.parse(text.trim() || "null");
      items = Array.isArray(parsed) ? parsed : Array.isArray(parsed?.stores) ? parsed.stores : parsed && typeof parsed === "object" ? [parsed] : null;
      if (!items || items.length === 0) throw new Error("esperava uma lista de lojas: [ { ... }, { ... } ]");
    } catch (error) {
      results.replaceChildren(errorList([`JSON inválido: ${error.message}`]));
      return;
    }

    running = true;
    const rows = [];
    let created = 0, updated = 0, skipped = 0, failed = 0;
    for (const [index, raw] of items.entries()) {
      results.replaceChildren(h("div", { class: "muted" }, `A importar… ${index + 1}/${items.length}`));
      const label = raw?.name || `#${index + 1}`;
      try {
        if (!raw || typeof raw !== "object" || Array.isArray(raw)) throw new Error("não é um objeto");
        const existing = state.stores.find((store) => sameName(store.name, String(raw.name || "")))
          || (hostOf(raw.baseUrl) && state.stores.find((store) => hostOf(store.baseUrl) === hostOf(raw.baseUrl)));
        if (existing) {
          if (!updateClassification) { skipped++; rows.push([label, "grey", `já existe (${existing.name}) — ignorada`]); continue; }
          const merged = { ...existing, country: raw.country || existing.country, tier: raw.tier || existing.tier, type: raw.type || existing.type,
            tags: [...new Set([...(existing.tags || []), ...(Array.isArray(raw.tags) ? raw.tags : [])].filter((tag) => tag !== "por testar"))],
            notes: existing.notes || raw.notes || "" };
          await post("/store/save", storeToSaveRequest(merged, existing.name));
          updated++; rows.push([label, "violet", `classificação atualizada (${existing.name})`]);
          continue;
        }
        const draft = {
          name: "", enabled: false, platform: "Manual", baseUrl: "", unmarkedLanguage: "Unknown",
          wooCommerceProductsPath: "/wp-json/wc/store/v1/products", shopifyCatalogPaths: "", html: null,
          awinFeedKey: "", country: "Portugal", tier: "None", type: "Unknown", tags: [], notes: "", ...raw,
        };
        await post("/store/save", storeToSaveRequest(draft, null));
        state.stores.push(draft);
        created++; rows.push([label, "green", `criada (${draft.platform})`]);
      } catch (error) {
        failed++;
        rows.push([label, "red", (error.errors || [error.message]).join("; ")]);
      }
    }
    running = false;

    await reloadConfiguration();
    results.replaceChildren(
      h("div", { class: "notice violet" }, `${created} criada(s), ${updated} atualizada(s), ${skipped} ignorada(s), ${failed} com erro.`),
      h("ul", { class: "import-results" }, rows.map(([label, tone, message]) => h("li", {}, h("span", { class: `pill ${tone}` }, message), h("span", {}, label)))));
    if (failed === 0) toast(`Importação concluída: ${created + updated} loja(s)`, "success");
  };

  openDrawer({
    title: "Importar lojas (JSON)",
    subtitle: "Cola uma lista de lojas ou escolhe um ficheiro. O formato é o mesmo do \"Exportar JSON\".",
    body: [
      h("div", { class: "drawer-section" },
        field("Ficheiro", fileInput, "Opcional: carrega um .json em vez de colar.", true),
        field("JSON", textarea, "Campos: name, platform (Shopify, WooCommerce, Html, Amazon, Fnac, Worten, ElCorteIngles, Awin, Manual), baseUrl, country (Portugal/Spain/Europe), tier (S/A/B/C), type (Retail/Specialist/Importer/Marketplace/Official), tags, notes, enabled, shopifyCatalogPaths, wooCommerceProductsPath, html, awinFeedKey.", true),
        h("div", { style: "display:flex;align-items:center;gap:12px;margin-top:8px" },
          switchInput(updateClassification, (checked) => { updateClassification = checked; }, "Atualizar a classificação das que já existem"),
          h("span", {}, "Nas lojas que já existem (mesmo nome ou site), atualizar só área, tier, tipo e etiquetas"))),
      results,
    ],
    footer: [h("button", { class: "button ghost", onclick: closeDrawer }, "Fechar"), h("button", { class: "button primary", onclick: runImport }, icon("check"), "Importar")],
  });
}

/* ----- MSRP (reference prices kept by the bot; shown in Compare and in the alerts) ----- */

/** "−8% vs MSRP" (green below, amber up to +15%, red above), or nothing when either price is unknown. */
function msrpDelta(price, msrp, subject) {
  if (!price || !msrp) return null;
  const percent = Math.round(((price - msrp) / msrp) * 100);
  const tone = percent <= 0 ? "green" : percent <= 15 ? "amber" : "red";
  const text = percent === 0 ? "= MSRP" : `${percent < 0 ? "−" : "+"}${Math.abs(percent)}% vs MSRP`;
  return h("div", { class: `msrp-delta ${tone}`, title: `${subject ? `${subject}: ` : ""}${money(price)} · MSRP ${money(msrp)}` }, text);
}

/* ------------------------------------------------------------- actions -- */

/** Runs a mutation, reloads the configuration and reports the outcome. */
async function mutate(action, successMessage) {
  try {
    await action();
    await reloadConfiguration();
    if (successMessage) toast(successMessage, "success");
    return true;
  } catch (error) {
    toast((error.errors || [error.message]).join(" "), "error");
    return false;
  }
}

function setTaskEnabled(id, enabled) {
  const message = id
    ? (enabled ? "Tarefa iniciada" : "Tarefa parada")
    : (enabled ? "Todas as tarefas iniciadas" : "Todas as tarefas paradas");
  return mutate(() => post("/stocktask/enable", { id, enabled }), message);
}

function setStoreEnabled(name, enabled) {
  return mutate(() => post("/store/enable", { name, enabled }), `${name} ${enabled ? "ativada" : "desligada"}`);
}

async function deleteTask(task) {
  if (!await confirmDialog({ title: "Apagar tarefa?", message: `“${task.name || task.id}” deixa de ser vigiada.`, confirmLabel: "Apagar", danger: true })) return;
  state.expandedTasks.delete(task.id);
  await mutate(() => post(`/stocktask/delete?identifier=${encodeURIComponent(task.id)}`), "Tarefa apagada");
}

async function deleteAllTasks() {
  const tasks = state.tasks || [];
  if (tasks.length === 0) return;
  if (!await confirmDialog({
    title: `Apagar as ${tasks.length} tarefas?`,
    message: "Deixam todas de ser vigiadas. Antes de apagar é descarregada uma cópia (JSON) que podes voltar a importar.",
    confirmLabel: "Apagar todas", danger: true,
  })) return;

  exportTasks();
  const failed = [];
  for (const task of tasks) {
    try {
      await post(`/stocktask/delete?identifier=${encodeURIComponent(task.id)}`);
    } catch (error) {
      failed.push(task.name || task.id);
    }
  }
  state.expandedTasks.clear();
  await reloadConfiguration();
  if (failed.length === 0) toast(`${tasks.length} tarefa(s) apagada(s)`, "success");
  else toast(`Não foi possível apagar: ${failed.join(", ")}`, "error");
}

async function deleteStore(store) {
  if (!await confirmDialog({ title: "Apagar loja?", message: `“${store.name}” deixa de ser pesquisada e sai das tarefas que a usam.`, confirmLabel: "Apagar", danger: true })) return;
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
    await refresh();
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
    toast((error.errors || [error.message]).join(" "), "error");
  }
}

/* ------------------------------------------------------ modal & toasts -- */

function confirmDialog({ title, message, confirmLabel, danger = false }) {
  const backdrop = document.getElementById("modal");
  return new Promise((resolve) => {
    const close = (result) => { backdrop.classList.remove("open"); backdrop.replaceChildren(); resolve(result); };
    const confirmButton = h("button", { class: `button ${danger ? "danger" : "primary"}`, onclick: () => close(true) }, confirmLabel);
    backdrop.replaceChildren(h("div", { class: "modal", role: "dialog", "aria-modal": "true" },
      h("h2", {}, title), h("p", {}, message),
      h("div", { class: "buttons" }, h("button", { class: "button ghost", onclick: () => close(false) }, "Cancelar"), confirmButton)));
    backdrop.onclick = (event) => { if (event.target === backdrop) close(false); };
    backdrop.classList.add("open");
    confirmButton.focus();
  });
}

function toast(message, tone = "success") {
  const element = h("div", { class: `toast ${tone}` }, icon(tone === "error" ? "alert" : "check"), h("div", {}, message));
  document.getElementById("toasts").append(element);
  setTimeout(() => element.remove(), tone === "error" ? 7000 : 3500);
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
    if (state.view === "settings") renderView(true);
  } catch {
    // The settings page shows "A carregar canais…" until it works.
  }
}

async function refresh() {
  try {
    state.monitor = await api("/monitor/get");
    const cycleAt = state.monitor.lastCycle?.finishedAt || null;
    if (cycleAt !== state.lastCycleAt) {
      state.lastCycleAt = cycleAt;
      state.alerts = await api("/alert/list");
      if (state.view === "compare") loadCompare();
    }
    renderChrome();
    renderView();
  } catch (error) {
    document.getElementById("next-check-chip").replaceChildren(icon("alert"), h("strong", { style: "color:var(--red)" }, error.message || "Sem ligação ao bot"));
  }
}

async function start() {
  try {
    [state.tasks, state.stores, state.settings, state.monitor, state.alerts] = await Promise.all([
      api("/stocktask/list"), api("/store/list"), api("/setting/get"), api("/monitor/get"), api("/alert/list"),
    ]);
    state.lastCycleAt = state.monitor.lastCycle?.finishedAt || null;
  } catch (error) {
    document.getElementById("view").replaceChildren(h("div", { class: "card" }, emptyState("alert", `Não foi possível ligar ao bot: ${error.message}`)));
    setTimeout(start, 3000);
    return;
  }

  navigate((location.hash.match(/^#\/(\w+)/) || [])[1] || "overview");
  setInterval(refresh, POLL_MS);
  setInterval(renderNextCheck, 1000);
}

/* -------------------------------------------------------------- wiring -- */

window.addEventListener("hashchange", () => {
  const view = (location.hash.match(/^#\/(\w+)/) || [])[1];
  if (view && view !== state.view) navigate(view);
});

document.getElementById("run-now").addEventListener("click", runNow);
document.getElementById("drawer-backdrop").addEventListener("click", closeDrawer);
document.getElementById("menu-button").addEventListener("click", () => document.getElementById("sidebar").classList.toggle("open"));

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") { closeDrawer(); return; }
  const typing = /^(INPUT|TEXTAREA|SELECT)$/.test(event.target.tagName);
  if (typing || event.ctrlKey || event.metaKey || event.altKey) return;
  if (document.getElementById("drawer").classList.contains("open") || document.getElementById("modal").classList.contains("open")) return;

  const view = VIEWS.find((item) => item.key === event.key);
  if (view) { navigate(view.id); return; }
  if (event.key.toLowerCase() === "r") runNow();
});

start();
