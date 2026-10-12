"use strict";

/* ==========================================================================
   Restocks — when each store usually gets stock back, learnt from the price history (every sold out → in stock
   change; products back within 3 hours of each other are one delivery). Shown on the Stores page as a weekday ×
   hour heatmap, on each store card, and on the product page next to the stores that are sold out.
   ========================================================================== */

state.restocks = null;          // StoreRestockDTO[] from /pricehistory/restocks
state.restocksAt = 0;
state.restockStore = "";        // heatmap: "" = every store

const WEEKDAYS_LONG = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
const RESTOCKS_STALE_MS = 10 * 60 * 1000;

/** Loads the patterns (again when older than 10 minutes) and redraws the page that asked. */
async function loadRestocks() {
  if (state.restocksLoading || (state.restocks && Date.now() - state.restocksAt < RESTOCKS_STALE_MS)) return;
  state.restocksLoading = true;
  try {
    state.restocks = await api("/pricehistory/restocks");
    state.restocksAt = Date.now();
  } catch {
    state.restocks = state.restocks || [];
  } finally {
    state.restocksLoading = false;
  }
  if (["stores", "product"].includes(state.route.view)) renderView(true);
}

const restockOf = (storeName) => (state.restocks || []).find((pattern) => sameName(pattern.storeName, storeName)) || null;
const hourText = (hour) => `${String(hour % 24).padStart(2, "0")}:00`;

/** "Usually Tue & Thu · 10:00–12:00", or null when the store has no clear pattern yet. */
function restockText(pattern) {
  if (!pattern?.confident) return null;
  const days = (pattern.usualWeekdays || []).map((day) => WEEKDAYS[day]).join(" & ");
  const hours = pattern.usualHour === null || pattern.usualHour === undefined ? null : `${hourText(pattern.usualHour)}–${hourText(pattern.usualHour + 2)}`;
  if (!days && !hours) return "No fixed day or time";
  return `Usually ${[days, hours].filter(Boolean).join(" · ")}`;
}

/** The store card line: the pattern, or how many restocks were seen so far. */
function storeRestockText(store) {
  const pattern = restockOf(store.name);
  if (!state.restocks) return h("span", { class: "muted" }, "…");
  if (!pattern) return h("span", { class: "muted" }, "none seen yet");
  const text = restockText(pattern);
  const count = `${pattern.restocks} in ${pattern.days} ${pattern.days === 1 ? "day" : "days"}`;
  return text ? [text, h("span", { class: "muted small" }, ` · ${count}`)] : h("span", { class: "muted" }, `${count} — too few for a pattern`);
}

/** Every sold out → in stock change of one listing, newest first. */
function listingRestocks(listing) {
  const times = [];
  listing.points.forEach((point, index) => {
    if (index > 0 && point.inStock && !listing.points[index - 1].inStock) times.push(point.at);
  });
  return times.reverse();
}

/** The weekday × hour heatmap of the Stores page, with one line per store underneath (the table view). */
function restockCard() {
  if (!state.restocks) {
    loadRestocks();
    return h("section", { class: "card section-card" }, h("h2", {}, "When stores restock"), h("p", { class: "muted small" }, "Reading the history…"));
  }
  const patterns = state.restocks;
  const card = h("section", { class: "card section-card restock-card", "aria-labelledby": "restock-h" });
  if (patterns.length === 0) {
    return append(card, [h("h2", { id: "restock-h" }, "When stores restock"),
      h("p", { class: "muted small" }, "No restock seen yet. Every time a product goes from sold out to in stock, the bot notes the day and hour — after a few, each store's pattern shows up here.")]);
  }

  const chosen = patterns.find((pattern) => pattern.storeName === state.restockStore) || null;
  const grid = chosen ? chosen.byWeekdayHour : WEEKDAYS.map((_, day) => Array.from({ length: 24 }, (__, hour) => patterns.reduce((sum, pattern) => sum + (pattern.byWeekdayHour[day]?.[hour] || 0), 0)));
  const total = grid.flat().reduce((sum, value) => sum + value, 0);
  const max = Math.max(1, ...grid.flat());
  const level = (value) => (value === 0 ? 0 : Math.min(4, Math.ceil((value / max) * 4)));
  const readout = h("div", { class: "heat-readout muted small", "aria-live": "polite" }, "Point at a square to see the restocks in that hour.");
  const say = (day, hour, value) => {
    readout.textContent = `${WEEKDAYS_LONG[day]} ${hourText(hour)}–${hourText(hour + 1)} · ${value} ${value === 1 ? "restock" : "restocks"}`;
  };

  const heat = h("div", { class: "heatmap", role: "grid", "aria-label": `Restocks by weekday and hour${chosen ? ` at ${chosen.storeName}` : " in every store"}` },
    h("div", { class: "heat-row heat-hours", role: "row", "aria-hidden": "true" }, h("span"),
      Array.from({ length: 24 }, (_, hour) => h("span", { class: "heat-hour" }, hour % 6 === 0 ? String(hour).padStart(2, "0") : ""))),
    grid.map((hours, day) => h("div", { class: "heat-row", role: "row" },
      h("span", { class: "heat-day", role: "rowheader" }, WEEKDAYS[day]),
      hours.map((value, hour) => h("span", {
        class: `heat-cell l${level(value)}`, role: "gridcell", tabindex: value ? 0 : -1,
        title: `${WEEKDAYS[day]} ${hourText(hour)} · ${value}`, "aria-label": `${WEEKDAYS_LONG[day]} ${hourText(hour)}: ${value}`,
        onmouseenter: () => say(day, hour, value), onfocus: () => say(day, hour, value),
      })))));

  const scale = h("div", { class: "heat-scale muted small", "aria-hidden": "true" }, "fewer",
    [0, 1, 2, 3, 4].map((step) => h("span", { class: `heat-cell l${step}` })), "more");

  const lines = h("ul", { class: "restock-list" }, patterns.map((pattern) => h("li", {},
    h("button", {
      type: "button", class: `text-link ${pattern.storeName === state.restockStore ? "on" : ""}`,
      onclick: () => { state.restockStore = pattern.storeName === state.restockStore ? "" : pattern.storeName; renderView(true); },
    }, pattern.storeName),
    h("span", {}, restockText(pattern) || h("span", { class: "muted" }, "too few for a pattern")),
    h("span", { class: "muted small nowrap" }, `${pattern.restocks} ${pattern.restocks === 1 ? "restock" : "restocks"} · last ${relative(pattern.lastRestockAt)}`))));

  return append(card, [
    h("div", { class: "card-head-row wrap" },
      h("div", {}, h("h2", { id: "restock-h" }, "When stores restock"),
        h("p", { class: "muted small" }, `${total} ${total === 1 ? "restock" : "restocks"} ${chosen ? `at ${chosen.storeName}` : "across every store"}, Portugal time. Products back within 3 hours count once.`)),
      selectPill("Store", state.restockStore, [["", "Every store"], ...patterns.map((pattern) => [pattern.storeName, pattern.storeName])],
        (value) => { state.restockStore = value; renderView(true); })),
    h("div", { class: "restock-body" },
      h("div", { class: "heat-side" }, h("div", { class: "heat-wrap" }, heat), h("div", { class: "heat-foot" }, readout, scale)),
      lines),
  ]);
}

/** Product page: when this product came back in each store, and when those stores usually restock. */
function fichaRestocks(group) {
  if (!state.restocks) loadRestocks();
  const rows = [...new Set(group.listings.map((listing) => listing.storeName))].map((storeName) => {
    const listings = group.listings.filter((listing) => listing.storeName === storeName);
    const times = listings.flatMap(listingRestocks).sort((a, b) => new Date(b) - new Date(a));
    const inStock = listings.some((listing) => listing.currentInStock);
    return { storeName, times, inStock, pattern: restockOf(storeName) };
  }).filter((row) => row.times.length > 0 || restockText(row.pattern))
    .sort((a, b) => (a.inStock - b.inStock) || ((b.times[0] ? new Date(b.times[0]) : 0) - (a.times[0] ? new Date(a.times[0]) : 0)));
  if (rows.length === 0) return null;

  return h("section", { class: "card section-card", "aria-labelledby": "restocks-h" },
    h("div", { class: "card-head-row wrap" },
      h("h2", { id: "restocks-h", class: "small-h" }, "Restocks"),
      h("a", { class: "muted small", href: routeHash("stores") }, "Every store's pattern")),
    h("ul", { class: "restock-list" }, rows.map((row) => h("li", {},
      h("strong", {}, row.storeName),
      h("span", {}, row.times.length
        ? `This product came back ${row.times.length === 1 ? "once" : `${row.times.length} times`} · last ${relative(row.times[0])}`
        : h("span", { class: "muted" }, "Not back in stock here yet")),
      h("span", { class: "muted small" }, restockText(row.pattern) ? `Store: ${restockText(row.pattern).replace("Usually", "usually")}` : (row.inStock ? "in stock now" : ""))))));
}
