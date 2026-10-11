"use strict";

/* ==========================================================================
   Radar — the offers you can buy now: what is new (last 24 h) in big cards, everything else in a list you can
   filter by product kind and sort. Store health and activity on the side.
   ========================================================================== */

const RADAR_SORTS = { recent: "Mais recentes", cheap: "Mais baratas", msrp: "Melhor vs MSRP" };

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

  const searchBox = searchField(state.radarQuery, "Procurar oferta, loja ou produto", (value) => { state.radarQuery = value; renderView(true); }, "radar-search");

  return [
    pageHead("Radar", "Ofertas em stock, no idioma certo e dentro da tua regra de preço.", [
      searchBox,
      button("Verificar agora", { iconName: "bolt", onClick: runNow, kbd: "R", title: "Pesquisa já em todas as lojas (atalho R)" }),
      linkButton("Vigiar produto", routeHash("vigiar"), { iconName: "plus", kind: "primary" }),
    ]),

    gameTabs(everyOffer.map((offer) => offer.task.game), "ofertas"),
    h("div", { class: "kpis" },
      kpi("Novas nas últimas 24 h", fresh.length, fresh.length ? "accent" : ""),
      kpi("Disponíveis ao teu preço", all.length),
      kpi("A vigiar", `${watching}`, "", `${watching === 1 ? "produto" : "produtos"} em ${storesUsed} lojas`, routeHash("a-vigiar")),
      kpi("Última verificação", last ? relative(last.finishedAt) : "—", last?.failed ? "amber" : "",
        last ? `${last.searches} pesquisas${last.failed ? ` · ${last.failed} com erro` : " · sem erros"}` : "ainda sem ciclos")),

    h("div", { class: "split" },
      h("div", { class: "split-main radar-main" },
        h("section", { class: "section", "aria-labelledby": "fresh-h" },
          sectionHead("Novidades", { id: "fresh-h", count: fresh.length, note: "apareceram ou baixaram de preço nas últimas 24 horas" }),
          fresh.length
            ? h("div", { class: "offer-grid" }, fresh.map(offerCard))
            : h("div", { class: "card soft" }, emptyState("radar", "Nada de novo nas últimas 24 horas.",
              rest.length ? "As ofertas que continuam disponíveis estão na lista abaixo." : "Quando uma loja tiver um produto que vigias ao teu preço, aparece aqui e recebes o alerta."))),

        h("section", { class: "section", "aria-labelledby": "available-h" },
          sectionHead("Continuam disponíveis", { id: "available-h", count: rest.length, note: "em stock há mais de um dia, ou sem alerta recente" }),
          rest.length ? offerTable(rest) : h("p", { class: "muted small" }, all.length ? "Todas as ofertas são novidades." : "Nenhuma oferta ao teu preço neste momento.")),
        state.tasks.length === 0
          ? h("div", { class: "card soft" }, emptyState("eye", "Ainda não vigias nenhum produto.", null,
            linkButton("Vigiar o primeiro produto", routeHash("vigiar"), { iconName: "plus", kind: "primary" })))
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
    h("a", { class: "offer-art", href: safeHref(listing.url) || routeHash("produto", task.id), target: "_blank", rel: "noopener noreferrer", tabindex: "-1", "aria-hidden": "true" },
      productArt({ imageUrl: listing.imageUrl, game: task.game, title: listing.title, query: task.searchQuery, size: "hero", caption: true }),
      h("span", { class: `art-badge ${dropped ? "drop" : ""}` }, dropped ? "Baixou" : "Novo"),
      h("span", { class: "art-corner" }, h("span", { class: "kind-chip" }, offer.kindLabel))),
    h("div", { class: "offer-body" },
      h("a", { class: "offer-set", href: routeHash("produto", task.id) }, task.name),
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
        listing.outcome === "MatchLanguageToConfirm" ? h("span", { class: "tag amber" }, "Confirma o idioma na página") : null,
        listing.preOrder ? h("span", { class: "tag accent" }, "Pré-venda") : null)
      : null,
    h("div", { class: "offer-actions" },
      safeHref(listing.url) ? linkButton("Abrir na loja", listing.url, { kind: "primary grow", external: true, trailingIcon: "external" }) : null,
      linkButton("Detalhes", routeHash("produto", task.id))));
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
      h("div", { class: "chips small", role: "group", "aria-label": "Tipo de produto" },
        [["all", "Todos", offers.length], ...kinds.map((kind) => [kind, kindLabel(kind), offers.filter((offer) => offer.kind === kind).length])]
          .map(([key, label, count]) => h("button", {
            type: "button", class: `chip ${state.radarKind === key ? "on" : ""}`, "aria-pressed": state.radarKind === key ? "true" : "false",
            onclick: () => { state.radarKind = key; renderView(true); },
          }, label, h("span", { class: "chip-count" }, count)))),
      h("div", { class: "toolbar-end" },
        selectPill("Ordenar", state.radarSort, Object.entries(RADAR_SORTS), (value) => { state.radarSort = value; writePreference("radarSort", value); renderView(true); }))),
    h("div", { class: "card flush" }, h("div", { class: "table-wrap" }, h("table", { class: "table offers" },
      h("thead", {}, h("tr", {},
        h("th", {}, "Oferta"), h("th", {}, "Tipo"), h("th", {}, "Loja"), h("th", { class: "num" }, "Preço"), h("th", {}, "vs MSRP"), h("th", {}, "Desde"), h("th", {}))),
      h("tbody", {}, visible.map((offer) => h("tr", {},
        h("td", { class: "title-cell" }, h("div", { class: "offer-line" },
          productArt({ imageUrl: offer.listing.imageUrl, game: offer.task.game, title: offer.listing.title, query: offer.task.searchQuery, size: "mini" }),
          h("div", { class: "offer-line-text" },
            externalLink(offer.listing.url, offer.listing.title, "offer-line-title"),
            h("a", { class: "offer-line-set", href: routeHash("produto", offer.task.id) }, offer.task.name,
              offer.listing.outcome === "MatchLanguageToConfirm" ? h("span", { class: "amber" }, " · confirma o idioma") : null)))),
        h("td", { class: "nowrap muted" }, offer.kindLabel),
        h("td", { class: "nowrap" }, offer.storeName),
        h("td", { class: "num nowrap mono strong" }, money(offer.listing.price)),
        h("td", { class: "nowrap" }, msrpPill(offer.listing.price, offer.msrp) || h("span", { class: "dim" }, "—")),
        h("td", { class: "nowrap muted", title: dateTime(offer.since) }, relative(offer.since).replace("há ", "")),
        h("td", { class: "actions" }, safeHref(offer.listing.url)
          ? linkButton("Abrir", offer.listing.url, { kind: "small", external: true, trailingIcon: "external" }) : null))))))),
  ];
}

/**
 * "Últimos movimentos": the latest restocks, price drops and pre-orders the bot alerted, with the game — like a feed
 * of what changed in the stores.
 */
function movesCard() {
  const moves = state.alerts.filter((alert) => inGameTab(alertTask(alert)?.game)).slice(0, 6);
  const since = Date.now() - 30 * 864e5;
  const month = state.alerts.filter((alert) => new Date(alert.at).getTime() >= since);
  const drops = month.filter((alert) => alert.previousPrice !== null && alert.previousPrice !== undefined).length;
  return h("section", { class: "card side-card", "aria-labelledby": "moves-h" },
    h("div", { class: "side-card-head" }, h("h2", { id: "moves-h" }, "Últimos movimentos"),
      h("a", { class: "text-link small", href: routeHash("alertas") }, "Ver todos")),
    h("div", { class: "move-stats" },
      h("div", {}, h("strong", { class: "mono" }, month.length - drops), h("span", {}, "alertas de stock")),
      h("div", {}, h("strong", { class: "mono" }, drops), h("span", {}, "descidas de preço"))),
    h("p", { class: "dim small move-note" }, "Últimos 30 dias"),
    moves.length === 0
      ? h("p", { class: "muted small" }, "Sem movimentos ainda.")
      : h("ul", { class: "moves" }, moves.map((alert) => {
        const task = alertTask(alert);
        const dropped = alert.previousPrice !== null && alert.previousPrice !== undefined;
        const [label, tone] = dropped ? ["Baixou", "drop"] : alert.preOrder ? ["Pré-venda", "pre"] : ["Reposição", "restock"];
        return h("li", {},
          h("div", { class: "move-top" },
            h("span", { class: `move-kind ${tone}` }, label),
            task ? h("span", { class: `move-game ${gameClass(task.game)}` }, h("span", { class: "game-dot", "aria-hidden": "true" }), GAMES[gameKey(task.game)]) : null,
            h("time", { class: "muted small", datetime: alert.at, title: dateTime(alert.at) }, relative(alert.at))),
          h("a", { class: "move-title", href: safeHref(alert.url) || routeHash("alertas"), target: "_blank", rel: "noopener noreferrer", title: alert.title }, alert.title),
          h("span", { class: "move-meta" }, alert.storeName, " · ", h("strong", { class: "mono" }, money(alert.price)),
            dropped ? h("s", { class: "dim mono" }, money(alert.previousPrice)) : null));
      })));
}

function activityCard() {
  const events = state.monitor.events.slice(0, 8);
  return h("section", { class: "card side-card", "aria-labelledby": "activity-h" },
    h("div", { class: "side-card-head" }, h("h2", { id: "activity-h" }, "Atividade"), h("span", { class: "live" }, "em tempo real")),
    events.length === 0
      ? h("p", { class: "muted small" }, "Sem atividade ainda.")
      : h("ul", { class: "feed", "data-keep-scroll": "feed" }, events.map((event) =>
        h("li", { class: event.level },
          h("span", { class: "feed-dot", "aria-hidden": "true" }),
          h("span", { class: "feed-message" }, event.message),
          h("time", { datetime: event.at }, clock(event.at))))),
    state.monitor.events.length > 8 ? h("button", { class: "text-link small", type: "button", onclick: openActivityDrawer }, `Ver as ${state.monitor.events.length} entradas`) : null);
}

function openActivityDrawer() {
  openDrawer({
    title: "Atividade",
    subtitle: "As últimas entradas do bot, mais recentes primeiro.",
    body: h("ul", { class: "feed full" }, state.monitor.events.map((event) =>
      h("li", { class: event.level }, h("span", { class: "feed-dot" }), h("span", { class: "feed-message" }, event.message), h("time", { datetime: event.at }, clock(event.at))))),
    footer: [button("Fechar", { kind: "ghost", onClick: closeDrawer })],
  });
}

function storesHealthCard() {
  const stores = enabledStores().filter((store) => store.platform !== "Manual");
  const health = stores.map((store) => ({ store, health: storeHealth(store) }));
  const responding = health.filter((item) => ["ok", "slow", "partial"].includes(item.health.kind)).length;
  const problems = health.filter((item) => item.health.kind === "error");

  return h("section", { class: "card side-card", "aria-labelledby": "stores-h" },
    h("div", { class: "side-card-head" }, h("h2", { id: "stores-h" }, "Lojas"),
      h("span", { class: "muted small" }, h("strong", {}, `${responding}`), ` de ${stores.length} a responder`)),
    stores.length === 0
      ? h("p", { class: "muted small" }, "Nenhuma loja ativa.")
      : h("div", { class: "health-squares" }, health.map(({ store, health: item }) =>
        h("a", { class: `sq ${item.kind}`, href: routeHash("lojas"), title: `${store.name}: ${item.label}`, "aria-label": `${store.name}: ${item.label}` }))),
    problems.length
      ? h("div", { class: "callout red" }, icon("alert"),
        h("div", {}, h("strong", {}, problems.slice(0, 3).map((item) => item.store.name).join(", ")),
          problems.length > 3 ? ` e mais ${problems.length - 3}` : "", problems.length === 1 ? " não respondeu no último ciclo." : " não responderam no último ciclo."),
        h("a", { class: "text-link", href: routeHash("lojas") }, "Ver"))
      : null);
}
