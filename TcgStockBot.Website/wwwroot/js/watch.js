"use strict";

/* ==========================================================================
   A vigiar (the watch list), the product page and the "Vigiar produto" wizard.
   A "produto" is a stock task: a search, the words it must / must not have, a max price and the stores.
   ========================================================================== */

/* ----- A vigiar ----- */

RENDERERS["a-vigiar"] = function viewWatchlist() {
  const insights = state.tasks.map(taskInsight);
  const storeCount = new Set(insights.flatMap((insight) => insight.stores.map((store) => store.name))).size;
  const games = Object.keys(GAMES).filter((game) => insights.some((insight) => gameKey(insight.task.game) === game));
  if (state.watchGame !== "all" && !games.includes(state.watchGame)) state.watchGame = "all";

  const inGame = insights.filter((insight) => state.watchGame === "all" || gameKey(insight.task.game) === state.watchGame);
  const statusOf = (insight) => (insight.status === "stock" ? "stock" : insight.status === "paused" ? "paused" : "searching");
  const counts = { all: inGame.length, stock: 0, searching: 0, paused: 0 };
  for (const insight of inGame) counts[statusOf(insight)]++;

  const grid = h("div", { class: "watch-grid" });
  const renderGrid = () => {
    const visible = inGame.filter((insight) => (state.watchStatus === "all" || statusOf(insight) === state.watchStatus)
      && (!state.watchQuery || matchesWords(state.watchQuery, insight.task.name, insight.task.searchQuery, insight.task.id)));
    const order = { stock: 0, searching: 1, waiting: 1, error: 2, paused: 3 };
    visible.sort((a, b) => (order[a.status] - order[b.status]) || (a.task.name || "").localeCompare(b.task.name || "", "pt"));
    grid.replaceChildren(...(visible.length ? visible.map(watchCard)
      : [h("div", { class: "card soft span-all" }, emptyState("eye", state.tasks.length === 0 ? "Ainda não vigias nenhum produto." : "Nenhum produto neste filtro.",
        state.tasks.length === 0 ? "Procura um produto em todas as lojas, escolhe os resultados certos e o bot passa a vigiá-lo." : null,
        state.tasks.length === 0 ? linkButton("Vigiar produto", routeHash("vigiar"), { iconName: "plus", kind: "primary" }) : null))]));
  };
  renderGrid();

  return [
    pageHead("A vigiar", `${state.tasks.length} ${state.tasks.length === 1 ? "produto" : "produtos"} em ${storeCount} ${storeCount === 1 ? "loja" : "lojas"}. Toca num produto para ver o que cada loja tem.`, [
      watchMenu(),
      linkButton("Vigiar produto", routeHash("vigiar"), { iconName: "plus", kind: "primary" }),
    ]),
    games.length > 1 ? h("div", { class: "chips", role: "group", "aria-label": "Jogo" },
      [["all", "Todos", insights.length], ...games.map((game) => [game, GAMES[game], insights.filter((insight) => gameKey(insight.task.game) === game).length])]
        .map(([key, label, count]) => h("button", {
          type: "button", class: `chip ${key === "all" ? "" : gameClass(key)} ${state.watchGame === key ? "on" : ""}`, "aria-pressed": state.watchGame === key ? "true" : "false",
          onclick: () => { state.watchGame = key; renderView(true); },
        }, key === "all" ? null : h("span", { class: "chip-dot" }), label, h("span", { class: "chip-count" }, count)))) : null,
    h("div", { class: "toolbar" },
      segmented([["all", "Todos", null], ["stock", "Em stock", counts.stock], ["searching", "À procura", counts.searching], ["paused", "Em pausa", counts.paused]],
        state.watchStatus, (value) => { state.watchStatus = value; renderGrid(); }, "Estado"),
      searchField(state.watchQuery, "Filtrar", (value) => { state.watchQuery = value; renderGrid(); }, "watch-search", "Filtrar produtos")),
    h("hr", { class: "rule" }),
    grid,
  ];
};

/** "Mais" menu of the watch list: bulk actions and JSON import/export. */
function watchMenu() {
  const tasks = state.tasks;
  const item = (label, iconName, onClick, disabled = false, danger = false) =>
    h("button", { type: "button", class: `menu-item ${danger ? "danger" : ""}`, disabled, onclick: (event) => { event.currentTarget.closest("details").open = false; onClick(); } }, icon(iconName), label);
  return h("details", { class: "menu" },
    h("summary", { class: "btn", "aria-label": "Mais ações" }, icon("more"), "Mais"),
    h("div", { class: "menu-list" },
      item("Iniciar todos", "play", () => setTaskEnabled(null, true), tasks.length === 0),
      item("Pausar todos", "pause", () => setTaskEnabled(null, false), tasks.length === 0),
      h("hr"),
      item("Formulário completo (nova tarefa)", "edit", () => openTaskDrawer(null)),
      item("Exportar JSON", "download", exportTasks, tasks.length === 0),
      item("Importar JSON", "upload", openTaskImportDrawer),
      h("hr"),
      item("Apagar todos", "trash", deleteAllTasks, tasks.length === 0, true)));
}

function watchCard(insight) {
  const { task, best } = insight;
  const bestNow = best || insight.aboveMax[0] || null;
  const statusLabel = STATUS_LABELS[insight.status];
  let footer = insight.summary;
  if (insight.status === "stock") footer = `${best.storeName} · ${insight.stores.length} ${insight.stores.length === 1 ? "loja" : "lojas"}${best.outcome === "MatchLanguageToConfirm" ? " · confirmar idioma" : ""}`;
  else if (insight.tone === "amber") footer = `Em stock na ${shortStoreName(insight.aboveMax[0].storeName)}, acima do máximo`;
  const link = routeHash("produto", task.id);

  return h("article", { class: `watch-card ${insight.status}` },
    h("a", { class: "watch-art", href: link, tabindex: "-1", "aria-hidden": "true" },
      productArt({ imageUrl: insight.photo, game: task.game, title: task.name, query: task.searchQuery, size: "card" }),
      h("span", { class: `art-badge ${insight.status}` }, statusLabel),
      h("span", { class: "art-corner" }, gamePill(task.game))),
    h("div", { class: "watch-card-body" },
      h("h3", { class: "watch-card-title" }, h("a", { href: link }, task.name || task.id)),
      h("div", { class: "query" }, `“${task.searchQuery}”`)),
    h("div", { class: "mini-stats" },
      h("div", { class: "mini-stat" }, h("span", {}, "Máximo"), h("strong", { class: "mono" }, money(task.maxPrice))),
      h("div", { class: "mini-stat" }, h("span", {}, "Melhor agora"),
        h("strong", { class: `mono ${best ? "accent" : bestNow ? "amber" : "dim"}` }, bestNow ? money(bestNow.price) : "—"))),
    h("div", { class: "watch-card-foot" },
      h("span", { class: `foot-text ${insight.status === "error" ? "red" : ""}` }, footer),
      switchInput(task.enabled, (checked) => setTaskEnabled(task.id, checked), task.enabled ? `Pausar ${task.name}` : `Retomar ${task.name}`)));
}

/* ----- Produto ----- */

RENDERERS.produto = function viewProduct() {
  const task = findTask(state.route.param);
  const back = h("a", { class: "back-link", href: routeHash("a-vigiar") }, icon("back"), "A vigiar");
  if (!task) {
    return [back, h("div", { class: "card soft" }, emptyState("eye", "Este produto já não está a ser vigiado.", null, linkButton("Ver todos", routeHash("a-vigiar"))))];
  }

  const insight = taskInsight(task);
  const { best } = insight;
  const bestNow = best || insight.aboveMax[0] || null;
  const photoListing = [...insight.matches, ...insight.aboveMax, ...insight.tracked].find((listing) => safeHref(listing.imageUrl));
  const matchStores = new Set(insight.matches.map((listing) => listing.storeName));
  const statusChip = insight.status === "stock"
    ? h("span", { class: "status-chip-lg stock" }, `Em stock em ${matchStores.size} ${matchStores.size === 1 ? "loja" : "lojas"}`)
    : h("span", { class: `status-chip-lg ${insight.status}` }, STATUS_LABELS[insight.status]);

  const keywordChips = (words, tone) => words.length
    ? h("div", { class: "kw-row" }, words.map((word) => h("span", { class: `kw ${tone}` }, word)))
    : h("span", { class: "muted" }, "—");

  return [
    back,
    h("section", { class: "product-hero" },
      h("div", { class: "product-photo" },
        productArt({ imageUrl: photoListing?.imageUrl, game: task.game, title: task.name, query: task.searchQuery, size: "square" }),
        photoListing ? h("span", { class: "photo-credit" }, `Imagem da ${photoListing.storeName}`) : null),
      h("div", { class: "product-info" },
        h("div", { class: "tag-row" }, statusChip, gamePill(task.game), h("span", { class: "tag" }, TASK_LANGUAGES[task.language]?.[1] || "Inglês")),
        h("h1", { class: "product-title" }, task.name || task.id),
        h("div", { class: "stat-boxes" },
          h("div", { class: "stat-box" }, h("span", {}, "Preço máximo"), h("strong", { class: "mono" }, money(task.maxPrice))),
          h("div", { class: "stat-box" }, h("span", {}, "Melhor agora"),
            h("strong", { class: `mono ${best ? "accent" : bestNow ? "amber" : ""}` }, bestNow ? money(bestNow.price) : "—"),
            bestNow ? h("small", {}, bestNow.storeName) : null),
          h("div", { class: "stat-box" }, h("span", {}, "Lojas vigiadas"), h("strong", { class: "mono" }, insight.stores.length))),
        h("dl", { class: "rules" },
          h("dt", {}, "Pesquisa"), h("dd", {}, h("code", { class: "kw mono" }, task.searchQuery)),
          h("dt", {}, "Tem de ter"), h("dd", {}, keywordChips(task.requiredKeywords || [], "accent")),
          h("dt", {}, "Não pode ter"), h("dd", {}, keywordChips(task.excludedKeywords || [], "red"))),
        h("div", { class: "button-row" },
          button("Verificar agora", { iconName: "bolt", onClick: runNow, kbd: "R" }),
          button("Editar regras", { iconName: "edit", onClick: () => openTaskDrawer(task) }),
          task.enabled
            ? button("Pausar", { iconName: "pause", onClick: () => setTaskEnabled(task.id, false) })
            : button("Retomar", { iconName: "play", kind: "primary", onClick: () => setTaskEnabled(task.id, true) }),
          button("Duplicar", { iconName: "copy", onClick: () => openTaskDrawer({ ...task, id: "", name: `${task.name} (cópia)` }, true) }),
          button("Apagar", { kind: "text-danger", onClick: () => deleteTask(task) })))),

    h("div", { class: "split" },
      h("section", { class: "split-main section", "aria-labelledby": "found-h" }, productResults(insight)),
      h("aside", { class: "split-side" }, productAlertsCard(insight), productStoresCard(insight))),
  ];
};

function productResults(insight) {
  const { task } = insight;
  const candidates = (report) => report.listings.filter((listing) => !isIrrelevant(listing));
  const total = insight.reports.reduce((sum, report) => sum + report.listings.length, 0);
  const candidateCount = insight.reports.reduce((sum, report) => sum + candidates(report).length, 0);
  const only = state.productOnlyCandidates;

  const rank = (report) => (!report.succeeded ? 4
    : report.listings.some((listing) => isMatch(listing.outcome)) ? 0
      : report.listings.some((listing) => listing.outcome === "AboveMaxPrice" && listing.inStock) ? 1
        : candidates(report).length ? 2 : 3);
  const withResults = insight.reports.filter((report) => !report.succeeded || (only ? candidates(report) : report.listings).length > 0)
    .sort((a, b) => rank(a) - rank(b) || a.storeName.localeCompare(b.storeName, "pt"));
  const empty = insight.reports.filter((report) => report.succeeded && (only ? candidates(report) : report.listings).length === 0).map((report) => report.storeName);
  const waiting = insight.stores.filter((store) => !insight.reports.some((report) => report.storeName === store.name)).map((store) => store.name);
  const last = insight.lastChecked;

  return [
    h("div", { class: "section-head" },
      h("div", {},
        h("h2", { id: "found-h" }, "O que cada loja encontrou"),
        h("p", { class: "section-sub" }, last ? `Última pesquisa ${relative(last)}. Cada resultado diz porque é que avisou ou não.` : "Ainda não houve pesquisa para este produto.")),
      total ? segmented([["all", "Tudo", total], ["candidates", "Só candidatos", candidateCount]], only ? "candidates" : "all",
        (value) => { state.productOnlyCandidates = value === "candidates"; writePreference("onlyProducts", state.productOnlyCandidates); renderView(true); }, "Resultados") : null),
    withResults.length
      ? h("div", { class: "store-blocks" }, withResults.map((report) => storeBlock(task, report, only, insight)))
      : h("div", { class: "card soft" }, emptyState("search", insight.reports.length ? "Nenhuma loja encontrou este produto." : "À espera da primeira verificação.",
        insight.reports.length ? "Experimenta outro texto de pesquisa em “Editar regras”." : "Carrega em “Verificar agora” para não esperar pelo próximo ciclo.")),
    empty.length ? h("p", { class: "muted small" }, `Sem resultados para esta pesquisa: ${empty.join(", ")}.`) : null,
    waiting.length ? h("p", { class: "muted small" }, `À espera da primeira pesquisa: ${waiting.join(", ")}.`) : null,
  ];
}

function storeBlock(task, report, onlyCandidates, insight) {
  const store = findStore(report.storeName);
  const listings = (onlyCandidates ? report.listings.filter((listing) => !isIrrelevant(listing)) : report.listings)
    .slice().sort((a, b) => (isMatch(b.outcome) - isMatch(a.outcome)) || ((b.outcome === "AboveMaxPrice") - (a.outcome === "AboveMaxPrice")) || byPrice(a, b))
    .slice(0, 12);
  const kind = !report.succeeded ? "error" : report.listings.some((listing) => isMatch(listing.outcome)) ? "match"
    : report.listings.some((listing) => listing.outcome === "AboveMaxPrice" && listing.inStock) ? "above" : "none";

  return h("article", { class: `store-block ${kind}` },
    h("header", { class: "store-block-head" },
      h("span", { class: `sq ${kind === "match" ? "ok" : kind === "above" ? "slow" : kind === "error" ? "error" : "off"}`, "aria-hidden": "true" }),
      h("strong", {}, report.storeName),
      store ? h("span", { class: "tag small" }, PLATFORM_LABELS[store.platform] || store.platform) : null,
      h("span", { class: "store-block-meta mono" }, `${relative(report.checkedAt)} · ${report.succeeded ? durationText(report.durationMs) : "erro"}`)),
    !report.succeeded
      ? h("div", { class: "store-block-error" }, icon("alert"), h("span", {}, report.error || "A loja não respondeu."),
        store && safeHref(store.baseUrl) ? h("a", { class: "text-link", href: store.baseUrl, target: "_blank", rel: "noopener noreferrer" }, "Ver loja") : null)
      : h("ul", { class: "result-rows" }, listings.map((listing) => resultRow(task, listing, insight))));
}

function resultRow(task, listing, insight) {
  const meta = [listing.inStock ? "Em stock" : "Esgotado", LANGUAGES[listing.language] || listing.language];
  if (listing.seller) meta.push(`vendido por ${listing.seller}`);
  const showLowest = listing.lowestPrice !== null && listing.lowestPrice !== undefined && listing.price !== null && listing.lowestPrice < listing.price - 0.005;

  return h("li", { class: `result-row ${isMatch(listing.outcome) ? "match" : ""}` },
    productArt({ imageUrl: listing.imageUrl, game: task.game, title: listing.title, query: task.searchQuery, size: "thumb" }),
    h("div", { class: "result-text" },
      externalLink(listing.url, listing.title, "result-title"),
      h("div", { class: "result-meta" }, meta.join(" · "), listing.preOrder ? h("span", { class: "tag accent small" }, "Pré-venda") : null)),
    h("div", { class: "result-price" },
      h("span", { class: `mono price ${isMatch(listing.outcome) ? "accent" : listing.outcome === "AboveMaxPrice" ? "amber" : ""}` }, money(listing.price)),
      listing.priceHistoryKey && TRACKED_OUTCOMES.has(listing.outcome)
        ? iconButton("chart", `Histórico de preço de ${listing.title}`, () => openPriceHistory(listing.priceHistoryKey), "tiny")
        : null,
      showLowest ? h("span", { class: "lowest" }, `mín. ${money(listing.lowestPrice)}`) : null),
    verdict(task, listing, insight));
}

/** Why a result alerted or not, in words — with the exact word when it can be told from the title. */
function verdict(task, listing, insight) {
  const title = normalizeText(listing.title);
  const words = (list) => (list || []).flatMap((keyword) => keyword.split("|")).map((word) => word.replace(/\*/g, "").trim()).filter(Boolean);
  switch (listing.outcome) {
    case "Match": {
      const alerted = insight.alerts.some((alert) => alert.url === listing.url);
      return h("span", { class: "verdict accent solid" }, alerted ? "Avisado · dentro do preço" : "Dentro do preço");
    }
    case "MatchLanguageToConfirm":
      return h("span", { class: "verdict amber" }, "Dentro do preço · confirmar idioma");
    case "AboveMaxPrice":
      return h("span", { class: "verdict amber" }, listing.price && task.maxPrice ? `Acima do máximo · +${money(listing.price - task.maxPrice)}` : "Acima do máximo");
    case "ExcludedKeyword": {
      const found = words(task.excludedKeywords).find((word) => title.includes(normalizeText(word)));
      return h("span", { class: "verdict" }, found ? `Tem “${found}”` : "Tem uma palavra excluída");
    }
    case "MissingKeyword": {
      const missing = (task.requiredKeywords || []).find((keyword) => !keyword.split("|").some((word) => title.includes(normalizeText(word.replace(/\*/g, "").trim()))));
      return h("span", { class: "verdict" }, missing ? `Falta “${missing.split("|")[0].replace(/\*/g, "")}”` : "Não é o produto");
    }
    default: {
      const [label, tone] = OUTCOMES[listing.outcome] || [listing.outcome, "plain"];
      return h("span", { class: `verdict ${tone === "plain" ? "" : tone}` }, label);
    }
  }
}

function productAlertsCard(insight) {
  const alerts = insight.alerts.slice(0, 6);
  return h("section", { class: "card side-card", "aria-labelledby": "product-alerts-h" },
    h("div", { class: "side-card-head" }, h("h2", { id: "product-alerts-h" }, "Alertas deste produto"),
      insight.alerts.length > 6 ? h("a", { class: "text-link small", href: routeHash("alertas") }, "Ver todos") : null),
    alerts.length === 0
      ? h("p", { class: "muted small" }, "Ainda nenhum alerta. Quando uma loja o tiver ao teu preço, avisamos-te.")
      : h("ol", { class: "timeline" }, alerts.map((alert) => h("li", {},
        h("time", { class: "mono", datetime: alert.at }, `${dayHeading(alert.at)}, ${clock(alert.at)}`),
        h("div", { class: "timeline-main" }, h("strong", {}, alert.storeName), ` · ${money(alert.price)}`),
        alert.languageToConfirm ? h("div", { class: "timeline-sub amber" }, "Idioma por confirmar") : null,
        safeHref(alert.url) ? h("a", { class: "text-link small", href: alert.url, target: "_blank", rel: "noopener noreferrer" }, "Abrir", icon("external")) : null))));
}

function productStoresCard(insight) {
  const names = insight.task.storeNames || [];
  return h("section", { class: "card side-card", "aria-labelledby": "product-stores-h" },
    h("div", { class: "side-card-head" }, h("h2", { id: "product-stores-h" }, "Lojas vigiadas")),
    insight.stores.length
      ? h("div", { class: "kw-row" }, insight.stores.map((store) => h("span", { class: "kw" }, store.name)))
      : h("p", { class: "muted small" }, "Nenhuma loja ativa para este produto."),
    h("p", { class: "muted small" }, names.length ? "Só as lojas escolhidas nas regras." : "Todas as lojas ativas (nenhuma escolhida nas regras)."));
}

/* ----- Price history of one listing ----- */

async function openPriceHistory(key) {
  let history;
  try {
    history = await api(`/pricehistory/get?identifier=${encodeURIComponent(key)}`);
  } catch (error) {
    toast(errorText(error), "error");
    return;
  }

  const summary = history.summary;
  const points = [...history.points].reverse();
  const stat = (label, value, note) => h("div", { class: "stat-box small" }, h("span", {}, label), h("strong", { class: "mono" }, value), note ? h("small", {}, note) : null);

  openDrawer({
    title: summary.title,
    subtitle: `${summary.storeName} · a seguir desde ${dateTime(summary.firstSeen)}`,
    body: [
      h("div", { class: "stat-boxes four" },
        stat("Atual", money(summary.currentPrice), summary.currentInStock ? "em stock" : "esgotado"),
        stat("Mínimo", money(summary.lowestPrice), dateTime(summary.lowestPriceAt)),
        stat("Máximo", money(summary.highestPrice)),
        stat("Alterações", String(summary.pointCount))),
      drawerSection("Preço ao longo do tempo", priceHistoryChart(history)),
      drawerSection("Alterações",
        h("div", { class: "table-wrap" }, h("table", {},
          h("thead", {}, h("tr", {}, h("th", {}, "Desde"), h("th", { class: "num" }, "Preço"), h("th", {}, "Stock"))),
          h("tbody", {}, points.map((point) => h("tr", {},
            h("td", { class: "nowrap" }, dateTime(point.at)),
            h("td", { class: "num mono" }, money(point.price)),
            h("td", {}, point.inStock ? pill("Em stock", "accent") : pill("Esgotado")))))))),
    ],
    footer: [
      button("Fechar", { kind: "ghost", onClick: closeDrawer }),
      safeHref(summary.url) ? linkButton("Abrir produto", summary.url, { kind: "primary", external: true, trailingIcon: "external" }) : null,
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
 * is never told by colour alone.
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

  const lowest = points.reduce((bestPoint, point) => (point.price < bestPoint.price ? point : bestPoint), points[0]);
  const current = points[points.length - 1];
  const markers = points.map((point) => svg("circle", { class: point.inStock ? "history-dot" : "history-dot sold-out", cx: x(point.time), cy: y(point.price), r: 4 }));
  const labels = [svg("text", { class: "history-label", x: x(end) + 8, y: y(current.price) + 4 }, money(current.price))];
  if (lowest !== current && Math.abs(y(lowest.price) - y(current.price)) > 14) {
    labels.push(svg("text", { class: "history-label", x: x(lowest.time), y: y(lowest.price) + 18, "text-anchor": "middle" }, `mín. ${money(lowest.price)}`));
  }

  const axis = [
    svg("line", { class: "history-grid", x1: left, x2: width - right, y1: height - bottom, y2: height - bottom }),
    svg("text", { class: "history-axis", x: left, y: height - 8, "text-anchor": "start" }, dayMonthFormat.format(new Date(start))),
    svg("text", { class: "history-axis", x: width - right, y: height - 8, "text-anchor": "end" }, dayMonthFormat.format(new Date(end))),
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

/* ----- Vigiar produto (wizard) ----- */

const WIZARD_WORKERS = 4;
const STOP_WORDS = new Set(["pokemon", "pokémon", "tcg", "the", "de", "da", "do", "of", "and", "e", "card", "game", "jogo", "cartas", "en", "eng", "english", "ingles", "inglês"]);
const BASE_EXCLUDED = ["sleeves", "binder", "proxy", "acrylic", "acrilico"];
const DISCRIMINATORS = ["display", "case", "jp", "japanese", "japones", "korean", "coreano", "chinese", "chines", "es", "espanol", "español", "espanhol", "francais",
  "deutsch", "italiano", "pokemon center", "half", "mini", "code", "codigo", "empty", "vazio", "replica", "graded", "psa", "single", "carta"];

const wordsOf = (text) => normalizeText(text).replace(/[^a-z0-9]+/g, " ").split(" ").filter(Boolean);
const containsWord = (text, phrase) => ` ${wordsOf(text).join(" ")} `.includes(` ${wordsOf(phrase).join(" ")} `);

function newWizard() {
  return {
    query: "", game: "Pokemon", running: false, searched: false, done: 0, total: 0,
    results: [],                                          // { key, storeName, listing, score }
    hidden: 0,
    failures: [],                                         // { storeName, message }
    selected: new Set(),
    rules: { name: "", required: [], excluded: [...BASE_EXCLUDED], maxPrice: "", language: "English", stores: new Set() },
    touched: new Set(),                                   // rule fields the user edited: suggestions leave them alone
    saving: false,
    errors: null,
  };
}

RENDERERS.vigiar = function viewNewWatch() {
  if (!state.wizard) state.wizard = newWizard();
  const wizard = state.wizard;
  const root = h("div", { class: "wizard" });

  const render = () => keepFocus(root, () => {
    const step = wizard.selected.size > 0 ? 3 : wizard.searched ? 2 : 1;
    root.replaceChildren(
      h("a", { class: "back-link", href: routeHash("a-vigiar") }, icon("back"), "Cancelar"),
      h("header", { class: "page-head" }, h("div", { class: "page-title" }, h("h1", {}, "Vigiar um produto"),
        h("ol", { class: "steps" }, [["Procurar", 1], ["Escolher o produto certo", 2], ["Preço e lojas", 3]].map(([label, number]) =>
          h("li", { class: number < step ? "done" : number === step ? "on" : "" },
            h("span", { class: "step-num" }, number < step ? icon("check") : number), label))))),
      h("div", { class: "wizard-layout" },
        h("section", { class: "wizard-main", "aria-label": "Resultados" }, wizardSearch(wizard, render), wizardResults(wizard, render)),
        h("aside", { class: "wizard-side" }, wizardRules(wizard, render))));
  });
  render();
  return root;
};

function wizardSearch(wizard, render) {
  const input = h("input", {
    type: "search", value: wizard.query, placeholder: "ex.: 30th booster bundle", "aria-label": "O que procuras", "data-keep-focus": "wizard-query",
    oninput: (event) => { wizard.query = event.target.value; },
    onkeydown: (event) => { if (event.key === "Enter") runWizardSearch(wizard, render); },
  });
  setTimeout(() => { if (!wizard.searched && document.activeElement === document.body) input.focus(); }, 0);
  const game = h("select", { "aria-label": "Jogo", onchange: (event) => { wizard.game = event.target.value; } },
    Object.entries(GAMES).map(([value, label]) => h("option", { value, selected: wizard.game === value }, label)));

  return h("div", { class: "wizard-search" },
    h("div", { class: "wizard-search-row" },
      h("label", { class: "search-field big" }, icon("search"), input),
      h("label", { class: "select-pill big" }, h("span", { class: "select-label" }, "Jogo"), game, icon("down", "select-caret")),
      button(wizard.running ? "A procurar…" : "Procurar", { kind: "primary", disabled: wizard.running, onClick: () => runWizardSearch(wizard, render) })),
    h("p", { class: "muted small" }, "Procura em todas as lojas ativas ao mesmo tempo. Escreve como as lojas escrevem (evita “pokemon”: há lojas que não o põem nos títulos)."));
}

async function runWizardSearch(wizard, render) {
  const query = wizard.query.trim();
  if (!query || wizard.running) return;
  const stores = enabledStores().filter((store) => store.platform !== "Manual");
  if (stores.length === 0) { toast("Não há lojas ativas para procurar.", "error"); return; }

  Object.assign(wizard, { running: true, searched: true, done: 0, total: stores.length, results: [], hidden: 0, failures: [], selected: new Set(), errors: null });
  wizard.rules.stores = new Set();
  wizard.touched.delete("stores");
  if (!wizard.touched.has("language")) wizard.rules.language = /\b(jp|japanese|japones|japonês)\b/i.test(query) ? "Japanese" : "English";
  render();

  const queryWords = wordsOf(query).filter((word) => !STOP_WORDS.has(word));
  const queue = [...stores];
  await Promise.all(Array.from({ length: WIZARD_WORKERS }, async () => {
    while (queue.length && state.wizard === wizard) {
      const store = queue.shift();
      try {
        const listings = await post("/store/probe", { store: storeToSaveRequest(store, null), query });
        const scored = listings.map((listing) => {
          const titleWords = new Set(wordsOf(listing.title));
          const hits = queryWords.filter((word) => titleWords.has(word)).length;
          return { key: `${store.name}|${listing.url}`, storeName: store.name, listing, score: queryWords.length ? hits / queryWords.length : 1 };
        });
        const otherLanguage = wizard.rules.language === "English" ? /\b(jp|japanese|japones|japonês|korean|chinese|es|espanol|español)\b/i : /\b(en|english|ingles|inglês)\b/i;
        for (const item of scored) item.otherLanguage = otherLanguage.test(item.listing.title);
        const relevant = scored.filter((item) => item.score >= 0.5).sort(byRelevance).slice(0, 6);
        wizard.hidden += scored.length - relevant.length;
        wizard.results.push(...relevant);
        // Pre-select the obvious ones: every word of the search, and not another language.
        for (const item of relevant) if (item.score === 1 && !item.otherLanguage) wizard.selected.add(item.key);
      } catch (error) {
        wizard.failures.push({ storeName: store.name, message: errorText(error) });
      }
      wizard.done++;
      if (state.wizard === wizard) { suggestRules(wizard); render(); }
    }
  }));
  wizard.running = false;
  if (state.wizard === wizard) { suggestRules(wizard); render(); }
}

/** Fills the rules from the chosen results, leaving alone whatever the user already edited. */
function suggestRules(wizard) {
  const chosen = wizard.results.filter((item) => wizard.selected.has(item.key));
  const others = wizard.results.filter((item) => !wizard.selected.has(item.key));
  const rules = wizard.rules;
  const titles = chosen.map((item) => item.listing.title);

  if (!wizard.touched.has("name")) rules.name = chosen.length ? suggestName(titles) : titleCase(wizard.query);

  if (!wizard.touched.has("required")) {
    const words = wordsOf(wizard.query).filter((word) => !STOP_WORDS.has(word) && word.length > 1);
    const kept = titles.length ? words.filter((word) => titles.every((title) => containsWord(title, word))) : words;
    // Join neighbours that always appear together ("booster bundle").
    const phrases = [];
    for (const word of kept) {
      const previous = phrases[phrases.length - 1];
      const joined = previous ? `${previous} ${word}` : null;
      const plainWords = !/\d/.test(word) && !/\d/.test(previous || "");
      if (joined && plainWords && words.join(" ").includes(joined) && titles.every((title) => containsWord(title, joined))) phrases[phrases.length - 1] = joined;
      else phrases.push(word);
    }
    rules.required = phrases;
  }

  if (!wizard.touched.has("excluded")) {
    const extra = DISCRIMINATORS.filter((word) => others.some((item) => containsWord(item.listing.title, word)) && !titles.some((title) => containsWord(title, word)));
    rules.excluded = [...new Set([...BASE_EXCLUDED, ...extra])];
  }

  if (!wizard.touched.has("maxPrice")) {
    const prices = chosen.map((item) => item.listing.price).filter((price) => price !== null && price !== undefined).sort((a, b) => a - b);
    rules.maxPrice = prices.length ? Math.ceil(prices[Math.floor((prices.length - 1) / 2)]) : "";
  }

  if (!wizard.touched.has("stores")) rules.stores = new Set(chosen.map((item) => item.storeName));
}

/** Best wizard results first: all the words, the right language, in stock, cheaper. */
const byRelevance = (a, b) => (b.score - a.score) || (a.otherLanguage - b.otherLanguage) || (b.listing.inStock - a.listing.inStock) || byPrice(a.listing, b.listing);

const titleCase = (text) => text.trim().split(/\s+/).map((word) => (/\d/.test(word) || word.length < 3 ? word : word[0].toUpperCase() + word.slice(1))).join(" ");

/** The cleanest of the chosen titles: no "Pokémon TCG:" prefix, no language marker, no store noise. */
function suggestName(titles) {
  const cleaned = titles.map((title) => title
    .replace(/^(pok[eé]mon\s*(tcg)?|pkm|tcg)\s*[:\-–—]?\s*/i, "")
    .replace(/\s*[([]?\b(en|eng|english|ingl[eê]s|inglesa?)\b[)\]]?\s*/gi, " ")
    .replace(/\s*[-–—|]\s*$/g, "")
    .replace(/\s{2,}/g, " ")
    .trim()).filter(Boolean);
  return cleaned.sort((a, b) => a.length - b.length)[0] || titles[0] || "";
}

function wizardResults(wizard, render) {
  if (!wizard.searched) {
    return h("div", { class: "card soft" }, emptyState("search", "Começa por procurar o produto.",
      "Vês o que cada loja tem, marcas os resultados certos e as regras são sugeridas a partir deles."));
  }
  const storesWithResults = new Set(wizard.results.map((item) => item.storeName));
  const sorted = wizard.results.slice().sort(byRelevance);

  return h("div", { class: "wizard-results" },
    h("div", { class: "section-head" },
      h("h2", {}, wizard.running ? `A procurar… ${wizard.done} de ${wizard.total} lojas` : `Encontrado em ${storesWithResults.size} ${storesWithResults.size === 1 ? "loja" : "lojas"}`),
      wizard.results.length ? h("span", { class: "section-note" }, "Marca os que são o produto que queres. ", h("strong", {}, `${wizard.selected.size} escolhidos`)) : null),
    wizard.running ? h("div", { class: "bar wide" }, h("span", { style: `width:${Math.round((wizard.done / Math.max(1, wizard.total)) * 100)}%` })) : null,
    sorted.length
      ? h("div", { class: "pick-grid" }, sorted.map((item) => pickCard(wizard, item, render)))
      : wizard.running ? null : h("div", { class: "card soft" }, emptyState("search", "Nenhuma loja tem algo parecido.", "Experimenta menos palavras ou outra forma de escrever.")),
    !wizard.running && (wizard.hidden || wizard.failures.length)
      ? h("p", { class: "muted small" },
        wizard.hidden ? `${wizard.hidden} resultado(s) escondido(s) por não terem as palavras da pesquisa. ` : null,
        wizard.failures.length ? `Sem resposta: ${wizard.failures.map((failure) => failure.storeName).join(", ")}.` : null)
      : null);
}

function pickCard(wizard, item, render) {
  const { listing } = item;
  const checked = wizard.selected.has(item.key);
  const toggle = (event) => {
    if (event.target.checked) wizard.selected.add(item.key); else wizard.selected.delete(item.key);
    suggestRules(wizard);
    render();
  };
  return h("label", { class: `pick-card ${checked ? "on" : ""}` },
    h("div", { class: "pick-art" },
      productArt({ imageUrl: listing.imageUrl, game: wizard.game, title: listing.title, query: wizard.query, size: "card" }),
      h("input", { type: "checkbox", class: "pick-check", checked, "aria-label": `${listing.title} (${item.storeName})`, onchange: toggle })),
    h("div", { class: "pick-store" }, item.storeName),
    h("div", { class: "pick-title", title: listing.title }, listing.title),
    h("div", { class: "pick-foot" },
      h("span", { class: "mono price" }, money(listing.price)),
      h("span", { class: listing.inStock ? "accent small" : "muted small" }, listing.inStock ? "Em stock" : "Esgotado")));
}

/** Editable list of words: removable chips and a "+ palavra" input. */
function keywordEditor(words, tone, onChange, label) {
  const container = h("div", { class: "kw-row editable", role: "group", "aria-label": label });
  const render = () => {
    const input = h("input", {
      type: "text", class: "kw-input", placeholder: "+ palavra", "aria-label": `Adicionar a “${label}”`, "data-keep-focus": `kw-${slugify(label)}`,
      onkeydown: (event) => {
        if ((event.key === "Enter" || event.key === ",") && input.value.trim()) {
          event.preventDefault();
          words.push(...splitList(input.value).filter((word) => !words.includes(word)));
          onChange(words);
          render();
          container.querySelector(".kw-input")?.focus();
        } else if (event.key === "Backspace" && !input.value && words.length) {
          words.pop();
          onChange(words);
          render();
          container.querySelector(".kw-input")?.focus();
        }
      },
      onblur: () => { if (input.value.trim()) { words.push(...splitList(input.value).filter((word) => !words.includes(word))); onChange(words); render(); } },
    });
    container.replaceChildren(...words.map((word, index) => h("span", { class: `kw ${tone}` }, word,
      h("button", { type: "button", class: "kw-remove", "aria-label": `Tirar “${word}”`, onclick: () => { words.splice(index, 1); onChange(words); render(); } }, "×"))), input);
  };
  render();
  return container;
}

function wizardRules(wizard, render) {
  const rules = wizard.rules;
  const disabled = !wizard.searched;
  const chosen = wizard.results.filter((item) => wizard.selected.has(item.key));
  const prices = chosen.map((item) => item.listing.price).filter((price) => price !== null && price !== undefined);
  const low = prices.length ? Math.min(...prices) : null;
  const high = prices.length ? Math.max(...prices) : null;
  const max = Number(rules.maxPrice) || 0;
  const touch = (name) => wizard.touched.add(name);

  const info = h("p", { class: "muted small price-info" });
  const renderInfo = (value) => {
    const count = new Set(chosen.filter((item) => item.listing.inStock && item.listing.price !== null && item.listing.price <= value).map((item) => item.storeName)).size;
    info.replaceChildren();
    append(info, [prices.length
      ? [`Nos escolhidos vês `, h("span", { class: "mono strong" }, low === high ? money(low) : `${money(low)} – ${money(high)}`), `. `,
        value ? [`Com ${money(value)} avisava já em `, h("strong", {}, `${count} ${count === 1 ? "loja" : "lojas"}`), "."] : "Escolhe o preço máximo."]
      : "Escolhe resultados para ver os preços."]);
  };
  renderInfo(max);

  const sliderMax = Math.max(10, Math.ceil((high || max || 50) * 1.6));
  const priceInput = h("input", {
    type: "number", min: 0, step: "0.01", value: rules.maxPrice, class: "mono price-input", "aria-label": "Preço máximo em euros", disabled, "data-keep-focus": "wizard-price",
    oninput: (event) => { rules.maxPrice = event.target.value; touch("maxPrice"); slider.value = event.target.value; renderInfo(Number(event.target.value) || 0); },
  });
  const slider = h("input", {
    type: "range", min: 0, max: sliderMax, step: 1, value: max || 0, "aria-label": "Preço máximo", disabled,
    oninput: (event) => { rules.maxPrice = event.target.value; touch("maxPrice"); priceInput.value = event.target.value; renderInfo(Number(event.target.value) || 0); },
  });

  const storeChips = h("div", { class: "store-picks", role: "group", "aria-label": "Vigiar em" },
    enabledStores().filter((store) => store.platform !== "Manual").map((store) => {
      const on = rules.stores.has(store.name);
      return h("button", {
        type: "button", class: `pick-chip ${on ? "on" : ""}`, "aria-pressed": on ? "true" : "false", disabled,
        onclick: (event) => {
          if (rules.stores.has(store.name)) rules.stores.delete(store.name); else rules.stores.add(store.name);
          touch("stores");
          const now = rules.stores.has(store.name);
          event.currentTarget.classList.toggle("on", now);
          event.currentTarget.setAttribute("aria-pressed", now ? "true" : "false");
        },
      }, store.name);
    }));

  const save = async () => {
    wizard.errors = null;
    const name = rules.name.trim();
    let id = slugify(name || wizard.query) || "produto";
    const taken = new Set(state.tasks.map((task) => task.id.toLowerCase()));
    for (let suffix = 2; taken.has(id.toLowerCase()); suffix++) id = `${slugify(name || wizard.query).slice(0, 36)}-${suffix}`;
    wizard.saving = true;
    render();
    try {
      await post("/stocktask/save", {
        isNew: true, id, name, enabled: true, storeNames: [...rules.stores], searchQuery: wizard.query.trim(),
        requiredKeywords: rules.required, excludedKeywords: rules.excluded, maxPrice: Number(rules.maxPrice) || 0,
        language: rules.language, game: wizard.game,
      });
      await reloadConfiguration();
      state.wizard = null;
      toast(`A vigiar “${name}”`, "success");
      post("/monitor/run").then(() => setTimeout(refresh, 700)).catch(() => { /* the next cycle will do */ });
      go("produto", id);
    } catch (error) {
      wizard.saving = false;
      wizard.errors = error.errors || [error.message];
      render();
    }
  };

  const openFullForm = () => openTaskDrawer({
    id: "", name: rules.name, enabled: true, searchQuery: wizard.query, requiredKeywords: rules.required, excludedKeywords: rules.excluded,
    maxPrice: rules.maxPrice, storeNames: [...rules.stores], language: rules.language, game: wizard.game,
  }, true);

  return h("div", { class: `card rules-panel ${disabled ? "disabled" : ""}` },
    h("h2", {}, "Regras sugeridas"),
    h("p", { class: "muted small" }, disabled ? "Aparecem depois da pesquisa." : "Tiradas dos produtos que escolheste. Ajusta se for preciso."),
    field("Nome", textInput(rules.name, (value) => { rules.name = value; touch("name"); }, { disabled, placeholder: "ex.: 30th Celebration Booster Bundle", "data-keep-focus": "wizard-name" })),
    field("Tem de ter todas", keywordEditor(rules.required, "accent", () => touch("required"), "Tem de ter todas"),
      "Alternativas com | (booster box|booster display) e * para o início de uma palavra (op* apanha OP-15)."),
    field("Ignorar se tiver", keywordEditor(rules.excluded, "red", () => touch("excluded"), "Ignorar se tiver")),
    h("div", { class: "field" },
      h("div", { class: "price-head" }, h("span", { class: "field-label" }, "Preço máximo"), h("label", { class: "price-box" }, priceInput, h("span", { "aria-hidden": "true" }, "€"))),
      slider, info),
    field("Idioma", segmented([["English", "Inglês"], ["Japanese", "Japonês"]], rules.language, (value) => { rules.language = value; touch("language"); }, "Idioma")),
    field("Vigiar em", storeChips, rules.stores.size ? "As lojas onde encontrámos o produto já vêm marcadas." : "Nenhuma marcada = todas as lojas ativas."),
    wizard.errors ? errorList(wizard.errors) : null,
    h("div", { class: "rules-actions" },
      button(wizard.saving ? "A guardar…" : "Começar a vigiar", { kind: "primary big block", disabled: disabled || wizard.saving, onClick: save }),
      button("Abrir no formulário completo", { kind: "block", disabled, onClick: openFullForm })));
}
