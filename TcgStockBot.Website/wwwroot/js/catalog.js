"use strict";

/* ==========================================================================
   Products — the catalogue of everything the bot found: pick a game, then a format, and every set's product shows
   its lowest price. The product page puts the price in every store first (cheapest on top), then the other formats
   of the set, the price chart, the history and manual regrouping.
   A product is a Compare group: game + set + product kind, unless corrected by hand.
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
const shortDate = (iso) => (iso ? new Date(iso).toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "2-digit" }) : "—");

async function loadCatalog() {
  try {
    state.compare = await api("/compare/list");
  } catch (error) {
    state.compare = state.compare || [];
    toast(errorText(error), "error");
  }
  if (state.route.view === "products" || state.route.view === "releases" || (state.route.view === "product" && state.ficha)) renderView(true);
}

async function loadFicha(key, { quiet = false } = {}) {
  try {
    state.ficha = await api(`/compare/get?identifier=${encodeURIComponent(key)}`);
  } catch (error) {
    state.ficha = { key, missing: true, message: errorText(error) };
    if (!quiet) toast(errorText(error), "error");
  }
  if (state.route.view === "product" && state.route.param === key) renderView(true);
}

/** Cheapest in stock, runner-up, lowest ever and most recent drop of one product. */
function compareSummary(group) {
  const listings = group.listings;
  const inStock = listings.filter((listing) => listing.currentInStock && listing.currentPrice !== null).sort((a, b) => a.currentPrice - b.currentPrice);
  const lowestEver = listings.filter((listing) => listing.lowestPrice !== null)
    .reduce((best, listing) => (!best || listing.lowestPrice < best.lowestPrice ? listing : best), null);
  const lastDrop = listings.filter((listing) => listing.lastDropAt).sort((a, b) => new Date(b.lastDropAt) - new Date(a.lastDropAt))[0] || null;
  const inStockCount = new Set(inStock.map((listing) => listing.storeName)).size;   // stores, not listings
  return { best: inStock[0] || null, second: inStock[1] || null, inStockCount, lowestEver, lastDrop };
}

/** What a catalogue card shows: "from", stock, movement over 30 days, picture, dates. */
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
  if (move === null || move === undefined || !Number.isFinite(move)) return h("span", { class: "move-pill flat", title: "No price from 30 days ago" }, long ? "no data" : "—");
  const rounded = Math.round(move);
  if (rounded === 0) return h("span", { class: "move-pill flat", title: "The lowest price didn't change in 30 days" }, "=");
  return h("span", { class: `move-pill ${rounded < 0 ? "down" : "up"}`, title: "Lowest price now vs 30 days ago" }, `${rounded < 0 ? "↓" : "↑"} ${Math.abs(rounded)}%`);
}

/**
 * The formats of the Products page: the product kinds grouped the way people shop. Every kind code is in exactly
 * one format; anything new falls into "Other".
 */
const FORMATS = [
  ["etb", "Elite Trainer Box", ["etb", "pokemon-center-etb", "etb-booster-box-combo"]],
  ["display", "Booster Display", ["booster-display", "half-display"]],
  ["collector", "Collector", ["collector-booster-display", "collector-booster"]],
  ["bundle", "Bundles", ["booster-bundle", "bundle", "gift-bundle"]],
  ["booster", "Boosters", ["booster-pack", "sleeved-booster"]],
  ["blister", "Blisters", ["checklane-blister", "blister-2", "blister-3"]],
  ["collection", "Collections", ["ultra-premium", "super-premium", "premium-collection", "collection", "ex-box", "binder-collection", "poster-collection", "build-battle"]],
  ["tin", "Tins", ["tin", "mini-tin"]],
  ["deck", "Decks", ["deck", "commander-deck"]],
  ["prerelease", "Prerelease", ["prerelease-pack"]],
  ["other", "Other", ["other"]],
];
const formatOf = (kind) => (FORMATS.find(([, , kinds]) => kinds.includes(kind)) || FORMATS[FORMATS.length - 1])[0];
const formatLabel = (id) => FORMATS.find(([key]) => key === id)?.[1] || "All formats";
const formatIndex = (kind) => FORMATS.findIndex(([key]) => key === formatOf(kind));

const CATALOG_SORTS = { newest: "Newest set", cheap: "Cheapest", stores: "Most stores in stock", drop: "Biggest drop (30 days)", name: "Name" };

/** The set's release date as a time, or null when the set is not known. */
const releaseTime = (group) => (group.releaseDate ? new Date(`${group.releaseDate}T00:00:00`).getTime() : null);

/** "New set" for a set out in the last 45 days, "Out 23 Oct" for one still to come, or null. */
function setBadge(group) {
  const time = releaseTime(group);
  if (time === null) return null;
  if (time > Date.now()) return `Out ${dayMonthFormat.format(new Date(time))}`;
  return Date.now() - time < 45 * DAY ? "New set" : null;
}

/** "Released 17 Jul 2026" / "Out 23 Oct 2026", or null. */
function releaseText(group) {
  const time = releaseTime(group);
  if (time === null) return null;
  return `${time > Date.now() ? "Out" : "Released"} ${shortDate(new Date(time).toISOString())}`;
}

/** What a product is called on its card and page: the set ("Pitch Black"), or the store title of a split listing. */
const productHeading = (group) => (group.key.startsWith("solo|") || !group.setName ? group.title : group.setName);

/** "Elite Trainer Box", "Checklane Blister – Luxray". */
const productKindText = (group) => [group.kindLabel || kindLabel(group.kind), group.variantLabel].filter(Boolean).join(" – ");

const COUNTRY_CODES = { Portugal: "PT", Spain: "ES", Europe: "EU" };

/** "PT" / "ES" next to a store name, from the store's area. */
function countryTag(storeName) {
  const store = (state.stores || []).find((item) => item.name === storeName);
  const country = store ? store.country || "Portugal" : null;
  return country && COUNTRY_CODES[country] ? h("span", { class: "country-tag", title: COUNTRIES[country]?.[0] }, COUNTRY_CODES[country]) : null;
}

/** The game tabs of the Products page: one card per game, with how many products and how many in stock. */
function gameCards(items) {
  const byGame = new Map();
  for (const item of items) {
    const key = gameKey(item.group.game);
    const entry = byGame.get(key) || { total: 0, stock: 0 };
    entry.total++;
    if (!item.soldOut) entry.stock++;
    byGame.set(key, entry);
  }
  const games = Object.keys(GAMES).filter((game) => MAIN_GAMES.includes(game) || byGame.has(game) || state.game === game);
  const card = (key, label, { total = 0, stock = 0 } = {}) => h("button", {
    type: "button", role: "tab", class: `game-card ${key === "all" ? "" : gameClass(key)} ${state.game === key ? "on" : ""}`,
    "aria-selected": state.game === key ? "true" : "false",
    onclick: () => { state.game = key; writePreference("game", key); state.catalogShown = 48; renderView(true); },
    title: total ? `${total} ${total === 1 ? "product" : "products"} · ${stock} in stock` : "Nothing found yet",
  },
    h("span", { class: key === "all" ? "game-dot every" : "game-dot", "aria-hidden": "true" }),
    label,
    h("span", { class: "game-card-count" }, total));
  const all = { total: items.length, stock: items.filter((item) => !item.soldOut).length };
  return h("div", { class: "game-cards", role: "tablist", "aria-label": "Game" },
    card("all", "All", all), games.map((game) => card(game, GAMES[game], byGame.get(game))));
}

RENDERERS.products = function viewCatalog() {
  if (state.compare === null) {
    return [pageHead("Products", "Loading the catalogue…"), h("div", { class: "card soft" }, h("p", { class: "muted pad" }, "Loading…"))];
  }

  const all = state.compare.map(catalogItem);
  const inTab = all.filter((item) => inGameTab(item.group.game));
  const formatCounts = new Map();
  for (const item of inTab) formatCounts.set(formatOf(item.group.kind), (formatCounts.get(formatOf(item.group.kind)) || 0) + 1);
  if (state.catalogKind !== "all" && !formatCounts.has(state.catalogKind)) state.catalogKind = "all";
  const storeNames = [...new Set(inTab.flatMap((item) => item.group.listings.map((listing) => listing.storeName)))].sort((a, b) => a.localeCompare(b, "en"));
  if (state.catalogStore && !storeNames.includes(state.catalogStore)) state.catalogStore = "";
  if (!CATALOG_SORTS[state.catalogSort]) state.catalogSort = "newest";
  const onlySet = state.catalogSet ? state.compare.find((group) => group.setId === state.catalogSet) : null;
  if (state.catalogSet && !onlySet) state.catalogSet = null;

  const grid = h("div", { class: "catalog-grid" });
  const counter = h("p", { class: "catalog-count" });
  const more = h("div", { class: "center" });

  const renderGrid = () => {
    const visible = inTab.filter((item) => (state.catalogKind === "all" || formatOf(item.group.kind) === state.catalogKind)
      && (!state.catalogSet || item.group.setId === state.catalogSet)
      && (!state.catalogStore || item.group.listings.some((listing) => listing.storeName === state.catalogStore && (!state.catalogStock || listing.currentInStock)))
      && (!state.catalogStock || !item.soldOut)
      && matchesWords(state.catalogQuery, item.group.title, item.group.setName, item.group.setCode || "", item.group.listings.map((listing) => `${listing.storeName} ${listing.title}`).join(" ")));
    const byName = (a, b) => a.group.title.localeCompare(b.group.title, "en");
    const sorters = {
      newest: (a, b) => ((releaseTime(b.group) ?? -1) - (releaseTime(a.group) ?? -1)) || (formatIndex(a.group.kind) - formatIndex(b.group.kind)) || byName(a, b),
      stores: (a, b) => (b.summary.inStockCount - a.summary.inStockCount) || ((a.from ?? Infinity) - (b.from ?? Infinity)),
      cheap: (a, b) => (a.soldOut - b.soldOut) || ((a.from ?? Infinity) - (b.from ?? Infinity)),
      drop: (a, b) => (a.move ?? Infinity) - (b.move ?? Infinity),
      name: byName,
    };
    visible.sort(sorters[state.catalogSort] || sorters.newest);
    counter.replaceChildren(h("strong", {}, `${visible.length} ${visible.length === 1 ? "product" : "products"}`),
      h("span", { class: "muted" }, ` · ${CATALOG_SORTS[state.catalogSort].toLowerCase()} first`));
    grid.replaceChildren(...(visible.length
      ? visible.slice(0, state.catalogShown).map(catalogCard)
      : [h("div", { class: "card soft span-all" }, emptyState("grid", all.length === 0 ? "The catalogue is still empty." : "No products with these filters.",
        all.length === 0 ? "After the first cycle, everything the bot finds for the products you watch shows up here, grouped by set and format." : null,
        all.length === 0 ? linkButton("Watch a product", routeHash("watch"), { iconName: "plus", kind: "primary" }) : null))]));
    more.replaceChildren(visible.length > state.catalogShown
      ? button(`Show ${Math.min(48, visible.length - state.catalogShown)} more`, { onClick: () => { state.catalogShown += 48; renderGrid(); } })
      : "");
  };
  renderGrid();
  const rerender = () => { state.catalogShown = 48; renderGrid(); };

  const formatChip = (id, label, count) => {
    const on = state.catalogKind === id;
    return h("button", {
      type: "button", class: `chip ${on ? "on" : ""}`, "aria-pressed": on ? "true" : "false",
      onclick: () => { state.catalogKind = id; state.catalogShown = 48; renderView(true); },
    }, label, h("span", { class: "chip-count" }, count));
  };

  return [
    h("header", { class: "page-head" },
      h("div", { class: "page-title" },
        h("span", { class: "page-eyebrow" }, "Products"),
        h("h1", {}, "Lowest prices, every store."),
        h("p", {}, `${enabledStores().length} stores in Portugal and Spain, checked every few minutes. Newest sets first.`)),
      h("div", { class: "page-actions" },
        searchField(state.catalogQuery, "Search sets, codes, stores", (value) => { state.catalogQuery = value; rerender(); }, "catalog-search", "Search products"))),
    gameCards(all),
    h("div", { class: "format-chips", role: "group", "aria-label": "Format" },
      formatChip("all", "All formats", inTab.length),
      FORMATS.filter(([id]) => formatCounts.has(id)).map(([id, label]) => formatChip(id, label, formatCounts.get(id)))),
    h("div", { class: "catalog-toolbar" },
      h("div", { class: "catalog-count-row" }, counter,
        onlySet ? h("button", {
          type: "button", class: "chip small-chip on", title: "Show every set",
          onclick: () => { state.catalogSet = null; renderView(true); },
        }, `Set: ${onlySet.setName}`, icon("close")) : null),
      h("div", { class: "toolbar wrap" },
        storeNames.length > 1 ? selectPill("Store", state.catalogStore, [["", "All"], ...storeNames.map((name) => [name, name])], (value) => { state.catalogStore = value; rerender(); }) : null,
        labelledSwitch(state.catalogStock, "In stock only", (checked) => { state.catalogStock = checked; writePreference("catalogStock", checked); rerender(); }),
        selectPill("Sort", state.catalogSort, Object.entries(CATALOG_SORTS), (value) => { state.catalogSort = value; writePreference("catalogSort", value); rerender(); }))),
    grid,
    more,
  ];
};

/** The pack drawn when a product has no photo: its kind and its set. */
const productPack = (group) => ({ kind: group.kindLabel || kindLabel(group.kind), name: productHeading(group) });

/** One bar per store (up to 12), the ones with stock lit. */
function stockMeter(inStock, stores) {
  const cells = Math.min(Math.max(stores, 1), 12);
  const lit = stores ? Math.round((inStock / stores) * cells) : 0;
  return h("span", { class: "stock-meter", "aria-hidden": "true" },
    Array.from({ length: cells }, (_, index) => h("span", { class: index < lit ? "on" : "" })));
}

function catalogCard(item) {
  const { group, summary } = item;
  const badge = item.soldOut ? "Sold out" : setBadge(group);
  return h("a", { class: `catalog-card ${item.soldOut ? "sold-out" : ""}`, href: routeHash("product", group.key) },
    h("div", { class: "catalog-art" },
      productArt({ imageUrl: item.image, game: group.game, title: group.title, query: group.kindLabel, size: "hero", pack: productPack(group) }),
      group.setCode ? h("span", { class: "set-code-badge" }, group.setCode) : null,
      badge ? h("span", { class: `set-badge ${item.soldOut ? "out" : ""}` }, badge) : null),
    h("div", { class: "catalog-body" },
      h("div", { class: "catalog-heading" },
        h("h3", { class: "catalog-title", title: group.title }, productHeading(group)),
        h("span", { class: "catalog-kind" }, state.game === "all" ? [h("span", { class: `game-dot ${gameClass(group.game)}`, "aria-hidden": "true" }), " "] : null, productKindText(group))),
      h("div", { class: "catalog-price" },
        h("span", { class: "catalog-from" },
          h("span", { class: "muted small" }, item.soldOut ? "last" : "from"),
          h("strong", { class: `price-l mono ${item.soldOut ? "struck" : ""}` }, money(item.from))),
        item.soldOut ? h("span", { class: "dim small" }, "no stock") : msrpPill(summary.best?.currentPrice, group.msrp)),
      h("div", { class: "catalog-foot" },
        stockMeter(summary.inStockCount, item.stores),
        h("span", { class: "catalog-stock" }, item.soldOut
          ? `sold out · ${item.stores} ${item.stores === 1 ? "store" : "stores"}`
          : `${summary.inStockCount} of ${item.stores} ${item.stores === 1 ? "store" : "stores"}`))));
}

/* ----- Ficha (product page) ----- */

const PERIODS = [["7", "7 d"], ["30", "30 d"], ["90", "90 d"], ["180", "180 d"], ["365", "1 year"], ["all", "All"]];

/** Cardmarket search for the product (only games Cardmarket lists). */
const CARDMARKET_GAMES = { Pokemon: "Pokemon", OnePiece: "OnePiece", Lorcana: "Lorcana", Magic: "Magic", Riftbound: "Riftbound" };
function cardmarketUrl(group) {
  const slug = CARDMARKET_GAMES[gameKey(group.game)];
  if (!slug) return null;
  const query = `${group.setName || ""} ${group.kindLabel || ""}`.trim() || group.title.replace(" · ", " ");
  return `https://www.cardmarket.com/en/${slug}/Products/Search?searchString=${encodeURIComponent(query)}`;
}

/** The other products of the same set (same set id, or the same set name and game for sets the bot doesn't know). */
function sameSetProducts(group) {
  if (group.key.startsWith("solo|") || !group.setName) return [];
  return (state.compare || [])
    .filter((other) => other.key !== group.key && !other.key.startsWith("solo|")
      && (group.setId ? other.setId === group.setId : !other.setId && other.setName === group.setName && gameKey(other.game) === gameKey(group.game)))
    .map(catalogItem)
    .sort((a, b) => (formatIndex(a.group.kind) - formatIndex(b.group.kind)) || a.group.title.localeCompare(b.group.title, "en"));
}

RENDERERS.product = function viewFicha() {
  const key = state.route.param;
  const group = state.ficha && state.ficha.key === key ? state.ficha : null;
  const back = h("a", {
    class: "back-link", href: routeHash("products"),
    onclick: () => {
      if (!group) return;
      state.catalogKind = formatOf(group.kind);
      if (state.game !== "all") { state.game = gameKey(group.game); writePreference("game", state.game); }
    },
  }, icon("back"), group && !group.missing ? `${GAMES[gameKey(group.game)]} · ${formatLabel(formatOf(group.kind))}` : "Products");
  if (!group) {
    loadFicha(key);
    return [back, h("div", { class: "card soft" }, h("p", { class: "muted pad" }, "Loading the product…"))];
  }
  if (group.missing) {
    return [back, h("div", { class: "card soft" }, emptyState("grid", "This product is no longer in the catalogue.", group.message || "It may have been split or merged into another one."))];
  }

  const item = catalogItem(group);
  const { summary } = item;
  const colors = storeColors(group.listings.map((listing) => listing.storeName));
  const task = group.stockTaskId ? findTask(group.stockTaskId) : null;
  const cardmarket = cardmarketUrl(group);
  const best = summary.best;
  const lastSeenListing = [...group.listings].sort((a, b) => new Date(b.lastSeen) - new Date(a.lastSeen))[0];
  const limit = task ? limitFor(task, group.kind) : null;

  const toGo = best && limit && best.currentPrice > limit ? best.currentPrice - limit : null;

  return [
    back,
    h("div", { class: "ficha-layout" },
      h("aside", { class: "ficha-aside" },
        h("div", { class: "ficha-art" }, productArt({ imageUrl: item.image, game: group.game, title: group.title, query: group.kindLabel, size: "hero", pack: productPack(group) })),
        fichaFacts(group, item, lastSeenListing)),
      h("section", { class: "ficha-main" },
        h("div", { class: "catalog-meta" }, gamePill(group.game),
          group.setCode ? h("span", { class: "set-code mono" }, group.setCode) : null,
          releaseText(group) ? h("span", { class: "muted small" }, releaseText(group)) : null,
          group.key.startsWith("solo|") ? pill("split by hand", "plain") : null),
        h("div", { class: "ficha-heading" },
          h("h1", { class: "ficha-title" }, productHeading(group)),
          h("p", { class: "ficha-sub" }, productKindText(group))),
        h("div", { class: "ficha-best" },
          h("div", { class: "ficha-best-price" },
            h("span", { class: "ficha-best-label" }, best
              ? ["Best price now · ", h("strong", {}, best.storeName), countryTag(best.storeName) ? [" ", countryTag(best.storeName)] : null]
              : "Not in stock in any store"),
            h("div", { class: "ficha-price-row" },
              h("span", { class: "price-xl" }, money(item.from)),
              best ? msrpPill(best.currentPrice, group.msrp) : h("span", { class: "ficha-best-label" }, "last price"),
              group.msrp ? h("span", { class: "ficha-best-label" }, `MSRP ${money(group.msrp)}`) : null),
            task ? h("span", { class: "ficha-best-label" },
              limit ? (toGo ? `Your alert fires at ${money(limit)} or less · ${money(toGo)} to go` : `Your alert fires at ${money(limit)} or less`) : `Alert rule: ${priceRuleText(task)}`,
              " · ", h("a", { href: routeHash("watched", task.id) }, "change"))
              : h("span", { class: "ficha-best-label" }, "Not watched any more. ", h("a", { href: routeHash("watch") }, "Watch it"))),
          h("div", { class: "ficha-actions" },
            best && safeHref(best.url) ? linkButton(`Open in ${best.storeName}`, best.url, { kind: "invert", external: true, trailingIcon: "external" }) : null,
            cardmarket ? linkButton("Cardmarket", cardmarket, { kind: "on-ink", external: true, trailingIcon: "external" }) : null)),
        fichaStores(group, summary),
        fichaSameSet(group))),

    fichaChart(group, colors),
    fichaHistory(group),
    h("p", { class: "ficha-fix muted small" }, "Is a listing here not this product, or is a store missing? Use “Split” on a store, or ",
      h("button", { type: "button", class: "text-link small", onclick: () => joinListing(group) }, "merge a listing"),
      ". Corrections are kept for the next checks."),
  ];
};

/** Every store with this product: in stock first, cheapest first; the sold-out ones folded away. */
function fichaStores(group, summary) {
  const byPrice = (a, b) => (a.currentPrice ?? Infinity) - (b.currentPrice ?? Infinity);
  const inStock = group.listings.filter((listing) => listing.currentInStock && listing.currentPrice !== null).sort(byPrice);
  const soldOut = group.listings.filter((listing) => !inStock.includes(listing)).sort(byPrice);
  // One price scale for the bars: from a bit under the cheapest (or the MSRP) to the dearest.
  const prices = group.listings.map((listing) => listing.currentPrice).filter((price) => price !== null);
  const low = Math.min(...prices, group.msrp ?? Infinity) * 0.9;
  const high = Math.max(...prices, group.msrp ?? 0) * 1.02;
  const scale = { at: (price) => (high > low ? Math.min(100, Math.max(3, ((price - low) / (high - low)) * 100)) : 50), msrp: group.msrp };
  const rows = [...inStock.map((listing, index) => storeRow(group, listing, index + 1, listing === summary.best, scale)),
    ...(state.fichaShowSoldOut || inStock.length === 0 ? soldOut.map((listing) => storeRow(group, listing, null, false, scale)) : [])];
  const note = [inStock.length ? `${inStock.length} in stock` : "none in stock", soldOut.length ? `${soldOut.length} sold out` : null, "cheapest first"].filter(Boolean).join(" · ");

  return h("section", { class: "card price-card", "aria-labelledby": "stores-table-h" },
    h("div", { class: "price-card-head" },
      h("h2", { id: "stores-table-h" }, "Every store, cheapest first"),
      h("span", { class: "muted small price-card-note" }, group.msrp ? h("span", { class: "msrp-key", "aria-hidden": "true" }) : null, group.msrp ? `MSRP ${money(group.msrp)} · ` : "", note.replace(" · cheapest first", ""))),
    h("div", { class: "price-head", "aria-hidden": "true" }, h("span", {}, "#"), h("span", {}, "Store"), h("span", {}, ""), h("span", { class: "num" }, "Price"), h("span", {}, "vs MSRP"), h("span", {})),
    h("ol", { class: "price-rows" }, rows),
    soldOut.length && inStock.length
      ? h("button", {
        type: "button", class: "price-more", "aria-expanded": state.fichaShowSoldOut ? "true" : "false",
        onclick: () => { state.fichaShowSoldOut = !state.fichaShowSoldOut; renderView(true); },
      }, state.fichaShowSoldOut ? `Hide the ${soldOut.length} sold-out ${soldOut.length === 1 ? "store" : "stores"}` : `Show ${soldOut.length} sold-out ${soldOut.length === 1 ? "store" : "stores"}`,
      icon("down", state.fichaShowSoldOut ? "flip" : ""))
      : null);
}

function storeRow(group, listing, rank, isBest, scale) {
  const out = !listing.currentInStock;
  const bar = listing.currentPrice === null ? null : h("span", { class: "price-bar", "aria-hidden": "true" },
    h("span", { class: "price-bar-fill", style: `width:${scale.at(listing.currentPrice)}%` }),
    scale.msrp ? h("span", { class: "price-bar-msrp", style: `left:${scale.at(scale.msrp)}%` }) : null);
  return h("li", { class: `price-row ${isBest ? "best" : ""} ${out ? "out" : ""}` },
    h("span", { class: "price-rank mono" }, rank ?? "–"),
    h("div", { class: "price-store" },
      h("div", { class: "price-store-name" }, h("strong", {}, listing.storeName), countryTag(listing.storeName),
        isBest ? h("span", { class: "best-badge" }, "Cheapest") : null,
        listing.isManual ? pill("manual", "plain", "In this product because of a manual correction") : null),
      h("div", { class: "price-store-meta" },
        h("span", { class: "listing-title", title: listing.title }, listing.title),
        h("span", { class: "nowrap", title: dateTime(listing.lastSeen) }, out ? `sold out · seen ${relative(listing.lastSeen)}` : `checked ${relative(listing.lastSeen)}`),
        h("button", { type: "button", class: "text-link small", onclick: () => separateListing(group, listing) }, "Split"),
        listing.isManual ? h("button", { type: "button", class: "text-link small", onclick: () => assignListing(listing.historyKey, "", group.key) }, "Back to automatic") : null)),
    bar || h("span", { class: "price-bar", style: "visibility:hidden" }),
    h("strong", { class: `price-value mono ${out ? "struck" : ""}` }, money(listing.currentPrice)),
    h("span", { class: "price-delta" }, out ? h("span", { class: "muted small" }, `low ${money(listing.lowestPrice)}`) : msrpPill(listing.currentPrice, group.msrp) || h("span", { class: "dim small" }, "—")),
    h("span", { class: "price-action" }, safeHref(listing.url)
      ? linkButton(out ? "Page" : "Open", listing.url, { kind: isBest ? "primary small" : "small", external: true, trailingIcon: "external" })
      : null));
}

/** All-time low, 30-day move, MSRP and how long the bot has known the product: four tiles under the picture. */
function fichaFacts(group, item, lastSeenListing) {
  const { summary } = item;
  const fact = (label, value, note = null, tone = "") => h("div", { class: "fact" }, h("span", {}, label), h("strong", { class: `mono ${tone}` }, value), note ? h("small", {}, note) : null);
  const move = item.move === null ? null : Math.round(item.move);
  return h("div", { class: "facts", role: "group", "aria-label": "Price facts" },
    fact("MSRP", group.msrp ? money(group.msrp) : "—", group.msrp ? (MSRP_BASIS[group.msrpBasis] || MSRP_BASIS.Estimate)[0] : "no reference"),
    fact("All-time low", money(summary.lowestEver?.lowestPrice), summary.lowestEver ? `${shortStoreName(summary.lowestEver.storeName)} · ${shortDate(summary.lowestEver.lowestPriceAt)}` : null),
    fact(group.releaseDate ? "Released" : "Known since", group.releaseDate ? shortDate(`${group.releaseDate}T00:00:00`) : item.firstSeen ? shortDate(new Date(item.firstSeen).toISOString()) : "—",
      lastSeenListing ? `read ${relative(lastSeenListing.lastSeen)}` : null),
    fact("30 days", move === null ? "—" : move === 0 ? "=" : `${move < 0 ? "↓" : "↑"} ${Math.abs(move)}%`, "lowest price", move === null || move === 0 ? "" : move < 0 ? "good" : "red"));
}

/** The other formats of the same set, so you can hop from the ETB to the Booster Bundle. */
function fichaSameSet(group) {
  const others = sameSetProducts(group);
  if (others.length === 0) return null;
  return h("section", { class: "card section-card", "aria-labelledby": "same-set-h" },
    h("h2", { id: "same-set-h", class: "small-h" }, `More from ${group.setName}`),
    h("ul", { class: "same-set" }, others.map((other) => h("li", {},
      h("a", { href: routeHash("product", other.group.key) },
        h("span", { class: "same-set-kind" }, productKindText(other.group)),
        h("span", { class: "muted small nowrap" }, other.soldOut ? "sold out" : `in stock at ${other.summary.inStockCount}`),
        h("strong", { class: `mono ${other.soldOut ? "muted" : ""}` }, money(other.from)),
        icon("chevron"))))));
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
      h("h2", { id: "chart-h" }, "Price history"),
      segmented(PERIODS.map(([value, label]) => [value, label]), state.fichaPeriod, (value) => { state.fichaPeriod = value; writePreference("fichaPeriod", value); renderView(true); }, "Period")),
    stores.length > 1 ? h("div", { class: "store-toggles", role: "group", "aria-label": "Stores in the chart" }, stores.map((name) => {
      const on = !state.fichaHidden.includes(name);
      return h("button", {
        type: "button", class: `chip ${on ? "on" : ""}`, "aria-pressed": on ? "true" : "false",
        onclick: () => { state.fichaHidden = on ? [...state.fichaHidden, name] : state.fichaHidden.filter((item) => item !== name); renderView(true); },
      }, h("span", { class: "swatch", style: `background:${colors.get(name)}` }), name);
    })) : null,
    h("div", { class: "chart-stats" },
      h("div", {}, h("span", {}, "Usual price"), h("strong", { class: "mono" }, money(usual))),
      h("div", {}, h("span", {}, "Low"), h("strong", { class: "mono accent" }, money(prices[0] ?? null))),
      h("div", {}, h("span", {}, "High"), h("strong", { class: "mono" }, money(prices.at(-1) ?? null))),
      h("div", {}, h("span", {}, "Changes"), h("strong", { class: "mono" }, changes))),
    shown.listings.length ? compareChart(shown, colors, { width: 1100, height: 320 }) : emptyState("chart", "No readings in this period."));
}

/** Every change the bot saw, newest first: first reading, restock, sold out, price up or down. */
function fichaHistory(group) {
  const events = [];
  for (const listing of group.listings) {
    listing.points.forEach((point, index) => {
      const previous = listing.points[index - 1];
      const at = point.at;
      if (!previous) {
        events.push({ at, store: listing.storeName, kind: "First reading", tone: "plain", detail: `${money(point.price)} · ${point.inStock ? "in stock" : "sold out"}` });
        return;
      }
      if (previous.inStock !== point.inStock) {
        events.push({ at, store: listing.storeName, kind: point.inStock ? "Restock" : "Sold out", tone: point.inStock ? "accent" : "red", detail: money(point.price) });
      }
      if (previous.price !== point.price) {
        const down = point.price < previous.price;
        events.push({ at, store: listing.storeName, kind: down ? "Dropped" : "Went up", tone: down ? "accent" : "amber", detail: `${money(previous.price)} → ${money(point.price)}` });
      }
    });
  }
  events.sort((a, b) => new Date(b.at) - new Date(a.at));
  const box = h("div", {});
  const render = (limit) => append(box.replaceChildren() || box, [
    events.length === 0 ? h("p", { class: "muted small" }, "No readings yet.") : h("div", { class: "table-wrap" }, h("table", { class: "table" },
      h("thead", {}, h("tr", {}, h("th", {}, "When"), h("th", {}, "Store"), h("th", {}, "Type"), h("th", {}, "Detail"))),
      h("tbody", {}, events.slice(0, limit).map((event) => h("tr", {},
        h("td", { class: "nowrap muted" }, dateTime(event.at)),
        h("td", { class: "nowrap" }, event.store),
        h("td", { class: "nowrap" }, pill(event.kind, event.tone)),
        h("td", { class: "mono small" }, event.detail)))))),
    events.length > limit ? h("div", { class: "center" }, button(`Show all ${events.length}`, { kind: "small", onClick: () => render(Infinity) })) : null]);
  render(25);
  return h("details", { class: "card section-card ficha-details" },
    h("summary", {}, h("h2", { class: "small-h" }, "Every change"), h("span", { class: "muted small" }, `${events.length} ${events.length === 1 ? "entry" : "entries"}`), icon("down", "details-caret")),
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
  if (series.length === 0) return emptyState("chart", "No points.");

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
    "aria-label": `Price of ${group.title} in ${series.length} stores. The table below has every value.`,
  }, grid, lines, labels, crosshair, svg("rect", { class: "history-hit", x: left, y: top, width: width - left - right, height: height - top - bottom }));

  const showAt = (time, px) => {
    crosshair.setAttribute("x1", px); crosshair.setAttribute("x2", px); crosshair.setAttribute("visibility", "visible");
    const rows = series.map((item) => {
      const seen = item.points.filter((point) => point.time <= time);
      return seen.length ? { item, point: seen[seen.length - 1] } : null;
    }).filter(Boolean).sort((a, b) => (b.point.inStock - a.point.inStock) || (a.point.price - b.point.price));
    tooltip.replaceChildren(h("span", { class: "muted" }, dateTime(new Date(time).toISOString())), ...rows.map(({ item, point }) =>
      h("div", { class: `tip-row ${point.inStock ? "" : "sold-out"}` }, h("span", { class: "line-key", style: `background:${item.color}` }),
        h("strong", {}, money(point.price)), item.listing.storeName, point.inStock ? null : "· sold out")));
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
      svg("svg", { viewBox: "0 0 20 8", class: "key-line" }, svg("path", { class: "history-line store-line sold-out", style: "stroke:var(--text-2)", d: "M1,4 H19" })), "sold out")));
}

async function assignListing(historyKey, groupKey, reopenKey) {
  try {
    await post("/compare/assign", { historyKey, groupKey });
    toast("Correction saved", "success");
  } catch (error) {
    toast(errorText(error), "error");
    return;
  }
  await loadCatalog();
  if (reopenKey && (state.compare || []).some((group) => group.key === reopenKey)) {
    await loadFicha(reopenKey, { quiet: true });
    if (state.route.view !== "product" || state.route.param !== reopenKey) go("product", reopenKey);
  } else {
    go("products");
  }
}

async function separateListing(group, listing) {
  const others = (state.compare || []).filter((other) => other.key !== group.key && !other.key.startsWith("solo|"));
  const target = await chooseDialog({
    title: "Split this listing?",
    message: `“${listing.title}” (${listing.storeName}) no longer counts as ${group.title}.`,
    label: "Becomes",
    options: [["solo", "A product of its own"], ...others.map((other) => [other.key, other.title])],
    confirmLabel: "Split",
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
    toast("There are no other listings in the history.", "error");
    return;
  }

  const target = await chooseDialog({
    title: `Merge into ${group.title}`,
    message: "Pick the listing of another store that is this same product (the ones from the same set come first):",
    label: "Listing",
    options: candidates.map(({ item }) => [item.key, `${item.storeName} — ${item.title} · ${money(item.currentPrice)}${item.currentInStock ? " · in stock" : " · sold out"}`]),
    confirmLabel: "Merge",
  });
  if (target !== null) await assignListing(target, group.key, group.key);
}
