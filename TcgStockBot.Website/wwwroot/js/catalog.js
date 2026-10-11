"use strict";

/* ==========================================================================
   Produtos — the catalogue of everything the bot found (one card per product, every store inside), and the product
   page ("ficha"): stores table, price chart by period, history and manual regrouping.
   A product is a Compare group: the set (task) + product kind, unless corrected by hand.
   ========================================================================== */

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

const DAY = 864e5;
const shortDate = (iso) => (iso ? new Date(iso).toLocaleDateString("pt-PT", { day: "2-digit", month: "2-digit", year: "2-digit" }) : "—");

async function loadCatalog() {
  try {
    state.compare = await api("/compare/list");
  } catch (error) {
    state.compare = state.compare || [];
    toast(errorText(error), "error");
  }
  if (state.route.view === "produtos") renderView(true);
}

async function loadFicha(key, { quiet = false } = {}) {
  try {
    state.ficha = await api(`/compare/get?identifier=${encodeURIComponent(key)}`);
  } catch (error) {
    state.ficha = { key, missing: true, message: errorText(error) };
    if (!quiet) toast(errorText(error), "error");
  }
  if (state.route.view === "ficha" && state.route.param === key) renderView(true);
}

/** Cheapest in stock, runner-up, lowest ever and most recent drop of one product. */
function compareSummary(group) {
  const listings = group.listings;
  const inStock = listings.filter((listing) => listing.currentInStock && listing.currentPrice !== null).sort((a, b) => a.currentPrice - b.currentPrice);
  const lowestEver = listings.filter((listing) => listing.lowestPrice !== null)
    .reduce((best, listing) => (!best || listing.lowestPrice < best.lowestPrice ? listing : best), null);
  const lastDrop = listings.filter((listing) => listing.lastDropAt).sort((a, b) => new Date(b.lastDropAt) - new Date(a.lastDropAt))[0] || null;
  return { best: inStock[0] || null, second: inStock[1] || null, inStockCount: inStock.length, lowestEver, lastDrop };
}

/** What a catalogue card shows: "desde", stock, movement over 30 days, picture, dates. */
function catalogItem(group) {
  const summary = compareSummary(group);
  const priced = group.listings.filter((listing) => listing.currentPrice !== null).map((listing) => listing.currentPrice);
  const before = group.listings.map((listing) => listing.price30DaysAgo).filter((price) => price !== null && price !== undefined);
  const now = summary.best?.currentPrice ?? (priced.length ? Math.min(...priced) : null);
  const then = before.length ? Math.min(...before) : null;
  const firstSeen = Math.min(...group.listings.map((listing) => new Date(listing.firstSeen).getTime()).filter(Boolean));
  const lastSeen = Math.max(...group.listings.map((listing) => new Date(listing.lastSeen).getTime()).filter(Boolean));
  return {
    group, summary, from: now, soldOut: !summary.best,
    move: now && then ? ((now - then) / then) * 100 : null,
    image: group.listings.find((listing) => listing.currentInStock && listing.imageUrl)?.imageUrl || group.listings.find((listing) => listing.imageUrl)?.imageUrl || null,
    firstSeen: Number.isFinite(firstSeen) ? firstSeen : null,
    lastSeen: Number.isFinite(lastSeen) ? lastSeen : null,
    stores: new Set(group.listings.map((listing) => listing.storeName)).size,
  };
}

/** "↓ 8%" (accent), "↑ 5%" (red) or "=" — the 30-day movement of the lowest price. */
function movePill(move, long = false) {
  if (move === null || move === undefined || !Number.isFinite(move)) return h("span", { class: "move-pill flat", title: "Sem preço de há 30 dias" }, long ? "sem dados" : "—");
  const rounded = Math.round(move);
  if (rounded === 0) return h("span", { class: "move-pill flat", title: "O preço mais baixo não mudou em 30 dias" }, "=");
  return h("span", { class: `move-pill ${rounded < 0 ? "down" : "up"}`, title: "Preço mais baixo agora vs há 30 dias" }, `${rounded < 0 ? "↓" : "↑"} ${Math.abs(rounded)}%`);
}

const CATALOG_SORTS = { stores: "Mais lojas com stock", cheap: "Mais baratos", drop: "Maior descida (30 dias)", recent: "Novos no catálogo", name: "Nome" };

RENDERERS.produtos = function viewCatalog() {
  if (state.compare === null) {
    return [pageHead("Produtos", "A carregar o catálogo…"), h("div", { class: "card soft" }, h("p", { class: "muted pad" }, "A carregar…"))];
  }

  const all = state.compare.map(catalogItem);
  const inTab = all.filter((item) => inGameTab(item.group.game));
  const kinds = [...new Set(inTab.map((item) => item.group.kind))].sort((a, b) => kindOrder(a) - kindOrder(b));
  if (state.catalogKind !== "all" && !kinds.includes(state.catalogKind)) state.catalogKind = "all";
  const storeNames = [...new Set(inTab.flatMap((item) => item.group.listings.map((listing) => listing.storeName)))].sort((a, b) => a.localeCompare(b, "pt"));
  if (state.catalogStore && !storeNames.includes(state.catalogStore)) state.catalogStore = "";

  const grid = h("div", { class: "catalog-grid" });
  const counter = h("span", { class: "muted small toolbar-end" });
  const more = h("div", { class: "center" });

  const renderGrid = () => {
    const visible = inTab.filter((item) => (state.catalogKind === "all" || item.group.kind === state.catalogKind)
      && (!state.catalogStore || item.group.listings.some((listing) => listing.storeName === state.catalogStore && (!state.catalogStock || listing.currentInStock)))
      && (!state.catalogStock || !item.soldOut)
      && matchesWords(state.catalogQuery, item.group.title, item.group.setName, item.group.listings.map((listing) => `${listing.storeName} ${listing.title}`).join(" ")));
    const sorters = {
      stores: (a, b) => (b.summary.inStockCount - a.summary.inStockCount) || ((a.from ?? Infinity) - (b.from ?? Infinity)),
      cheap: (a, b) => (a.soldOut - b.soldOut) || ((a.from ?? Infinity) - (b.from ?? Infinity)),
      drop: (a, b) => (a.move ?? Infinity) - (b.move ?? Infinity),
      recent: (a, b) => (b.firstSeen ?? 0) - (a.firstSeen ?? 0),
      name: (a, b) => a.group.title.localeCompare(b.group.title, "pt"),
    };
    visible.sort(sorters[state.catalogSort] || sorters.stores);
    counter.textContent = `${visible.length} ${visible.length === 1 ? "produto" : "produtos"} · ${storeNames.length} lojas`;
    grid.replaceChildren(...(visible.length
      ? visible.slice(0, state.catalogShown).map(catalogCard)
      : [h("div", { class: "card soft span-all" }, emptyState("grid", all.length === 0 ? "O catálogo ainda está vazio." : "Nenhum produto com estes filtros.",
        all.length === 0 ? "Depois do primeiro ciclo, tudo o que o bot encontra para os produtos que vigias aparece aqui, agrupado por produto." : null,
        all.length === 0 ? linkButton("Vigiar produto", routeHash("vigiar"), { iconName: "plus", kind: "primary" }) : null))]));
    more.replaceChildren(visible.length > state.catalogShown
      ? button(`Mostrar mais ${Math.min(48, visible.length - state.catalogShown)}`, { onClick: () => { state.catalogShown += 48; renderGrid(); } })
      : "");
  };
  renderGrid();
  const rerender = () => { state.catalogShown = 48; renderGrid(); };

  return [
    pageHead("Produtos", "Tudo o que o bot encontra nas lojas, agrupado por produto: o preço mais baixo, quem tem stock e como o preço mexeu.", [
      searchField(state.catalogQuery, "Procurar produto ou loja", (value) => { state.catalogQuery = value; rerender(); }, "catalog-search", "Procurar produtos"),
    ]),
    gameTabs(all.map((item) => item.group.game), "produtos"),
    h("div", { class: "toolbar wrap catalog-toolbar" },
      selectPill("Formato", state.catalogKind, [["all", "Todos"], ...kinds.map((kind) => [kind, kindLabel(kind)])], (value) => { state.catalogKind = value; rerender(); }),
      storeNames.length > 1 ? selectPill("Loja", state.catalogStore, [["", "Todas"], ...storeNames.map((name) => [name, name])], (value) => { state.catalogStore = value; rerender(); }) : null,
      labelledSwitch(state.catalogStock, "Só em stock", (checked) => { state.catalogStock = checked; writePreference("catalogStock", checked); rerender(); }),
      selectPill("Ordenar", state.catalogSort, Object.entries(CATALOG_SORTS), (value) => { state.catalogSort = value; writePreference("catalogSort", value); rerender(); }),
      counter),
    grid,
    more,
  ];
};

function catalogCard(item) {
  const { group, summary } = item;
  return h("a", { class: `catalog-card ${item.soldOut ? "sold-out" : ""}`, href: routeHash("ficha", group.key) },
    h("div", { class: "catalog-art" },
      productArt({ imageUrl: item.image, game: group.game, title: group.title, query: group.kindLabel, size: "hero" }),
      h("span", { class: `stock-badge ${item.soldOut ? "out" : "in"}` }, item.soldOut ? "Esgotado" : "Em stock")),
    h("div", { class: "catalog-body" },
      h("div", { class: "catalog-meta" }, gamePill(group.game), h("span", { class: "muted small" }, group.kindLabel || kindLabel(group.kind))),
      h("h3", { class: "catalog-title", title: group.title }, group.title),
      h("div", { class: "catalog-price" },
        h("span", { class: "muted small" }, item.soldOut ? "último preço" : "desde"),
        h("strong", { class: "price-l mono" }, money(item.from)),
        msrpPill(summary.best?.currentPrice, group.msrp)),
      h("div", { class: "catalog-foot" },
        h("span", { class: "catalog-stock" }, h("span", { class: `bot-dot ${item.soldOut ? "off" : "on"}`, "aria-hidden": "true" }),
          `em stock ${summary.inStockCount}/${item.stores} ${item.stores === 1 ? "loja" : "lojas"}`),
        h("span", { class: "catalog-move" }, h("span", { class: "dim small" }, "30d"), movePill(item.move)))));
}

/* ----- Ficha (product page) ----- */

const PERIODS = [["7", "7 d"], ["30", "30 d"], ["90", "90 d"], ["180", "180 d"], ["365", "1 ano"], ["all", "Tudo"]];

/** Cardmarket search for the product (only games Cardmarket lists). */
const CARDMARKET_GAMES = { Pokemon: "Pokemon", OnePiece: "OnePiece", Lorcana: "Lorcana", Magic: "Magic", Riftbound: "Riftbound" };
function cardmarketUrl(group) {
  const slug = CARDMARKET_GAMES[gameKey(group.game)];
  if (!slug) return null;
  const query = `${group.setName || ""} ${group.kindLabel || ""}`.trim() || group.title.replace(" · ", " ");
  return `https://www.cardmarket.com/en/${slug}/Products/Search?searchString=${encodeURIComponent(query)}`;
}

RENDERERS.ficha = function viewFicha() {
  const key = state.route.param;
  const group = state.ficha && state.ficha.key === key ? state.ficha : null;
  const back = h("a", { class: "back-link", href: routeHash("produtos") }, icon("back"), "Produtos");
  if (!group) {
    loadFicha(key);
    return [back, h("div", { class: "card soft" }, h("p", { class: "muted pad" }, "A carregar o produto…"))];
  }
  if (group.missing) {
    return [back, h("div", { class: "card soft" }, emptyState("grid", "Este produto já não existe no catálogo.", group.message || "Pode ter sido separado ou juntado a outro."))];
  }

  const item = catalogItem(group);
  const { summary } = item;
  const colors = storeColors(group.listings.map((listing) => listing.storeName));
  const task = group.stockTaskId ? findTask(group.stockTaskId) : null;
  const cardmarket = cardmarketUrl(group);
  const stat = (label, value, note = null, tone = "") => h("div", { class: "stat-box" }, h("span", {}, label), h("strong", { class: `mono ${tone}` }, value), note ? h("small", {}, note) : null);

  return [
    back,
    h("section", { class: "ficha-hero" },
      h("div", { class: "ficha-art" }, productArt({ imageUrl: item.image, game: group.game, title: group.title, query: group.kindLabel, size: "hero" })),
      h("div", { class: "ficha-main" },
        h("div", { class: "catalog-meta" }, gamePill(group.game), h("span", { class: "muted" }, group.kindLabel || kindLabel(group.kind)),
          group.key.startsWith("solo|") ? pill("separado à mão", "plain") : null),
        h("h1", { class: "ficha-title" }, group.title),
        task ? h("a", { class: "ficha-task", href: routeHash("produto", task.id) }, icon("eye"), `Vigiado em “${task.name}” · ${priceRuleText(task, true)}`) : null,
        h("div", { class: "ficha-price" },
          h("div", {},
            h("span", { class: "muted small" }, summary.best ? `Mais barato com stock · ${summary.best.storeName}` : "Sem stock em nenhuma loja"),
            h("div", { class: "ficha-price-row" }, h("span", { class: "price-xl" }, money(item.from)), msrpPill(summary.best?.currentPrice, group.msrp)))),
        h("div", { class: "ficha-actions" },
          summary.best && safeHref(summary.best.url) ? linkButton("Abrir na loja", summary.best.url, { kind: "primary", external: true, trailingIcon: "external" }) : null,
          cardmarket ? linkButton("Cardmarket", cardmarket, { external: true, trailingIcon: "external" }) : null,
          task ? linkButton("Regras de alerta", routeHash("produto", task.id), { iconName: "sliders" }) : null))),

    h("div", { class: "stat-boxes six" },
      stat("Estado", summary.best ? "Em stock" : "Esgotado", summary.best ? `${summary.inStockCount} ${summary.inStockCount === 1 ? "loja" : "lojas"} com stock` : "avisamos quando voltar", summary.best ? "accent" : "red"),
      stat("Lojas", `${item.stores}`, `${summary.inStockCount} com stock`),
      stat("Mínimo de sempre", money(summary.lowestEver?.lowestPrice), summary.lowestEver ? `${summary.lowestEver.storeName} · ${shortDate(summary.lowestEver.lowestPriceAt)}` : null),
      stat("MSRP", group.msrp ? money(group.msrp) : "—", group.msrp ? `${group.msrpLabel} · ${(MSRP_BASIS[group.msrpBasis] || MSRP_BASIS.Estimate)[0].toLowerCase()}` : "sem referência"),
      stat("Mov. 30 dias", movePill(item.move, true), "preço mais baixo"),
      stat("No catálogo desde", item.firstSeen ? shortDate(new Date(item.firstSeen).toISOString()) : "—", item.lastSeen ? `última leitura ${relative(new Date(item.lastSeen).toISOString())}` : null)),

    fichaStores(group, summary, colors),
    fichaChart(group, colors),
    fichaHistory(group),
    h("section", { class: "card section-card", "aria-labelledby": "fix-h" },
      h("h2", { id: "fix-h" }, "Correção"),
      h("p", { class: "muted small" }, "Falta uma loja com este produto, ou há aqui um anúncio que não é este produto? Junta ou separa anúncios. A correção fica guardada e vale para os próximos ciclos."),
      h("div", { class: "card-actions left" }, button("Juntar anúncio…", { iconName: "plus", onClick: () => joinListing(group) }))),
  ];
};

function fichaStores(group, summary, colors) {
  const rows = group.listings
    .filter((listing) => !state.fichaHideSoldOut || listing.currentInStock)
    .sort((a, b) => (b.currentInStock - a.currentInStock) || ((a.currentPrice ?? Infinity) - (b.currentPrice ?? Infinity)));
  const soldOut = group.listings.filter((listing) => !listing.currentInStock).length;
  return h("section", { class: "card section-card", "aria-labelledby": "stores-table-h" },
    h("div", { class: "card-head-row" },
      h("h2", { id: "stores-table-h" }, "Lojas"),
      soldOut ? labelledSwitch(state.fichaHideSoldOut, `Esconder esgotadas (${soldOut})`, (checked) => { state.fichaHideSoldOut = checked; renderView(true); }) : null),
    h("div", { class: "table-wrap" }, h("table", { class: "table ficha-table" },
      h("thead", {}, h("tr", {}, h("th", {}, "Loja"), h("th", { class: "num" }, "Preço"), h("th", {}, "Stock"), h("th", { class: "num" }, "Mínimo"), h("th", {}, "Atualizado"), h("th", {}))),
      h("tbody", {}, rows.map((listing) => h("tr", { class: listing === summary.best ? "best" : "" },
        h("td", { class: "title-cell" },
          h("div", { class: "store-line" }, h("span", { class: "swatch", style: `background:${colors.get(listing.storeName)}` }), h("strong", {}, listing.storeName),
            listing === summary.best ? h("span", { class: "best-badge" }, "mais barato") : null,
            listing.isManual ? pill("manual", "plain", "Neste produto por uma correção manual") : null),
          h("div", { class: "listing-title muted small", title: listing.title }, listing.title),
          h("div", { class: "link-row" },
            h("button", { type: "button", class: "text-link small", onclick: () => separateListing(group, listing) }, "Separar"),
            listing.isManual ? h("button", { type: "button", class: "text-link small", onclick: () => assignListing(listing.historyKey, "", group.key) }, "Repor automático") : null)),
        h("td", { class: "num nowrap" }, h("span", { class: "mono strong" }, money(listing.currentPrice)), h("div", {}, msrpPill(listing.currentPrice, group.msrp))),
        h("td", { class: "nowrap" }, listing.currentInStock ? pill("Em stock", "accent") : pill("Esgotado", "plain")),
        h("td", { class: "num nowrap" }, h("div", { class: "mono" }, money(listing.lowestPrice)), h("div", { class: "muted small" }, shortDate(listing.lowestPriceAt))),
        h("td", { class: "nowrap muted small", title: dateTime(listing.lastSeen) }, relative(listing.lastSeen)),
        h("td", { class: "actions" }, safeHref(listing.url) ? linkButton("Abrir", listing.url, { kind: "small", external: true, trailingIcon: "external" }) : null)))))));
}

/** The group with only the chosen stores and the points of the chosen period (the line starts at the period start). */
function periodGroup(group) {
  const cutoff = state.fichaPeriod === "all" ? -Infinity : Date.now() - Number(state.fichaPeriod) * DAY;
  return {
    ...group,
    listings: group.listings.filter((listing) => !state.fichaHidden.includes(listing.storeName)).map((listing) => {
      const points = listing.points.map((point) => ({ ...point, time: new Date(point.at).getTime() }));
      const before = points.filter((point) => point.time < cutoff).at(-1);
      const inside = points.filter((point) => point.time >= cutoff);
      const clipped = before ? [{ ...before, at: new Date(cutoff).toISOString() }, ...inside] : inside;
      return { ...listing, points: clipped.map(({ time, ...point }) => point) };
    }).filter((listing) => listing.points.length > 0),
  };
}

function fichaChart(group, colors) {
  const shown = periodGroup(group);
  const prices = shown.listings.flatMap((listing) => listing.points.filter((point) => point.inStock).map((point) => point.price)).sort((a, b) => a - b);
  const changes = shown.listings.reduce((sum, listing) => sum + Math.max(0, listing.points.length - 1), 0);
  const usual = prices.length ? prices[Math.floor(prices.length / 2)] : null;
  const stores = [...new Set(group.listings.map((listing) => listing.storeName))];

  return h("section", { class: "card section-card", "aria-labelledby": "chart-h" },
    h("div", { class: "card-head-row wrap" },
      h("h2", { id: "chart-h" }, "Evolução do preço"),
      segmented(PERIODS.map(([value, label]) => [value, label]), state.fichaPeriod, (value) => { state.fichaPeriod = value; writePreference("fichaPeriod", value); renderView(true); }, "Período")),
    stores.length > 1 ? h("div", { class: "store-toggles", role: "group", "aria-label": "Lojas no gráfico" }, stores.map((name) => {
      const on = !state.fichaHidden.includes(name);
      return h("button", {
        type: "button", class: `chip ${on ? "on" : ""}`, "aria-pressed": on ? "true" : "false",
        onclick: () => { state.fichaHidden = on ? [...state.fichaHidden, name] : state.fichaHidden.filter((item) => item !== name); renderView(true); },
      }, h("span", { class: "swatch", style: `background:${colors.get(name)}` }), name);
    })) : null,
    h("div", { class: "chart-stats" },
      h("div", {}, h("span", {}, "Preço habitual"), h("strong", { class: "mono" }, money(usual))),
      h("div", {}, h("span", {}, "Mínimo"), h("strong", { class: "mono accent" }, money(prices[0] ?? null))),
      h("div", {}, h("span", {}, "Máximo"), h("strong", { class: "mono" }, money(prices.at(-1) ?? null))),
      h("div", {}, h("span", {}, "Alterações"), h("strong", { class: "mono" }, changes))),
    shown.listings.length ? compareChart(shown, colors, { width: 1100, height: 320 }) : emptyState("chart", "Sem leituras neste período."));
}

/** Every change the bot saw, newest first: first reading, restock, sold out, price up or down. */
function fichaHistory(group) {
  const events = [];
  for (const listing of group.listings) {
    listing.points.forEach((point, index) => {
      const previous = listing.points[index - 1];
      const at = point.at;
      if (!previous) {
        events.push({ at, store: listing.storeName, kind: "Primeira leitura", tone: "plain", detail: `${money(point.price)} · ${point.inStock ? "em stock" : "esgotado"}` });
        return;
      }
      if (previous.inStock !== point.inStock) {
        events.push({ at, store: listing.storeName, kind: point.inStock ? "Reposição" : "Esgotou", tone: point.inStock ? "accent" : "red", detail: money(point.price) });
      }
      if (previous.price !== point.price) {
        const down = point.price < previous.price;
        events.push({ at, store: listing.storeName, kind: down ? "Baixou" : "Subiu", tone: down ? "accent" : "amber", detail: `${money(previous.price)} → ${money(point.price)}` });
      }
    });
  }
  events.sort((a, b) => new Date(b.at) - new Date(a.at));
  const box = h("div", {});
  const render = (limit) => append(box.replaceChildren() || box, [
    events.length === 0 ? h("p", { class: "muted small" }, "Ainda sem leituras.") : h("div", { class: "table-wrap" }, h("table", { class: "table" },
      h("thead", {}, h("tr", {}, h("th", {}, "Quando"), h("th", {}, "Loja"), h("th", {}, "Tipo"), h("th", {}, "Detalhe"))),
      h("tbody", {}, events.slice(0, limit).map((event) => h("tr", {},
        h("td", { class: "nowrap muted" }, dateTime(event.at)),
        h("td", { class: "nowrap" }, event.store),
        h("td", { class: "nowrap" }, pill(event.kind, event.tone)),
        h("td", { class: "mono small" }, event.detail)))))),
    events.length > limit ? h("div", { class: "center" }, button(`Mostrar as ${events.length}`, { kind: "small", onClick: () => render(Infinity) })) : null]);
  render(25);
  return h("section", { class: "card section-card", "aria-labelledby": "history-h" },
    h("div", { class: "card-head-row" }, h("h2", { id: "history-h" }, "Histórico"), h("span", { class: "muted small" }, `${events.length} ${events.length === 1 ? "entrada" : "entradas"}`)),
    box);
}

/**
 * Step chart of one product in every store: one line per store in the store's colour; sold-out stretches are the
 * same colour, dashed and lighter. Stores are named at the line ends, in the legend (hover a store to highlight it)
 * and in the tooltip, which lists every store at the pointer's time — so identity never rests on colour alone.
 */
function compareChart(group, colors, size = {}) {
  const series = group.listings
    .map((listing) => ({ listing, color: colors.get(listing.storeName), points: listing.points.map((point) => ({ ...point, time: new Date(point.at).getTime() })) }))
    .filter((item) => item.points.length > 0);
  if (series.length === 0) return emptyState("chart", "Sem pontos.");

  const width = size.width || 640, height = size.height || 260, left = 52, right = 110, top = 14, bottom = 28;
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
  grid.push(svg("line", { class: "history-grid", x1: left, x2: width - right, y1: height - bottom, y2: height - bottom }),
    svg("text", { class: "history-axis", x: left, y: height - 8 }, dayMonthFormat.format(new Date(start))),
    svg("text", { class: "history-axis", x: width - right, y: height - 8, "text-anchor": "end" }, dayMonthFormat.format(new Date(end))));

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
        type: "button",
        onmouseenter: () => focus(item.listing.storeName), onmouseleave: () => focus(null),
        onfocus: () => focus(item.listing.storeName), onblur: () => focus(null),
      }, svg("svg", { viewBox: "0 0 20 8", class: "key-line" }, svg("path", { class: "history-line store-line", style: `stroke:${item.color}`, d: "M1,4 H19" })), item.listing.storeName)),
    h("span", { class: "legend-end" },
      svg("svg", { viewBox: "0 0 20 8", class: "key-line" }, svg("path", { class: "history-line store-line sold-out", style: "stroke:var(--text-2)", d: "M1,4 H19" })), "esgotado")));
}

async function assignListing(historyKey, groupKey, reopenKey) {
  try {
    await post("/compare/assign", { historyKey, groupKey });
    toast("Correção guardada", "success");
  } catch (error) {
    toast(errorText(error), "error");
    return;
  }
  await loadCatalog();
  if (reopenKey && (state.compare || []).some((group) => group.key === reopenKey)) {
    await loadFicha(reopenKey, { quiet: true });
    if (state.route.view !== "ficha" || state.route.param !== reopenKey) go("ficha", reopenKey);
  } else {
    go("produtos");
  }
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
