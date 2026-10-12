"use strict";

/* ==========================================================================
   Editors (drawers): product rules, stores, platform detection, bulk store tests, JSON import/export.
   ========================================================================== */

const DEFAULT_EXCLUDED = ["sleeves", "binder", "proxy", "break", "acrylic", "acrilico"];

/* ----- Product (stock task) ----- */

function openTaskDrawer(task, isCopy = false) {
  const isNew = !task || isCopy;
  const draft = {
    id: task?.id || "",
    name: task?.name || "",
    enabled: task?.enabled ?? true,
    searchQuery: task?.searchQuery || "",
    requiredKeywords: [...(task?.requiredKeywords || [])],
    excludedKeywords: [...(task?.excludedKeywords || DEFAULT_EXCLUDED)],
    maxPrice: task?.maxPrice ?? "",
    storeNames: new Set(task?.storeNames || []),
    language: task?.language || "English",
    game: task?.game || "Pokemon",
    priceRule: task?.priceRule || (task ? "MaxPrice" : "Msrp"),
    tolerance: task?.msrpTolerancePercent ?? 10,
    kinds: new Set(task?.kinds || []),
  };
  if (isNew && !draft.id && draft.name) draft.id = slugify(draft.name);
  const errors = h("div", {});
  const idInput = textInput(draft.id, (value) => { draft.id = value; }, { readOnly: !isNew, placeholder: "made from the name", class: "mono" });
  idInput.addEventListener("input", () => { idInput.dataset.touched = "1"; });

  const stores = state.stores.filter((store) => store.platform !== "Manual");
  const body = [
    drawerSection("Product",
      h("div", { class: "fields" },
        field("Name", textInput(draft.name, (value) => {
          draft.name = value;
          if (isNew && !idInput.dataset.touched) { draft.id = slugify(value); idInput.value = draft.id; }
        }, { placeholder: "e.g. Perfect Order ETB" }), null, true),
        field("Game", h("select", { onchange: (event) => { draft.game = event.target.value; } },
          Object.entries(GAMES).map(([value, label]) => h("option", { value, selected: draft.game === value }, label)))),
        field("Language", segmented(Object.entries(TASK_LANGUAGES).map(([key, [, label]]) => [key, label]), draft.language, (value) => { draft.language = value; }, "Product language"),
          "Japanese: only alerts when the title says JP / Japanese."),
        field("Search text", textInput(draft.searchQuery, (value) => { draft.searchQuery = value; }, { placeholder: "elite trainer box" }),
          "What is typed in each store's search. Skip “pokemon”: some stores leave it out of titles.", true),
        field("Must have all", keywordEditor(draft.requiredKeywords, "accent", () => {}, "Must have all"),
          "All of them must be in the title. Alternatives with | (booster box|booster display = either) and * for the start of a word (op* matches OP-15 and OP10).", true),
        field("Ignore if it has", keywordEditor(draft.excludedKeywords, "red", () => {}, "Ignore if it has"), "None of them may be in the title. | and * work here too.", true),
        field("Id", idInput, isNew ? "Internal identifier (can't change later)." : "Can't be changed."))),
    priceSection(draft),
    drawerSection("Product types",
      h("p", { class: "muted small" }, "To watch a whole set but only some types (e.g. ETB and Booster Bundle, no loose boosters). None ticked = all."),
      h("div", { class: "store-picks" }, state.msrp.kinds.filter((kind) => kind.code !== "other").map((kind) => {
        const on = draft.kinds.has(kind.code);
        return h("button", {
          type: "button", class: `pick-chip ${on ? "on" : ""}`, "aria-pressed": on ? "true" : "false",
          onclick: (event) => {
            const now = !draft.kinds.has(kind.code);
            if (now) draft.kinds.add(kind.code); else draft.kinds.delete(kind.code);
            event.currentTarget.classList.toggle("on", now);
            event.currentTarget.setAttribute("aria-pressed", now ? "true" : "false");
          },
        }, kind.label);
      }))),
    drawerSection("Stores",
      h("p", { class: "muted small" }, "None ticked = every active store."),
      h("div", { class: "store-picks" }, stores.map((store) => {
        const on = [...draft.storeNames].some((name) => sameName(name, store.name));
        return h("button", {
          type: "button", class: `pick-chip ${on ? "on" : ""}`, "aria-pressed": on ? "true" : "false",
          onclick: (event) => {
            const selected = [...draft.storeNames].some((name) => sameName(name, store.name));
            for (const name of [...draft.storeNames]) if (sameName(name, store.name)) draft.storeNames.delete(name);
            if (!selected) draft.storeNames.add(store.name);
            event.currentTarget.classList.toggle("on", !selected);
            event.currentTarget.setAttribute("aria-pressed", !selected ? "true" : "false");
          },
        }, store.name, store.enabled ? null : h("span", { class: "muted" }, " (off)"));
      }))),
    drawerSection(null, labelledSwitch(draft.enabled, "Watching (turn off to pause)", (checked) => { draft.enabled = checked; })),
    errors,
  ];

  const save = async () => {
    errors.replaceChildren();
    try {
      const id = draft.id.trim();
      await post("/stocktask/save", {
        isNew,
        id,
        name: draft.name.trim(),
        enabled: draft.enabled,
        storeNames: [...draft.storeNames],
        searchQuery: draft.searchQuery.trim(),
        requiredKeywords: draft.requiredKeywords,
        excludedKeywords: draft.excludedKeywords,
        maxPrice: Number(String(draft.maxPrice).replace(",", ".")) || 0,
        priceRule: draft.priceRule,
        msrpTolerancePercent: Number(draft.tolerance) || 0,
        kinds: [...draft.kinds],
        language: draft.language,
        game: draft.game,
      });
      await reloadConfiguration();
      closeDrawer();
      toast(isNew ? "Watching the new product" : "Rules saved", "success");
      if (isNew && state.route.view === "watch") { state.wizard = null; go("watched", id); }
    } catch (error) {
      errors.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  openDrawer({
    title: isNew ? "Watch a product" : "Edit rules",
    subtitle: isNew ? "A product to watch, with the price you are willing to pay." : task.name,
    body,
    footer: [button("Cancel", { kind: "ghost", onClick: closeDrawer }), button(isNew ? "Start watching" : "Save", { iconName: "check", kind: "primary", onClick: save })],
  });
}

/** The price rule of the task editor: MSRP + margin (per kind) or a fixed maximum. Re-renders itself. */
function priceSection(draft) {
  const section = h("section", { class: "drawer-section" });
  const render = () => {
    const msrpMode = draft.priceRule === "Msrp";
    const samples = ["etb", "booster-bundle", "booster-display", "booster-pack"].filter((kind) => msrpOf(kind, draft.game));
    section.replaceChildren(
      h("h3", {}, "Price"),
      segmented([["Msrp", "MSRP + margin"], ["MaxPrice", "Fixed price"]], draft.priceRule, (value) => { draft.priceRule = value; render(); }, "Price rule"),
      msrpMode
        ? h("div", { class: "fields" },
          field("Margin over MSRP (%)", h("input", { type: "number", min: -50, max: 300, step: 1, value: draft.tolerance, class: "mono", oninput: (event) => { draft.tolerance = event.target.value; } }),
            samples.length ? `With 10%: ${samples.map((kind) => `${kindLabel(kind)} up to ${money(Math.round(msrpOf(kind, draft.game) * 110) / 100)}`).join(" · ")}.` : "This game has no MSRP yet: use the fixed max below."),
          field("Types without MSRP: up to (€)", h("input", { type: "number", min: 0, step: "0.01", value: draft.maxPrice || "", placeholder: "don't alert", class: "mono", oninput: (event) => { draft.maxPrice = event.target.value; } }),
            "Optional. Empty = no alerts for types without MSRP."))
        : field("Max price (€)", h("input", { type: "number", min: 0, step: "0.01", value: draft.maxPrice, class: "mono", oninput: (event) => { draft.maxPrice = event.target.value; } }),
          "The same for everything the search finds. For a whole set, MSRP + margin is better: it compares each type with its own price."));
  };
  render();
  return section;
}

/* ----- Products: JSON import / export ----- */

const TASK_IMPORT_EXAMPLE = `[
  {
    "id": "pkm-etb-en",
    "name": "Pokémon ETB (EN)",
    "searchQuery": "elite trainer box",
    "requiredKeywords": ["pokemon", "elite trainer box"],
    "excludedKeywords": ["sleeves", "binder", "proxy"],
    "maxPrice": 0,
    "priceRule": "Msrp",
    "msrpTolerancePercent": 10,
    "kinds": ["etb"],
    "language": "English",
    "game": "Pokemon",
    "storeNames": [],
    "enabled": true
  }
]`;

/** The task fields the import accepts, in the same shape "Export JSON" produces. */
function taskToJson(task) {
  return {
    id: task.id, name: task.name, enabled: task.enabled, storeNames: task.storeNames || [], searchQuery: task.searchQuery,
    requiredKeywords: task.requiredKeywords || [], excludedKeywords: task.excludedKeywords || [], maxPrice: task.maxPrice, language: task.language,
    game: task.game || "Pokemon", priceRule: task.priceRule || "MaxPrice", msrpTolerancePercent: task.msrpTolerancePercent ?? 10, kinds: task.kinds || [],
  };
}

function exportTasks() {
  downloadJson(JSON.stringify(state.tasks.map(taskToJson), null, 2), `holo-products-${new Date().toISOString().slice(0, 10)}.json`);
}

/**
 * Turns one imported object into a StockTaskSaveRequest (without isNew). Lenient on the shape — keywords as an
 * array or "a, b" text, language as English/Japanese/EN/JP, price as 59.99 or "59,99" — the API still validates.
 */
function normalizeImportedTask(raw) {
  if (!raw || typeof raw !== "object" || Array.isArray(raw)) throw new Error("not an object");
  const list = (value) => Array.isArray(value) ? value.map((item) => String(item).trim()).filter(Boolean) : splitList(String(value ?? ""));
  const name = String(raw.name ?? "").trim();
  const id = String(raw.id ?? "").trim()
    || name.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
  const languageText = String(raw.language ?? "English").trim().toLowerCase();
  const language = ["japanese", "jp", "jpn", "japones", "japonês", "ja"].includes(languageText) ? "Japanese"
    : ["english", "en", "eng", "ingles", "inglês"].includes(languageText) ? "English" : null;
  if (!language) throw new Error(`unknown language "${raw.language}" (use English or Japanese)`);
  const gameText = String(raw.game ?? "Pokemon").trim().toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/[^a-z]/g, "");
  const game = { pokemon: "Pokemon", onepiece: "OnePiece", op: "OnePiece", lorcana: "Lorcana", magic: "Magic", mtg: "Magic", magicthegathering: "Magic", riftbound: "Riftbound", cyberpunk: "Cyberpunk", other: "Other", outro: "Other" }[gameText];
  if (!game) throw new Error(`unknown game "${raw.game}" (use Pokemon, OnePiece, Riftbound, Cyberpunk, Lorcana, Magic or Other)`);
  return {
    id, name, enabled: raw.enabled === undefined ? true : Boolean(raw.enabled),
    storeNames: list(raw.storeNames), searchQuery: String(raw.searchQuery ?? "").trim(),
    requiredKeywords: list(raw.requiredKeywords), excludedKeywords: list(raw.excludedKeywords),
    maxPrice: typeof raw.maxPrice === "number" ? raw.maxPrice : Number(String(raw.maxPrice ?? "").replace(",", ".")) || 0,
    language,
    game,
    priceRule: String(raw.priceRule ?? "MaxPrice").toLowerCase() === "msrp" ? "Msrp" : "MaxPrice",
    msrpTolerancePercent: Number(raw.msrpTolerancePercent ?? 10) || 0,
    kinds: list(raw.kinds),
  };
}

/** Shared body of the two JSON import drawers. */
function importDrawerBody({ placeholder, help, switchLabel, switchValue, onSwitch, onText, results }) {
  const textarea = h("textarea", { rows: 16, spellcheck: "false", placeholder, class: "mono code", oninput: (event) => onText(event.target.value) });
  const fileInput = h("input", {
    type: "file", accept: ".json,application/json",
    onchange: async (event) => { const file = event.target.files?.[0]; if (file) { const text = await file.text(); textarea.value = text; onText(text); } },
  });
  return [
    drawerSection(null,
      field("File", fileInput, "Optional: load a .json instead of pasting.", true),
      field("JSON", textarea, help, true),
      labelledSwitch(switchValue, switchLabel, onSwitch)),
    results,
  ];
}

function importSummary(results, rows, created, updated, skipped, failed) {
  results.replaceChildren(
    h("div", { class: "callout accent" }, icon("info"), h("div", {}, `${created} created, ${updated} updated, ${skipped} skipped, ${failed} failed.`)),
    h("ul", { class: "import-results" }, rows.map(([label, tone, message]) => h("li", {}, pill(message, tone), h("span", {}, label)))));
}

function parseImportList(text, key) {
  const parsed = JSON.parse(text.trim() || "null");
  const items = Array.isArray(parsed) ? parsed : Array.isArray(parsed?.[key]) ? parsed[key] : parsed && typeof parsed === "object" ? [parsed] : null;
  if (!items || items.length === 0) throw new Error("expected a list: [ { ... }, { ... } ]");
  return items;
}

function openTaskImportDrawer() {
  let text = "";
  let updateExisting = false;
  let running = false;
  const results = h("div", {});

  const runImport = async () => {
    if (running) return;
    let items;
    try { items = parseImportList(text, "tasks"); } catch (error) { results.replaceChildren(errorList([`Invalid JSON: ${error.message}`])); return; }

    running = true;
    const existing = new Set(state.tasks.map((task) => task.id.toLowerCase()));
    const rows = [];
    let created = 0, updated = 0, skipped = 0, failed = 0;
    for (const [index, raw] of items.entries()) {
      results.replaceChildren(h("p", { class: "muted" }, `Importing… ${index + 1}/${items.length}`));
      const label = raw?.name || raw?.id || `#${index + 1}`;
      try {
        const task = normalizeImportedTask(raw);
        const exists = existing.has(task.id.toLowerCase());
        if (exists && !updateExisting) { skipped++; rows.push([label, "plain", "already exists — skipped"]); continue; }
        await post("/stocktask/save", { isNew: !exists, ...task });
        existing.add(task.id.toLowerCase());
        if (exists) { updated++; rows.push([label, "blue", "updated"]); } else { created++; rows.push([label, "accent", "created"]); }
      } catch (error) {
        failed++;
        rows.push([label, "red", errorText(error)]);
      }
    }
    running = false;
    await reloadConfiguration();
    importSummary(results, rows, created, updated, skipped, failed);
    if (failed === 0) toast(`Import done: ${created + updated} product(s)`, "success");
  };

  openDrawer({
    title: "Import products (JSON)",
    subtitle: "Paste a list or pick a file. The format is the same as “Export JSON”.",
    body: importDrawerBody({
      placeholder: TASK_IMPORT_EXAMPLE,
      help: "Fields: id, name, searchQuery, requiredKeywords, excludedKeywords, priceRule (Msrp/MaxPrice), msrpTolerancePercent, maxPrice, kinds (etb, booster-bundle…; [] = all), language (English/Japanese), game (Pokemon/OnePiece/Riftbound/Cyberpunk/Lorcana/Magic/Other), storeNames ([] = all), enabled. Without an id, one is made from the name.",
      switchLabel: "Update the ones that already exist (same id)", switchValue: updateExisting, onSwitch: (checked) => { updateExisting = checked; },
      onText: (value) => { text = value; }, results,
    }),
    footer: [button("Close", { kind: "ghost", onClick: closeDrawer }), button("Import", { iconName: "check", kind: "primary", onClick: runImport })],
  });
}

/* ----- Stores ----- */

/** Builds the StoreSaveRequest the API expects from a store draft. */
function storeToSaveRequest(store, originalName) {
  return {
    originalName,
    name: (store.name || "").trim(),
    enabled: store.enabled,
    platform: store.platform,
    baseUrl: (store.baseUrl || "").trim(),
    unmarkedLanguage: store.unmarkedLanguage,
    wooCommerceProductsPath: store.wooCommerceProductsPath,
    shopifyCatalogPaths: store.platform === "Shopify" ? (store.shopifyCatalogPaths || "").trim() : "",
    html: store.platform === "Html" ? store.html : null,
    awinFeedKey: store.platform === "Awin" ? (store.awinFeedKey || "").trim() : "",
    country: store.country || "Portugal",
    tier: store.tier || "None",
    type: store.type || "Unknown",
    tags: Array.isArray(store.tags) ? store.tags : splitList(String(store.tags || "")),
    notes: (store.notes || "").trim(),
    shippingCost: euroOrNull(store.shippingCost),
    freeShippingFrom: euroOrNull(store.freeShippingFrom),
  };
}

/** "4,99" / 4.99 → 4.99; empty or not a number → null. */
function euroOrNull(value) {
  if (value === null || value === undefined || String(value).trim() === "") return null;
  const number = Number(String(value).replace(",", "."));
  return Number.isFinite(number) && number >= 0 ? number : null;
}

function openStoreDrawer(store, preset = null) {
  const isNew = !store;
  const originalName = store?.name || null;
  const draft = structuredClone(preset || store || {
    name: "", enabled: true, platform: "Shopify", baseUrl: "https://", unmarkedLanguage: "Unknown",
    wooCommerceProductsPath: "/wp-json/wc/store/v1/products", shopifyCatalogPaths: "", html: null,
    awinFeedKey: "", country: "Portugal", tier: "None", type: "Unknown", tags: [], notes: "",
  });
  draft.html = draft.html || { searchUrlTemplate: "", productSelector: "", titleSelector: "", priceSelector: "", linkSelector: "", outOfStockSelector: "", inStockSelector: "" };

  const errors = h("div", {});
  const platformFields = h("section", { class: "drawer-section" });
  const testResults = h("div", {});
  let testQuery = "elite trainer box";

  const renderPlatformFields = () => {
    if (draft.platform === "WooCommerce") {
      platformFields.replaceChildren(h("h3", {}, "WooCommerce"),
        field("Store API path", textInput(draft.wooCommerceProductsPath, (value) => { draft.wooCommerceProductsPath = value; }, { class: "mono" }),
          "Usually /wp-json/wc/store/v1/products. Some stores only answer without /v1."));
    } else if (draft.platform === "Html") {
      const selector = (label, key, hint) => field(label, textInput(draft.html[key], (value) => { draft.html[key] = value; }, { class: "mono" }), hint);
      platformFields.replaceChildren(h("h3", {}, "HTML selectors"),
        h("div", { class: "fields" },
          field("Search URL", textInput(draft.html.searchUrlTemplate, (value) => { draft.html.searchUrlTemplate = value; }, { placeholder: "https://store.pt/search?q={query}", class: "mono" }),
            "Use {query} for the search text, or the fixed URL of a category.", true),
          selector("Product card", "productSelector", "e.g. li.product-item"),
          selector("Title", "titleSelector", "empty = text of the card"),
          selector("Price", "priceSelector", "e.g. .price"),
          selector("Link", "linkSelector", "empty = first <a>"),
          selector("Sold-out marker", "outOfStockSelector", "only there when sold out"),
          selector("In-stock marker", "inStockSelector", "e.g. the buy button")));
    } else if (draft.platform === "Amazon") {
      platformFields.replaceChildren(h("div", { class: "callout accent" }, icon("info"), h("div", {},
        "Uses Amazon's official Creators API (no scraping). Set Amazon__CredentialId, Amazon__CredentialSecret and Amazon__PartnerTag in .env. "
        + "Base URL: https://www.amazon.es. The search text can be keywords or an ASIN (e.g. B0XXXXXXXX).")));
    } else if (draft.platform === "Awin") {
      platformFields.replaceChildren(h("h3", {}, "Feed Awin"),
        field("Feed key", textInput(draft.awinFeedKey, (value) => { draft.awinFeedKey = value; }, { placeholder: "e.g. MediaMarktES", class: "mono" }),
          "Letters, digits and _ only. The bot reads the Create-a-Feed URL from AwinFeeds__Feeds__<key> in .env (the store's site is never contacted). "
          + "Only works once the store approves you in its Awin programme.", true));
    } else if (draft.platform === "Manual") {
      platformFields.replaceChildren(h("div", { class: "callout" }, icon("info"), h("div", {},
        "No integration: the store stays in the list (link, tier, tags) but the bot never searches it. Pick another platform once you know how to read it.")));
    } else if (AWIN_FEED_KEYS[draft.platform]) {
      platformFields.replaceChildren(h("div", { class: "callout accent" }, icon("info"), h("div", {},
        "Reads this store's authorised Awin product feed (the store's site is not contacted). Put the Create-a-Feed URL in "
        + `AwinFeeds__Feeds__${AWIN_FEED_KEYS[draft.platform]} in .env. The feed is downloaded at most every 6 hours; without a feed, the store shows "integration not configured".`)));
    } else {
      platformFields.replaceChildren(h("h3", {}, "Shopify"),
        field("Catalogue (optional)", textInput(draft.shopifyCatalogPaths, (value) => { draft.shopifyCatalogPaths = value; }, { placeholder: "/collections/pokemon, /collections/riftbound", class: "mono" }),
          "Empty = the store's search (at most 10 products per search). With collections (comma-separated) or /products.json, the bot reads the catalogue "
          + "once per cycle and filters it for every product — no limit of 10. Prefer collections: /products.json reads the whole store.", true));
    }
  };
  renderPlatformFields();

  const runTest = async () => {
    testResults.replaceChildren(h("p", { class: "muted" }, "Searching…"));
    try {
      const results = await post("/store/probe", { store: storeToSaveRequest(draft, null), query: testQuery });
      testResults.replaceChildren();
      append(testResults, [results.length === 0
        ? h("div", { class: "callout" }, icon("info"), h("div", {}, "The store answered, but with no products for this search."))
        : [h("p", { class: "muted small" }, `${results.length} product(s) found`),
          h("ul", { class: "result-rows compact" }, results.slice(0, 10).map((listing) => h("li", { class: "result-row" },
            productArt({ imageUrl: listing.imageUrl, title: listing.title, size: "mini" }),
            h("div", { class: "result-text" }, externalLink(listing.url, listing.title, "result-title"),
              h("div", { class: "result-meta" }, `${listing.inStock ? "In stock" : "Sold out"} · ${LANGUAGES[listing.language] || listing.language}`)),
            h("div", { class: "result-price" }, h("span", { class: "mono price" }, money(listing.price))),
            Object.values(listing.outcomeByTask).some(isMatch) ? h("span", { class: "verdict accent solid" }, "Matches") : null)))]]);
    } catch (error) {
      testResults.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  const testQueryInput = textInput(testQuery, (value) => { testQuery = value; });
  testQueryInput.addEventListener("keydown", (event) => { if (event.key === "Enter") runTest(); });

  const body = [
    draft.detect ? detectNotice(draft.detect) : null,
    drawerSection("Store",
      h("div", { class: "fields" },
        field("Name", textInput(draft.name, (value) => { draft.name = value; }, { placeholder: "e.g. Versus Gamecenter" })),
        field("Platform", h("select", { onchange: (event) => { draft.platform = event.target.value; renderPlatformFields(); } },
          Object.entries(PLATFORMS).map(([value, label]) => h("option", { value, selected: draft.platform === value }, label)))),
        field("Base URL", h("div", { class: "input-with-button" },
          textInput(draft.baseUrl, (value) => { draft.baseUrl = value; }, { placeholder: "https://store.pt", "aria-label": "Base URL" }),
          button("Detect", { iconName: "search", onClick: () => detectInDrawer(store, draft), title: "Finds out if it is Shopify, WooCommerce or PrestaShop and fills in the configuration" })),
          "Paste the site (or its Pokémon category page) and press Detect.", true),
        field("Titles without a language", h("select", { onchange: (event) => { draft.unmarkedLanguage = event.target.value; } },
          Object.entries(UNMARKED_LANGUAGE).map(([value, [label]]) => h("option", { value, selected: draft.unmarkedLanguage === value }, label))),
          "“Count as English” only if the store always marks PT/ES/JP. “Alert, language to check” alerts anyway, asking you to check the page.", true)),
      labelledSwitch(draft.enabled, "Store on", (checked) => { draft.enabled = checked; })),
    drawerSection("Classification",
      h("div", { class: "fields" },
        field("Area", h("select", { onchange: (event) => { draft.country = event.target.value; } },
          Object.entries(COUNTRIES).map(([value, [label]]) => h("option", { value, selected: draft.country === value }, label)))),
        field("Tier", h("select", { onchange: (event) => { draft.tier = event.target.value; } },
          Object.entries(TIERS).map(([value, [label, , description]]) => h("option", { value, selected: (draft.tier || "None") === value }, `${label} — ${description}`)))),
        field("Store type", h("select", { onchange: (event) => { draft.type = event.target.value; } },
          Object.entries(STORE_TYPES).map(([value, label]) => h("option", { value, selected: (draft.type || "Unknown") === value }, label)))),
        field("Tags", textInput((draft.tags || []).join(", "), (value) => { draft.tags = splitList(value); }, { placeholder: "untested, ships to PT" }),
          "Comma-separated. They show on the card and work as filters."),
        field("Notes", h("textarea", { rows: 3, oninput: (event) => { draft.notes = event.target.value; } }, draft.notes || ""), null, true))),
    drawerSection("Shipping",
      h("div", { class: "fields" },
        field("Shipping (€)", h("input", { type: "number", min: 0, max: 500, step: "0.01", value: draft.shippingCost ?? "", placeholder: "not known", class: "mono", inputmode: "decimal",
          oninput: (event) => { draft.shippingCost = event.target.value; } }), "What one order costs to ship to you. 0 = always free."),
        field("Free shipping from (€)", h("input", { type: "number", min: 0, max: 500, step: "0.01", value: draft.freeShippingFrom ?? "", placeholder: "never", class: "mono", inputmode: "decimal",
          oninput: (event) => { draft.freeShippingFrom = event.target.value; } }), "Orders from this amount ship free. Prices are compared with shipping included."))),
    platformFields,
    drawerSection("Test before saving",
      h("div", { class: "input-with-button" }, testQueryInput, button("Test", { iconName: "flask", onClick: runTest })),
      testResults),
    errors,
  ];

  const save = async () => {
    errors.replaceChildren();
    try {
      await post("/store/save", storeToSaveRequest(draft, originalName));
      await reloadConfiguration();
      closeDrawer();
      toast(isNew ? "Store added" : "Store saved", "success");
    } catch (error) {
      errors.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  openDrawer({
    title: isNew ? "Add store" : `Edit ${store.name}`,
    subtitle: "Test the configuration before saving.",
    body,
    footer: [
      !isNew ? button("Delete", { kind: "text-danger", onClick: () => deleteStore(store) }) : null,
      h("span", { class: "spacer" }),
      button("Cancel", { kind: "ghost", onClick: closeDrawer }),
      button(isNew ? "Add" : "Save", { iconName: "check", kind: "primary", onClick: save }),
    ],
  });
}

/* ----- Stores: detect platform ----- */

/** Applies a detection to a store draft (platform setup only; name only when empty). */
function applyDetection(draft, result) {
  if (result.status !== "Detected") return draft;
  return {
    ...draft,
    name: (draft.name || "").trim() || result.suggestedName,
    baseUrl: result.baseUrl,
    platform: result.platform,
    shopifyCatalogPaths: result.platform === "Shopify" ? result.shopifyCatalogPaths : "",
    wooCommerceProductsPath: result.platform === "WooCommerce" ? result.wooCommerceProductsPath : draft.wooCommerceProductsPath,
    html: result.platform === "Html" ? result.html : draft.html,
  };
}

async function detectInDrawer(store, draft) {
  try {
    toast("Detecting the platform…", "info");
    const result = await post("/store/detect", { url: draft.baseUrl });
    const next = applyDetection(draft, result);
    if (result.status !== "Detected") { next.baseUrl = result.baseUrl || draft.baseUrl; if (!next.name) next.name = result.suggestedName || ""; }
    next.detect = result;
    openStoreDrawer(store, next);
  } catch (error) {
    toast(errorText(error), "error");
  }
}

function detectNotice(result) {
  const tone = result.status === "Detected" ? "accent" : result.status === "Unknown" ? "" : "amber";
  return h("div", { class: `callout ${tone}` }, icon(result.status === "Detected" ? "check" : "info"),
    h("div", {}, h("strong", {}, result.message),
      result.evidence?.length ? h("details", {}, h("summary", {}, "What was checked"),
        h("ul", {}, result.evidence.map((line) => h("li", {}, line)))) : null));
}

/* ----- Stores: test many at once ----- */

const TEST_WORKERS = 4;

function openStoreTestDrawer() {
  const scopes = {
    visible: ["The ones visible with the current filters", () => state.stores.filter(storeMatchesFilters)],
    never: ["Never tested", () => state.stores.filter((store) => !store.lastTest)],
    failed: ["Failed the last test", () => state.stores.filter((store) => store.lastTest && TEST_FAILED.has(store.lastTest.status))],
    disabled: ["Off", () => state.stores.filter((store) => !store.enabled)],
    all: ["All", () => state.stores],
  };
  let scope = "visible";
  let query = "pokemon";
  let running = false;
  let stopRequested = false;
  let list = [];
  const rows = new Map();                                  // name -> { state: "waiting" | "running" | "done", result, note }

  const table = h("div", {});
  const progress = h("span", { class: "muted small" });
  const actions = h("div", { class: "button-row" });
  const scopeCount = h("span", { class: "muted small" });
  const refreshCount = () => { scopeCount.textContent = `${scopes[scope][1]().length} store(s)`; };

  const render = () => {
    const done = [...rows.values()].filter((row) => row.state === "done").length;
    progress.textContent = rows.size ? `${done} of ${rows.size} tested${running ? "…" : ""}` : "";
    table.replaceChildren(rows.size === 0 ? h("p", { class: "muted" }, "Pick the stores and press Start.") : h("div", { class: "table-wrap" }, h("table", { class: "table" },
      h("thead", {}, h("tr", {}, h("th", {}, "Store"), h("th", {}, "Result"), h("th", { class: "num" }, "Products"), h("th", { class: "num" }, "Recognised"), h("th", {}, "Example / reason"))),
      h("tbody", {}, list.map((listed) => {
        const store = state.stores.find((candidate) => candidate.name === listed.name) || listed;
        const row = rows.get(store.name);
        const result = row?.result;
        return h("tr", {},
          h("td", { class: "nowrap" }, h("div", { class: "store-line" }, storeAvatar(store.name, "small"), h("strong", {}, store.name)),
            h("div", { class: "muted small" }, `${store.platform}${store.enabled ? " · on" : " · off"}`)),
          h("td", { class: "nowrap" }, row?.state === "running" ? pill("Testing…", "accent")
            : result ? testPill(result) : pill("Waiting")),
          h("td", { class: "num mono" }, result ? result.results : "—"),
          h("td", { class: "num mono" }, result ? `${result.relevant}${result.matches ? ` (${result.matches} alerta)` : ""}` : "—"),
          h("td", { class: "title-cell" },
            result ? h("span", { class: "clamp", title: result.sample || result.message }, result.sample || result.message || "—") : null,
            row?.note ? h("div", { class: "muted small" }, row.note) : null));
      })))));
  };

  const testOne = async (store) => {
    const row = rows.get(store.name);
    row.state = "running";
    render();
    try {
      row.result = await post("/store/test", { name: store.name, query });
    } catch (error) {
      row.result = { name: store.name, status: "Error", results: 0, relevant: 0, matches: 0, message: errorText(error) };
    }
    row.state = "done";
    render();
  };

  const runAll = async (stores) => {
    running = true;
    stopRequested = false;
    renderActions();
    const queue = [...stores];
    await Promise.all(Array.from({ length: TEST_WORKERS }, async () => {
      while (queue.length && !stopRequested) await testOne(queue.shift());
    }));
    running = false;
    await reloadConfiguration();
    render();
    renderActions();
  };

  const start = () => {
    if (running) return;
    list = scopes[scope][1]().slice().sort((a, b) => a.name.localeCompare(b.name, "en"));
    rows.clear();
    list.forEach((store) => rows.set(store.name, { state: "waiting" }));
    render();
    runAll(list);
  };

  const byResult = (predicate) => list.filter((store) => {
    const result = rows.get(store.name)?.result;
    const current = state.stores.find((candidate) => candidate.name === store.name) || store;
    return result && predicate(result, current);
  }).map((store) => state.stores.find((candidate) => candidate.name === store.name) || store);

  const bulk = async (stores, action, message) => {
    running = true;
    renderActions();
    let failed = 0;
    for (const store of stores) {
      try { await action(store); } catch (error) { failed++; const row = rows.get(store.name); if (row) row.note = errorText(error); }
    }
    running = false;
    await reloadConfiguration();
    render();
    renderActions();
    toast(failed ? `${message}: ${stores.length - failed} ok, ${failed} failed` : `${message}: ${stores.length}`, failed ? "error" : "success");
  };

  const redetect = async (stores) => {
    running = true;
    renderActions();
    for (const store of stores) {
      const row = rows.get(store.name);
      row.state = "running";
      render();
      try {
        const detection = await post("/store/detect", { url: store.baseUrl });
        if (detection.status === "Detected" && (detection.platform !== store.platform || detection.shopifyCatalogPaths !== (store.shopifyCatalogPaths || ""))) {
          const updated = applyDetection(store, detection);
          await post("/store/save", storeToSaveRequest({ ...updated, tags: (store.tags || []).filter((tag) => tag !== "plataforma por identificar" && tag !== "platform unknown") }, store.name));
          row.note = `Detected ${detection.platform}${detection.shopifyCatalogPaths ? ` (${detection.shopifyCatalogPaths})` : ""} — configuration updated.`;
          row.result = await post("/store/test", { name: store.name, query });
        } else {
          row.note = detection.message;
        }
      } catch (error) {
        row.note = errorText(error);
      }
      row.state = "done";
      render();
    }
    running = false;
    await reloadConfiguration();
    render();
    renderActions();
  };

  function renderActions() {
    if (running) {
      actions.replaceChildren(button("Stop", { iconName: "stop", onClick: () => { stopRequested = true; } }));
      return;
    }
    if (rows.size === 0) { actions.replaceChildren(h("p", { class: "muted small" }, "After the test, the actions show up here (turn on the ones that work, turn off the ones that failed…).")); return; }
    const toEnable = byResult((result, store) => result.status === "Ok" && !store.enabled && store.platform !== "Manual");
    const toDisable = byResult((result, store) => TEST_FAILED.has(result.status) && store.enabled);
    const blocked = byResult((result, store) => result.status === "Blocked" && store.platform !== "Manual");
    const toDetect = byResult((result) => ["Unreachable", "Error", "Empty", "NoIntegration"].includes(result.status));
    const action = (stores, label, onClick, primary) => button(`${label} (${stores.length})`, { kind: primary ? "primary" : "", disabled: stores.length === 0, onClick });
    actions.replaceChildren(
      action(toEnable, "Turn on the ones that work", () => bulk(toEnable, (store) => post("/store/enable", { name: store.name, enabled: true }), "Stores turned on"), true),
      action(toDisable, "Turn off the ones that failed", () => bulk(toDisable, (store) => post("/store/enable", { name: store.name, enabled: false }), "Stores turned off")),
      action(blocked, "Blocked → No integration", () => bulk(blocked, (store) => post("/store/save", storeToSaveRequest({
        ...store, platform: "Manual", enabled: false, tags: [...new Set([...(store.tags || []), "blocks bots (403)"])],
      }, store.name)), "Marked as No integration")),
      action(toDetect, "Detect the platform again", () => redetect(toDetect)),
    );
  }

  const scopeSelect = h("select", { "aria-label": "Stores to test", onchange: (event) => { scope = event.target.value; refreshCount(); } },
    Object.entries(scopes).map(([value, [label]]) => h("option", { value, selected: value === scope }, label)));
  refreshCount();
  render();
  renderActions();

  openDrawer({
    title: "Test several stores",
    subtitle: `Runs a search in each store (${TEST_WORKERS} at a time, within the per-domain limits) and saves the result on its card. “No integration” stores are not contacted.`,
    body: [
      drawerSection(null,
        h("div", { class: "fields" },
          field("Stores", h("div", { class: "input-with-button" }, scopeSelect, scopeCount)),
          field("Search text", textInput(query, (value) => { query = value; }), "“pokemon” matches almost everything; use “elite trainer box” for something finer.")),
        h("div", { class: "button-row" }, button("Start", { iconName: "play", kind: "primary", onClick: start }), progress)),
      drawerSection(null, table),
      drawerSection("After the test", actions),
    ],
    footer: [button("Close", { kind: "ghost", onClick: () => { stopRequested = true; closeDrawer(); } })],
    wide: true,
  });
}

/* ----- Stores: JSON import / export ----- */

function exportStores() {
  const json = JSON.stringify(state.stores.map((store) => ({ ...storeToSaveRequest(store, null), originalName: undefined })), null, 2);
  downloadJson(json, `holo-stores-${new Date().toISOString().slice(0, 10)}.json`);
}

/** The site of a store URL ("loja.pt"); a URL with a query (a search link) keeps it, so two searches are not one site. */
const hostOf = (url) => {
  try {
    const parsed = new URL(url);
    return (parsed.host.replace(/^www\./, "") + parsed.search).toLowerCase();
  } catch {
    return "";
  }
};

/**
 * Imports stores in the "Exportar JSON" shape. A store that already exists (same name, or same site) is not replaced:
 * with "Update classification" only its area, tier, type, tags and notes change — its platform setup is kept.
 */
function openStoreImportDrawer() {
  let text = "";
  let updateClassification = true;
  let running = false;
  const results = h("div", {});

  const runImport = async () => {
    if (running) return;
    let items;
    try { items = parseImportList(text, "stores"); } catch (error) { results.replaceChildren(errorList([`Invalid JSON: ${error.message}`])); return; }

    running = true;
    const rows = [];
    let created = 0, updated = 0, skipped = 0, failed = 0;
    for (const [index, raw] of items.entries()) {
      results.replaceChildren(h("p", { class: "muted" }, `Importing… ${index + 1}/${items.length}`));
      const label = raw?.name || `#${index + 1}`;
      try {
        if (!raw || typeof raw !== "object" || Array.isArray(raw)) throw new Error("not an object");
        const existing = state.stores.find((store) => sameName(store.name, String(raw.name || "")))
          || (hostOf(raw.baseUrl) && state.stores.find((store) => hostOf(store.baseUrl) === hostOf(raw.baseUrl)));
        if (existing) {
          if (!updateClassification) { skipped++; rows.push([label, "plain", `already exists (${existing.name}) — skipped`]); continue; }
          const merged = {
            ...existing, country: raw.country || existing.country, tier: raw.tier || existing.tier, type: raw.type || existing.type,
            tags: [...new Set([...(existing.tags || []), ...(Array.isArray(raw.tags) ? raw.tags : [])].filter((tag) => tag !== "por testar" && tag !== "untested"))],
            notes: existing.notes || raw.notes || "",
            shippingCost: raw.shippingCost ?? existing.shippingCost ?? null,
            freeShippingFrom: raw.freeShippingFrom ?? existing.freeShippingFrom ?? null,
          };
          await post("/store/save", storeToSaveRequest(merged, existing.name));
          updated++; rows.push([label, "blue", `classification updated (${existing.name})`]);
          continue;
        }
        const draft = {
          name: "", enabled: false, platform: "Manual", baseUrl: "", unmarkedLanguage: "Unknown",
          wooCommerceProductsPath: "/wp-json/wc/store/v1/products", shopifyCatalogPaths: "", html: null,
          awinFeedKey: "", country: "Portugal", tier: "None", type: "Unknown", tags: [], notes: "", ...raw,
        };
        await post("/store/save", storeToSaveRequest(draft, null));
        state.stores.push(draft);
        created++; rows.push([label, "accent", `created (${draft.platform})`]);
      } catch (error) {
        failed++;
        rows.push([label, "red", errorText(error)]);
      }
    }
    running = false;
    await reloadConfiguration();
    importSummary(results, rows, created, updated, skipped, failed);
    if (failed === 0) toast(`Import done: ${created + updated} store(s)`, "success");
  };

  openDrawer({
    title: "Import stores (JSON)",
    subtitle: "Paste a list of stores or pick a file. The format is the same as “Export JSON”.",
    body: importDrawerBody({
      placeholder: '[\n  { "name": "Store", "platform": "Shopify", "baseUrl": "https://store.pt", "country": "Portugal", "tier": "A", "type": "Specialist", "tags": ["untested"], "enabled": false }\n]',
      help: "Fields: name, platform (Shopify, WooCommerce, Html, Amazon, Fnac, Worten, ElCorteIngles, Awin, Manual), baseUrl, country (Portugal/Spain/Europe), tier (S/A/B/C), type (Retail/Specialist/Importer/Marketplace/Official), tags, notes, enabled, shippingCost, freeShippingFrom, shopifyCatalogPaths, wooCommerceProductsPath, html, awinFeedKey.",
      switchLabel: "For the ones that already exist (same name or site), only update area, tier, type, tags and shipping", switchValue: updateClassification,
      onSwitch: (checked) => { updateClassification = checked; }, onText: (value) => { text = value; }, results,
    }),
    footer: [button("Close", { kind: "ghost", onClick: closeDrawer }), button("Import", { iconName: "check", kind: "primary", onClick: runImport })],
  });
}
