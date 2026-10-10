"use strict";

/* ==========================================================================
   Lojas — where the bot searches, grouped by area and tier, and how each store answered in the last cycle.
   ========================================================================== */

state.storeHealthFilter = "all";                         // all | ok | slow | error | off

const HEALTH_GROUPS = {
  ok: ["A responder", "accent", (kind) => kind === "ok"],
  slow: ["Lentas", "amber", (kind) => kind === "slow"],
  error: ["Com erro", "red", (kind) => kind === "error" || kind === "partial"],
  off: ["Desligadas", "plain", (kind) => kind === "off"],
};

RENDERERS.lojas = function viewStores() {
  const stores = state.stores;
  const tags = [...new Set(stores.flatMap((store) => store.tags || []))].sort((a, b) => a.localeCompare(b, "pt"));
  const health = new Map(stores.map((store) => [store.name, storeHealth(store)]));
  const listBox = h("div", { class: "store-areas" });

  const renderList = () => {
    const visible = stores.filter((store) => storeMatchesFilters(store)
      && (state.storeHealthFilter === "all" || HEALTH_GROUPS[state.storeHealthFilter][2](health.get(store.name).kind)));
    if (stores.length === 0) {
      listBox.replaceChildren(h("div", { class: "card soft" }, emptyState("store", "Ainda não há lojas.", "Adiciona a primeira: cola o site e carrega em Detetar.",
        button("Adicionar loja", { iconName: "plus", kind: "primary", onClick: () => openStoreDrawer(null) }))));
      return;
    }
    if (visible.length === 0) { listBox.replaceChildren(h("div", { class: "card soft" }, emptyState("search", "Nenhuma loja corresponde aos filtros."))); return; }

    // Area (country) first, then tier inside it: S → A → B → C → sem tier.
    const tierOrder = ["S", "A", "B", "C", "None"];
    listBox.replaceChildren(...Object.keys(COUNTRIES).map((country) => {
      const inCountry = visible.filter((store) => (store.country || "Portugal") === country);
      if (inCountry.length === 0) return null;
      const [label, flag] = COUNTRIES[country];
      return h("section", { class: "store-area" },
        h("h2", { class: "area-title" }, h("span", { "aria-hidden": "true" }, flag), label,
          h("span", { class: "muted small" }, `${inCountry.length} ${inCountry.length === 1 ? "loja" : "lojas"} · ${inCountry.filter((store) => store.enabled).length} ativas`)),
        tierOrder.map((tier) => {
          const inTier = inCountry.filter((store) => (store.tier || "None") === tier)
            .sort((a, b) => (b.enabled - a.enabled) || a.name.localeCompare(b.name, "pt"));
          if (inTier.length === 0) return null;
          const [tierLabel, tone, description] = TIERS[tier];
          return h("div", { class: "store-tier" },
            h("div", { class: "tier-title" }, pill(tierLabel, tone), h("span", {}, description), h("span", { class: "muted" }, `· ${inTier.length}`)),
            h("div", { class: "store-grid" }, inTier.map((store) => storeCard(store, health.get(store.name)))));
        }));
    }).filter(Boolean));
  };
  renderList();

  const counts = Object.fromEntries(Object.entries(HEALTH_GROUPS).map(([key, [, , test]]) =>
    [key, stores.filter((store) => test(health.get(store.name).kind)).length]));

  return [
    pageHead("Lojas", "Onde o bot procura, e se cada loja está a responder bem.", [
      storesMenu(),
      button("Adicionar loja", { iconName: "plus", kind: "primary", onClick: () => openStoreDrawer(null) }),
    ]),
    h("div", { class: "health-tiles", role: "group", "aria-label": "Filtrar por estado" },
      Object.entries(HEALTH_GROUPS).map(([key, [label, tone]]) => h("button", {
        type: "button", class: `health-tile ${tone} ${state.storeHealthFilter === key ? "on" : ""}`, "aria-pressed": state.storeHealthFilter === key ? "true" : "false",
        onclick: () => { state.storeHealthFilter = state.storeHealthFilter === key ? "all" : key; renderView(true); },
      }, h("span", {}, label), h("strong", { class: "mono" }, counts[key])))),
    h("div", { class: "toolbar wrap" },
      searchField(state.storeQuery, "Filtrar: nome, site, etiqueta…", (value) => { state.storeQuery = value; renderList(); }, "store-search", "Filtrar lojas"),
      selectPill("Área", state.storeCountry, [["all", "Todas"], ...Object.entries(COUNTRIES).map(([key, [label]]) => [key, label])],
        (value) => { state.storeCountry = value; renderView(true); }),
      selectPill("Tier", state.storeTier, [["all", "Todos"], ...Object.entries(TIERS).map(([key, [label, , description]]) => [key, `${label} — ${description}`])],
        (value) => { state.storeTier = value; renderView(true); }),
      selectPill("Tipo", state.storeType, [["all", "Todos"], ...Object.entries(STORE_TYPES)], (value) => { state.storeType = value; renderView(true); }),
      tags.length ? selectPill("Etiqueta", state.storeTag, [["", "Todas"], ...tags.map((tag) => [tag, tag])], (value) => { state.storeTag = value; renderView(true); }) : null,
      selectPill("Teste", state.storeTest, [["all", "Qualquer"], ["never", "Nunca testadas"], ["ok", "Último OK"], ["failed", "Último falhou"], ["auto", "Desligadas pelo bot"]],
        (value) => { state.storeTest = value; renderView(true); }),
      h("span", { class: "muted small toolbar-end" }, `${stores.length} lojas · ${stores.filter((store) => store.enabled).length} ativas`)),
    listBox,
  ];
};

function storesMenu() {
  const item = (label, iconName, onClick) =>
    h("button", { type: "button", class: "menu-item", onclick: (event) => { event.currentTarget.closest("details").open = false; onClick(); } }, icon(iconName), label);
  return h("details", { class: "menu" },
    h("summary", { class: "btn", "aria-label": "Mais ações" }, icon("more"), "Mais"),
    h("div", { class: "menu-list" },
      item("Testar várias lojas", "flask", openStoreTestDrawer),
      h("hr"),
      item("Exportar JSON", "download", exportStores),
      item("Importar JSON", "upload", openStoreImportDrawer)));
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
  return matchesWords(state.storeQuery, store.name, store.baseUrl, (store.tags || []).join(" "), store.notes || "");
}

const HEALTH_TONE = { ok: "accent", slow: "amber", partial: "amber", error: "red", pending: "plain", off: "plain", manual: "plain" };

function storeCard(store, health) {
  const tasks = state.tasks.filter((task) => task.enabled && ((task.storeNames || []).length === 0 || task.storeNames.some((name) => sameName(name, store.name))));
  const isFeed = ["Fnac", "Worten", "ElCorteIngles", "Awin"].includes(store.platform);
  const right = health.kind === "ok" || health.kind === "slow" || health.kind === "partial"
    ? (isFeed ? "feed" : durationText(health.averageMs))
    : health.kind === "error" ? "erro" : "—";
  const [tierLabel, tierTone] = TIERS[store.tier || "None"];
  const unmarked = (UNMARKED_LANGUAGE[store.unmarkedLanguage] || UNMARKED_LANGUAGE.Unknown)[1];

  let label = health.label;
  if (health.kind === "error" && health.lastChecked) label = `Sem resposta desde as ${clock(health.lastChecked)}`;

  return h("article", { class: `store-card ${health.kind} ${store.enabled ? "" : "disabled"}` },
    h("header", { class: "store-card-head" },
      storeAvatar(store.name),
      h("div", { class: "store-card-name" },
        h("h3", {}, store.name),
        h("div", { class: "muted small" }, PLATFORM_LABELS[store.platform] || store.platform,
          safeHref(store.baseUrl) ? [" · ", h("a", { href: store.baseUrl, target: "_blank", rel: "noopener noreferrer", class: "plain-link" }, hostLabel(store.baseUrl))] : null)),
      switchInput(store.enabled, (checked) => setStoreEnabled(store.name, checked), store.enabled ? `Desligar ${store.name}` : `Ativar ${store.name}`)),
    h("div", { class: `health-line ${HEALTH_TONE[health.kind]}` },
      h("span", { class: "health-dot", "aria-hidden": "true" }), h("span", { class: "health-label", title: health.error || "" }, label),
      h("span", { class: "mono muted small health-right" }, right)),
    searchSquares(health),
    health.kind === "error" || health.kind === "partial"
      ? h("div", { class: "store-error", title: health.error || "" }, health.error)
      : null,
    h("div", { class: "info-box" }, "Sem marcador de idioma: ", h("strong", {}, unmarked)),
    h("div", { class: "tag-row" },
      store.tier && store.tier !== "None" ? pill(tierLabel, tierTone) : null,
      store.type && store.type !== "Unknown" ? h("span", { class: "tag" }, STORE_TYPES[store.type]) : null,
      (store.tags || []).map((tag) => h("button", {
        type: "button", class: `tag tag-button ${tag === state.storeTag ? "accent" : ""}`, title: `Filtrar pela etiqueta “${tag}”`,
        onclick: () => { state.storeTag = tag === state.storeTag ? "" : tag; renderView(true); },
      }, `#${tag}`))),
    h("dl", { class: "store-facts" },
      h("dt", {}, "Produtos"), h("dd", {}, `${tasks.length} a vigiar aqui`),
      h("dt", {}, "Último teste"), h("dd", {}, store.lastTest
        ? [testPill(store.lastTest), h("span", { class: "muted small" }, ` ${relative(store.lastTest.at)} · ${store.lastTest.results} produtos`)]
        : h("span", { class: "muted" }, "nunca")),
      store.enabled && store.consecutiveFailures > 0
        ? [h("dt", {}, "Ciclos c/ erro"), h("dd", { class: "amber" }, `${store.consecutiveFailures} seguidos`)] : null,
      store.autoDisabledAt && !store.enabled
        ? [h("dt", {}, "Desligada"), h("dd", { class: "red" }, `pelo bot ${relative(store.autoDisabledAt)}, após ${store.consecutiveFailures} ciclos com erro`)] : null),
    store.notes ? h("p", { class: "store-notes" }, store.notes) : null,
    h("footer", { class: "store-card-actions" },
      button("Testar pesquisa", { kind: "grow", disabled: store.platform === "Manual", onClick: () => {
        Object.assign(state.probe, { storeName: store.name, results: null, errors: null });
        go("testar");
      } }),
      button("Editar", { onClick: () => openStoreDrawer(store) }),
      iconButton("trash", `Apagar ${store.name}`, () => deleteStore(store), "danger")));
}

/** One square per search of the last cycle (one per product), coloured by how it went. */
function searchSquares(health) {
  const reports = health.reports || [];
  if (reports.length === 0) {
    return h("div", { class: "squares-none" }, health.kind === "manual" ? "Não é pesquisada (só referência)." : health.kind === "off" ? "Não é pesquisada enquanto estiver desligada." : "Ainda sem pesquisas — aparecem depois do próximo ciclo.");
  }
  const ordered = reports.slice().sort((a, b) => new Date(a.checkedAt) - new Date(b.checkedAt));
  return h("div", { class: "squares-wrap" },
    h("div", { class: "squares", role: "img", "aria-label": `${reports.length} pesquisas no último ciclo, ${health.failed.length} com erro` },
      ordered.map((report) => {
        const task = findTask(report.stockTaskId);
        const kind = !report.succeeded ? "error" : report.durationMs > SLOW_MS ? "slow" : report.matchCount > 0 ? "match" : "ok";
        return h("span", { class: `sq ${kind}`, title: `${task?.name || report.stockTaskId}: ${!report.succeeded ? report.error : `${report.listings.length} resultados · ${durationText(report.durationMs)}`}` });
      })),
    h("div", { class: "squares-caption" }, h("span", {}, `último ciclo · ${reports.length} ${reports.length === 1 ? "pesquisa" : "pesquisas"}`), h("span", {}, relative(health.lastChecked))));
}

const hostLabel = (url) => { try { return new URL(url).host.replace(/^www\./, ""); } catch { return url; } };

/* ----- Testar loja (one search in one store, with the verdict for every product) ----- */

RENDERERS.testar = function viewProbe() {
  const probe = state.probe;
  const stores = state.stores.filter((store) => store.platform !== "Manual");
  if (!probe.storeName && stores.length) probe.storeName = stores[0].name;

  const storeSelect = h("select", { onchange: (event) => { probe.storeName = event.target.value; } },
    stores.map((store) => h("option", { value: store.name, selected: store.name === probe.storeName }, `${store.name}${store.enabled ? "" : " (desligada)"}`)));
  const queryInput = h("input", { type: "search", value: probe.query, placeholder: "ex.: elite trainer box", oninput: (event) => { probe.query = event.target.value; } });
  queryInput.addEventListener("keydown", (event) => { if (event.key === "Enter") runProbe(); });

  let results = null;
  if (probe.loading) results = h("div", { class: "card soft" }, h("p", { class: "muted pad" }, "A pesquisar na loja…"));
  else if (probe.errors) results = errorList(probe.errors);
  else if (probe.results) {
    results = probe.results.length === 0
      ? h("div", { class: "card soft" }, emptyState("search", "A loja não devolveu resultados."))
      : h("section", { class: "section" },
        sectionHead("Resultados", { count: probe.results.length }),
        h("ul", { class: "result-rows card" }, probe.results.map((listing) => probeRow(listing))));
  }

  return [
    pageHead("Testar loja", "Faz uma pesquisa numa loja e mostra, para cada produto que vigias, porque é que cada resultado corresponde ou não."),
    h("div", { class: "card form-card" },
      h("div", { class: "probe-form" },
        field("Loja", storeSelect),
        field("Pesquisa", queryInput),
        button(probe.loading ? "A testar…" : "Testar", { iconName: "search", kind: "primary", disabled: probe.loading || stores.length === 0, onClick: runProbe }))),
    results,
  ];
};

function probeRow(listing) {
  const tasks = state.tasks;
  return h("li", { class: "result-row probe" },
    productArt({ imageUrl: listing.imageUrl, title: listing.title, size: "thumb" }),
    h("div", { class: "result-text" },
      externalLink(listing.url, listing.title, "result-title"),
      h("div", { class: "result-meta" }, `${listing.inStock ? "Em stock" : "Esgotado"} · ${LANGUAGES[listing.language] || listing.language}`, listing.preOrder ? " · pré-venda" : ""),
      tasks.length ? h("div", { class: "tag-row" }, tasks.map((task) => {
        const [label, tone] = OUTCOMES[listing.outcomeByTask[task.id]] || ["—", "plain"];
        return h("span", { class: `tag small ${tone === "plain" ? "" : tone}`, title: `${task.name}: ${label}` }, `${task.name || task.id}: ${label}`);
      })) : null),
    h("div", { class: "result-price" }, h("span", { class: "mono price" }, money(listing.price))));
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
    if (state.route.view === "testar") renderView(true);
  }
}
