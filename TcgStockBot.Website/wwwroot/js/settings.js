"use strict";

/* ==========================================================================
   Settings — appearance (per browser), check rhythm (saved in the bot), alert channels (from .env) and accounts.
   ========================================================================== */

RENDERERS.settings = function viewSettings() {
  const settings = state.settings;
  const draft = {
    interval: Math.round(settings.pollIntervalSeconds / 60),
    jitter: Math.round(settings.pollJitterSeconds / 60),
    delay: settings.delayBetweenRequestsSeconds,
    userAgent: settings.userAgent,
    autoDisable: settings.autoDisableAfterFailures,
    summary: Boolean(settings.dailySummaryEnabled),
    summaryHour: settings.dailySummaryHour ?? 9,
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
        dailySummaryEnabled: draft.summary,
        dailySummaryHour: draft.summaryHour,
      });
      toast("Settings saved", "success");
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
    pageHead("Settings", "Appearance, how often the bot checks, alerts and your account.", [button("Save changes", { kind: "primary", onClick: save })]),
    appearanceCard(),
    h("section", { class: "card settings-card", "aria-labelledby": "rhythm-h" },
      h("h2", { id: "rhythm-h" }, "Checks"),
      h("p", { class: "muted small" }, "Saved in the bot, for every browser."),
      h("div", { class: "fields three" },
        numberField("Interval between cycles", "interval", "min", "At least 1. Between 5 and 15 is kind to the stores.", 1),
        numberField("Random variation", "jitter", "± min", "Added to the interval so checks don't always land at the same time.", 0),
        numberField("Pause between requests", "delay", "s", "Between consecutive requests to the same store.", 0),
        numberField("Turn a store off after", "autoDisable", "cycles", "Failed cycles in a row before the bot turns the store off and tells you. 0 = never.", 0),
        field("User-Agent", textInput(draft.userAgent, (value) => { draft.userAgent = value; }, { class: "mono" }), "How the bot introduces itself to stores. Keep it honest.", true)),
      errors,
      h("div", { class: "card-actions" }, button("Save changes", { kind: "primary", onClick: save }))),
    channelsCard(),
    h("section", { class: "card settings-card", "aria-labelledby": "summary-h" },
      h("h2", { id: "summary-h" }, "Daily summary"),
      h("p", { class: "muted small" }, "One message a day with the alerts of the last 24 hours, what is in stock at your price and the stores with errors. Sent by every channel that is on (push, email, Telegram, WhatsApp)."),
      h("div", { class: "setting-row" },
        h("div", {}, h("strong", {}, "Send the summary"), h("div", { class: "muted small" }, "Saved with “Save changes”.")),
        switchInput(draft.summary, (checked) => { draft.summary = checked; }, "Send the daily summary")),
      h("div", { class: "setting-row" },
        h("div", {}, h("strong", {}, "Time"), h("div", { class: "muted small" }, "Portugal time (the NAS clock).")),
        selectPill("Hour", String(draft.summaryHour), Array.from({ length: 24 }, (_, hour) => [String(hour), `${String(hour).padStart(2, "0")}:00`]),
          (value) => { draft.summaryHour = Number(value); }))),
    msrpCard(),
    accountCard(),
    h("section", { class: "card settings-card", "aria-labelledby": "memory-h" },
      h("h2", { id: "memory-h" }, "Alert memory"),
      h("p", { class: "muted" }, "The bot doesn't repeat an alert while the product stays in stock. Clearing the memory alerts you again about everything available."),
      h("div", { class: "card-actions" }, button("Clear memory", { iconName: "refresh", onClick: resetAlertMemory }))),
  ];
};

function appearanceCard() {
  const box = h("section", { class: "card settings-card", "aria-labelledby": "look-h" });
  const render = () => box.replaceChildren(
    h("h2", { id: "look-h" }, "Appearance"),
    h("p", { class: "muted small" }, "Pick a theme. It changes right away and is kept in this browser."),
    h("div", { class: "theme-grid", role: "radiogroup", "aria-label": "Theme" }, Object.entries(ACCENTS).map(([key, [label, dark, light]]) => {
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
      h("div", {}, h("strong", {}, "Mode"), h("div", { class: "muted small" },
        theme.mode === "dark" ? "Dark background. The default." : theme.mode === "light" ? "Light background." : "Follows the system (dark or light).")),
      segmented(Object.entries(MODES).map(([key, label]) => [key, label]), theme.mode, (value) => { setTheme({ mode: value }); render(); }, "Mode")));
  render();
  return box;
}

function channelsCard() {
  const notification = state.notification;
  const tile = (name, iconName, channel, enabled, rows, note) => h("div", { class: `channel ${enabled ? "on" : ""}` },
    h("div", { class: "channel-head" },
      h("span", { class: "channel-icon" }, icon(iconName)),
      h("div", {}, h("strong", {}, name), h("div", { class: "muted small" }, enabled ? "On · set up in .env" : "Off")),
      h("span", { class: `bot-dot ${enabled ? "on" : "off"}`, "aria-hidden": "true" })),
    h("dl", { class: "store-facts" }, rows.map(([label, value]) => [h("dt", {}, label), h("dd", {}, value)])),
    h("p", { class: "muted small" }, note),
    button("Send a test message", { kind: "small", disabled: !enabled, onClick: () => sendTest(channel) }));
  const missing = (text) => h("span", { class: "amber" }, text);

  return h("section", { class: "card settings-card", "aria-labelledby": "channels-h" },
    h("div", { class: "card-head-row" }, h("h2", { id: "channels-h" }, "Alert channels"),
      button("Send a test message", { onClick: sendTestToAll, disabled: !anyChannelEnabled() })),
    !notification
      ? h("p", { class: "muted" }, "Loading channels…")
      : h("div", { class: "channels" },
        tile("WhatsApp", "chat", "WhatsApp", notification.whatsApp.enabled, [
          ["Number", notification.whatsApp.maskedPhone || missing("missing")],
          ["API key", notification.whatsApp.hasApiKey ? "set" : missing("missing")],
        ], "WhatsApp__Enabled, WhatsApp__Phone and WhatsApp__ApiKey in .env. The key never goes through Holo."),
        tile("Email", "mail", "Email", notification.email.enabled, [
          ["Server", notification.email.smtpHost || "—"],
          ["From", notification.email.from || "—"],
          ["To", notification.email.to.length ? notification.email.to.join(", ") : "—"],
          ["Password", notification.email.hasPassword ? "set" : missing("missing")],
        ], "Email__… in .env. The password never goes through Holo."),
        tile("Telegram", "send", "Telegram", Boolean(notification.telegram?.enabled), [
          ["Chat", notification.telegram?.maskedChatId || missing("missing")],
          ["Bot token", notification.telegram?.hasBotToken ? "set" : missing("missing")],
        ], "Create a bot with @BotFather and put Telegram__Enabled, Telegram__BotToken and Telegram__ChatId in .env. The token never goes through Holo."),
        pushTile()));
}

/** The signed-in account: change password, sign out; admins also manage the other accounts. */
function accountCard() {
  const box = h("section", { class: "card settings-card", "aria-labelledby": "account-h" });
  const userName = APP?.dataset.user || "";
  const isAdmin = APP?.dataset.admin === "true";
  const draft = { current: "", next: "", confirm: "" };
  const errors = h("div", {});

  const changePassword = async () => {
    errors.replaceChildren();
    if (draft.next !== draft.confirm) { errors.replaceChildren(errorList(["The two new passwords are different."])); return; }
    try {
      await post("/user/password", { currentPassword: draft.current, newPassword: draft.next });
      toast("Password changed. Other devices are signed out.", "success");
      draft.current = draft.next = draft.confirm = "";
      box.querySelectorAll("input[type=password]").forEach((input) => { input.value = ""; });
    } catch (error) {
      errors.replaceChildren(errorList(error.errors || [error.message]));
    }
  };
  const passwordInput = (key, autocomplete) => h("input", { type: "password", autocomplete, oninput: (event) => { draft[key] = event.target.value; } });

  append(box, [
    h("div", { class: "card-head-row" },
      h("h2", { id: "account-h" }, "Account"),
      h("div", { class: "account-who" }, h("span", { class: "avatar-initial", "aria-hidden": "true" }, (userName[0] || "?").toUpperCase()),
        h("span", {}, "Signed in as ", h("strong", {}, userName || "—")), isAdmin ? pill("admin", "accent") : null)),
    h("div", { class: "fields three" },
      field("Current password", passwordInput("current", "current-password")),
      field("New password", passwordInput("next", "new-password"), "At least 8 characters."),
      field("Confirm new password", passwordInput("confirm", "new-password"))),
    errors,
    h("div", { class: "card-actions" },
      linkButton("About Holo", "/about", { iconName: "info", kind: "ghost" }),
      button("Sign out", { iconName: "logout", onClick: signOut }),
      button("Change password", { kind: "primary", onClick: changePassword })),
    isAdmin ? usersSection() : null,
  ]);
  return box;
}

/** Admins: every account, add one, delete one. */
function usersSection() {
  const box = h("div", { class: "users-box" });
  const draft = { userName: "", password: "" };
  const errors = h("div", {});

  const load = async () => {
    let users = [];
    try {
      users = await api("/user/list");
    } catch (error) {
      box.replaceChildren(errorList([errorText(error)]));
      return;
    }
    const me = APP?.dataset.user || "";
    box.replaceChildren(
      h("h3", { class: "users-title" }, "Accounts", h("span", { class: "muted small" }, ` · ${users.length}`)),
      h("div", { class: "table-wrap" }, h("table", { class: "table" },
        h("thead", {}, h("tr", {}, h("th", {}, "User"), h("th", {}, "Role"), h("th", {}, "Created"), h("th", {}, "Last sign-in"), h("th", {}))),
        h("tbody", {}, users.map((user) => h("tr", {},
          h("td", {}, h("strong", {}, user.userName), user.userName === me ? h("span", { class: "muted small" }, " (you)") : null),
          h("td", {}, user.role === "Admin" ? pill("admin", "accent") : pill("user")),
          h("td", { class: "muted small nowrap" }, dateTime(user.createdAt)),
          h("td", { class: "muted small nowrap" }, user.lastSignInAt ? relative(user.lastSignInAt) : "never"),
          h("td", { class: "actions" }, user.userName === me ? null : iconButton("trash", `Delete ${user.userName}`, () => removeUser(user)))))))),
      h("h3", { class: "users-title" }, "Add an account"),
      h("div", { class: "fields three" },
        field("User name", textInput("", (value) => { draft.userName = value; }, { autocomplete: "off", autocapitalize: "none" })),
        field("Password", h("input", { type: "password", autocomplete: "new-password", oninput: (event) => { draft.password = event.target.value; } }), "At least 8 characters. Share it with them; they can change it.")),
      errors,
      h("div", { class: "card-actions" }, button("Add account", { iconName: "plus", onClick: addUser })));
  };

  const addUser = async () => {
    errors.replaceChildren();
    try {
      await post("/user/create", { userName: draft.userName.trim(), password: draft.password });
      toast(`Account ${draft.userName.trim()} created`, "success");
      draft.userName = draft.password = "";
      await load();
    } catch (error) {
      errors.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  const removeUser = async (user) => {
    if (!await confirmDialog({ title: `Delete ${user.userName}?`, message: "They are signed out everywhere and can't sign in any more. The watched products, stores and alerts stay — they are shared.", confirmLabel: "Delete", danger: true })) return;
    try {
      await post(`/user/delete?identifier=${encodeURIComponent(user.id)}`);
      toast(`Account ${user.userName} deleted`, "success");
      await load();
    } catch (error) {
      toast(errorText(error), "error");
    }
  };

  box.append(h("p", { class: "muted small" }, "Loading accounts…"));
  load();
  return box;
}

/** POSTs /sign-out with the anti-forgery token (a plain form, so the browser follows the redirect). */
function signOut() {
  const form = h("form", { method: "post", action: "/sign-out", hidden: true },
    h("input", { type: "hidden", name: "__RequestVerificationToken", value: ANTIFORGERY_TOKEN }));
  document.body.append(form);
  form.submit();
}

/** The MSRP table the "MSRP + margin" rule uses (read-only here: it lives in data/msrp.json). */
function msrpCard() {
  const entries = state.msrp.entries.slice().sort((a, b) => kindOrder(a.kind) - kindOrder(b.kind));
  return h("section", { class: "card settings-card", "aria-labelledby": "msrp-h" },
    h("h2", { id: "msrp-h" }, "Reference prices (MSRP)"),
    h("p", { class: "muted small" }, "What the “MSRP + margin” rule uses for each product type. To change a value, edit data/msrp.json in the bot's volume."),
    entries.length
      ? h("div", { class: "msrp-grid" }, entries.map((entry) => h("div", { class: "msrp-item" },
        h("span", {}, kindLabel(entry.kind)), h("strong", { class: "mono" }, money(entry.price)),
        h("small", {}, `${entry.game} · ${(MSRP_BASIS[entry.basis] || MSRP_BASIS.Estimate)[0].toLowerCase()}`))))
      : h("p", { class: "muted" }, "No MSRP table."));
}
