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
    { id: "probe", label: "Testar loja", icon: "flask", key: "5", section: "Ferramentas" },
    { id: "settings", label: "Definições", icon: "settings", key: "6" },
];

/** Views re-rendered by the poll; the others hold form inputs and only render on navigation. */
const LIVE_VIEWS = new Set(["overview", "tasks", "stores", "alerts"]);

function navigate(view) {
    if (!VIEWS.some((item) => item.id === view)) view = "overview";
    state.view = view;
    if (location.hash !== `#/${view}`) history.replaceState(null, "", `#/${view}`);
    document.getElementById("sidebar").classList.remove("open");
    if (view === "settings") refreshNotification();
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

    const signature = JSON.stringify([state.view, state.monitor, state.alerts.length, state.tasks, state.stores, state.taskFilter, [...state.expandedTasks]]);
    if (!force && signature === state.renderedSignature) return;
    state.renderedSignature = signature;

    const renderers = { overview: viewOverview, tasks: viewTasks, stores: viewStores, alerts: viewAlerts, probe: viewProbe, settings: viewSettings };
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
            h("td", { class: "title-cell" },
                h("span", { class: "primary-text", title: alert.title }, alert.title),
                alert.languageToConfirm ? h("span", { class: "pill amber", style: "margin-top:4px" }, "Confirmar idioma") : null),
            full ? h("td", {}, alert.stockTaskName) : null,
            h("td", { class: "num" }, money(alert.price)),
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
        state.taskFilter === "all" ? true
            : state.taskFilter === "running" ? task.enabled
                : state.taskFilter === "stopped" ? !task.enabled
                    : status.key === state.taskFilter);

    return [
        h("div", { class: "page-head" },
            h("div", {}, h("h1", {}, "Tarefas"), h("p", {}, "Cada tarefa procura um produto nas lojas escolhidas e avisa-te quando aparece em inglês, em stock e ao preço.")),
            h("div", { class: "actions" },
                h("button", { class: "button", onclick: () => setTaskEnabled(null, false) }, icon("stop"), "Parar todas"),
                h("button", { class: "button", onclick: () => setTaskEnabled(null, true) }, icon("play"), "Iniciar todas"),
                h("button", { class: "button", onclick: exportTasks, disabled: tasks.length === 0, title: "Descarrega as tarefas num ficheiro JSON" }, icon("external"), "Exportar JSON"),
                h("button", { class: "button", onclick: openTaskImportDrawer, title: "Cria ou atualiza várias tarefas a partir de JSON" }, icon("copy"), "Importar JSON"),
                h("button", { class: "button primary", onclick: () => openTaskDrawer(null) }, icon("plus"), "Nova tarefa"))),

        h("div", { class: "filters" },
            filters.map(([key, label]) =>
                h("button", { class: `filter ${state.taskFilter === key ? "active" : ""}`, onclick: () => { state.taskFilter = key; renderView(true); } },
                    label, h("span", { class: "count" }, counts[key]))),
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
            h("div", { class: "primary-text", style: "display:flex;align-items:center;gap:8px" }, taskLanguagePill(task.language), task.name || task.id),
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

function listingsTable(listings, outcomeCell) {
    return h("div", { class: "table-wrap" }, h("table", {},
        h("thead", {}, h("tr", {}, h("th", {}, "Produto"), h("th", { class: "num" }, "Preço"), h("th", {}, "Stock"), h("th", {}, "Idioma"), h("th", {}, "Resultado"))),
        h("tbody", {}, listings.map((listing) => h("tr", {},
            h("td", { class: "title-cell" }, h("span", { class: "primary-text" }, externalLink(listing.url, listing.title))),
            h("td", { class: "num" }, money(listing.price)),
            h("td", {}, listing.inStock ? h("span", { class: "pill green" }, "Sim") : h("span", { class: "pill red" }, "Não")),
            h("td", {}, languagePill(listing.language)),
            h("td", {}, outcomeCell(listing)))))));
}

/* ----- Stores ----- */

function viewStores() {
    const stores = state.stores;
    return [
        h("div", { class: "page-head" },
            h("div", {}, h("h1", {}, "Lojas"), h("p", {}, "Só são pesquisadas as lojas ativas. Lojas que bloqueiam pedidos automáticos ficam de fora.")),
            h("div", { class: "actions" }, h("button", { class: "button primary", onclick: () => openStoreDrawer(null) }, icon("plus"), "Adicionar loja"))),
        stores.length === 0
            ? h("div", { class: "card" }, emptyState("store", "Sem lojas configuradas."))
            : h("div", { class: "grid stores" }, stores.map(storeCard)),
    ];
}

function storeCard(store) {
    const reports = searches().filter((search) => search.storeName === store.name);
    const failed = reports.filter((report) => !report.succeeded);
    const tasks = state.tasks.filter((task) => task.enabled && ((task.storeNames || []).length === 0 || task.storeNames.some((name) => sameName(name, store.name))));
    const lastChecked = reports.map((report) => report.checkedAt).sort().at(-1);
    const health = !store.enabled ? h("span", { class: "pill grey" }, "Desligada")
        : reports.length === 0 ? h("span", { class: "pill blue" }, "À espera")
            : failed.length === reports.length ? h("span", { class: "pill red" }, "Erro")
                : failed.length > 0 ? h("span", { class: "pill amber" }, "Parcial")
                    : h("span", { class: "pill green" }, "OK");

    return h("div", { class: `card store-card ${store.enabled ? "" : "disabled"}` },
        h("div", { class: "card-body" },
            h("div", { class: "store-card-top" },
                storeAvatar(store.name),
                h("div", { style: "min-width:0" }, h("div", { class: "name" }, store.name), h("div", { class: "url" }, store.baseUrl)),
                switchInput(store.enabled, (checked) => setStoreEnabled(store.name, checked), `Ativar ${store.name}`)),
            h("dl", { class: "kv" },
                h("dt", {}, "Estado"), h("dd", {}, health),
                h("dt", {}, "Plataforma"), h("dd", {}, store.platform),
                h("dt", {}, "Sem marcador"), h("dd", {}, (UNMARKED_LANGUAGE[store.unmarkedLanguage] || UNMARKED_LANGUAGE.Unknown)[1]),
                h("dt", {}, "Tarefas"), h("dd", {}, tasks.length),
                h("dt", {}, "Verificada"), h("dd", {}, lastChecked ? relative(lastChecked) : "—"),
                failed.length > 0 ? [h("dt", {}, "Último erro"), h("dd", { style: "color:var(--red)" }, failed[0].error)] : null)),
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

function openDrawer({ title, subtitle, body, footer }) {
    const drawer = document.getElementById("drawer");
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
    };
    const errors = h("div", {});
    const idInput = textInput(draft.id, (value) => { draft.id = value; }, { readOnly: !isNew, placeholder: "gerado a partir do nome" });
    idInput.addEventListener("input", () => { idInput.dataset.touched = "1"; });

    const body = [
        h("div", { class: "drawer-section" },
            h("h3", {}, "Produto"),
            h("div", { class: "fields" },
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
                    h("input", {
                        type: "checkbox", checked: [...draft.storeNames].some((name) => sameName(name, store.name)), onchange: (event) => {
                            for (const name of [...draft.storeNames]) if (sameName(name, store.name)) draft.storeNames.delete(name);
                            if (event.target.checked) draft.storeNames.add(store.name);
                        }
                    }),
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
    "storeNames": [],
    "enabled": true
  }
]`;

/** The task fields the import accepts, in the same shape "Exportar JSON" produces. */
function taskToJson(task) {
    return {
        id: task.id, name: task.name, enabled: task.enabled, storeNames: task.storeNames || [], searchQuery: task.searchQuery,
        requiredKeywords: task.requiredKeywords || [], excludedKeywords: task.excludedKeywords || [], maxPrice: task.maxPrice, language: task.language,
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
    return {
        id, name, enabled: raw.enabled === undefined ? true : Boolean(raw.enabled),
        storeNames: list(raw.storeNames), searchQuery: String(raw.searchQuery ?? "").trim(),
        requiredKeywords: list(raw.requiredKeywords), excludedKeywords: list(raw.excludedKeywords),
        maxPrice: typeof raw.maxPrice === "number" ? raw.maxPrice : Number(String(raw.maxPrice ?? "").replace(",", ".")) || 0,
        language,
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
                field("JSON", textarea, "Campos: id, name, searchQuery, requiredKeywords, excludedKeywords, maxPrice, language (English/Japanese), storeNames ([] = todas), enabled. Sem id, é gerado a partir do nome.", true),
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
        html: store.platform === "Html" ? store.html : null,
    };
}

function openStoreDrawer(store) {
    const isNew = !store;
    const originalName = store?.name || null;
    const draft = structuredClone(store || {
        name: "", enabled: true, platform: "Shopify", baseUrl: "https://", unmarkedLanguage: "Unknown",
        wooCommerceProductsPath: "/wp-json/wc/store/v1/products", html: null,
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
        } else if (AWIN_FEED_KEYS[draft.platform]) {
            platformFields.replaceChildren(h("div", { class: "notice violet" },
                "Lê o feed de produtos Awin autorizado desta loja (o site da loja não é contactado). Põe o URL do Create-a-Feed em "
                + `AwinFeeds__Feeds__${AWIN_FEED_KEYS[draft.platform]} no .env. O feed é descarregado no máximo de 6 em 6 horas; sem feed, a loja mostra "integração não configurada".`));
        } else {
            platformFields.replaceChildren(h("div", { class: "notice violet" }, "Shopify não precisa de mais nada: o bot usa a pesquisa pública da loja."));
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
        h("div", { class: "drawer-section" },
            h("h3", {}, "Loja"),
            h("div", { class: "fields" },
                field("Nome", textInput(draft.name, (value) => { draft.name = value; }, { placeholder: "ex.: Versus Gamecenter" })),
                field("Plataforma", h("select", { onchange: (event) => { draft.platform = event.target.value; renderPlatformFields(); } },
                    Object.entries(PLATFORMS).map(([value, label]) => h("option", { value, selected: draft.platform === value }, label)))),
                field("URL base", textInput(draft.baseUrl, (value) => { draft.baseUrl = value; }, { placeholder: "https://loja.pt" }), null, true),
                field("Títulos sem indicação de idioma", h("select", { onchange: (event) => { draft.unmarkedLanguage = event.target.value; } },
                    Object.entries(UNMARKED_LANGUAGE).map(([value, [label]]) => h("option", { value, selected: draft.unmarkedLanguage === value }, label))),
                    "“Contar como inglês” só se a loja marca sempre os PT/ES/JP. “Idioma por confirmar” avisa na mesma, com um aviso para confirmares na página.", true)),
            h("div", { style: "display:flex;align-items:center;gap:12px" },
                switchInput(draft.enabled, (checked) => { draft.enabled = checked; }, "Loja ativa"), h("span", {}, "Loja ativa"))),
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
