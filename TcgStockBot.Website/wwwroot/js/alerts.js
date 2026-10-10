"use strict";

/* ==========================================================================
   Alertas — everything the bot sent, by day, with the last 14 days at the top.
   ========================================================================== */

let alertsShown = 100;

RENDERERS.alertas = function viewAlerts() {
  const alerts = state.alerts;
  const toConfirm = alerts.filter((alert) => alert.languageToConfirm);
  const storeNames = [...new Set(alerts.map((alert) => alert.storeName))].sort((a, b) => a.localeCompare(b, "pt"));
  if (state.alertStore && !storeNames.includes(state.alertStore)) state.alertStore = "";

  const visible = alerts.filter((alert) => (state.alertFilter === "all" || alert.languageToConfirm)
    && (!state.alertStore || alert.storeName === state.alertStore));

  return [
    pageHead("Alertas", `Tudo o que o bot te enviou. ${channelsSentence()}`, [
      alertsMenu(),
      button("Enviar alerta de teste", { iconName: "bell", onClick: sendTestToAll, disabled: !anyChannelEnabled() }),
    ]),
    alertsChart(alerts),
    h("div", { class: "toolbar" },
      segmented([["all", "Todos", null], ["confirm", "Por confirmar idioma", toConfirm.length]], state.alertFilter,
        (value) => { state.alertFilter = value; alertsShown = 100; renderView(true); }, "Filtro"),
      storeNames.length > 1 ? h("div", { class: "toolbar-end" },
        selectPill("Loja", state.alertStore, [["", "Todas"], ...storeNames.map((name) => [name, name])],
          (value) => { state.alertStore = value; alertsShown = 100; renderView(true); })) : null),
    visible.length === 0
      ? h("div", { class: "card soft" }, emptyState("bell", alerts.length === 0 ? "Ainda nenhum alerta." : "Nenhum alerta neste filtro.",
        alerts.length === 0 ? "Quando um produto aparecer em stock, no idioma certo e ao teu preço, recebes o aviso e fica registado aqui." : null))
      : alertDays(visible.slice(0, alertsShown)),
    visible.length > alertsShown
      ? h("div", { class: "center" }, button(`Mostrar mais ${Math.min(100, visible.length - alertsShown)}`, { onClick: () => { alertsShown += 100; renderView(true); } }))
      : null,
  ];
};

function anyChannelEnabled() {
  const notification = state.notification;
  return !notification || notification.whatsApp.enabled || notification.email.enabled;
}

function channelsSentence() {
  const notification = state.notification;
  if (!notification) return "";
  const on = [notification.whatsApp.enabled ? "WhatsApp" : null, notification.email.enabled ? "email" : null].filter(Boolean);
  if (on.length === 2) return "WhatsApp e email ligados.";
  if (on.length === 1) return `${on[0][0].toUpperCase()}${on[0].slice(1)} ligado.`;
  return "Nenhum canal de aviso ligado — configura no .env.";
}

async function sendTestToAll() {
  const notification = state.notification || await api("/notification/get").catch(() => null);
  const channels = notification ? [notification.whatsApp.enabled ? "WhatsApp" : null, notification.email.enabled ? "Email" : null].filter(Boolean) : ["WhatsApp", "Email"];
  if (channels.length === 0) { toast("Nenhum canal ligado. Configura WhatsApp ou email no .env.", "error"); return; }
  for (const channel of channels) await sendTest(channel);
}

function alertsMenu() {
  const item = (label, iconName, onClick, disabled = false) =>
    h("button", { type: "button", class: "menu-item", disabled, onclick: (event) => { event.currentTarget.closest("details").open = false; onClick(); } }, icon(iconName), label);
  return h("details", { class: "menu" },
    h("summary", { class: "btn", "aria-label": "Mais ações" }, icon("more"), "Mais"),
    h("div", { class: "menu-list" },
      item("Limpar a lista", "trash", clearAlertList, state.alerts.length === 0),
      item("Limpar memória de alertas", "refresh", resetAlertMemory)));
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

  return h("section", { class: "card chart-card", "aria-label": "Alertas nos últimos 14 dias" },
    h("div", { class: "chart-total" },
      h("span", { class: "muted" }, "Últimos 14 dias"),
      h("strong", { class: "big-number" }, total),
      h("span", { class: "muted small" }, total === 1 ? "alerta" : "alertas",
        confirm ? [" · ", h("span", { class: "amber" }, `${confirm} por confirmar idioma`)] : null)),
    h("ol", { class: "bars" }, buckets.map((bucket, index) => {
      const today = index === buckets.length - 1;
      const label = `${dayMonthFormat.format(bucket.day)}: ${bucket.count} ${bucket.count === 1 ? "alerta" : "alertas"}`;
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
      h("div", { class: "alert-title" }, task ? h("a", { href: routeHash("produto", task.id) }, alert.title) : alert.title),
      h("div", { class: "alert-sub" }, alert.storeName, ` · para “${alert.stockTaskName || task?.name || "?"}”`),
      alert.languageToConfirm || dropped || alert.preOrder
        ? h("div", { class: "tag-row" },
          alert.languageToConfirm ? h("span", { class: "tag amber" }, "A loja não diz o idioma: confirma na página") : null,
          dropped ? h("span", { class: "tag accent" }, `Baixou de ${money(alert.previousPrice)}`) : null,
          alert.preOrder ? h("span", { class: "tag" }, "Pré-venda") : null)
        : null),
    h("div", { class: "alert-price" },
      h("strong", { class: "mono price" }, money(alert.price)),
      task ? h("span", { class: "muted small" }, `máx. ${money(task.maxPrice)}`) : null,
      msrpDelta(alert.price, alert.msrp)),
    h("time", { class: "alert-time mono", datetime: alert.at }, clock(alert.at)),
    safeHref(alert.url) ? linkButton("Abrir", alert.url, { external: true, trailingIcon: "external", kind: "small" }) : null);
}
