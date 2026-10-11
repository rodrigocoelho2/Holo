"use strict";

/* ==========================================================================
   Releases — the launch calendar: what comes out when, when pre-orders open, and whether you already watch it
   (and what the bot found for it). A list grouped by moment, or a month calendar.
   ========================================================================== */

const MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
const WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

async function loadReleases() {
  try {
    state.releases = await api("/release/list");
  } catch (error) {
    state.releases = state.releases || [];
    toast(errorText(error), "error");
  }
  if (state.route.view === "releases") renderView(true);
}

/** "2026-11-14" → a local Date at midnight (DateOnly comes as yyyy-MM-dd). */
const localDay = (value) => {
  const [year, month, day] = String(value).split("-").map(Number);
  return new Date(year, month - 1, day);
};
const today = () => { const now = new Date(); now.setHours(0, 0, 0, 0); return now; };
const daysFrom = (date) => Math.round((date - today()) / DAY);
const isoDay = (date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;

/** "today", "tomorrow", "in 12 days", "3 days ago". */
function dayDistance(date) {
  const days = daysFrom(date);
  if (days === 0) return "today";
  if (days === 1) return "tomorrow";
  if (days === -1) return "yesterday";
  return days > 0 ? `in ${days} days` : `${-days} days ago`;
}

/** Where a release stands: pre-orders open, upcoming, out. */
function releasePhase(release) {
  const out = daysFrom(localDay(release.releaseDate));
  const preOrder = release.preOrderDate ? daysFrom(localDay(release.preOrderDate)) : null;
  if (out <= 0) return "out";
  if (preOrder !== null && preOrder <= 0) return "preorder";
  return "soon";
}

RENDERERS.releases = function viewReleases() {
  const all = state.releases;
  const actions = [
    segmented([["list", "List"], ["calendar", "Calendar"]], state.releaseView, (value) => { state.releaseView = value; writePreference("releaseView", value); renderView(true); }, "View"),
    button("Add release", { iconName: "plus", kind: "primary", onClick: () => openReleaseDrawer(null) }),
  ];
  const head = pageHead("Releases", "What's coming: release dates, when pre-orders open and whether you are watching it.", actions);
  if (all === null) return [head, h("div", { class: "card soft" }, h("p", { class: "muted pad" }, "Loading…"))];

  const releases = all.filter((release) => inGameTab(release.game));
  return [
    head,
    gameTabs(all.map((release) => release.game), "releases"),
    all.length === 0
      ? h("div", { class: "card soft" }, emptyState("calendar", "No releases in the calendar yet.",
        "Add the next sets (release date and, if you know it, when pre-orders open) and link each one to the product you watch.",
        button("Add release", { iconName: "plus", kind: "primary", onClick: () => openReleaseDrawer(null) })))
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
      : h("details", { class: "release-past" }, h("summary", {}, `Show all ${items.length}`), h("div", { class: "release-list" }, items.map(releaseCard))));

  if (releases.length === 0) return h("div", { class: "card soft" }, emptyState("calendar", "No releases for this game."));
  return [
    section("rel-pre", "Pre-orders open", "you can reserve now", phases.preorder),
    section("rel-soon", "Coming up", "by release date", phases.soon),
    section("rel-out", "Already out", null, phases.out, phases.preorder.length + phases.soon.length === 0),
  ];
}

/** What the bot knows about the linked task: status and best offer. */
function releaseWatch(release) {
  const task = release.stockTaskId ? findTask(release.stockTaskId) : null;
  if (!task) {
    return h("div", { class: "release-watch none" },
      h("span", { class: "muted small" }, release.stockTaskId ? "The linked product no longer exists." : "You are not watching this release yet."),
      button("Watch", { iconName: "plus", kind: "small", onClick: () => watchRelease(release) }));
  }
  const insight = taskInsight(task);
  const offers = insight.matches || [];
  return h("a", { class: "release-watch", href: routeHash("watched", task.id) },
    h("span", { class: `bot-dot ${offers.length ? "on" : task.enabled ? "busy" : "off"}`, "aria-hidden": "true" }),
    h("span", {}, offers.length
      ? [h("strong", {}, `${offers.length} ${offers.length === 1 ? "offer" : "offers"}`), ` from ${money(Math.min(...offers.map((offer) => offer.price ?? Infinity)))}`]
      : task.enabled ? "Watching · no offers at your price yet" : "Watched · paused"),
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
        phase === "preorder" ? h("span", { class: "tag accent" }, "Pre-orders open") : null,
        phase === "out" ? h("span", { class: "tag" }, "Out") : null),
      h("h3", { class: "release-name" }, release.name),
      h("p", { class: "muted small" },
        `Out ${weekdayFormat.format(date)} · ${dayDistance(date)}`,
        preOrder ? ` · pre-orders ${phase === "soon" ? `on ${dayMonthFormat.format(preOrder)} (${dayDistance(preOrder)})` : `since ${dayMonthFormat.format(preOrder)}`}` : ""),
      release.notes ? h("p", { class: "release-notes" }, release.notes) : null,
      releaseWatch(release)),
    h("div", { class: "release-actions" },
      safeHref(release.url) ? linkButton("Page", release.url, { kind: "small", external: true, trailingIcon: "external" }) : null,
      iconButton("edit", `Edit ${release.name}`, () => openReleaseDrawer(release))));
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
        type: "button", class: `cal-chip ${kind} ${gameClass(release.game)}`, title: `${kind === "pre" ? "Pre-orders open" : "Release"}: ${release.name}`,
        onclick: () => openReleaseDrawer(release),
      }, kind === "pre" ? "Pre · " : null, release.name))));
  }
  const inMonth = releases.filter((release) => {
    const date = localDay(release.releaseDate);
    return date.getFullYear() === month.getFullYear() && date.getMonth() === month.getMonth();
  });
  return [
    h("section", { class: "card calendar-card", "aria-label": "Calendar" },
      h("div", { class: "cal-head" },
        iconButton("back", "Previous month", () => shift(-1)),
        h("h2", {}, `${MONTHS[month.getMonth()]} ${month.getFullYear()}`),
        iconButton("chevron", "Next month", () => shift(1)),
        h("button", { type: "button", class: "text-link small", onclick: () => { state.releaseMonth = null; renderView(true); } }, "Today")),
      h("div", { class: "cal-grid" }, WEEKDAYS.map((name) => h("div", { class: "cal-weekday" }, name)), cells),
      h("div", { class: "cal-legend muted small" }, h("span", { class: "cal-chip out sample" }, "Release"), h("span", { class: "cal-chip pre sample" }, "Pre-orders"))),
    inMonth.length ? h("section", { class: "section" }, sectionHead(`In ${MONTHS[month.getMonth()]}`, { count: inMonth.length }), h("div", { class: "release-list" }, inMonth.map(releaseCard))) : null,
  ];
}

/** Opens "Vigiar produto" with the release name already in the search. */
function watchRelease(release) {
  state.wizard = { ...newWizard(), query: release.name, game: gameKey(release.game) };
  go("watch");
}

function openReleaseDrawer(release) {
  const draft = release
    ? { ...release }
    : { id: "", name: "", game: state.game !== "all" ? state.game : "Pokemon", releaseDate: "", preOrderDate: "", url: "", notes: "", stockTaskId: "" };
  const errors = h("div", {});
  const tasks = (state.tasks || []).slice().sort((a, b) => (a.name || "").localeCompare(b.name || "", "en"));

  const save = async () => {
    errors.replaceChildren();
    try {
      await post("/release/save", {
        id: draft.id || null, name: draft.name.trim(), game: draft.game, releaseDate: draft.releaseDate || null,
        preOrderDate: draft.preOrderDate || null, url: draft.url?.trim() || null, notes: draft.notes?.trim() || null, stockTaskId: draft.stockTaskId || null,
      });
      toast(release ? "Release saved" : "Release added", "success");
      closeDrawer();
      await loadReleases();
    } catch (error) {
      errors.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  const remove = async () => {
    if (!await confirmDialog({ title: "Delete release?", message: `“${release.name}” leaves the calendar. The watched product (if any) stays.`, confirmLabel: "Delete", danger: true })) return;
    try {
      await post(`/release/delete?identifier=${encodeURIComponent(release.id)}`);
      toast("Release deleted", "success");
      closeDrawer();
      await loadReleases();
    } catch (error) {
      toast(errorText(error), "error");
    }
  };

  openDrawer({
    title: release ? "Edit release" : "New release",
    subtitle: "Saved in the bot (data/releases.json).",
    body: [
      h("div", { class: "fields" },
        field("Name", textInput(draft.name, (value) => { draft.name = value; }, { placeholder: "e.g. Pokémon — Pitch Black" }), null, true),
        field("Game", h("select", { onchange: (event) => { draft.game = event.target.value; } },
          Object.entries(GAMES).map(([value, label]) => h("option", { value, selected: draft.game === value }, label)))),
        field("Release date", h("input", { type: "date", value: draft.releaseDate || "", oninput: (event) => { draft.releaseDate = event.target.value; } })),
        field("Pre-orders open", h("input", { type: "date", value: draft.preOrderDate || "", oninput: (event) => { draft.preOrderDate = event.target.value; } }), "Optional."),
        field("Product you watch", h("select", { onchange: (event) => { draft.stockTaskId = event.target.value; } },
          h("option", { value: "", selected: !draft.stockTaskId }, "— none —"),
          tasks.map((task) => h("option", { value: task.id, selected: draft.stockTaskId === task.id }, task.name || task.id))),
        "Link it to a watched product to see its offers here.", true),
        field("Official page", textInput(draft.url || "", (value) => { draft.url = value; }, { type: "url", placeholder: "https://…" }), null, true),
        field("Notes", h("textarea", { rows: 3, placeholder: "Products in the wave, MSRP, stores taking reservations…", oninput: (event) => { draft.notes = event.target.value; } }, draft.notes || ""), null, true)),
      errors,
    ],
    footer: [
      release ? button("Delete", { iconName: "trash", kind: "ghost danger-text", onClick: remove }) : null,
      h("span", { class: "spacer" }),
      button("Cancel", { kind: "ghost", onClick: closeDrawer }),
      button(release ? "Save" : "Add", { kind: "primary", onClick: save }),
    ],
  });
}
