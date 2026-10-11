"use strict";

/* ==========================================================================
   A vigiar (the watch list), the product page and the "Vigiar produto" wizard.
   A "produto" is a stock task: usually one set (Chaos Rising) or one product (30th ETB), with the words it must /
   must not have, the product kinds wanted, the price rule (MSRP + margin, or a fixed maximum) and the stores.
   ========================================================================== */

/* ----- A vigiar ----- */

RENDERERS["a-vigiar"] = function viewWatchlist() {
  const insights = state.tasks.map(taskInsight);
  const storeCount = new Set(insights.flatMap((insight) => insight.stores.map((store) => store.name))).size;
  const inGame = insights.filter((insight) => inGameTab(insight.task.game));
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
    pageHead("A vigiar", `${state.tasks.length} ${state.tasks.length === 1 ? "produto" : "produtos"} em ${storeCount} ${storeCount === 1 ? "loja" : "lojas"}. Cada cartão mostra o melhor preço de cada tipo (ETB, Bundle, Display…).`, [
      watchMenu(),
      linkButton("Vigiar produto", routeHash("vigiar"), { iconName: "plus", kind: "primary" }),
    ]),
    gameTabs(insights.map((insight) => insight.task.game), "produtos"),
    h("div", { class: "toolbar wrap" },
      segmented([["all", "Todos", null], ["stock", "Com ofertas", counts.stock], ["searching", "À procura", counts.searching], ["paused", "Em pausa", counts.paused]],
        state.watchStatus, (value) => { state.watchStatus = value; renderGrid(); }, "Estado"),
      h("div", { class: "toolbar-end" }, searchField(state.watchQuery, "Filtrar", (value) => { state.watchQuery = value; renderGrid(); }, "watch-search", "Filtrar produtos"))),
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
      item("Retomar todos", "play", () => setTaskEnabled(null, true), tasks.length === 0),
      item("Pausar todos", "pause", () => setTaskEnabled(null, false), tasks.length === 0),
      item("Passar todos para MSRP + 10%", "check", switchAllToMsrp, !tasks.some((task) => task.priceRule !== "Msrp" && gameKey(task.game) === "Pokemon")),
      h("hr"),
      item("Formulário completo (novo)", "edit", () => openTaskDrawer(null)),
      item("Exportar JSON", "download", exportTasks, tasks.length === 0),
      item("Importar JSON", "upload", openTaskImportDrawer),
      h("hr"),
      item("Apagar todos", "trash", deleteAllTasks, tasks.length === 0, true)));
}

/** Moves every Pokémon product still on a fixed maximum to "MSRP + 10%" (the fixed maximum is dropped). */
async function switchAllToMsrp() {
  const targets = state.tasks.filter((task) => task.priceRule !== "Msrp" && gameKey(task.game) === "Pokemon");
  if (!await confirmDialog({
    title: `Passar ${targets.length} ${targets.length === 1 ? "produto" : "produtos"} para MSRP + 10%?`,
    message: "Cada tipo (ETB, Bundle, Display, Booster…) passa a ser comparado com o seu MSRP, mais 10%. O preço máximo fixo deixa de contar; tipos sem MSRP deixam de avisar. Podes mudar cada um em “Editar regras”.",
    confirmLabel: "Passar para MSRP",
  })) return;
  let failed = 0;
  for (const task of targets) {
    try {
      await post("/stocktask/save", { ...taskToJson(task), isNew: false, priceRule: "Msrp", msrpTolerancePercent: 10, maxPrice: 0 });
    } catch {
      failed++;
    }
  }
  await reloadConfiguration();
  toast(failed ? `${targets.length - failed} atualizados, ${failed} com erro` : `${targets.length} produtos a usar MSRP + 10%`, failed ? "error" : "success");
}

const STATUS_TONES = { stock: "accent", searching: "", waiting: "", error: "red", paused: "muted" };

/** One watched product: what it is, its rule, and per product kind the best offer now. */
function watchCard(insight) {
  const { task } = insight;
  const link = routeHash("produto", task.id);
  const kinds = kindBreakdown(insight);
  const shown = kinds.slice(0, 5);
  const offerCount = new Set(insight.matches.map((listing) => listing.url)).size;
  const statusText = insight.status === "stock" ? `${offerCount} ${offerCount === 1 ? "oferta" : "ofertas"}` : STATUS_LABELS[insight.status];

  return h("article", { class: `watch-card ${insight.status}` },
    h("header", { class: "watch-head" },
      h("a", { href: link, tabindex: "-1", "aria-hidden": "true" }, productArt({ imageUrl: insight.photo, game: task.game, title: task.name, query: task.searchQuery, size: "thumb" })),
      h("div", { class: "watch-head-text" },
        h("h3", { class: "watch-card-title" }, h("a", { href: link }, task.name || task.id)),
        h("div", { class: "watch-meta" }, gamePill(task.game), h("span", { class: `status-dot ${insight.status}` }), h("span", { class: STATUS_TONES[insight.status] }, statusText))),
      switchInput(task.enabled, (checked) => setTaskEnabled(task.id, checked), task.enabled ? `Pausar ${task.name}` : `Retomar ${task.name}`)),
    h("div", { class: "rule-line" }, icon("check", "rule-icon"), h("span", {}, priceRuleText(task, true), " · ", kindsText(task))),
    shown.length
      ? h("ul", { class: "kind-rows" }, shown.map((group) => kindRow(group)))
      : h("p", { class: "kind-empty" }, insight.status === "paused" ? "Em pausa: não está a ser pesquisado." : insight.reports.length ? "Nenhuma loja tem este produto agora." : "À espera da primeira verificação."),
    h("footer", { class: "watch-foot" },
      kinds.length > shown.length ? h("span", { class: "muted small" }, `+${kinds.length - shown.length} tipos`) : h("span", { class: "muted small" }, `${insight.stores.length} lojas · ${insight.lastChecked ? relative(insight.lastChecked) : "por verificar"}`),
      h("a", { class: "text-link small", href: link }, "Ver tudo", icon("chevron"))));
}

/** One product kind of a card: the best price within the rule (or the cheapest above it), and how many stores. */
function kindRow(group) {
  let price = null, tone = "dim", note;
  if (group.best) { price = group.best.price; tone = "accent"; note = group.best.storeName; }
  else if (group.above) { price = group.above.price; tone = "amber"; note = `acima · ${shortStoreName(group.above.storeName)}`; }
  else note = group.soldOut ? `esgotado em ${group.storeCount} ${group.storeCount === 1 ? "loja" : "lojas"}` : "sem stock";
  return h("li", { class: `kind-row ${group.best ? "ok" : ""}` },
    h("span", { class: "kind-name" }, group.label),
    h("span", { class: "kind-note" }, note),
    h("span", { class: `kind-price mono ${tone}` }, price !== null ? money(price) : "—"));
}

/* ----- Produto ----- */

RENDERERS.produto = function viewProduct() {
  const task = findTask(state.route.param);
  const back = h("a", { class: "back-link", href: routeHash("a-vigiar") }, icon("back"), "A vigiar");
  if (!task) {
    return [back, h("div", { class: "card soft" }, emptyState("eye", "Este produto já não está a ser vigiado.", null, linkButton("Ver todos", routeHash("a-vigiar"))))];
  }

  const insight = taskInsight(task);
  const kinds = kindBreakdown(insight);
  const photoListing = [...insight.matches, ...insight.aboveMax, ...insight.tracked].find((listing) => safeHref(listing.imageUrl));
  const offerCount = new Set(insight.matches.map((listing) => listing.url)).size;
  const statusChip = insight.status === "stock"
    ? h("span", { class: "status-chip-lg stock" }, `${offerCount} ${offerCount === 1 ? "oferta" : "ofertas"} ao teu preço`)
    : h("span", { class: `status-chip-lg ${insight.status}` }, STATUS_LABELS[insight.status]);

  const keywordChips = (words, tone) => words.length
    ? h("div", { class: "kw-row" }, words.map((word) => h("span", { class: `kw small ${tone}` }, word)))
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
        h("p", { class: "rule-sentence" }, "Avisa quando aparecer ", h("strong", {}, "em stock"), ", em ", h("strong", {}, (TASK_LANGUAGES[task.language]?.[1] || "Inglês").toLowerCase()),
          " e ", h("strong", {}, priceRuleText(task).replace("Até", "até")), ". Tipos: ", h("strong", {}, kindsText(task)), "."),
        task.priceRule === "Msrp" ? limitsTable(task, kinds) : null,
        h("dl", { class: "rules" },
          h("dt", {}, "Pesquisa"), h("dd", {}, h("code", { class: "kw mono" }, task.searchQuery)),
          h("dt", {}, "Tem de ter"), h("dd", {}, keywordChips(task.requiredKeywords || [], "accent")),
          h("dt", {}, "Ignora se tiver"), h("dd", {}, keywordChips(task.excludedKeywords || [], "red"))),
        h("div", { class: "button-row" },
          button("Verificar agora", { iconName: "bolt", onClick: runNow, kbd: "R" }),
          button("Editar regras", { iconName: "edit", onClick: () => openTaskDrawer(task) }),
          task.enabled
            ? button("Pausar", { iconName: "pause", onClick: () => setTaskEnabled(task.id, false) })
            : button("Retomar", { iconName: "play", kind: "primary", onClick: () => setTaskEnabled(task.id, true) }),
          button("Duplicar", { iconName: "copy", onClick: () => openTaskDrawer({ ...task, id: "", name: `${task.name} (cópia)` }, true) }),
          button("Apagar", { kind: "text-danger", onClick: () => deleteTask(task) })))),

    h("div", { class: "split" },
      h("section", { class: "split-main section", "aria-labelledby": "found-h" }, productResults(insight, kinds)),
      h("aside", { class: "split-side" }, productAlertsCard(insight), productStoresCard(insight))),
  ];
};

/** The price limit of each product kind under the MSRP rule (the kinds wanted, or the ones found). */
function limitsTable(task, kinds) {
  const codes = (task.kinds || []).length ? task.kinds : [...new Set([...kinds.map((group) => group.kind), "etb", "booster-bundle", "booster-display", "booster-pack"])];
  const rows = codes.map((code) => ({ code, msrp: msrpOf(code, task.game), limit: limitFor(task, code) }))
    .filter((row) => row.limit !== null).sort((a, b) => kindOrder(a.code) - kindOrder(b.code)).slice(0, 8);
  if (!rows.length) return null;
  return h("div", { class: "limits" }, rows.map((row) => h("div", { class: "limit" },
    h("span", {}, kindLabel(row.code)),
    h("strong", { class: "mono" }, `até ${money(row.limit)}`),
    row.msrp ? h("small", {}, `MSRP ${money(row.msrp)}`) : h("small", {}, "sem MSRP"))));
}

function productResults(insight, kinds) {
  const { task } = insight;
  const only = state.productOnlyCandidates;
  const byKind = state.productGroup !== "store";
  const total = insight.reports.reduce((sum, report) => sum + report.listings.length, 0);
  const candidateCount = insight.reports.reduce((sum, report) => sum + report.listings.filter((listing) => !isIrrelevant(listing) && listing.outcome !== "OtherKind").length, 0);
  const keep = (listing) => !only || (!isIrrelevant(listing) && listing.outcome !== "OtherKind");
  const failed = insight.reports.filter((report) => !report.succeeded);
  const waiting = insight.stores.filter((store) => !insight.reports.some((report) => report.storeName === store.name)).map((store) => store.name);
  const last = insight.lastChecked;

  let body;
  if (byKind) {
    const groups = new Map();
    for (const report of insight.reports.filter((item) => item.succeeded)) {
      for (const listing of report.listings.filter(keep)) {
        const kind = listing.kind || "other";
        if (!groups.has(kind)) groups.set(kind, []);
        groups.get(kind).push({ ...listing, storeName: report.storeName, checkedAt: report.checkedAt });
      }
    }
    const ordered = [...groups.entries()].map(([kind, listings]) => ({ kind, listings, info: kinds.find((group) => group.kind === kind) }))
      .sort((a, b) => (b.listings.some((listing) => isMatch(listing.outcome)) - a.listings.some((listing) => isMatch(listing.outcome))) || kindOrder(a.kind) - kindOrder(b.kind));
    body = ordered.length
      ? h("div", { class: "store-blocks" }, ordered.map((group) => kindBlock(task, group, insight)))
      : null;
  } else {
    const withResults = insight.reports.filter((report) => report.succeeded && report.listings.some(keep))
      .sort((a, b) => (b.listings.some((listing) => isMatch(listing.outcome)) - a.listings.some((listing) => isMatch(listing.outcome))) || a.storeName.localeCompare(b.storeName, "pt"));
    body = withResults.length ? h("div", { class: "store-blocks" }, withResults.map((report) => storeBlock(task, report, keep, insight))) : null;
  }
  const empty = insight.reports.filter((report) => report.succeeded && !report.listings.some(keep)).map((report) => report.storeName);

  return [
    h("div", { class: "section-head" },
      h("div", {},
        h("h2", { id: "found-h" }, "O que as lojas têm"),
        h("p", { class: "section-sub" }, last ? `Última pesquisa ${relative(last)}. Cada resultado diz porque é que avisou ou não.` : "Ainda não houve pesquisa para este produto.")),
      total ? h("div", { class: "button-row" },
        segmented([["kind", "Por tipo"], ["store", "Por loja"]], byKind ? "kind" : "store",
          (value) => { state.productGroup = value; writePreference("productGroup", value); renderView(true); }, "Agrupar"),
        segmented([["candidates", "Só candidatos", candidateCount], ["all", "Tudo", total]], only ? "candidates" : "all",
          (value) => { state.productOnlyCandidates = value === "candidates"; writePreference("onlyProducts", state.productOnlyCandidates); renderView(true); }, "Resultados")) : null),
    body || h("div", { class: "card soft" }, emptyState("search", insight.reports.length ? "Nenhuma loja encontrou este produto." : "À espera da primeira verificação.",
      insight.reports.length ? "Experimenta outro texto de pesquisa em “Editar regras”." : "Carrega em “Verificar agora” para não esperar pelo próximo ciclo.")),
    failed.length ? h("div", { class: "callout red" }, icon("alert"), h("div", {},
      h("strong", {}, `${failed.length} ${failed.length === 1 ? "loja deu" : "lojas deram"} erro: `),
      failed.map((report, index) => [index ? " · " : "", h("span", { title: report.error || "" }, report.storeName)]))) : null,
    empty.length ? h("p", { class: "muted small" }, `Sem resultados ${only ? "relevantes " : ""}em: ${empty.join(", ")}.`) : null,
    waiting.length ? h("p", { class: "muted small" }, `À espera da primeira pesquisa: ${waiting.join(", ")}.`) : null,
  ];
}

/** Results of one product kind, across stores, cheapest offer first. */
function kindBlock(task, group, insight) {
  const listings = group.listings.slice()
    .sort((a, b) => (isMatch(b.outcome) - isMatch(a.outcome)) || ((b.inStock) - (a.inStock)) || byPrice(a, b)).slice(0, 15);
  const hasMatch = group.listings.some((listing) => isMatch(listing.outcome));
  const msrp = group.info?.msrp ?? group.listings.find((listing) => listing.msrp)?.msrp ?? null;
  const limit = group.info?.limit ?? null;
  return h("article", { class: `store-block ${hasMatch ? "match" : ""}` },
    h("header", { class: "store-block-head" },
      h("strong", {}, kindLabel(group.kind)),
      hasMatch ? h("span", { class: "tag accent small" }, "ao teu preço") : null,
      h("span", { class: "store-block-meta" },
        msrp ? `MSRP ${money(msrp)}` : "sem MSRP",
        limit ? ` · avisa até ${money(limit)}` : "",
        ` · ${group.listings.length} ${group.listings.length === 1 ? "resultado" : "resultados"}`)),
    h("ul", { class: "result-rows" }, listings.map((listing) => resultRow(task, listing, insight, true))));
}

/** Results of one store (the "Por loja" view). */
function storeBlock(task, report, keep, insight) {
  const store = findStore(report.storeName);
  const listings = report.listings.filter(keep).slice()
    .sort((a, b) => (isMatch(b.outcome) - isMatch(a.outcome)) || ((b.outcome === "AboveMaxPrice") - (a.outcome === "AboveMaxPrice")) || byPrice(a, b))
    .slice(0, 12);
  const kind = report.listings.some((listing) => isMatch(listing.outcome)) ? "match"
    : report.listings.some((listing) => listing.outcome === "AboveMaxPrice" && listing.inStock) ? "above" : "none";

  return h("article", { class: `store-block ${kind}` },
    h("header", { class: "store-block-head" },
      h("span", { class: `sq ${kind === "match" ? "ok" : kind === "above" ? "slow" : "off"}`, "aria-hidden": "true" }),
      h("strong", {}, report.storeName),
      store ? h("span", { class: "tag small" }, PLATFORM_LABELS[store.platform] || store.platform) : null,
      h("span", { class: "store-block-meta mono" }, `${relative(report.checkedAt)} · ${durationText(report.durationMs)}`)),
    h("ul", { class: "result-rows" }, listings.map((listing) => resultRow(task, { ...listing, storeName: report.storeName }, insight, false))));
}

function resultRow(task, listing, insight, showStore) {
  const meta = [];
  if (showStore) meta.push(listing.storeName);
  else meta.push(listing.kindLabel || kindLabel(listing.kind));
  meta.push(listing.inStock ? "em stock" : "esgotado", (LANGUAGES[listing.language] || listing.language).toLowerCase());
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
      h("span", { class: "lowest" }, msrpPill(listing.price, listing.msrp), showLowest ? ` mín. ${money(listing.lowestPrice)}` : null)),
    verdict(task, listing, insight));
}

/** Why a result alerted or not, in words — with the exact word when it can be told from the title. */
function verdict(task, listing, insight) {
  const title = normalizeText(listing.title);
  const words = (list) => (list || []).flatMap((keyword) => keyword.split("|")).map((word) => word.replace(/\*/g, "").trim()).filter(Boolean);
  switch (listing.outcome) {
    case "Match": {
      const alerted = insight.alerts.some((alert) => alert.url === listing.url);
      return h("span", { class: "verdict accent solid" }, alerted ? "Avisado · ao teu preço" : "Ao teu preço");
    }
    case "MatchLanguageToConfirm":
      return h("span", { class: "verdict amber" }, "Ao teu preço · confirma o idioma");
    case "AboveMaxPrice": {
      const limit = listing.priceLimit ?? task.maxPrice;
      return h("span", { class: "verdict amber" }, listing.price && limit ? `Acima · avisa até ${money(limit)}` : "Acima do teu preço");
    }
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
        h("div", { class: "timeline-sub muted", title: alert.title }, alert.title),
        alert.languageToConfirm ? h("div", { class: "timeline-sub amber" }, "Idioma por confirmar") : null,
        safeHref(alert.url) ? h("a", { class: "text-link small", href: alert.url, target: "_blank", rel: "noopener noreferrer" }, "Abrir", icon("external")) : null))));
}

function productStoresCard(insight) {
  const names = insight.task.storeNames || [];
  return h("section", { class: "card side-card", "aria-labelledby": "product-stores-h" },
    h("div", { class: "side-card-head" }, h("h2", { id: "product-stores-h" }, "Lojas vigiadas")),
    insight.stores.length
      ? h("div", { class: "kw-row" }, insight.stores.map((store) => h("span", { class: "kw small" }, store.name)))
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
    rules: { name: "", required: [], excluded: [...BASE_EXCLUDED], maxPrice: "", language: "English", stores: new Set(), priceRule: "Msrp", tolerance: 10, kinds: new Set() },
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

  // Kinds: the ones chosen (a set search keeps ETB, Bundle… and drops the rest); MSRP rule when they all have one.
  if (!wizard.touched.has("kinds")) rules.kinds = new Set(chosen.map((item) => item.listing.kind || "other").filter((kind) => kind !== "other"));
  if (!wizard.touched.has("priceRule")) {
    const withMsrp = chosen.filter((item) => item.listing.msrp);
    rules.priceRule = gameKey(wizard.game) === "Pokemon" && chosen.length > 0 && withMsrp.length === chosen.length ? "Msrp" : "MaxPrice";
  }
  // With the MSRP rule the fixed maximum is only an optional fallback: leave it empty unless the user typed one.
  if (rules.priceRule === "Msrp" && !wizard.touched.has("maxPrice")) rules.maxPrice = "";
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
    h("div", { class: "pick-store" }, item.storeName, h("span", { class: "muted" }, ` · ${listing.kindLabel || kindLabel(listing.kind)}`)),
    h("div", { class: "pick-title", title: listing.title }, listing.title),
    h("div", { class: "pick-foot" },
      h("span", { class: "mono price" }, money(listing.price), " ", msrpPill(listing.price, listing.msrp)),
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
  const touch = (name) => wizard.touched.add(name);
  const msrpMode = rules.priceRule === "Msrp";

  /** The limit of one chosen result under the rules as they are now. */
  const limitOf = (item) => {
    const fixed = Number(rules.maxPrice) || 0;
    if (msrpMode && item.listing.msrp) return Math.round(item.listing.msrp * (100 + (Number(rules.tolerance) || 0))) / 100;
    return fixed > 0 ? fixed : null;
  };

  const info = h("p", { class: "muted small price-info" });
  const renderInfo = () => {
    const ok = chosen.filter((item) => item.listing.inStock && item.listing.price !== null && limitOf(item) !== null && item.listing.price <= limitOf(item));
    const count = new Set(ok.map((item) => item.storeName)).size;
    info.replaceChildren();
    append(info, [prices.length
      ? [`Nos escolhidos vês `, h("span", { class: "mono strong" }, low === high ? money(low) : `${money(low)} – ${money(high)}`), `. `,
        `Com estas regras avisava já em `, h("strong", {}, `${count} ${count === 1 ? "loja" : "lojas"}`), "."]
      : "Escolhe resultados para ver os preços."]);
  };
  renderInfo();

  // Price rule: MSRP + margin per kind, or one fixed maximum.
  const toleranceInput = h("input", {
    type: "number", min: -50, max: 300, step: 1, value: rules.tolerance, class: "mono price-input", "aria-label": "Margem sobre o MSRP em percentagem", disabled, "data-keep-focus": "wizard-tolerance",
    oninput: (event) => { rules.tolerance = event.target.value; touch("tolerance"); renderInfo(); renderLimits(); },
  });
  const priceInput = h("input", {
    type: "number", min: 0, step: "0.01", value: rules.maxPrice, class: "mono price-input", "aria-label": msrpMode ? "Máximo para tipos sem MSRP, em euros" : "Preço máximo em euros",
    disabled, "data-keep-focus": "wizard-price", placeholder: msrpMode ? "—" : "",
    oninput: (event) => { rules.maxPrice = event.target.value; touch("maxPrice"); renderInfo(); renderLimits(); },
  });
  const limits = h("div", { class: "limits compact" });
  const renderLimits = () => {
    const kinds = [...new Set(chosen.map((item) => item.listing.kind).filter(Boolean))].filter((kind) => !rules.kinds.size || rules.kinds.has(kind));
    limits.replaceChildren(...kinds.sort((a, b) => kindOrder(a) - kindOrder(b)).map((kind) => {
      const item = chosen.find((candidate) => candidate.listing.kind === kind);
      const limit = limitOf(item);
      return h("div", { class: "limit" }, h("span", {}, item.listing.kindLabel || kindLabel(kind)),
        h("strong", { class: "mono" }, limit ? `até ${money(limit)}` : "não avisa"),
        h("small", {}, item.listing.msrp ? `MSRP ${money(item.listing.msrp)}` : "sem MSRP"));
    }));
  };
  renderLimits();

  const kindsFound = [...new Set(wizard.results.map((item) => item.listing.kind || "other"))].filter((kind) => kind !== "other").sort((a, b) => kindOrder(a) - kindOrder(b));
  const kindChips = h("div", { class: "store-picks", role: "group", "aria-label": "Tipos de produto" },
    kindsFound.map((kind) => {
      const on = rules.kinds.has(kind);
      return h("button", {
        type: "button", class: `pick-chip ${on ? "on" : ""}`, "aria-pressed": on ? "true" : "false", disabled,
        onclick: () => { if (rules.kinds.has(kind)) rules.kinds.delete(kind); else rules.kinds.add(kind); touch("kinds"); render(); },
      }, kindLabel(kind));
    }));

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

  const request = () => ({
    name: rules.name.trim(), enabled: true, storeNames: [...rules.stores], searchQuery: wizard.query.trim(),
    requiredKeywords: rules.required, excludedKeywords: rules.excluded, maxPrice: Number(rules.maxPrice) || 0,
    priceRule: rules.priceRule, msrpTolerancePercent: Number(rules.tolerance) || 0, kinds: [...rules.kinds],
    language: rules.language, game: wizard.game,
  });

  const save = async () => {
    wizard.errors = null;
    const name = rules.name.trim();
    let id = slugify(name || wizard.query) || "produto";
    const taken = new Set(state.tasks.map((task) => task.id.toLowerCase()));
    for (let suffix = 2; taken.has(id.toLowerCase()); suffix++) id = `${slugify(name || wizard.query).slice(0, 36)}-${suffix}`;
    wizard.saving = true;
    render();
    try {
      await post("/stocktask/save", { isNew: true, id, ...request() });
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

  const openFullForm = () => openTaskDrawer({ id: "", ...request() }, true);

  return h("div", { class: `card rules-panel ${disabled ? "disabled" : ""}` },
    h("h2", {}, "Regras sugeridas"),
    h("p", { class: "muted small" }, disabled ? "Aparecem depois da pesquisa." : "Tiradas dos produtos que escolheste. Ajusta se for preciso."),
    field("Nome", textInput(rules.name, (value) => { rules.name = value; touch("name"); }, { disabled, placeholder: "ex.: 30th Celebration Booster Bundle", "data-keep-focus": "wizard-name" })),
    kindsFound.length ? field("Tipos de produto", kindChips, rules.kinds.size ? "Só avisa destes tipos." : "Nenhum marcado = todos os tipos.") : null,
    h("div", { class: "field" },
      h("span", { class: "field-label" }, "Preço"),
      segmented([["Msrp", "MSRP + margem"], ["MaxPrice", "Preço fixo"]], rules.priceRule, (value) => { rules.priceRule = value; touch("priceRule"); render(); }, "Regra de preço"),
      msrpMode
        ? [h("div", { class: "price-head" }, h("span", { class: "muted small" }, "Avisa até ao MSRP de cada tipo mais"),
          h("label", { class: "price-box" }, toleranceInput, h("span", { "aria-hidden": "true" }, "%"))),
          limits,
          h("div", { class: "price-head" }, h("span", { class: "muted small" }, "Tipos sem MSRP: até (opcional)"),
            h("label", { class: "price-box" }, priceInput, h("span", { "aria-hidden": "true" }, "€")))]
        : h("div", { class: "price-head" }, h("span", { class: "muted small" }, "Avisa até"),
          h("label", { class: "price-box" }, priceInput, h("span", { "aria-hidden": "true" }, "€"))),
      info),
    field("Tem de ter todas", keywordEditor(rules.required, "accent", () => touch("required"), "Tem de ter todas"),
      "Alternativas com | (booster box|booster display) e * para o início de uma palavra (op* apanha OP-15)."),
    field("Ignorar se tiver", keywordEditor(rules.excluded, "red", () => touch("excluded"), "Ignorar se tiver")),
    field("Idioma", segmented([["English", "Inglês"], ["Japanese", "Japonês"]], rules.language, (value) => { rules.language = value; touch("language"); }, "Idioma")),
    field("Vigiar em", storeChips, rules.stores.size ? "As lojas onde encontrámos o produto já vêm marcadas." : "Nenhuma marcada = todas as lojas ativas."),
    wizard.errors ? errorList(wizard.errors) : null,
    h("div", { class: "rules-actions" },
      button(wizard.saving ? "A guardar…" : "Começar a vigiar", { kind: "primary big block", disabled: disabled || wizard.saving, onClick: save }),
      button("Abrir no formulário completo", { kind: "block", disabled, onClick: openFullForm })));
}
