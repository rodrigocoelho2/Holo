"use strict";

/* ==========================================================================
   Radar — what is on sale right now at your price, what is still being searched, activity and store health.
   ========================================================================== */

RENDERERS.radar = function viewRadar() {
  const insights = state.tasks.map(taskInsight);
  const query = state.radarQuery;
  const visible = (insight) => !query || matchesWords(query, insight.task.name, insight.task.searchQuery,
    insight.stores.map((store) => store.name).join(" "), insight.matches.map((listing) => listing.title).join(" "));

  const inStock = insights.filter((insight) => insight.status === "stock" && visible(insight))
    .sort((a, b) => new Date(b.lastAlert?.at || b.best.checkedAt) - new Date(a.lastAlert?.at || a.best.checkedAt));
  const order = { searching: 0, waiting: 1, error: 2, paused: 3 };
  const searching = insights.filter((insight) => insight.status !== "stock" && visible(insight))
    .sort((a, b) => (order[a.status] - order[b.status]) || (b.tone === "amber") - (a.tone === "amber") || a.task.name.localeCompare(b.task.name, "pt"));

  const last = state.monitor.lastCycle;
  const subtitle = last ? `O que está à venda agora, ao teu preço. Última verificação ${relative(last.finishedAt)}.` : "O que está à venda agora, ao teu preço.";

  const searchBox = searchField(state.radarQuery, "Procurar produto ou loja", (value) => { state.radarQuery = value; renderView(true); }, "radar-search");

  return [
    pageHead("Radar", subtitle, [
      searchBox,
      button("Verificar agora", { iconName: "bolt", onClick: runNow, kbd: "R", title: "Pesquisa já em todas as lojas (atalho R)" }),
      linkButton("Vigiar produto", routeHash("vigiar"), { iconName: "plus", kind: "primary" }),
    ]),

    h("section", { class: "section", "aria-labelledby": "stock-h" },
      sectionHead("Em stock agora", { id: "stock-h", count: inStock.length, note: "dentro do teu preço máximo e no idioma certo" }),
      inStock.length
        ? h("div", { class: "stock-grid" }, inStock.map(stockCard))
        : h("div", { class: "card soft" }, emptyState("radar", query ? "Nada em stock com esse filtro." : "Nada em stock ao teu preço, por agora.",
          query ? null : "Quando uma loja tiver um produto que vigias, em inglês e abaixo do teu máximo, aparece aqui e recebes o alerta."))),

    h("div", { class: "split" },
      h("section", { class: "split-main section", "aria-labelledby": "watch-h" },
        sectionHead("Ainda à procura", { id: "watch-h", count: searching.length, action: h("a", { class: "text-link", href: routeHash("a-vigiar") }, "Ver todos os produtos", icon("chevron")) }),
        searching.length
          ? [h("div", { class: "watch-rows" }, searching.slice(0, 12).map(watchRow)),
            searching.length > 12 ? h("a", { class: "text-link more", href: routeHash("a-vigiar") }, `Mais ${searching.length - 12} produtos`, icon("chevron")) : null,
            dotsLegend()]
          : h("div", { class: "card soft" }, emptyState("eye", state.tasks.length === 0 ? "Ainda não vigias nenhum produto." : "Tudo o que vigias está em stock.",
            null, state.tasks.length === 0 ? linkButton("Vigiar o primeiro produto", routeHash("vigiar"), { iconName: "plus", kind: "primary" }) : null))),
      h("aside", { class: "split-side" }, activityCard(), storesHealthCard())),
  ];
};

function stockCard(insight) {
  const { task, best, lastAlert } = insight;
  const store = findStore(best.storeName);
  const below = task.maxPrice && best.price !== null ? task.maxPrice - best.price : null;
  const otherStores = new Set(insight.matches.map((listing) => listing.storeName));
  otherStores.delete(best.storeName);

  return h("article", { class: "stock-card" },
    h("div", { class: "stock-art" },
      productArt({ imageUrl: best.imageUrl || insight.photo, game: task.game, title: best.title, query: task.searchQuery, size: "hero", caption: true }),
      h("span", { class: "art-badge" }, "Em stock"),
      h("span", { class: "art-corner" }, gamePill(task.game))),
    h("div", { class: "stock-body" },
      h("h3", { class: "stock-title" }, h("a", { href: routeHash("produto", task.id) }, task.name || task.id)),
      h("div", { class: "stock-sub" }, best.storeName, store ? ` · ${PLATFORM_LABELS[store.platform] || store.platform}` : null)),
    h("div", { class: "stock-price-row" },
      h("div", {},
        h("div", { class: "price-xl" }, money(best.price)),
        below !== null && below >= 0 ? h("div", { class: "below" }, below < 0.005 ? "No teu máximo" : `${money(below)} abaixo do teu máximo`) : null),
      h("div", { class: "stock-meta" }, h("div", {}, `máx. ${money(task.maxPrice)}`), h("div", {}, relative(lastAlert?.at || best.checkedAt)))),
    h("div", { class: "tag-row" },
      best.outcome === "MatchLanguageToConfirm"
        ? h("span", { class: "tag amber" }, "Confirma o idioma na página")
        : h("span", { class: "tag" }, TASK_LANGUAGES[task.language]?.[1] || "Inglês"),
      best.preOrder ? h("span", { class: "tag accent" }, "Pré-venda") : null,
      lastAlert ? h("span", { class: "tag" }, `Alerta enviado às ${clock(lastAlert.at)}`) : null,
      otherStores.size ? h("span", { class: "tag" }, `Também em ${otherStores.size} ${otherStores.size === 1 ? "loja" : "lojas"}`) : null),
    h("div", { class: "stock-actions" },
      safeHref(best.url) ? linkButton("Abrir na loja", best.url, { kind: "primary grow", external: true, trailingIcon: "external" }) : null,
      linkButton("Detalhes", routeHash("produto", task.id))));
}

function watchRow(insight) {
  const { task } = insight;
  return h("a", { class: `watch-row ${insight.status}`, href: routeHash("produto", task.id) },
    productArt({ imageUrl: insight.photo, game: task.game, title: task.name, query: task.searchQuery, size: "thumb" }),
    h("div", { class: "watch-row-text" },
      h("div", { class: "watch-row-title" }, task.name || task.id),
      h("div", { class: "watch-row-sub" }, `${GAMES[gameKey(task.game)]} · máx. ${money(task.maxPrice)}`)),
    h("div", { class: "watch-row-status" },
      h("div", { class: `status-text ${insight.tone}` }, insight.summary),
      storeDots(insight.dots)),
    icon("chevron", "row-chevron"));
}

const DOT_LABELS = { match: "dentro do preço", above: "em stock, acima do preço", none: "esgotado ou sem resultados", error: "loja com erro", pending: "à espera", off: "em pausa" };

function storeDots(dots) {
  if (!dots.length) return null;
  return h("div", { class: "dots", role: "img", "aria-label": dots.map((dot) => `${dot.store}: ${DOT_LABELS[dot.kind]}`).join(", ") },
    dots.slice(0, 16).map((dot) => h("span", { class: `dot ${dot.kind}`, title: `${dot.store}: ${DOT_LABELS[dot.kind]}` })),
    dots.length > 16 ? h("span", { class: "dots-more" }, `+${dots.length - 16}`) : null);
}

function dotsLegend() {
  return h("div", { class: "legend" },
    ["match", "above", "none", "error"].map((kind) => h("span", {}, h("span", { class: `dot ${kind}` }), DOT_LABELS[kind])));
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
  const problem = health.find((item) => item.health.kind === "error") || health.find((item) => item.health.kind === "partial");

  return h("section", { class: "card side-card", "aria-labelledby": "stores-h" },
    h("div", { class: "side-card-head" }, h("h2", { id: "stores-h" }, "Lojas"),
      h("span", { class: "muted small" }, h("strong", {}, `${responding}`), ` de ${stores.length} ativas a responder`)),
    stores.length === 0
      ? h("p", { class: "muted small" }, "Nenhuma loja ativa.")
      : h("div", { class: "health-squares" }, health.map(({ store, health: item }) =>
        h("a", { class: `sq ${item.kind}`, href: routeHash("lojas"), title: `${store.name}: ${item.label}`, "aria-label": `${store.name}: ${item.label}` }))),
    problem
      ? h("div", { class: "callout red" }, icon("alert"),
        h("div", {}, h("strong", {}, problem.store.name), ` ${problem.health.kind === "error" ? "não respondeu no último ciclo" : "teve pesquisas com erro"}`,
          problem.health.error ? h("div", { class: "callout-detail", title: problem.health.error }, problem.health.error) : null),
        h("a", { class: "text-link", href: routeHash("lojas") }, "Ver"))
      : null);
}
