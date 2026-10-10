"use strict";

/* ==========================================================================
   Definições — appearance (per browser), check rhythm (saved in the bot) and alert channels (from .env).
   ========================================================================== */

RENDERERS.definicoes = function viewSettings() {
  const settings = state.settings;
  const draft = {
    interval: Math.round(settings.pollIntervalSeconds / 60),
    jitter: Math.round(settings.pollJitterSeconds / 60),
    delay: settings.delayBetweenRequestsSeconds,
    userAgent: settings.userAgent,
    autoDisable: settings.autoDisableAfterFailures,
  };
  const errors = h("div", {});

  const save = async () => {
    errors.replaceChildren();
    try {
      state.settings = await post("/setting/save", {
        pollIntervalSeconds: draft.interval * 60,
        pollJitterSeconds: draft.jitter * 60,
        delayBetweenRequestsSeconds: draft.delay,
        userAgent: draft.userAgent.trim(),
        autoDisableAfterFailures: draft.autoDisable,
      });
      toast("Definições guardadas", "success");
    } catch (error) {
      errors.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  const numberField = (label, key, unit, hint, min) => field(label,
    h("span", { class: "unit-input" },
      h("input", { type: "number", min, value: draft[key], class: "mono", oninput: (event) => { draft[key] = Number(event.target.value); } }),
      h("span", { class: "unit", "aria-hidden": "true" }, unit)),
    hint);

  return [
    pageHead("Definições", "Aparência, ritmo das verificações e avisos.", [button("Guardar alterações", { kind: "primary", onClick: save })]),
    appearanceCard(),
    h("section", { class: "card settings-card", "aria-labelledby": "rhythm-h" },
      h("h2", { id: "rhythm-h" }, "Verificação"),
      h("p", { class: "muted small" }, "Ficam guardadas no bot e valem para todos os browsers."),
      h("div", { class: "fields three" },
        numberField("Intervalo entre ciclos", "interval", "min", "Mínimo 1. Entre 5 e 15 é simpático para as lojas.", 1),
        numberField("Variação aleatória", "jitter", "± min", "Soma-se ao intervalo para não bater sempre à mesma hora.", 0),
        numberField("Pausa entre pedidos", "delay", "s", "Entre pedidos seguidos à mesma loja.", 0),
        numberField("Desligar loja após", "autoDisable", "ciclos", "Ciclos seguidos com erro até o bot desligar a loja e te avisar. 0 = nunca.", 0),
        field("User-Agent", textInput(draft.userAgent, (value) => { draft.userAgent = value; }, { class: "mono" }), "Como o bot se identifica às lojas. Mantém-no honesto.", true)),
      errors,
      h("div", { class: "card-actions" }, button("Guardar alterações", { kind: "primary", onClick: save }))),
    channelsCard(),
    h("section", { class: "card settings-card", "aria-labelledby": "memory-h" },
      h("h2", { id: "memory-h" }, "Memória de alertas"),
      h("p", { class: "muted" }, "O bot não repete o aviso de um produto enquanto ele continuar em stock. Limpar a memória faz com que tudo o que está disponível seja avisado de novo."),
      h("div", { class: "card-actions" }, button("Limpar memória", { iconName: "refresh", onClick: resetAlertMemory }))),
  ];
};

function appearanceCard() {
  const box = h("section", { class: "card settings-card", "aria-labelledby": "look-h" });
  const render = () => box.replaceChildren(
    h("h2", { id: "look-h" }, "Aparência"),
    h("p", { class: "muted small" }, "Escolhe um tema. Muda logo, e fica guardado neste browser."),
    h("div", { class: "theme-grid", role: "radiogroup", "aria-label": "Tema" }, Object.entries(ACCENTS).map(([key, [label, dark, light]]) => {
      const on = theme.accent === key;
      return h("button", {
        type: "button", role: "radio", "aria-checked": on ? "true" : "false", class: `theme-card ${on ? "on" : ""}`,
        style: `--swatch-dark:${dark};--swatch-light:${light}`, onclick: () => { setTheme({ accent: key }); render(); },
      },
      h("span", { class: "theme-preview", "aria-hidden": "true" },
        h("span", { class: "tp-side" }, h("span", { class: "tp-logo" }), h("span", { class: "tp-line on" }), h("span", { class: "tp-line" }), h("span", { class: "tp-line" })),
        h("span", { class: "tp-main" }, h("span", { class: "tp-title" }), h("span", { class: "tp-card" }, h("span", { class: "tp-chip" }), h("span", { class: "tp-button" })))),
      h("span", { class: "theme-name" }, h("span", { class: "theme-dot", "aria-hidden": "true" }), label, on ? icon("check", "theme-check") : null));
    })),
    h("div", { class: "setting-row" },
      h("div", {}, h("strong", {}, "Modo"), h("div", { class: "muted small" },
        theme.mode === "dark" ? "Fundo escuro. É o modo por omissão." : theme.mode === "light" ? "Fundo claro." : "Segue o sistema (escuro ou claro).")),
      segmented(Object.entries(MODES).map(([key, label]) => [key, label]), theme.mode, (value) => { setTheme({ mode: value }); render(); }, "Modo")));
  render();
  return box;
}

function channelsCard() {
  const notification = state.notification;
  const tile = (name, iconName, channel, enabled, rows, note) => h("div", { class: `channel ${enabled ? "on" : ""}` },
    h("div", { class: "channel-head" },
      h("span", { class: "channel-icon" }, icon(iconName)),
      h("div", {}, h("strong", {}, name), h("div", { class: "muted small" }, enabled ? "Ligado · configurado no .env" : "Desligado")),
      h("span", { class: `bot-dot ${enabled ? "on" : "off"}`, "aria-hidden": "true" })),
    h("dl", { class: "store-facts" }, rows.map(([label, value]) => [h("dt", {}, label), h("dd", {}, value)])),
    h("p", { class: "muted small" }, note),
    button("Enviar mensagem de teste", { kind: "small", disabled: !enabled, onClick: () => sendTest(channel) }));
  const missing = (text) => h("span", { class: "amber" }, text);

  return h("section", { class: "card settings-card", "aria-labelledby": "channels-h" },
    h("div", { class: "card-head-row" }, h("h2", { id: "channels-h" }, "Avisos"),
      button("Enviar mensagem de teste", { onClick: sendTestToAll, disabled: !anyChannelEnabled() })),
    !notification
      ? h("p", { class: "muted" }, "A carregar canais…")
      : h("div", { class: "channels" },
        tile("WhatsApp", "chat", "WhatsApp", notification.whatsApp.enabled, [
          ["Número", notification.whatsApp.maskedPhone || missing("em falta")],
          ["API key", notification.whatsApp.hasApiKey ? "definida" : missing("em falta")],
        ], "WhatsApp__Enabled, WhatsApp__Phone e WhatsApp__ApiKey no .env. A chave nunca passa pelo dashboard."),
        tile("Email", "mail", "Email", notification.email.enabled, [
          ["Servidor", notification.email.smtpHost || "—"],
          ["De", notification.email.from || "—"],
          ["Para", notification.email.to.length ? notification.email.to.join(", ") : "—"],
          ["Password", notification.email.hasPassword ? "definida" : missing("em falta")],
        ], "Email__… no .env. A password nunca passa pelo dashboard.")));
}
