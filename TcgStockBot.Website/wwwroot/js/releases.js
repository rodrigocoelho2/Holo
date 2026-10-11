"use strict";

/* ==========================================================================
   Lançamentos — the launch calendar: what comes out when, when pre-orders open, and whether you already watch it
   (and what the bot found for it). A list grouped by moment, or a month calendar.
   ========================================================================== */

const MONTHS = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];
const WEEKDAYS = ["seg", "ter", "qua", "qui", "sex", "sáb", "dom"];

async function loadReleases() {
  try {
    state.releases = await api("/release/list");
  } catch (error) {
    state.releases = state.releases || [];
    toast(errorText(error), "error");
  }
  if (state.route.view === "lancamentos") renderView(true);
}

/** "2026-11-14" → a local Date at midnight (DateOnly comes as yyyy-MM-dd). */
const localDay = (value) => {
  const [year, month, day] = String(value).split("-").map(Number);
  return new Date(year, month - 1, day);
};
const today = () => { const now = new Date(); now.setHours(0, 0, 0, 0); return now; };
const daysFrom = (date) => Math.round((date - today()) / DAY);
const isoDay = (date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;

/** "hoje", "amanhã", "daqui a 12 dias", "há 3 dias". */
function dayDistance(date) {
  const days = daysFrom(date);
  if (days === 0) return "hoje";
  if (days === 1) return "amanhã";
  if (days === -1) return "ontem";
  return days > 0 ? `daqui a ${days} dias` : `há ${-days} dias`;
}

/** Where a release stands: pre-orders open, upcoming, out. */
function releasePhase(release) {
  const out = daysFrom(localDay(release.releaseDate));
  const preOrder = release.preOrderDate ? daysFrom(localDay(release.preOrderDate)) : null;
  if (out <= 0) return "out";
  if (preOrder !== null && preOrder <= 0) return "preorder";
  return "soon";
}

RENDERERS.lancamentos = function viewReleases() {
  const all = state.releases;
  const actions = [
    segmented([["list", "Lista"], ["calendar", "Calendário"]], state.releaseView, (value) => { state.releaseView = value; writePreference("releaseView", value); renderView(true); }, "Vista"),
    button("Adicionar lançamento", { iconName: "plus", kind: "primary", onClick: () => openReleaseDrawer(null) }),
  ];
  const head = pageHead("Lançamentos", "O que vem aí: datas de lançamento, abertura das pré-vendas e se já estás a vigiar.", actions);
  if (all === null) return [head, h("div", { class: "card soft" }, h("p", { class: "muted pad" }, "A carregar…"))];

  const releases = all.filter((release) => inGameTab(release.game));
  return [
    head,
    gameTabs(all.map((release) => release.game), "lançamentos"),
    all.length === 0
      ? h("div", { class: "card soft" }, emptyState("calendar", "Ainda não há lançamentos no calendário.",
        "Junta as próximas expansões (data de lançamento e, se souberes, de abertura das pré-vendas) e liga cada uma ao produto que vigias.",
        button("Adicionar lançamento", { iconName: "plus", kind: "primary", onClick: () => openReleaseDrawer(null) })))
      : state.releaseView === "calendar" ? releaseCalendar(releases) : releaseList(releases),
  ];
};

function releaseList(releases) {
  const phases = { preorder: [], soon: [], out: [] };
  for (const release of releases) phases[releasePhase(release)].push(release);
  phases.out.reverse();                                   // most recent first
  const section = (id, title, note, items, open = true) => items.length === 0 ? null : h("section", { class: "section", "aria-labelledby": id },
    sectionHead(title, { id, count: items.length, note }),
    open ? h("div", { class: "release-list" }, items.map(releaseCard))
      : h("details", { class: "release-past" }, h("summary", {}, `Mostrar os ${items.length}`), h("div", { class: "release-list" }, items.map(releaseCard))));

  if (releases.length === 0) return h("div", { class: "card soft" }, emptyState("calendar", "Nenhum lançamento deste jogo."));
  return [
    section("rel-pre", "Pré-vendas abertas", "já se pode reservar", phases.preorder),
    section("rel-soon", "Próximos", "por data de lançamento", phases.soon),
    section("rel-out", "Já saíram", null, phases.out, phases.preorder.length + phases.soon.length === 0),
  ];
}

/** What the bot knows about the linked task: status and best offer. */
function releaseWatch(release) {
  const task = release.stockTaskId ? findTask(release.stockTaskId) : null;
  if (!task) {
    return h("div", { class: "release-watch none" },
      h("span", { class: "muted small" }, release.stockTaskId ? "O produto ligado já não existe." : "Ainda não vigias este lançamento."),
      button("Vigiar", { iconName: "plus", kind: "small", onClick: () => watchRelease(release) }));
  }
  const insight = taskInsight(task);
  const offers = insight.matches || [];
  return h("a", { class: "release-watch", href: routeHash("produto", task.id) },
    h("span", { class: `bot-dot ${offers.length ? "on" : task.enabled ? "busy" : "off"}`, "aria-hidden": "true" }),
    h("span", {}, offers.length
      ? [h("strong", {}, `${offers.length} ${offers.length === 1 ? "oferta" : "ofertas"}`), ` desde ${money(Math.min(...offers.map((offer) => offer.price ?? Infinity)))}`]
      : task.enabled ? "A vigiar · ainda sem ofertas ao teu preço" : "Vigiado · em pausa"),
    icon("chevron"));
}

function releaseCard(release) {
  const date = localDay(release.releaseDate);
  const phase = releasePhase(release);
  const preOrder = release.preOrderDate ? localDay(release.preOrderDate) : null;
  return h("article", { class: `release-card ${gameClass(release.game)} ${phase}` },
    h("div", { class: "release-date", "aria-hidden": "true" },
      h("span", { class: "release-day" }, date.getDate()), h("span", { class: "release-month" }, MONTHS[date.getMonth()].slice(0, 3)),
      h("span", { class: "release-year" }, date.getFullYear())),
    h("div", { class: "release-body" },
      h("div", { class: "catalog-meta" }, gamePill(release.game),
        phase === "preorder" ? h("span", { class: "tag accent" }, "Pré-vendas abertas") : null,
        phase === "out" ? h("span", { class: "tag" }, "Já saiu") : null),
      h("h3", { class: "release-name" }, release.name),
      h("p", { class: "muted small" },
        `Lançamento ${weekdayFormat.format(date)} · ${dayDistance(date)}`,
        preOrder ? ` · pré-vendas ${phase === "soon" ? `a ${dayMonthFormat.format(preOrder)} (${dayDistance(preOrder)})` : `desde ${dayMonthFormat.format(preOrder)}`}` : ""),
      release.notes ? h("p", { class: "release-notes" }, release.notes) : null,
      releaseWatch(release)),
    h("div", { class: "release-actions" },
      safeHref(release.url) ? linkButton("Página", release.url, { kind: "small", external: true, trailingIcon: "external" }) : null,
      iconButton("edit", `Editar ${release.name}`, () => openReleaseDrawer(release))));
}

function releaseCalendar(releases) {
  if (!state.releaseMonth) {
    const next = releases.map((release) => localDay(release.releaseDate)).filter((date) => date >= today()).sort((a, b) => a - b)[0] || today();
    state.releaseMonth = new Date(next.getFullYear(), next.getMonth(), 1);
  }
  const month = state.releaseMonth;
  const first = new Date(month.getFullYear(), month.getMonth(), 1);
  const offset = (first.getDay() + 6) % 7;                // Monday first
  const days = new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate();
  const byDay = new Map();
  const add = (date, entry) => { const key = isoDay(date); if (!byDay.has(key)) byDay.set(key, []); byDay.get(key).push(entry); };
  for (const release of releases) {
    add(localDay(release.releaseDate), { release, kind: "out" });
    if (release.preOrderDate) add(localDay(release.preOrderDate), { release, kind: "pre" });
  }
  const shift = (delta) => { state.releaseMonth = new Date(month.getFullYear(), month.getMonth() + delta, 1); renderView(true); };
  const cells = [];
  for (let index = 0; index < offset; index++) cells.push(h("div", { class: "cal-cell empty", "aria-hidden": "true" }));
  for (let day = 1; day <= days; day++) {
    const date = new Date(month.getFullYear(), month.getMonth(), day);
    const entries = byDay.get(isoDay(date)) || [];
    cells.push(h("div", { class: `cal-cell ${isoDay(date) === isoDay(today()) ? "today" : ""} ${entries.length ? "has" : ""}` },
      h("span", { class: "cal-day" }, day),
      entries.map(({ release, kind }) => h("button", {
        type: "button", class: `cal-chip ${kind} ${gameClass(release.game)}`, title: `${kind === "pre" ? "Abertura das pré-vendas" : "Lançamento"}: ${release.name}`,
        onclick: () => openReleaseDrawer(release),
      }, kind === "pre" ? "Pré · " : null, release.name))));
  }
  const inMonth = releases.filter((release) => {
    const date = localDay(release.releaseDate);
    return date.getFullYear() === month.getFullYear() && date.getMonth() === month.getMonth();
  });
  return [
    h("section", { class: "card calendar-card", "aria-label": "Calendário" },
      h("div", { class: "cal-head" },
        iconButton("back", "Mês anterior", () => shift(-1)),
        h("h2", {}, `${MONTHS[month.getMonth()]} ${month.getFullYear()}`),
        iconButton("chevron", "Mês seguinte", () => shift(1)),
        h("button", { type: "button", class: "text-link small", onclick: () => { state.releaseMonth = null; renderView(true); } }, "Hoje")),
      h("div", { class: "cal-grid" }, WEEKDAYS.map((name) => h("div", { class: "cal-weekday" }, name)), cells),
      h("div", { class: "cal-legend muted small" }, h("span", { class: "cal-chip out sample" }, "Lançamento"), h("span", { class: "cal-chip pre sample" }, "Pré-vendas"))),
    inMonth.length ? h("section", { class: "section" }, sectionHead(`Em ${MONTHS[month.getMonth()]}`, { count: inMonth.length }), h("div", { class: "release-list" }, inMonth.map(releaseCard))) : null,
  ];
}

/** Opens "Vigiar produto" with the release name already in the search. */
function watchRelease(release) {
  state.wizard = { ...newWizard(), query: release.name, game: gameKey(release.game) };
  go("vigiar");
}

function openReleaseDrawer(release) {
  const draft = release
    ? { ...release }
    : { id: "", name: "", game: state.game !== "all" ? state.game : "Pokemon", releaseDate: "", preOrderDate: "", url: "", notes: "", stockTaskId: "" };
  const errors = h("div", {});
  const tasks = (state.tasks || []).slice().sort((a, b) => (a.name || "").localeCompare(b.name || "", "pt"));

  const save = async () => {
    errors.replaceChildren();
    try {
      await post("/release/save", {
        id: draft.id || null, name: draft.name.trim(), game: draft.game, releaseDate: draft.releaseDate || null,
        preOrderDate: draft.preOrderDate || null, url: draft.url?.trim() || null, notes: draft.notes?.trim() || null, stockTaskId: draft.stockTaskId || null,
      });
      toast(release ? "Lançamento guardado" : "Lançamento adicionado", "success");
      closeDrawer();
      await loadReleases();
    } catch (error) {
      errors.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  const remove = async () => {
    if (!await confirmDialog({ title: "Apagar lançamento?", message: `“${release.name}” sai do calendário. O produto vigiado (se houver) continua.`, confirmLabel: "Apagar", danger: true })) return;
    try {
      await post(`/release/delete?identifier=${encodeURIComponent(release.id)}`);
      toast("Lançamento apagado", "success");
      closeDrawer();
      await loadReleases();
    } catch (error) {
      toast(errorText(error), "error");
    }
  };

  openDrawer({
    title: release ? "Editar lançamento" : "Novo lançamento",
    subtitle: "Fica guardado no bot (data/releases.json).",
    body: [
      h("div", { class: "fields" },
        field("Nome", textInput(draft.name, (value) => { draft.name = value; }, { placeholder: "ex.: Pokémon — Pitch Black" }), null, true),
        field("Jogo", h("select", { onchange: (event) => { draft.game = event.target.value; } },
          Object.entries(GAMES).map(([value, label]) => h("option", { value, selected: draft.game === value }, label)))),
        field("Data de lançamento", h("input", { type: "date", value: draft.releaseDate || "", oninput: (event) => { draft.releaseDate = event.target.value; } })),
        field("Abertura das pré-vendas", h("input", { type: "date", value: draft.preOrderDate || "", oninput: (event) => { draft.preOrderDate = event.target.value; } }), "Opcional."),
        field("Produto que vigias", h("select", { onchange: (event) => { draft.stockTaskId = event.target.value; } },
          h("option", { value: "", selected: !draft.stockTaskId }, "— nenhum —"),
          tasks.map((task) => h("option", { value: task.id, selected: draft.stockTaskId === task.id }, task.name || task.id))),
        "Liga ao produto do A vigiar para ver aqui as ofertas.", true),
        field("Página oficial", textInput(draft.url || "", (value) => { draft.url = value; }, { type: "url", placeholder: "https://…" }), null, true),
        field("Notas", h("textarea", { rows: 3, placeholder: "Produtos da vaga, MSRP, lojas com reserva…", oninput: (event) => { draft.notes = event.target.value; } }, draft.notes || ""), null, true)),
      errors,
    ],
    footer: [
      release ? button("Apagar", { iconName: "trash", kind: "ghost danger-text", onClick: remove }) : null,
      h("span", { class: "spacer" }),
      button("Cancelar", { kind: "ghost", onClick: closeDrawer }),
      button(release ? "Guardar" : "Adicionar", { kind: "primary", onClick: save }),
    ],
  });
}
