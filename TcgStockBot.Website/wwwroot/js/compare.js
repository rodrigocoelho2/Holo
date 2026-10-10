"use strict";

/* ==========================================================================
   Comparar — the same product in every store: cheapest now, lowest ever, last drop, MSRP; manual regrouping.
   ========================================================================== */

/** Store line colours: the validated dark categorical order (blue, orange, aqua, yellow, magenta, violet). */
const CHART_STORE_COLORS = ["#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#9085e9"];
const WEEK = 7 * 864e5;

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

async function loadCompare() {
  try {
    state.compare = await api("/compare/list");
  } catch (error) {
    state.compare = [];
    toast(error.message, "error");
  }
  if (state.route.view === "comparar") renderView(true);
}

/** Cheapest in stock, runner-up, lowest ever and most recent drop of one product. */
function compareSummary(group) {
  const listings = group.listings;
  const inStock = listings.filter((listing) => listing.currentInStock);
  const lowestEver = listings.reduce((best, listing) => (!best || listing.lowestPrice < best.lowestPrice ? listing : best), null);
  const lastDrop = listings.filter((listing) => listing.lastDropAt).sort((a, b) => new Date(b.lastDropAt) - new Date(a.lastDropAt))[0] || null;
  return { best: inStock[0] || null, second: inStock[1] || null, inStockCount: inStock.length, lowestEver, lastDrop };
}

const shortDate = (iso) => (iso ? new Date(iso).toLocaleDateString("pt-PT", { day: "2-digit", month: "2-digit" }) : "—");

function storeChip(listing, colors, isBest) {
  return h("span", { class: `store-chip ${listing.currentInStock ? "" : "sold-out"} ${isBest ? "best" : ""}`, title: listing.title },
    h("span", { class: "swatch", style: `background:${colors.get(listing.storeName)}` }),
    listing.storeName, h("strong", { class: "mono" }, money(listing.currentPrice)), listing.currentInStock ? null : h("span", { class: "muted" }, "esgotado"));
}

RENDERERS.comparar = function viewCompare() {
  const tableBox = h("div", {});
  const all = state.compare || [];
  const visibleBySize = all.filter((group) => state.compareSingles || group.listings.length >= 2);
  const counts = {
    all: visibleBySize.length,
    stock: visibleBySize.filter((group) => group.listings.some((listing) => listing.currentInStock)).length,
    drops: visibleBySize.filter((group) => group.listings.some((listing) => listing.lastDropAt && Date.now() - new Date(listing.lastDropAt) < WEEK)).length,
  };
  const stores = new Set(all.flatMap((group) => group.listings.map((listing) => listing.storeName)));

  const renderTable = () => {
    const rows = visibleBySize.filter((group) => {
      if (state.compareMode === "stock" && !group.listings.some((listing) => listing.currentInStock)) return false;
      if (state.compareMode === "drops" && !group.listings.some((listing) => listing.lastDropAt && Date.now() - new Date(listing.lastDropAt) < WEEK)) return false;
      return matchesWords(state.compareFilter, group.title, group.listings.map((listing) => `${listing.storeName} ${listing.title}`).join(" "));
    });
    tableBox.replaceChildren(state.compare === null
      ? h("div", { class: "card soft" }, h("p", { class: "muted pad" }, "A carregar…"))
      : rows.length === 0
        ? h("div", { class: "card soft" }, emptyState("compare", all.length === 0 ? "Ainda nada para comparar." : "Nenhum produto corresponde aos filtros.",
          all.length === 0 ? "Os produtos aparecem aqui depois de um ciclo, quando os encontramos em mais do que uma loja." : null))
        : h("div", { class: "card flush" }, compareTable(rows)));
  };
  renderTable();

  return [
    pageHead("Comparar", "O mesmo produto em todas as lojas: quem tem o preço mais baixo agora, o mínimo de sempre e as últimas descidas."),
    h("div", { class: "toolbar wrap" },
      searchField(state.compareFilter, "Filtrar: pitch black, booster bundle…", (value) => { state.compareFilter = value; renderTable(); }, "compare-search", "Filtrar produtos"),
      segmented([["all", "Todos", counts.all], ["stock", "Com stock", counts.stock], ["drops", "Baixaram (7 dias)", counts.drops]], state.compareMode,
        (value) => { state.compareMode = value; renderTable(); }, "Mostrar"),
      labelledSwitch(state.compareSingles, "Incluir os que só estão numa loja", (checked) => { state.compareSingles = checked; renderView(true); }),
      h("span", { class: "muted small toolbar-end" }, `${visibleBySize.length} produtos · ${stores.size} lojas`)),
    tableBox,
  ];
};

function compareTable(groups) {
  return h("div", { class: "table-wrap" }, h("table", { class: "table" },
    h("thead", {}, h("tr", {},
      h("th", {}, "Produto"), h("th", {}, "Lojas"), h("th", { class: "num" }, "Mais barato c/ stock"), h("th", { class: "num" }, "MSRP"),
      h("th", { class: "num" }, "Mínimo de sempre"), h("th", { class: "num" }, "Última descida"), h("th", {}))),
    h("tbody", {}, groups.map((group) => {
      const summary = compareSummary(group);
      const colors = storeColors(group.listings.map((listing) => listing.storeName));
      return h("tr", {},
        h("td", {}, h("div", { class: "group-title" },
          h("strong", {}, group.title),
          h("span", { class: "muted small" }, `${group.listings.length} ${group.listings.length === 1 ? "loja" : "lojas"} · ${summary.inStockCount} com stock`))),
        h("td", {}, h("div", { class: "compare-chips" }, group.listings.map((listing) => storeChip(listing, colors, listing === summary.best)))),
        h("td", { class: "num nowrap" }, summary.best
          ? [h("div", { class: "mono strong" }, money(summary.best.currentPrice)),
            h("div", { class: "muted small" }, summary.best.storeName + (summary.second ? ` · 2.ª +${money(summary.second.currentPrice - summary.best.currentPrice)}` : ""))]
          : pill("Sem stock", "red")),
        h("td", { class: "num nowrap", title: group.msrpLabel ? `Referência: ${group.msrpLabel}` : "Sem preço de referência para este tipo de produto" },
          group.msrp ? [h("div", { class: "mono" }, money(group.msrp)), msrpDelta(summary.best?.currentPrice, group.msrp)] : h("span", { class: "muted" }, "—")),
        h("td", { class: "num nowrap" }, h("div", { class: "mono" }, money(summary.lowestEver?.lowestPrice)),
          summary.lowestEver ? h("div", { class: "muted small" }, `${summary.lowestEver.storeName} · ${shortDate(summary.lowestEver.lowestPriceAt)}`) : null),
        h("td", { class: "num nowrap" }, summary.lastDrop
          ? [h("div", { class: "mono accent" }, `↓ ${money(summary.lastDrop.lastDropTo)}`),
            h("div", { class: "muted small" }, `${summary.lastDrop.storeName} · −${money(summary.lastDrop.lastDropFrom - summary.lastDrop.lastDropTo)} · ${relative(summary.lastDrop.lastDropAt)}`)]
          : h("span", { class: "muted" }, "—")),
        h("td", { class: "actions" }, iconButton("chart", `Comparar lojas de ${group.title}`, () => openCompareGroup(group.key))));
    }))));
}

async function openCompareGroup(key) {
  let group;
  try {
    group = await api(`/compare/get?identifier=${encodeURIComponent(key)}`);
  } catch (error) {
    toast(errorText(error), "error");
    return;
  }

  const summary = compareSummary(group);
  const colors = storeColors(group.listings.map((listing) => listing.storeName));
  const stat = (label, value, note) => h("div", { class: "stat-box small" }, h("span", {}, label), h("strong", { class: "mono" }, value), note ? h("small", {}, note) : null);

  openDrawer({
    title: group.title,
    subtitle: `${group.listings.length} ${group.listings.length === 1 ? "loja" : "lojas"} · ${group.key.startsWith("solo|") ? "separado manualmente" : "agrupado por set e tipo"}`,
    wide: true,
    body: [
      h("div", { class: "stat-boxes five" },
        stat("Mais barato agora", summary.best ? money(summary.best.currentPrice) : "—", summary.best ? summary.best.storeName : "sem stock"),
        stat("Mínimo de sempre", money(summary.lowestEver?.lowestPrice), summary.lowestEver ? `${summary.lowestEver.storeName} · ${dateTime(summary.lowestEver.lowestPriceAt)}` : null),
        stat("MSRP", group.msrp ? money(group.msrp) : "—", group.msrp
          ? [msrpDelta(summary.best?.currentPrice, group.msrp, "o mais barato"), h("div", {}, `${group.msrpLabel} · ${(MSRP_BASIS[group.msrpBasis] || MSRP_BASIS.Estimate)[0].toLowerCase()}`)]
          : "sem referência"),
        stat("Com stock", `${summary.inStockCount} de ${group.listings.length}`),
        stat("Última descida", summary.lastDrop ? money(summary.lastDrop.lastDropTo) : "—", summary.lastDrop ? `${summary.lastDrop.storeName} · ${relative(summary.lastDrop.lastDropAt)}` : null)),
      drawerSection("Preço por loja", compareChart(group, colors)),
      drawerSection("Lojas",
        h("div", { class: "table-wrap" }, h("table", { class: "table" },
          h("thead", {}, h("tr", {}, h("th", {}, "Loja"), h("th", { class: "num" }, "Atual"), h("th", { class: "num" }, "Mínimo"))),
          h("tbody", {}, group.listings.map((listing) => h("tr", {},
            h("td", { class: "title-cell" },
              h("div", { class: "store-line" }, h("span", { class: "swatch", style: `background:${colors.get(listing.storeName)}` }), h("strong", {}, listing.storeName),
                listing.isManual ? pill("manual", "plain", "Neste produto por uma correção manual") : null),
              h("div", { class: "listing-title" }, externalLink(listing.url, listing.title)),
              h("div", { class: "link-row" },
                h("button", { type: "button", class: "text-link small", title: "Tirar este anúncio deste produto", onclick: () => separateListing(group, listing) }, "Separar deste produto"),
                listing.isManual ? h("button", { type: "button", class: "text-link small", onclick: () => assignListing(listing.historyKey, "", group.key) }, "Repor automático") : null)),
            h("td", { class: "num nowrap" }, h("div", { class: "mono" }, money(listing.currentPrice)),
              listing.currentInStock ? pill("Em stock", "accent") : pill("Esgotado")),
            h("td", { class: "num nowrap" }, h("div", { class: "mono" }, money(listing.lowestPrice)), h("div", { class: "muted small" }, dateTime(listing.lowestPriceAt))))))))),
      drawerSection("Correção",
        h("p", { class: "muted small" }, "Falta aqui uma loja com este produto? Junta o anúncio dela a este grupo. A correção fica guardada e o bot respeita-a nos próximos ciclos."),
        h("div", {}, button("Juntar anúncio…", { iconName: "plus", kind: "small", onClick: () => joinListing(group) }))),
    ],
    footer: [button("Fechar", { kind: "ghost", onClick: closeDrawer })],
  });
}

/**
 * Step chart of one product in every store: one line per store in the store's colour; sold-out stretches are the
 * same colour, dashed and lighter. Stores are named at the line ends, in the legend (hover a store to highlight it)
 * and in the tooltip, which lists every store at the pointer's time — so identity never rests on colour alone.
 */
function compareChart(group, colors) {
  const series = group.listings
    .map((listing) => ({ listing, color: colors.get(listing.storeName), points: listing.points.map((point) => ({ ...point, time: new Date(point.at).getTime() })) }))
    .filter((item) => item.points.length > 0);
  if (series.length === 0) return emptyState("chart", "Sem pontos.");

  const width = 640, height = 260, left = 52, right = 110, top = 14, bottom = 28;
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
  await loadCompare();
  if (reopenKey && (state.compare || []).some((group) => group.key === reopenKey)) await openCompareGroup(reopenKey);
  else closeDrawer();
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
