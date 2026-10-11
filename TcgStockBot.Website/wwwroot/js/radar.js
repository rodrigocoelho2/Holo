"use strict";

/* ==========================================================================
   Radar — the offers you can buy now: what is new (last 24 h) in big cards, everything else in a list you can
   filter by product kind and sort. Store health and activity on the side.
   ========================================================================== */

const RADAR_SORTS = { recent: "Newest", cheap: "Cheapest", msrp: "Best vs MSRP" };

RENDERERS.radar = function viewRadar() {
  const query = state.radarQuery;
  const everyOffer = currentOffers();
  const all = everyOffer.filter((offer) => inGameTab(offer.task.game) && (!query
    || matchesWords(query, offer.listing.title, offer.task.name, offer.storeName, offer.kindLabel)));
  const fresh = all.filter((offer) => offer.isNew).sort((a, b) => new Date(b.since) - new Date(a.since));
  const freshKeys = new Set(fresh.map((offer) => offer.key));
  const rest = all.filter((offer) => !freshKeys.has(offer.key));

  const last = state.monitor.lastCycle;
  const watching = state.tasks.filter((task) => task.enabled && inGameTab(task.game)).length;
  const storesUsed = enabledStores().filter((store) => store.platform !== "Manual").length;

  const searchBox = searchField(state.radarQuery, "Search offers, stores or products", (value) => { state.radarQuery = value; renderView(true); }, "radar-search");

  return [
    pageHead("Radar", "Offers in stock, in the right language and within your price rule.", [
      searchBox,
      button("Check now", { iconName: "bolt", onClick: runNow, kbd: "R", title: "Search every store now (shortcut R)" }),
      linkButton("Watch a product", routeHash("watch"), { iconName: "plus", kind: "primary" }),
    ]),

    gameTabs(everyOffer.map((offer) => offer.task.game), "offers"),
    h("div", { class: "kpis" },
      kpi("New in the last 24 h", fresh.length, fresh.length ? "accent" : ""),
      kpi("Available at your price", all.length),
      kpi("Watching", `${watching}`, "", `${watching === 1 ? "product" : "products"} in ${storesUsed} stores`, routeHash("watching")),
      kpi("Last check", last ? relative(last.finishedAt) : "—", last?.failed ? "amber" : "",
        last ? `${last.searches} searches${last.failed ? ` · ${last.failed} failed` : " · no errors"}` : "no cycles yet")),

    h("div", { class: "split" },
      h("div", { class: "split-main radar-main" },
        h("section", { class: "section", "aria-labelledby": "fresh-h" },
          sectionHead("New", { id: "fresh-h", count: fresh.length, note: "showed up or dropped in price in the last 24 hours" }),
          fresh.length
            ? h("div", { class: "offer-grid" }, fresh.map(offerCard))
            : h("div", { class: "card soft" }, emptyState("radar", "Nothing new in the last 24 hours.",
              rest.length ? "The offers still available are in the list below." : "When a store has a product you watch at your price, it shows up here and you get an alert."))),

        h("section", { class: "section", "aria-labelledby": "available-h" },
          sectionHead("Still available", { id: "available-h", count: rest.length, note: "in stock for more than a day, or without a recent alert" }),
          rest.length ? offerTable(rest) : h("p", { class: "muted small" }, all.length ? "Every offer is new." : "No offers at your price right now.")),
        state.tasks.length === 0
          ? h("div", { class: "card soft" }, emptyState("eye", "You are not watching any product yet.", null,
            linkButton("Watch your first product", routeHash("watch"), { iconName: "plus", kind: "primary" })))
          : null),
      h("aside", { class: "split-side" }, movesCard(), storesHealthCard(), activityCard())),
  ];
};

function kpi(label, value, tone = "", note = null, href = null) {
  const content = [h("span", { class: "kpi-label" }, label), h("strong", { class: `kpi-value ${tone}` }, value), note ? h("span", { class: "kpi-note" }, note) : null];
  return href ? h("a", { class: "kpi link", href }, content) : h("div", { class: "kpi" }, content);
}

/** A big card for one new offer: the product as the store shows it, the price against the MSRP, and the way in. */
function offerCard(offer) {
  const { listing, task } = offer;
  const store = findStore(offer.storeName);
  const dropped = offer.alert?.previousPrice !== null && offer.alert?.previousPrice !== undefined;
  return h("article", { class: "offer-card" },
    h("a", { class: "offer-art", href: safeHref(listing.url) || routeHash("watched", task.id), target: "_blank", rel: "noopener noreferrer", tabindex: "-1", "aria-hidden": "true" },
      productArt({ imageUrl: listing.imageUrl, game: task.game, title: listing.title, query: task.searchQuery, size: "hero", caption: true }),
      h("span", { class: `art-badge ${dropped ? "drop" : ""}` }, dropped ? "Dropped" : "New"),
      h("span", { class: "art-corner" }, h("span", { class: "kind-chip" }, offer.kindLabel))),
    h("div", { class: "offer-body" },
      h("a", { class: "offer-set", href: routeHash("watched", task.id) }, task.name),
      h("h3", { class: "offer-title", title: listing.title }, listing.title),
      h("div", { class: "offer-store" }, storeAvatar(offer.storeName, "tiny"), offer.storeName,
        store ? h("span", { class: "muted" }, ` · ${relative(offer.since)}`) : null)),
    h("div", { class: "offer-price-row" },
      h("span", { class: "price-xl" }, money(listing.price)),
      h("div", { class: "offer-price-notes" },
        msrpPill(listing.price, offer.msrp),
        dropped ? h("span", { class: "was" }, money(offer.alert.previousPrice)) : null)),
    listing.outcome === "MatchLanguageToConfirm" || listing.preOrder
      ? h("div", { class: "tag-row" },
        listing.outcome === "MatchLanguageToConfirm" ? h("span", { class: "tag amber" }, "Check the language on the page") : null,
        listing.preOrder ? h("span", { class: "tag accent" }, "Pre-order") : null)
      : null,
    h("div", { class: "offer-actions" },
      safeHref(listing.url) ? linkButton("Open in store", listing.url, { kind: "primary grow", external: true, trailingIcon: "external" }) : null,
      linkButton("Details", routeHash("watched", task.id))));
}

/** The other offers, as a list you can filter by kind and sort. */
function offerTable(offers) {
  const kinds = [...new Set(offers.map((offer) => offer.kind))].sort((a, b) => kindOrder(a) - kindOrder(b));
  if (state.radarKind !== "all" && !kinds.includes(state.radarKind)) state.radarKind = "all";
  const visible = offers.filter((offer) => state.radarKind === "all" || offer.kind === state.radarKind);
  const ratio = (offer) => (offer.msrp ? offer.listing.price / offer.msrp : Infinity);
  const sorters = {
    recent: (a, b) => new Date(b.since) - new Date(a.since),
    cheap: (a, b) => byPrice(a.listing, b.listing),
    msrp: (a, b) => ratio(a) - ratio(b),
  };
  visible.sort(sorters[state.radarSort] || sorters.recent);

  return [
    h("div", { class: "toolbar" },
      h("div", { class: "chips small", role: "group", "aria-label": "Product type" },
        [["all", "All", offers.length], ...kinds.map((kind) => [kind, kindLabel(kind), offers.filter((offer) => offer.kind === kind).length])]
          .map(([key, label, count]) => h("button", {
            type: "button", class: `chip ${state.radarKind === key ? "on" : ""}`, "aria-pressed": state.radarKind === key ? "true" : "false",
            onclick: () => { state.radarKind = key; renderView(true); },
          }, label, h("span", { class: "chip-count" }, count)))),
      h("div", { class: "toolbar-end" },
        selectPill("Sort", state.radarSort, Object.entries(RADAR_SORTS), (value) => { state.radarSort = value; writePreference("radarSort", value); renderView(true); }))),
    h("div", { class: "card flush" }, h("div", { class: "table-wrap" }, h("table", { class: "table offers" },
      h("thead", {}, h("tr", {},
        h("th", {}, "Offer"), h("th", {}, "Type"), h("th", {}, "Store"), h("th", { class: "num" }, "Price"), h("th", {}, "vs MSRP"), h("th", {}, "Since"), h("th", {}))),
      h("tbody", {}, visible.map((offer) => h("tr", {},
        h("td", { class: "title-cell" }, h("div", { class: "offer-line" },
          productArt({ imageUrl: offer.listing.imageUrl, game: offer.task.game, title: offer.listing.title, query: offer.task.searchQuery, size: "mini" }),
          h("div", { class: "offer-line-text" },
            externalLink(offer.listing.url, offer.listing.title, "offer-line-title"),
            h("a", { class: "offer-line-set", href: routeHash("watched", offer.task.id) }, offer.task.name,
              offer.listing.outcome === "MatchLanguageToConfirm" ? h("span", { class: "amber" }, " · check the language") : null)))),
        h("td", { class: "nowrap muted" }, offer.kindLabel),
        h("td", { class: "nowrap" }, offer.storeName),
        h("td", { class: "num nowrap mono strong" }, money(offer.listing.price)),
        h("td", { class: "nowrap" }, msrpPill(offer.listing.price, offer.msrp) || h("span", { class: "dim" }, "—")),
        h("td", { class: "nowrap muted", title: dateTime(offer.since) }, relative(offer.since).replace(" ago", "")),
        h("td", { class: "actions" }, safeHref(offer.listing.url)
          ? linkButton("Open", offer.listing.url, { kind: "small", external: true, trailingIcon: "external" }) : null))))))),
  ];
}

/**
 * "Latest moves": the latest restocks, price drops and pre-orders the bot alerted, with the game — like a feed
 * of what changed in the stores.
 */
function movesCard() {
  const moves = state.alerts.filter((alert) => inGameTab(alertTask(alert)?.game)).slice(0, 6);
  const since = Date.now() - 30 * 864e5;
  const month = state.alerts.filter((alert) => new Date(alert.at).getTime() >= since);
  const drops = month.filter((alert) => alert.previousPrice !== null && alert.previousPrice !== undefined).length;
  return h("section", { class: "card side-card", "aria-labelledby": "moves-h" },
    h("div", { class: "side-card-head" }, h("h2", { id: "moves-h" }, "Latest moves"),
      h("a", { class: "text-link small", href: routeHash("alerts") }, "See all")),
    h("div", { class: "move-stats" },
      h("div", {}, h("strong", { class: "mono" }, month.length - drops), h("span", {}, "stock alerts")),
      h("div", {}, h("strong", { class: "mono" }, drops), h("span", {}, "price drops"))),
    h("p", { class: "dim small move-note" }, "Last 30 days"),
    moves.length === 0
      ? h("p", { class: "muted small" }, "No moves yet.")
      : h("ul", { class: "moves" }, moves.map((alert) => {
        const task = alertTask(alert);
        const dropped = alert.previousPrice !== null && alert.previousPrice !== undefined;
        const [label, tone] = dropped ? ["Dropped", "drop"] : alert.preOrder ? ["Pre-order", "pre"] : ["Restock", "restock"];
        return h("li", {},
          h("div", { class: "move-top" },
            h("span", { class: `move-kind ${tone}` }, label),
            task ? h("span", { class: `move-game ${gameClass(task.game)}` }, h("span", { class: "game-dot", "aria-hidden": "true" }), GAMES[gameKey(task.game)]) : null,
            h("time", { class: "muted small", datetime: alert.at, title: dateTime(alert.at) }, relative(alert.at))),
          h("a", { class: "move-title", href: safeHref(alert.url) || routeHash("alerts"), target: "_blank", rel: "noopener noreferrer", title: alert.title }, alert.title),
          h("span", { class: "move-meta" }, alert.storeName, " · ", h("strong", { class: "mono" }, money(alert.price)),
            dropped ? h("s", { class: "dim mono" }, money(alert.previousPrice)) : null));
      })));
}

function activityCard() {
  const events = state.monitor.events.slice(0, 8);
  return h("section", { class: "card side-card", "aria-labelledby": "activity-h" },
    h("div", { class: "side-card-head" }, h("h2", { id: "activity-h" }, "Activity"), h("span", { class: "live" }, "live")),
    events.length === 0
      ? h("p", { class: "muted small" }, "No activity yet.")
      : h("ul", { class: "feed", "data-keep-scroll": "feed" }, events.map((event) =>
        h("li", { class: event.level },
          h("span", { class: "feed-dot", "aria-hidden": "true" }),
          h("span", { class: "feed-message" }, event.message),
          h("time", { datetime: event.at }, clock(event.at))))),
    state.monitor.events.length > 8 ? h("button", { class: "text-link small", type: "button", onclick: openActivityDrawer }, `See all ${state.monitor.events.length} entries`) : null);
}

function openActivityDrawer() {
  openDrawer({
    title: "Activity",
    subtitle: "The latest entries of the bot, newest first.",
    body: h("ul", { class: "feed full" }, state.monitor.events.map((event) =>
      h("li", { class: event.level }, h("span", { class: "feed-dot" }), h("span", { class: "feed-message" }, event.message), h("time", { datetime: event.at }, clock(event.at))))),
    footer: [button("Close", { kind: "ghost", onClick: closeDrawer })],
  });
}

function storesHealthCard() {
  const stores = enabledStores().filter((store) => store.platform !== "Manual");
  const health = stores.map((store) => ({ store, health: storeHealth(store) }));
  const responding = health.filter((item) => ["ok", "slow", "partial"].includes(item.health.kind)).length;
  const problems = health.filter((item) => item.health.kind === "error");

  return h("section", { class: "card side-card", "aria-labelledby": "stores-h" },
    h("div", { class: "side-card-head" }, h("h2", { id: "stores-h" }, "Stores"),
      h("span", { class: "muted small" }, h("strong", {}, `${responding}`), ` of ${stores.length} answering`)),
    stores.length === 0
      ? h("p", { class: "muted small" }, "No active stores.")
      : h("div", { class: "health-squares" }, health.map(({ store, health: item }) =>
        h("a", { class: `sq ${item.kind}`, href: routeHash("stores"), title: `${store.name}: ${item.label}`, "aria-label": `${store.name}: ${item.label}` }))),
    problems.length
      ? h("div", { class: "callout red" }, icon("alert"),
        h("div", {}, h("strong", {}, problems.slice(0, 3).map((item) => item.store.name).join(", ")),
          problems.length > 3 ? ` and ${problems.length - 3} more` : "", " didn't answer in the last cycle."),
        h("a", { class: "text-link", href: routeHash("stores") }, "View"))
      : null);
}
