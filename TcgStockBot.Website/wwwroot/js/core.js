"use strict";

/* ==========================================================================
   TcgStockBot dashboard — vanilla JS, no build step. Scripts load in order (see Views/Dashboard/Index.cshtml)
   and share the global scope: core → shell → views → editors → main.
   Talks only to the Website controllers (which call the WebApi through the Sdk).
   Everything coming from stores (titles, URLs, images) is inserted as text or checked URLs, never HTML.
   ========================================================================== */

const APP = document.getElementById("app");
const POLL_MS = Number(APP?.dataset.pollMs) || 3000;
const ANTIFORGERY_TOKEN = document.querySelector('meta[name="request-verification-token"]')?.content || "";

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

const state = {
  monitor: null,
  tasks: null,
  stores: null,
  settings: null,
  notification: null,
  alerts: [],
  route: { view: "radar", param: "" },
  radarQuery: "",
  watchGame: "all",                                     // A vigiar: game chips
  watchStatus: "all",                                   // all | stock | searching | paused
  watchQuery: "",
  productOnlyCandidates: readPreference("onlyProducts", true),
  alertFilter: "all",                                   // all | confirm
  alertStore: "",
  compare: null,
  compareFilter: "",
  compareMode: "all",
  compareSingles: false,
  storeQuery: "",
  storeCountry: "all",
  storeTier: "all",
  storeType: "all",
  storeTag: "",
  storeTest: "all",
  probe: { storeName: "", query: "elite trainer box", results: null, errors: null, loading: false },
  wizard: null,                                         // "Vigiar produto" state while open
  msrp: { kinds: [], entries: [] },                     // product kinds and MSRP table (from /msrp/list)
  radarKind: "all",                                     // Radar: kind filter of "Disponíveis agora"
  radarSort: readPreference("radarSort", "recent"),     // recent | cheap | msrp
  productGroup: readPreference("productGroup", "kind"), // product page: results by kind or by store
  lastCycleAt: null,
  renderedSignature: "",
};

/* ---------------------------------------------------------------- icons -- */

const ICONS = {
  radar: "M3 12a9 9 0 1 0 18 0a9 9 0 1 0-18 0M7.5 12a4.5 4.5 0 1 0 9 0a4.5 4.5 0 1 0-9 0M12 12l6.5-6.5",
  eye: "M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12zM9 12a3 3 0 1 0 6 0a3 3 0 1 0-6 0",
  bell: "M6 16v-5a6 6 0 1 1 12 0v5l2 2H4zM10 20.5a2 2 0 0 0 4 0",
  store: "M3 9l2-5h14l2 5M4 9v11h16V9M3 9h18M9.5 20v-6h5v6",
  sliders: "M4 7h9M17 7h3M4 17h3M11 17h9M13 7a2 2 0 1 0 4 0a2 2 0 1 0-4 0M7 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0",
  compare: "M12 4v16M7 20h10M5 7h14M5 7l-3 6a3 3 0 0 0 6 0zM19 7l-3 6a3 3 0 0 0 6 0z",
  flask: "M9 3h6M10 3v6L4.5 18.5A1.7 1.7 0 0 0 6 21h12a1.7 1.7 0 0 0 1.5-2.5L14 9V3M7.5 15h9",
  bolt: "M13 3L4 14h7l-1 7 9-11h-7z",
  plus: "M12 5v14M5 12h14",
  search: "M4 11a7 7 0 1 0 14 0a7 7 0 1 0-14 0M20 20l-4-4",
  external: "M14 4h6v6M20 4l-9 9M18 14v5H5V6h5",
  chevron: "M9 6l6 6-6 6",
  back: "M15 6l-6 6 6 6",
  down: "M6 9l6 6 6-6",
  check: "M5 12l5 5 9-10",
  close: "M6 6l12 12M18 6L6 18",
  play: "M7 5l12 7-12 7z",
  pause: "M8 5v14M16 5v14",
  stop: "M6 6h12v12H6z",
  edit: "M4 20h4L19 9l-4-4L4 16zM13.5 6.5l4 4",
  copy: "M8 8h12v12H8zM16 8V4H4v12h4",
  trash: "M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3",
  alert: "M12 9v4M12 17h.01M10.3 3.9L2 18a2 2 0 0 0 1.7 3h16.6a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z",
  info: "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18M12 8h.01M12 11v5",
  clock: "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18M12 7v5l3 2",
  chart: "M4 19h16M5 15l4-5 4 3 6-7",
  refresh: "M20 11a8 8 0 0 0-14.9-3M4 5v4h4M4 13a8 8 0 0 0 14.9 3M20 19v-4h-4",
  download: "M12 4v11M7 10l5 5 5-5M5 20h14",
  upload: "M12 20V9M7 14l5-5 5 5M5 4h14",
  mail: "M3 5h18v14H3zM3 7l9 6 9-6",
  chat: "M4 20l1.4-4.2A8 8 0 1 1 8.2 18.6zM9 10h.01M12 10h.01M15 10h.01",
  menu: "M4 7h16M4 12h16M4 17h16",
  more: "M5 12h.01M12 12h.01M19 12h.01",
  sun: "M12 8a4 4 0 1 0 0 8a4 4 0 1 0 0-8M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4",
  box: "M21 8l-9-5-9 5 9 5zM3 8v8l9 5 9-5V8M12 13v8",
};

/** Static, trusted SVG markup only (paths from ICONS). */
function icon(name, className = "") {
  const element = svg("svg", { viewBox: "0 0 24 24", class: `icon ${className}`.trim(), "aria-hidden": "true", focusable: "false" },
    svg("path", { d: ICONS[name] || ICONS.box }));
  return element;
}

/* ------------------------------------------------------------ DOM helper -- */

const PROPERTY_KEYS = new Set(["value", "checked", "disabled", "selected", "readOnly", "type", "htmlFor", "indeterminate"]);

function h(tag, props, ...children) {
  const element = document.createElement(tag);
  for (const [key, value] of Object.entries(props || {})) {
    if (value === null || value === undefined || value === false) continue;
    if (key === "class") element.className = value;
    else if (key.startsWith("on") && typeof value === "function") element.addEventListener(key.slice(2), value);
    else if (PROPERTY_KEYS.has(key)) element[key] = value;
    else if (key === "href") { const safe = safeHref(value); if (safe) element.setAttribute("href", safe); }
    else if (key === "src") { const safe = safeHref(value); if (safe) element.setAttribute("src", safe); }
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
  for (const [key, value] of Object.entries(attributes || {})) {
    if (value === null || value === undefined) continue;
    element.setAttribute(key, String(value));
  }
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) continue;
    element.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return element;
}

/** Only http(s) URLs and in-page hashes ever reach an href/src. */
function safeHref(url) {
  if (typeof url !== "string") return null;
  if (url.startsWith("#")) return url;
  return /^https?:\/\//i.test(url) ? url : null;
}

function externalLink(url, text, className = "") {
  const href = safeHref(url);
  return href
    ? h("a", { href, target: "_blank", rel: "noopener noreferrer", title: url, class: className }, text)
    : h("span", { class: className }, text);
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

const errorText = (error) => (error?.errors || [error?.message || "Erro desconhecido."]).join(" ");

/* ------------------------------------------------------------ formatting -- */

const moneyFormat = new Intl.NumberFormat("pt-PT", { style: "currency", currency: "EUR" });
const timeFormat = new Intl.DateTimeFormat("pt-PT", { hour: "2-digit", minute: "2-digit" });
const dateTimeFormat = new Intl.DateTimeFormat("pt-PT", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" });
const dayMonthFormat = new Intl.DateTimeFormat("pt-PT", { day: "numeric", month: "short" });
const weekdayFormat = new Intl.DateTimeFormat("pt-PT", { weekday: "long", day: "numeric", month: "short" });

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
  const days = Math.round(hours / 24);
  return days === 1 ? "há 1 dia" : `há ${days} dias`;
}

function countdown(iso) {
  const seconds = Math.max(0, Math.round((new Date(iso).getTime() - Date.now()) / 1000));
  const minutes = Math.floor(seconds / 60);
  return `${String(minutes).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
}

/** "Hoje", "Ontem", or "terça, 6 out" — the day headings of the alert list. */
function dayHeading(date) {
  const day = new Date(date);
  day.setHours(0, 0, 0, 0);
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const diff = Math.round((today - day) / 864e5);
  if (diff === 0) return "Hoje";
  if (diff === 1) return "Ontem";
  return weekdayFormat.format(day).replace("-feira", "");
}

function slugify(text) {
  return text.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase()
    .replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 40);
}

const normalizeText = (text) => (text || "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
const splitList = (text) => text.split(",").map((item) => item.trim()).filter(Boolean);
const matchesWords = (query, ...texts) => {
  const words = normalizeText(query).split(/\s+/).filter(Boolean);
  const haystack = normalizeText(texts.join(" "));
  return words.every((word) => haystack.includes(word));
};

/* ---------------------------------------------------------- vocabulary -- */

const OUTCOMES = {
  Match: ["Dentro do preço", "accent"],
  MatchLanguageToConfirm: ["Dentro do preço · confirmar idioma", "amber"],
  MissingKeyword: ["Não é o produto", "plain"],
  ExcludedKeyword: ["Tem uma palavra excluída", "plain"],
  NotEnglish: ["Não é inglês", "plain"],
  NotJapanese: ["Não é japonês", "plain"],
  LanguageUnknown: ["Idioma desconhecido", "amber"],
  OutOfStock: ["Esgotado", "plain"],
  NoPrice: ["Sem preço", "amber"],
  AboveMaxPrice: ["Acima do teu preço", "amber"],
  OtherKind: ["Tipo que não vigias", "plain"],
  NoMsrp: ["Sem MSRP para este tipo", "amber"],
};

const isMatch = (outcome) => outcome === "Match" || outcome === "MatchLanguageToConfirm";

/** "Não é o produto": the store's search returned something unrelated. */
const isIrrelevant = (listing) => listing.outcome === "MissingKeyword";

/** Outcomes of products a task recognises: only these have a price history. */
const TRACKED_OUTCOMES = new Set(["Match", "MatchLanguageToConfirm", "LanguageUnknown", "OutOfStock", "NoPrice", "AboveMaxPrice", "NoMsrp"]);

const LANGUAGES = { English: "Inglês", Japanese: "Japonês", NonEnglish: "Outro idioma", Unknown: "Idioma ?" };
const TASK_LANGUAGES = { English: ["EN", "Inglês"], Japanese: ["JP", "Japonês"] };

/** The card games a task can belong to (TcgGame). The colour comes from CSS (.game-…). */
const GAMES = { Pokemon: "Pokémon", OnePiece: "One Piece", Lorcana: "Lorcana", Magic: "Magic", Other: "Outro" };
const gameKey = (game) => (GAMES[game] ? game : "Pokemon");
const gameClass = (game) => `game-${gameKey(game).toLowerCase()}`;
const gamePill = (game) => h("span", { class: `game-pill ${gameClass(game)}` }, GAMES[gameKey(game)]);

const UNMARKED_LANGUAGE = {
  Unknown: ["Não avisar", "não avisar"],
  English: ["Contar como inglês", "contar como inglês"],
  Confirm: ["Avisar com idioma por confirmar", "avisar e pedir para confirmares"],
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
const PLATFORM_LABELS = {
  Shopify: "Shopify", WooCommerce: "WooCommerce", Html: "Site próprio (HTML)", Amazon: "Amazon Creators API", Fnac: "Feed Awin",
  Worten: "Feed Awin", ElCorteIngles: "Feed Awin", Awin: "Feed Awin", Manual: "Sem integração",
};
const COUNTRIES = { Portugal: ["Portugal", "🇵🇹"], Spain: ["Espanha", "🇪🇸"], Europe: ["Europa / online", "🇪🇺"] };
const TIERS = {
  S: ["Tier S", "amber", "MSRP / grande retalho"],
  A: ["Tier A", "accent", "Especialistas TCG"],
  B: ["Tier B", "blue", "Importadores e lojas online"],
  C: ["Tier C", "plain", "Marketplaces — só referência"],
  None: ["Sem tier", "plain", "Por classificar"],
};
const STORE_TEST = {
  Ok: ["OK", "accent"], Empty: ["0 produtos", "amber"], Blocked: ["Bloqueia o bot", "red"], RateLimited: ["Limite de pedidos", "amber"],
  Unreachable: ["Sem ligação", "red"], NoIntegration: ["Sem integração", "plain"], NotConfigured: ["Por configurar", "plain"], Error: ["Erro", "red"],
};
/** Test outcomes that mean "this store does not work as configured". */
const TEST_FAILED = new Set(["Blocked", "Unreachable", "Error", "RateLimited"]);
const STORE_TYPES = {
  Retail: "Retalho", Specialist: "Especialista TCG", Importer: "Importador", Marketplace: "Marketplace", Official: "Oficial", Unknown: "Sem tipo",
};
const MSRP_BASIS = {
  Official: ["Oficial", "accent"], LaunchPrice: ["Preço de lançamento", "blue"], Estimate: ["Estimativa", "amber"],
};
const AWIN_FEED_KEYS = { Fnac: "Fnac", Worten: "Worten", ElCorteIngles: "ElCorteIngles" };

/** A small rounded label. tone: accent | amber | red | blue | plain. */
const pill = (text, tone = "plain", title = null) => h("span", { class: `pill ${tone}`, title }, text);
const outcomePill = (outcome) => { const [label, tone] = OUTCOMES[outcome] || [outcome, "plain"]; return pill(label, tone); };
const testPill = (test) => { const [label, tone] = STORE_TEST[test.status] || [test.status, "plain"]; return pill(label, tone, test.message || null); };

/* --------------------------------------------------------- store looks -- */

function storeInitials(name) {
  return (name || "?").replace(/[^\p{L}\p{N} ]/gu, "").split(" ").filter(Boolean).slice(0, 2).map((word) => word[0]).join("").toUpperCase() || "?";
}

const storeAvatar = (name, size = "") => h("span", { class: `store-avatar ${size}`, "aria-hidden": "true" }, storeInitials(name));

/* --------------------------------------------------------- product art -- */

/** Product kind from a title or search text: drives the placeholder drawing and the small caption. */
const KINDS = [
  [/elite trainer|\betb\b/, "box", "Elite Trainer Box"],
  [/booster bundle|\bbundle\b/, "bundle", "Booster Bundle"],
  [/booster box|booster display|\bdisplay\b|\bcase\b|\bcaixa de boosters?\b/, "display", "Booster Box"],
  [/\btin\b|\blata\b/, "tin", "Tin"],
  [/premium|collection|colec|cole[cç][aã]o|\bupc\b|chest|\bbox\b/, "premium", "Coleção"],
  [/blister|sleeved|booster pack|\bpack\b|checklane|\bbooster\b|starter|deck/, "pack", "Booster"],
];

function productKind(...texts) {
  const text = normalizeText(texts.filter(Boolean).join(" "));
  for (const [pattern, shape, label] of KINDS) if (pattern.test(text)) return { shape, label };
  return { shape: "box", label: "" };
}

const SHAPES = {
  box: "M20 38L50 24L80 38V70L50 84L20 70ZM20 38L50 52L80 38M50 52V84",
  pack: "M33 16H67L70 22L66 27L70 32V82H30V32L34 27L30 22ZM30 36H70M30 74H70",
  bundle: "M22 34H58V84H22ZM30 26H66V76H58M38 18H74V68H66",
  premium: "M12 36L58 26L88 38V66L42 78L12 66ZM12 36L42 48L88 38M42 48V78",
  tin: "M30 24H70V84H30ZM30 34H70M26 22H74V30H26Z",
  display: "M18 42H82V82H18ZM18 42L28 30H72L82 42M30 50V74M42 50V74M54 50V74M66 50V74",
};

/**
 * The picture of a product: the store's photo when there is one, otherwise a line drawing of the product kind
 * in the colour of its game. size: "hero" (cards), "thumb" (rows), "mini".
 */
function productArt({ imageUrl, game, title, query, size = "thumb", caption = false }) {
  const kind = productKind(title, query);
  const box = h("div", { class: `art ${size} ${gameClass(game)}` });
  const drawing = svg("svg", { class: "art-shape", viewBox: "0 0 100 100", "aria-hidden": "true" }, svg("path", { d: SHAPES[kind.shape] }));
  const src = safeHref(imageUrl);
  if (src) {
    const image = h("img", { src, alt: "", loading: "lazy", decoding: "async", referrerpolicy: "no-referrer" });
    image.addEventListener("error", () => { image.remove(); box.classList.remove("has-photo"); box.prepend(drawing); });
    box.classList.add("has-photo");
    box.append(image);
  } else {
    box.append(drawing);
  }
  if (caption && kind.label) box.append(h("span", { class: "art-caption" }, kind.label));
  return box;
}

/* --------------------------------------------------------- derived data -- */

const searches = () => state.monitor?.searches || [];
const enabledStores = () => (state.stores || []).filter((store) => store.enabled);
const sameName = (a, b) => (a || "").toLowerCase() === (b || "").toLowerCase();
const findTask = (id) => (state.tasks || []).find((task) => task.id === id);
const findStore = (name) => (state.stores || []).find((store) => sameName(store.name, name));

function storesForTask(task) {
  const names = task.storeNames || [];
  return enabledStores().filter((store) => store.platform !== "Manual" && (names.length === 0 || names.some((name) => sameName(name, store.name))));
}

function searchFor(task, storeName) {
  return searches().find((search) => search.stockTaskId === task.id && search.storeName === storeName);
}

function taskSearches(task) {
  return storesForTask(task).map((store) => searchFor(task, store.name)).filter(Boolean);
}

const byPrice = (a, b) => (a.price ?? Infinity) - (b.price ?? Infinity);

/**
 * Everything the screens say about one task, worked out from the last cycle:
 * status (stock | searching | error | paused | waiting), the best offer, a one-line summary, one dot per store,
 * and a photo to show.
 */
function taskInsight(task) {
  const stores = storesForTask(task);
  const reports = taskSearches(task);
  const all = reports.flatMap((report) => report.listings.map((listing) => ({ ...listing, storeName: report.storeName, checkedAt: report.checkedAt })));
  const matches = all.filter((listing) => isMatch(listing.outcome)).sort(byPrice);
  const aboveMax = all.filter((listing) => listing.outcome === "AboveMaxPrice" && listing.inStock).sort(byPrice);
  const tracked = all.filter((listing) => TRACKED_OUTCOMES.has(listing.outcome));
  const soldOutStores = new Set(tracked.filter((listing) => listing.outcome === "OutOfStock").map((listing) => listing.storeName));
  const failed = reports.filter((report) => !report.succeeded);
  const lastChecked = reports.map((report) => report.checkedAt).sort().at(-1) || null;
  const best = matches[0] || null;

  let status, summary, tone = "";
  if (!task.enabled) { status = "paused"; summary = "Em pausa"; }
  else if (best) {
    status = "stock";
    const offers = new Set(matches.map((listing) => listing.url)).size;
    summary = `${offers} ${offers === 1 ? "oferta" : "ofertas"} ao teu preço · desde ${money(best.price)}`;
    tone = "accent";
  } else if (stores.length === 0) { status = "paused"; summary = "Sem lojas ativas"; }
  else if (reports.length === 0) { status = "waiting"; summary = "À espera da primeira verificação"; }
  else if (failed.length === reports.length) { status = "error"; summary = "Todas as lojas deram erro"; tone = "red"; }
  else if (aboveMax.length) {
    status = "searching";
    summary = `${money(aboveMax[0].price)} na ${shortStoreName(aboveMax[0].storeName)} · acima do teu preço`;
    tone = "amber";
  } else if (soldOutStores.size) { status = "searching"; summary = `Esgotado em ${soldOutStores.size} ${soldOutStores.size === 1 ? "loja" : "lojas"}`; }
  else { status = "searching"; summary = "Ainda sem resultados"; }

  const dots = stores.map((store) => {
    const report = reports.find((item) => item.storeName === store.name);
    let kind = "none";
    if (!task.enabled) kind = "off";
    else if (!report) kind = "pending";
    else if (!report.succeeded) kind = "error";
    else if (report.listings.some((listing) => isMatch(listing.outcome))) kind = "match";
    else if (report.listings.some((listing) => listing.outcome === "AboveMaxPrice" && listing.inStock)) kind = "above";
    return { store: store.name, kind };
  });

  const photo = [...matches, ...aboveMax, ...tracked].find((listing) => safeHref(listing.imageUrl))?.imageUrl || null;
  const alerts = state.alerts.filter((alert) => alert.stockTaskId === task.id);
  const lastAlert = best ? alerts.find((alert) => alert.url === best.url) : null;

  return { task, stores, reports, all, matches, aboveMax, tracked, soldOutStores, failed, best, status, summary, tone, dots, photo, lastChecked, alerts, lastAlert };
}

const STATUS_LABELS = { stock: "Em stock", searching: "À procura", waiting: "À procura", error: "Com erro", paused: "Em pausa" };

/** "Versus Gamecenter" → "Versus": for tight spots; lists and tooltips keep the full name. */
function shortStoreName(name) {
  const first = (name || "").split(/\s+/)[0] || name;
  return first.length > 11 ? `${first.slice(0, 10)}…` : first;
}

function alertsToday() {
  const today = new Date().toDateString();
  return state.alerts.filter((alert) => new Date(alert.at).toDateString() === today).length;
}

/** The task an alert belongs to (by id, then by name for old alerts). */
const alertTask = (alert) => findTask(alert.stockTaskId) || (state.tasks || []).find((task) => task.name === alert.stockTaskName) || null;

/* ------------------------------------------------------------- widgets -- */

function emptyState(iconName, title, text = null, action = null) {
  return h("div", { class: "empty" }, h("span", { class: "empty-icon" }, icon(iconName)),
    h("div", { class: "empty-title" }, title), text ? h("div", { class: "empty-text" }, text) : null, action);
}

/** A labelled on/off switch. */
function switchInput(checked, onChange, label, attributes = {}) {
  return h("label", { class: "switch", title: label, ...attributes },
    h("input", { type: "checkbox", role: "switch", checked, "aria-label": label, onchange: (event) => onChange(event.target.checked) }),
    h("span", { class: "switch-track", "aria-hidden": "true" }));
}

function labelledSwitch(checked, label, onChange) {
  return h("label", { class: "inline-switch" }, switchInput(checked, onChange, label), h("span", {}, label));
}

/**
 * Segmented control: options [[value, label, count?]]. Re-renders itself on click and calls onChange.
 */
function segmented(options, value, onChange, label) {
  const container = h("div", { class: "seg", role: "radiogroup", "aria-label": label });
  const render = (current) => container.replaceChildren(...options.map(([key, text, count]) =>
    h("button", {
      type: "button", role: "radio", class: current === key ? "on" : "", "aria-checked": current === key ? "true" : "false",
      onclick: () => { render(key); onChange(key); },
    }, text, count !== undefined && count !== null ? h("span", { class: "seg-count" }, ` · ${count}`) : null)));
  render(value);
  return container;
}

/** A native select styled as a pill: "Loja  Todas ▾". */
function selectPill(label, value, options, onChange) {
  const select = h("select", { "aria-label": label, onchange: (event) => onChange(event.target.value) },
    options.map(([optionValue, optionLabel]) => h("option", { value: optionValue, selected: optionValue === value }, optionLabel)));
  return h("label", { class: "select-pill" }, h("span", { class: "select-label" }, label), select, icon("down", "select-caret"));
}

/** A search box. key keeps the focus and caret when the poll re-renders the page while typing. */
function searchField(value, placeholder, onInput, key, label = placeholder) {
  const input = h("input", { type: "search", value, placeholder, "aria-label": label, "data-keep-focus": key, oninput: (event) => onInput(event.target.value) });
  return h("label", { class: "search-field" }, icon("search"), input);
}

function button(label, { iconName = null, kind = "", onClick = null, title = null, disabled = false, kbd = null, type = "button" } = {}) {
  return h("button", { type, class: `btn ${kind}`.trim(), onclick: onClick, title, disabled },
    iconName ? icon(iconName) : null, label, kbd ? h("kbd", {}, kbd) : null);
}

function linkButton(label, href, { iconName = null, kind = "", external = false, trailingIcon = null } = {}) {
  return h("a", { class: `btn ${kind}`.trim(), href, target: external ? "_blank" : null, rel: external ? "noopener noreferrer" : null },
    iconName ? icon(iconName) : null, label, trailingIcon ? icon(trailingIcon) : null);
}

function iconButton(iconName, label, onClick, kind = "") {
  return h("button", { type: "button", class: `icon-btn ${kind}`.trim(), title: label, "aria-label": label, onclick: onClick }, icon(iconName));
}

function pageHead(title, subtitle, actions = []) {
  return h("header", { class: "page-head" },
    h("div", { class: "page-title" }, h("h1", {}, title), subtitle ? h("p", {}, subtitle) : null),
    actions.length ? h("div", { class: "page-actions" }, actions) : null);
}

function sectionHead(title, { count = null, note = null, action = null, id = null } = {}) {
  return h("div", { class: "section-head" },
    h("div", { class: "section-title" }, h("h2", { id }, title), count !== null ? h("span", { class: "count-badge" }, count) : null,
      note ? h("span", { class: "section-note" }, note) : null),
    action);
}

function errorList(errors) {
  return h("div", { class: "errors", role: "alert" }, h("ul", {}, (errors.length ? errors : ["Erro desconhecido."]).map((message) => h("li", {}, message))));
}

/** "−8% vs MSRP" (accent below, amber up to +15%, red above), or nothing when either price is unknown. */
function msrpDelta(price, msrp, subject) {
  if (!price || !msrp) return null;
  const percent = Math.round(((price - msrp) / msrp) * 100);
  const tone = percent <= 0 ? "accent" : percent <= 15 ? "amber" : "red";
  const text = percent === 0 ? "= MSRP" : `${percent < 0 ? "−" : "+"}${Math.abs(percent)}% vs MSRP`;
  return h("div", { class: `msrp-delta ${tone}`, title: `${subject ? `${subject}: ` : ""}${money(price)} · MSRP ${money(msrp)}` }, text);
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

/* ------------------------------------------------------ modal & toasts -- */

function toast(message, tone = "success") {
  const element = h("div", { class: `toast ${tone}` }, icon(tone === "error" ? "alert" : tone === "info" ? "info" : "check"), h("div", {}, message));
  document.getElementById("toasts").append(element);
  setTimeout(() => element.remove(), tone === "error" ? 7000 : 3500);
}

function confirmDialog({ title, message, confirmLabel, danger = false }) {
  const backdrop = document.getElementById("modal");
  return new Promise((resolve) => {
    const close = (result) => { backdrop.classList.remove("open"); backdrop.replaceChildren(); resolve(result); };
    const confirmButton = button(confirmLabel, { kind: danger ? "danger" : "primary", onClick: () => close(true) });
    backdrop.replaceChildren(h("div", { class: "modal", role: "alertdialog", "aria-modal": "true", "aria-label": title },
      h("h2", {}, title), h("p", {}, message),
      h("div", { class: "modal-buttons" }, button("Cancelar", { kind: "ghost", onClick: () => close(false) }), confirmButton)));
    backdrop.onclick = (event) => { if (event.target === backdrop) close(false); };
    backdrop.classList.add("open");
    confirmButton.focus();
  });
}

/** A modal with one select; resolves to the chosen value, or null when cancelled. */
function chooseDialog({ title, message, label, options, confirmLabel }) {
  const backdrop = document.getElementById("modal");
  return new Promise((resolve) => {
    const close = (result) => { backdrop.classList.remove("open"); backdrop.replaceChildren(); resolve(result); };
    const select = h("select", {}, options.map(([value, text]) => h("option", { value }, text)));
    backdrop.replaceChildren(h("div", { class: "modal wide", role: "dialog", "aria-modal": "true", "aria-label": title },
      h("h2", {}, title), h("p", {}, message), field(label, select),
      h("div", { class: "modal-buttons" },
        button("Cancelar", { kind: "ghost", onClick: () => close(null) }),
        button(confirmLabel, { kind: "primary", onClick: () => close(select.value) }))));
    backdrop.onclick = (event) => { if (event.target === backdrop) close(null); };
    backdrop.classList.add("open");
    select.focus();
  });
}

/* -------------------------------------------------------------- drawers -- */

let drawerReturnFocus = null;

function openDrawer({ title, subtitle, body, footer, wide = false }) {
  const drawer = document.getElementById("drawer");
  drawerReturnFocus = document.activeElement;
  drawer.classList.toggle("wide", wide);
  drawer.setAttribute("aria-label", title);
  drawer.replaceChildren(
    h("div", { class: "drawer-head" },
      h("div", {}, h("h2", {}, title), subtitle ? h("p", {}, subtitle) : null),
      iconButton("close", "Fechar", closeDrawer)),
    h("div", { class: "drawer-body" }, body),
    footer ? h("div", { class: "drawer-foot" }, footer) : null);
  drawer.classList.add("open");
  drawer.setAttribute("aria-hidden", "false");
  document.getElementById("drawer-backdrop").classList.add("open");
  setTimeout(() => (drawer.querySelector(".drawer-body input:not([type=checkbox]), .drawer-body select, .drawer-body textarea") || drawer.querySelector(".icon-btn"))?.focus(), 200);
}

function closeDrawer() {
  const drawer = document.getElementById("drawer");
  if (!drawer.classList.contains("open")) return;
  drawer.classList.remove("open");
  drawer.setAttribute("aria-hidden", "true");
  document.getElementById("drawer-backdrop").classList.remove("open");
  if (drawerReturnFocus && document.contains(drawerReturnFocus)) drawerReturnFocus.focus();
}

/** A form field. A single input/select/textarea sits inside its <label>; groups (switches, chips) get a plain caption. */
function field(label, control, hint, wide = false) {
  const single = control instanceof HTMLInputElement || control instanceof HTMLSelectElement || control instanceof HTMLTextAreaElement;
  return h("div", { class: `field ${wide ? "wide" : ""}` },
    single
      ? h("label", {}, h("span", { class: "field-label" }, label), control)
      : [h("span", { class: "field-label" }, label), control],
    hint ? h("div", { class: "hint" }, hint) : null);
}

function textInput(value, onInput, attributes = {}) {
  return h("input", { type: "text", value: value ?? "", oninput: (event) => onInput(event.target.value), ...attributes });
}

function drawerSection(title, ...children) {
  return h("section", { class: "drawer-section" }, title ? h("h3", {}, title) : null, children);
}

/* --------------------------------------------------------- store health -- */

const SLOW_MS = 2500;

/**
 * How a store did in the last cycle: ok | slow | partial | error | pending | off | manual, with the average
 * response time and the first error message.
 */
function storeHealth(store) {
  if (store.platform === "Manual") return { kind: "manual", label: "Sem integração", reports: [] };
  if (store.autoDisabledAt && !store.enabled) return { kind: "off", label: "Desligada pelo bot", reports: [] };
  if (!store.enabled) return { kind: "off", label: "Desligada", reports: [] };
  const reports = searches().filter((search) => search.storeName === store.name);
  if (reports.length === 0) return { kind: "pending", label: "À espera da primeira verificação", reports };
  const failed = reports.filter((report) => !report.succeeded);
  const ok = reports.filter((report) => report.succeeded);
  const averageMs = ok.length ? Math.round(ok.reduce((sum, report) => sum + report.durationMs, 0) / ok.length) : null;
  const lastChecked = reports.map((report) => report.checkedAt).sort().at(-1);
  const base = { reports, failed, averageMs, lastChecked, error: failed[0]?.error || null };
  if (failed.length === reports.length) return { ...base, kind: "error", label: "Sem resposta no último ciclo" };
  if (failed.length > 0) return { ...base, kind: "partial", label: `${failed.length} de ${reports.length} pesquisas com erro` };
  if (averageMs > SLOW_MS) return { ...base, kind: "slow", label: "Lenta" };
  return { ...base, kind: "ok", label: "A responder" };
}

/** "312 ms" / "1,8 s". */
const durationText = (ms) => (ms === null || ms === undefined ? "—" : ms < 1000 ? `${ms} ms` : `${(ms / 1000).toLocaleString("pt-PT", { maximumFractionDigits: 1 })} s`);

/**
 * Runs update() (which replaces children of container) and puts the focus and caret back on the element with the
 * same data-keep-focus key, so a re-render never steals the field the user is typing in.
 */
function keepFocus(container, update) {
  const focused = document.activeElement?.closest?.("[data-keep-focus]");
  const focus = focused && container.contains(focused)
    ? { key: focused.dataset.keepFocus, start: focused.selectionStart, end: focused.selectionEnd }
    : null;
  update();
  if (!focus) return;
  const element = container.querySelector(`[data-keep-focus="${CSS.escape(focus.key)}"]`);
  if (!element) return;
  element.focus({ preventScroll: true });
  try { element.setSelectionRange(focus.start, focus.end); } catch { /* not a text input */ }
}

/* ----------------------------------------------------- kinds and prices -- */

/** The label of a product kind code ("etb" → "Elite Trainer Box"). */
function kindLabel(code) {
  return state.msrp.kinds.find((kind) => kind.code === code)?.label || (code === "other" || !code ? "Outro" : code);
}

/** The MSRP of a kind (Pokémon table; other games have none yet). */
function msrpOf(kind, game = "Pokemon") {
  if (gameKey(game) !== "Pokemon") return null;
  return state.msrp.entries.find((entry) => entry.kind === kind && normalizeText(entry.game).includes("pokemon"))?.price ?? null;
}

/** Product kinds in a sensible display order (the classifier order, "other" last). */
const kindOrder = (code) => {
  const index = state.msrp.kinds.findIndex((kind) => kind.code === code);
  return index < 0 ? 999 : index;
};

/** One line that says when a task alerts on price: "Até ao MSRP + 10%" / "Até 60,00 €". */
function priceRuleText(task, short = false) {
  if (task.priceRule === "Msrp") {
    const tolerance = task.msrpTolerancePercent ?? 10;
    const margin = tolerance === 0 ? "" : tolerance > 0 ? ` + ${tolerance}%` : ` − ${Math.abs(tolerance)}%`;
    const fallback = task.maxPrice > 0 && !short ? ` (sem MSRP: até ${money(task.maxPrice)})` : "";
    return `Até ao MSRP${margin}${fallback}`;
  }
  return `Até ${money(task.maxPrice)}`;
}

/** "ETB, Booster Bundle" or "todos os tipos". */
function kindsText(task) {
  const kinds = task.kinds || [];
  return kinds.length ? kinds.map(kindLabel).join(", ") : "todos os tipos";
}

/** The limit that applies to one kind of one task (mirror of the bot's rule, for display only). */
function limitFor(task, kind) {
  if (task.priceRule === "Msrp") {
    const msrp = msrpOf(kind, task.game);
    if (msrp) return Math.round(msrp * (100 + (task.msrpTolerancePercent ?? 10))) / 100;
  }
  return task.maxPrice > 0 ? task.maxPrice : null;
}

/** "−8% vs MSRP" pill (accent below, amber up to +15%, red above), or null without MSRP. */
function msrpPill(price, msrp) {
  if (!price || !msrp) return null;
  const percent = Math.round(((price - msrp) / msrp) * 100);
  const tone = percent <= 0 ? "accent" : percent <= 15 ? "amber" : "red";
  const text = percent === 0 ? "= MSRP" : `${percent < 0 ? "−" : "+"}${Math.abs(percent)}% vs MSRP`;
  return h("span", { class: `delta-pill ${tone}`, title: `MSRP ${money(msrp)}` }, text);
}

/**
 * Every offer that alerts right now (in stock, right language, within the task's price rule), one per product URL,
 * with the task it belongs to and when it appeared (the alert time, or the check time).
 */
function currentOffers() {
  const byUrl = new Map();
  for (const search of searches()) {
    const task = findTask(search.stockTaskId);
    if (!task || !task.enabled) continue;
    for (const listing of search.listings) {
      if (!isMatch(listing.outcome) || byUrl.has(listing.url)) continue;
      const alert = state.alerts.find((item) => item.url === listing.url);
      const since = alert?.at || search.checkedAt;
      byUrl.set(listing.url, {
        key: listing.url, listing, task, storeName: search.storeName, kind: listing.kind || "other",
        kindLabel: listing.kindLabel || kindLabel(listing.kind), msrp: listing.msrp ?? null, alert, since,
        isNew: Boolean(alert) && Date.now() - new Date(alert.at).getTime() < 24 * 3600e3,
      });
    }
  }
  return [...byUrl.values()];
}

/**
 * What a task found, per product kind: the best in-stock offer within the rule, the cheapest in stock above it,
 * and how many stores have it — the rows of the A vigiar cards and the product page groups.
 */
function kindBreakdown(insight) {
  const groups = new Map();
  for (const listing of insight.tracked) {
    const kind = listing.kind || "other";
    if (!groups.has(kind)) groups.set(kind, { kind, label: listing.kindLabel || kindLabel(kind), listings: [] });
    groups.get(kind).listings.push(listing);
  }
  return [...groups.values()].map((group) => {
    const matches = group.listings.filter((listing) => isMatch(listing.outcome)).sort(byPrice);
    const above = group.listings.filter((listing) => listing.inStock && !isMatch(listing.outcome) && listing.price).sort(byPrice);
    const stores = new Set(group.listings.map((listing) => listing.storeName));
    return { ...group, best: matches[0] || null, above: above[0] || null, matchCount: matches.length, storeCount: stores.size,
      soldOut: group.listings.every((listing) => !listing.inStock), msrp: group.listings.find((listing) => listing.msrp)?.msrp ?? msrpOf(group.kind, insight.task.game),
      limit: group.listings.find((listing) => listing.priceLimit)?.priceLimit ?? limitFor(insight.task, group.kind) };
  }).sort((a, b) => (Boolean(b.best) - Boolean(a.best)) || (Boolean(b.above) - Boolean(a.above)) || kindOrder(a.kind) - kindOrder(b.kind));
}
