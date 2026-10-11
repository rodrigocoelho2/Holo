"use strict";

/* ==========================================================================
   Alertas — everything the bot sent, by day, with the last 14 days at the top.
   ========================================================================== */

let alertsShown = 100;

RENDERERS.alerts = function viewAlerts() {
  const alerts = state.alerts.filter((alert) => inGameTab(alertTask(alert)?.game));
  const toConfirm = alerts.filter((alert) => alert.languageToConfirm);
  const storeNames = [...new Set(alerts.map((alert) => alert.storeName))].sort((a, b) => a.localeCompare(b, "en"));
  if (state.alertStore && !storeNames.includes(state.alertStore)) state.alertStore = "";

  const visible = alerts.filter((alert) => (state.alertFilter === "all" || alert.languageToConfirm)
    && (!state.alertStore || alert.storeName === state.alertStore));

  return [
    pageHead("Alerts", `Everything the bot sent you. ${channelsSentence()}`, [
      alertsMenu(),
      button("Send a test alert", { iconName: "bell", onClick: sendTestToAll, disabled: !anyChannelEnabled() }),
    ]),
    gameTabs(state.alerts.map((alert) => alertTask(alert)?.game), "alerts"),
    alertsChart(alerts),
    h("div", { class: "toolbar" },
      segmented([["all", "All", null], ["confirm", "Language to check", toConfirm.length]], state.alertFilter,
        (value) => { state.alertFilter = value; alertsShown = 100; renderView(true); }, "Filter"),
      storeNames.length > 1 ? h("div", { class: "toolbar-end" },
        selectPill("Store", state.alertStore, [["", "All"], ...storeNames.map((name) => [name, name])],
          (value) => { state.alertStore = value; alertsShown = 100; renderView(true); })) : null),
    visible.length === 0
      ? h("div", { class: "card soft" }, emptyState("bell", alerts.length === 0 ? "No alerts yet." : "No alerts with this filter.",
        alerts.length === 0 ? "When a product shows up in stock, in the right language and at your price, you get alerted and it is listed here." : null))
      : alertDays(visible.slice(0, alertsShown)),
    visible.length > alertsShown
      ? h("div", { class: "center" }, button(`Show ${Math.min(100, visible.length - alertsShown)} more`, { onClick: () => { alertsShown += 100; renderView(true); } }))
      : null,
  ];
};

function anyChannelEnabled() {
  const notification = state.notification;
  return !notification || enabledChannels(notification).length > 0;
}

/** The channels turned on in .env, as the /notification/test channel names. */
function enabledChannels(notification) {
  return [notification.whatsApp.enabled ? "WhatsApp" : null, notification.telegram?.enabled ? "Telegram" : null, notification.email.enabled ? "Email" : null].filter(Boolean);
}

function channelsSentence() {
  const notification = state.notification;
  if (!notification) return "";
  const on = enabledChannels(notification).map((channel) => (channel === "Email" ? "email" : channel));
  if (on.length === 0) return "No alert channel is on — set one up in .env.";
  const list = on.length === 1 ? on[0] : `${on.slice(0, -1).join(", ")} and ${on.at(-1)}`;
  return `${list[0].toUpperCase()}${list.slice(1)} ${on.length === 1 ? "is on" : "are on"}.`;
}

async function sendTestToAll() {
  const notification = state.notification || await api("/notification/get").catch(() => null);
  const channels = notification ? enabledChannels(notification) : ["WhatsApp", "Telegram", "Email"];
  if (channels.length === 0) { toast("No channel is on. Set up WhatsApp, Telegram or email in .env.", "error"); return; }
  for (const channel of channels) await sendTest(channel);
}

function alertsMenu() {
  const item = (label, iconName, onClick, disabled = false) =>
    h("button", { type: "button", class: "menu-item", disabled, onclick: (event) => { event.currentTarget.closest("details").open = false; onClick(); } }, icon(iconName), label);
  return h("details", { class: "menu" },
    h("summary", { class: "btn", "aria-label": "More actions" }, icon("more"), "More"),
    h("div", { class: "menu-list" },
      item("Clear the list", "trash", clearAlertList, state.alerts.length === 0),
      item("Clear alert memory", "refresh", resetAlertMemory)));
}

function alertsChart(alerts) {
  const days = 14;
  const buckets = [];
  for (let offset = days - 1; offset >= 0; offset--) {
    const day = new Date();
    day.setHours(0, 0, 0, 0);
    day.setDate(day.getDate() - offset);
    buckets.push({ day, count: 0, confirm: 0 });
  }
  for (const alert of alerts) {
    const at = new Date(alert.at);
    at.setHours(0, 0, 0, 0);
    const bucket = buckets.find((item) => item.day.getTime() === at.getTime());
    if (bucket) { bucket.count++; if (alert.languageToConfirm) bucket.confirm++; }
  }
  const total = buckets.reduce((sum, bucket) => sum + bucket.count, 0);
  const confirm = buckets.reduce((sum, bucket) => sum + bucket.confirm, 0);
  const max = Math.max(1, ...buckets.map((bucket) => bucket.count));

  return h("section", { class: "card chart-card", "aria-label": "Alerts in the last 14 days" },
    h("div", { class: "chart-total" },
      h("span", { class: "muted" }, "Last 14 days"),
      h("strong", { class: "big-number" }, total),
      h("span", { class: "muted small" }, total === 1 ? "alert" : "alerts",
        confirm ? [" · ", h("span", { class: "amber" }, `${confirm} with language to check`)] : null)),
    h("ol", { class: "bars" }, buckets.map((bucket, index) => {
      const today = index === buckets.length - 1;
      const label = `${dayMonthFormat.format(bucket.day)}: ${bucket.count} ${bucket.count === 1 ? "alert" : "alerts"}`;
      return h("li", { class: today ? "today" : "", title: label, "aria-label": label },
        h("span", { class: "bar-value mono" }, bucket.count),
        h("span", { class: "bar-fill", style: `height:${bucket.count ? Math.max(4, (bucket.count / max) * 100) : 2}%` }),
        h("span", { class: "bar-day mono" }, bucket.day.getDate()));
    })));
}

function alertDays(alerts) {
  const groups = [];
  for (const alert of alerts) {
    const heading = dayHeading(alert.at);
    if (groups.at(-1)?.heading !== heading) groups.push({ heading, alerts: [] });
    groups.at(-1).alerts.push(alert);
  }
  return groups.map((group) => h("section", { class: "day-group" },
    h("h2", { class: "day-heading" }, group.heading),
    h("ul", { class: "alert-rows" }, group.alerts.map(alertRow))));
}

function alertRow(alert) {
  const task = alertTask(alert);
  const dropped = alert.previousPrice !== null && alert.previousPrice !== undefined;
  return h("li", { class: `alert-row ${alert.languageToConfirm ? "confirm" : ""}` },
    productArt({ imageUrl: alert.imageUrl, game: task?.game, title: alert.title, query: task?.searchQuery, size: "thumb" }),
    h("div", { class: "alert-text" },
      h("div", { class: "alert-title" }, task ? h("a", { href: routeHash("watched", task.id) }, alert.title) : alert.title),
      h("div", { class: "alert-sub" }, alert.storeName, ` · for “${alert.stockTaskName || task?.name || "?"}”`),
      alert.languageToConfirm || dropped || alert.preOrder
        ? h("div", { class: "tag-row" },
          alert.languageToConfirm ? h("span", { class: "tag amber" }, "The store doesn't say the language: check the page") : null,
          dropped ? h("span", { class: "tag accent" }, `Dropped from ${money(alert.previousPrice)}`) : null,
          alert.preOrder ? h("span", { class: "tag" }, "Pre-order") : null)
        : null),
    h("div", { class: "alert-price" },
      h("strong", { class: "mono price" }, money(alert.price)),
      msrpPill(alert.price, alert.msrp) || (task && task.priceRule !== "Msrp" ? h("span", { class: "muted small" }, `max ${money(task.maxPrice)}`) : null)),
    h("time", { class: "alert-time mono", datetime: alert.at }, clock(alert.at)),
    safeHref(alert.url) ? linkButton("Open", alert.url, { external: true, trailingIcon: "external", kind: "small" }) : null);
}
