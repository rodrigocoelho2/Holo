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
  const idInput = textInput(draft.id, (value) => { draft.id = value; }, { readOnly: !isNew, placeholder: "gerado a partir do nome", class: "mono" });
  idInput.addEventListener("input", () => { idInput.dataset.touched = "1"; });

  const stores = state.stores.filter((store) => store.platform !== "Manual");
  const body = [
    drawerSection("Produto",
      h("div", { class: "fields" },
        field("Nome", textInput(draft.name, (value) => {
          draft.name = value;
          if (isNew && !idInput.dataset.touched) { draft.id = slugify(value); idInput.value = draft.id; }
        }, { placeholder: "ex.: Perfect Order ETB" }), null, true),
        field("Jogo", h("select", { onchange: (event) => { draft.game = event.target.value; } },
          Object.entries(GAMES).map(([value, label]) => h("option", { value, selected: draft.game === value }, label)))),
        field("Idioma", segmented(Object.entries(TASK_LANGUAGES).map(([key, [, label]]) => [key, label]), draft.language, (value) => { draft.language = value; }, "Idioma do produto"),
          "Japonês: só avisa quando o título diz JP / Japanese / Japonês."),
        field("Texto de pesquisa", textInput(draft.searchQuery, (value) => { draft.searchQuery = value; }, { placeholder: "elite trainer box" }),
          "O que é escrito na pesquisa de cada loja. Evita “pokemon”: há lojas que não o põem nos títulos.", true),
        field("Tem de ter todas", keywordEditor(draft.requiredKeywords, "accent", () => {}, "Tem de ter todas"),
          "Todas têm de estar no título. Alternativas com | (booster box|booster display = uma delas) e * para o início de uma palavra (op* apanha OP-15 e OP10).", true),
        field("Ignorar se tiver", keywordEditor(draft.excludedKeywords, "red", () => {}, "Ignorar se tiver"), "Nenhuma pode estar no título. Também aceitam | e *.", true),
        field("Id", idInput, isNew ? "Identificador interno (não muda depois)." : "Não pode ser alterado."))),
    priceSection(draft),
    drawerSection("Tipos de produto",
      h("p", { class: "muted small" }, "Para vigiar um set inteiro e só querer alguns tipos (ex.: ETB e Booster Bundle, sem boosters soltos). Nenhum marcado = todos."),
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
    drawerSection("Lojas",
      h("p", { class: "muted small" }, "Nenhuma marcada = todas as lojas ativas."),
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
        }, store.name, store.enabled ? null : h("span", { class: "muted" }, " (desligada)"));
      }))),
    drawerSection(null, labelledSwitch(draft.enabled, "A vigiar (desliga para pausar)", (checked) => { draft.enabled = checked; })),
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
      toast(isNew ? "A vigiar o novo produto" : "Regras guardadas", "success");
      if (isNew && state.route.view === "vigiar") { state.wizard = null; go("produto", id); }
    } catch (error) {
      errors.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  openDrawer({
    title: isNew ? "Vigiar produto" : "Editar regras",
    subtitle: isNew ? "Um produto a vigiar, com o preço máximo que aceitas." : task.name,
    body,
    footer: [button("Cancelar", { kind: "ghost", onClick: closeDrawer }), button(isNew ? "Começar a vigiar" : "Guardar", { iconName: "check", kind: "primary", onClick: save })],
  });
}

/** The price rule of the task editor: MSRP + margin (per kind) or a fixed maximum. Re-renders itself. */
function priceSection(draft) {
  const section = h("section", { class: "drawer-section" });
  const render = () => {
    const msrpMode = draft.priceRule === "Msrp";
    const samples = ["etb", "booster-bundle", "booster-display", "booster-pack"].filter((kind) => msrpOf(kind, draft.game));
    section.replaceChildren(
      h("h3", {}, "Preço"),
      segmented([["Msrp", "MSRP + margem"], ["MaxPrice", "Preço fixo"]], draft.priceRule, (value) => { draft.priceRule = value; render(); }, "Regra de preço"),
      msrpMode
        ? h("div", { class: "fields" },
          field("Margem sobre o MSRP (%)", h("input", { type: "number", min: -50, max: 300, step: 1, value: draft.tolerance, class: "mono", oninput: (event) => { draft.tolerance = event.target.value; } }),
            samples.length ? `Com 10%: ${samples.map((kind) => `${kindLabel(kind)} até ${money(Math.round(msrpOf(kind, draft.game) * 110) / 100)}`).join(" · ")}.` : "Este jogo ainda não tem MSRP: usa o máximo fixo abaixo."),
          field("Tipos sem MSRP: até (€)", h("input", { type: "number", min: 0, step: "0.01", value: draft.maxPrice || "", placeholder: "não avisar", class: "mono", oninput: (event) => { draft.maxPrice = event.target.value; } }),
            "Opcional. Vazio = não avisa de tipos sem MSRP."))
        : field("Preço máximo (€)", h("input", { type: "number", min: 0, step: "0.01", value: draft.maxPrice, class: "mono", oninput: (event) => { draft.maxPrice = event.target.value; } }),
          "Igual para tudo o que a pesquisa encontrar. Para um set inteiro, o MSRP + margem é melhor: compara cada tipo com o seu preço."));
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

/** The task fields the import accepts, in the same shape "Exportar JSON" produces. */
function taskToJson(task) {
  return {
    id: task.id, name: task.name, enabled: task.enabled, storeNames: task.storeNames || [], searchQuery: task.searchQuery,
    requiredKeywords: task.requiredKeywords || [], excludedKeywords: task.excludedKeywords || [], maxPrice: task.maxPrice, language: task.language,
    game: task.game || "Pokemon", priceRule: task.priceRule || "MaxPrice", msrpTolerancePercent: task.msrpTolerancePercent ?? 10, kinds: task.kinds || [],
  };
}

function exportTasks() {
  downloadJson(JSON.stringify(state.tasks.map(taskToJson), null, 2), `tcgstockbot-tarefas-${new Date().toISOString().slice(0, 10)}.json`);
}

/**
 * Turns one imported object into a StockTaskSaveRequest (without isNew). Lenient on the shape — keywords as an
 * array or "a, b" text, language as English/Japanese/EN/JP, price as 59.99 or "59,99" — the API still validates.
 */
function normalizeImportedTask(raw) {
  if (!raw || typeof raw !== "object" || Array.isArray(raw)) throw new Error("não é um objeto");
  const list = (value) => Array.isArray(value) ? value.map((item) => String(item).trim()).filter(Boolean) : splitList(String(value ?? ""));
  const name = String(raw.name ?? "").trim();
  const id = String(raw.id ?? "").trim()
    || name.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
  const languageText = String(raw.language ?? "English").trim().toLowerCase();
  const language = ["japanese", "jp", "jpn", "japones", "japonês", "ja"].includes(languageText) ? "Japanese"
    : ["english", "en", "eng", "ingles", "inglês"].includes(languageText) ? "English" : null;
  if (!language) throw new Error(`idioma "${raw.language}" desconhecido (usa English ou Japanese)`);
  const gameText = String(raw.game ?? "Pokemon").trim().toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/[^a-z]/g, "");
  const game = { pokemon: "Pokemon", onepiece: "OnePiece", op: "OnePiece", lorcana: "Lorcana", magic: "Magic", mtg: "Magic", magicthegathering: "Magic", other: "Other", outro: "Other" }[gameText];
  if (!game) throw new Error(`jogo "${raw.game}" desconhecido (usa Pokemon, OnePiece, Lorcana, Magic ou Other)`);
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
      field("Ficheiro", fileInput, "Opcional: carrega um .json em vez de colar.", true),
      field("JSON", textarea, help, true),
      labelledSwitch(switchValue, switchLabel, onSwitch)),
    results,
  ];
}

function importSummary(results, rows, created, updated, skipped, failed) {
  results.replaceChildren(
    h("div", { class: "callout accent" }, icon("info"), h("div", {}, `${created} criada(s), ${updated} atualizada(s), ${skipped} ignorada(s), ${failed} com erro.`)),
    h("ul", { class: "import-results" }, rows.map(([label, tone, message]) => h("li", {}, pill(message, tone), h("span", {}, label)))));
}

function parseImportList(text, key) {
  const parsed = JSON.parse(text.trim() || "null");
  const items = Array.isArray(parsed) ? parsed : Array.isArray(parsed?.[key]) ? parsed[key] : parsed && typeof parsed === "object" ? [parsed] : null;
  if (!items || items.length === 0) throw new Error("esperava uma lista: [ { ... }, { ... } ]");
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
    try { items = parseImportList(text, "tasks"); } catch (error) { results.replaceChildren(errorList([`JSON inválido: ${error.message}`])); return; }

    running = true;
    const existing = new Set(state.tasks.map((task) => task.id.toLowerCase()));
    const rows = [];
    let created = 0, updated = 0, skipped = 0, failed = 0;
    for (const [index, raw] of items.entries()) {
      results.replaceChildren(h("p", { class: "muted" }, `A importar… ${index + 1}/${items.length}`));
      const label = raw?.name || raw?.id || `#${index + 1}`;
      try {
        const task = normalizeImportedTask(raw);
        const exists = existing.has(task.id.toLowerCase());
        if (exists && !updateExisting) { skipped++; rows.push([label, "plain", "já existe — ignorada"]); continue; }
        await post("/stocktask/save", { isNew: !exists, ...task });
        existing.add(task.id.toLowerCase());
        if (exists) { updated++; rows.push([label, "blue", "atualizada"]); } else { created++; rows.push([label, "accent", "criada"]); }
      } catch (error) {
        failed++;
        rows.push([label, "red", errorText(error)]);
      }
    }
    running = false;
    await reloadConfiguration();
    importSummary(results, rows, created, updated, skipped, failed);
    if (failed === 0) toast(`Importação concluída: ${created + updated} produto(s)`, "success");
  };

  openDrawer({
    title: "Importar produtos (JSON)",
    subtitle: "Cola uma lista ou escolhe um ficheiro. O formato é o mesmo do “Exportar JSON”.",
    body: importDrawerBody({
      placeholder: TASK_IMPORT_EXAMPLE,
      help: "Campos: id, name, searchQuery, requiredKeywords, excludedKeywords, priceRule (Msrp/MaxPrice), msrpTolerancePercent, maxPrice, kinds (etb, booster-bundle…; [] = todos), language (English/Japanese), game (Pokemon/OnePiece/Lorcana/Magic/Other), storeNames ([] = todas), enabled. Sem id, é gerado a partir do nome.",
      switchLabel: "Atualizar os que já existem (mesmo id)", switchValue: updateExisting, onSwitch: (checked) => { updateExisting = checked; },
      onText: (value) => { text = value; }, results,
    }),
    footer: [button("Fechar", { kind: "ghost", onClick: closeDrawer }), button("Importar", { iconName: "check", kind: "primary", onClick: runImport })],
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
  };
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
        field("Caminho da Store API", textInput(draft.wooCommerceProductsPath, (value) => { draft.wooCommerceProductsPath = value; }, { class: "mono" }),
          "Normalmente /wp-json/wc/store/v1/products. Algumas lojas só respondem sem o /v1."));
    } else if (draft.platform === "Html") {
      const selector = (label, key, hint) => field(label, textInput(draft.html[key], (value) => { draft.html[key] = value; }, { class: "mono" }), hint);
      platformFields.replaceChildren(h("h3", {}, "Seletores HTML"),
        h("div", { class: "fields" },
          field("URL de pesquisa", textInput(draft.html.searchUrlTemplate, (value) => { draft.html.searchUrlTemplate = value; }, { placeholder: "https://loja.pt/pesquisa?q={query}", class: "mono" }),
            "Usa {query} para o texto de pesquisa, ou o URL fixo de uma categoria.", true),
          selector("Cartão do produto", "productSelector", "ex.: li.product-item"),
          selector("Título", "titleSelector", "vazio = texto do cartão"),
          selector("Preço", "priceSelector", "ex.: .price"),
          selector("Link", "linkSelector", "vazio = primeiro <a>"),
          selector("Marcador de esgotado", "outOfStockSelector", "existe só se esgotado"),
          selector("Marcador de disponível", "inStockSelector", "ex.: botão de comprar")));
    } else if (draft.platform === "Amazon") {
      platformFields.replaceChildren(h("div", { class: "callout accent" }, icon("info"), h("div", {},
        "Usa a Creators API oficial da Amazon (sem scraping). Preenche Amazon__CredentialId, Amazon__CredentialSecret e Amazon__PartnerTag no .env. "
        + "URL base: https://www.amazon.es. O texto de pesquisa pode ser palavras-chave ou um ASIN (ex.: B0XXXXXXXX).")));
    } else if (draft.platform === "Awin") {
      platformFields.replaceChildren(h("h3", {}, "Feed Awin"),
        field("Chave do feed", textInput(draft.awinFeedKey, (value) => { draft.awinFeedKey = value; }, { placeholder: "ex.: MediaMarktES", class: "mono" }),
          "Só letras, números e _. O bot lê o URL do Create-a-Feed de AwinFeeds__Feeds__<chave> no .env (o site da loja nunca é contactado). "
          + "Só funciona depois de a loja te aprovar no programa Awin dela.", true));
    } else if (draft.platform === "Manual") {
      platformFields.replaceChildren(h("div", { class: "callout" }, icon("info"), h("div", {},
        "Sem integração: a loja fica na lista (link, tier, etiquetas) mas o bot nunca a pesquisa. Escolhe outra plataforma quando souberes como a ler.")));
    } else if (AWIN_FEED_KEYS[draft.platform]) {
      platformFields.replaceChildren(h("div", { class: "callout accent" }, icon("info"), h("div", {},
        "Lê o feed de produtos Awin autorizado desta loja (o site da loja não é contactado). Põe o URL do Create-a-Feed em "
        + `AwinFeeds__Feeds__${AWIN_FEED_KEYS[draft.platform]} no .env. O feed é descarregado no máximo de 6 em 6 horas; sem feed, a loja mostra "integração não configurada".`)));
    } else {
      platformFields.replaceChildren(h("h3", {}, "Shopify"),
        field("Catálogo (opcional)", textInput(draft.shopifyCatalogPaths, (value) => { draft.shopifyCatalogPaths = value; }, { placeholder: "/collections/pokemon, /collections/riftbound", class: "mono" }),
          "Vazio = pesquisa da loja (no máximo 10 produtos por pesquisa). Com coleções (separadas por vírgula) ou /products.json, o bot lê o catálogo "
          + "uma vez por ciclo e filtra-o para todos os produtos — sem limite de 10. Prefere coleções: /products.json lê a loja inteira.", true));
    }
  };
  renderPlatformFields();

  const runTest = async () => {
    testResults.replaceChildren(h("p", { class: "muted" }, "A pesquisar…"));
    try {
      const results = await post("/store/probe", { store: storeToSaveRequest(draft, null), query: testQuery });
      testResults.replaceChildren();
      append(testResults, [results.length === 0
        ? h("div", { class: "callout" }, icon("info"), h("div", {}, "A loja respondeu, mas sem produtos para esta pesquisa."))
        : [h("p", { class: "muted small" }, `${results.length} produto(s) encontrados`),
          h("ul", { class: "result-rows compact" }, results.slice(0, 10).map((listing) => h("li", { class: "result-row" },
            productArt({ imageUrl: listing.imageUrl, title: listing.title, size: "mini" }),
            h("div", { class: "result-text" }, externalLink(listing.url, listing.title, "result-title"),
              h("div", { class: "result-meta" }, `${listing.inStock ? "Em stock" : "Esgotado"} · ${LANGUAGES[listing.language] || listing.language}`)),
            h("div", { class: "result-price" }, h("span", { class: "mono price" }, money(listing.price))),
            Object.values(listing.outcomeByTask).some(isMatch) ? h("span", { class: "verdict accent solid" }, "Corresponde") : null)))]]);
    } catch (error) {
      testResults.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  const testQueryInput = textInput(testQuery, (value) => { testQuery = value; });
  testQueryInput.addEventListener("keydown", (event) => { if (event.key === "Enter") runTest(); });

  const body = [
    draft.detect ? detectNotice(draft.detect) : null,
    drawerSection("Loja",
      h("div", { class: "fields" },
        field("Nome", textInput(draft.name, (value) => { draft.name = value; }, { placeholder: "ex.: Versus Gamecenter" })),
        field("Plataforma", h("select", { onchange: (event) => { draft.platform = event.target.value; renderPlatformFields(); } },
          Object.entries(PLATFORMS).map(([value, label]) => h("option", { value, selected: draft.platform === value }, label)))),
        field("URL base", h("div", { class: "input-with-button" },
          textInput(draft.baseUrl, (value) => { draft.baseUrl = value; }, { placeholder: "https://loja.pt", "aria-label": "URL base" }),
          button("Detetar", { iconName: "search", onClick: () => detectInDrawer(store, draft), title: "Descobre se é Shopify, WooCommerce ou PrestaShop e preenche a configuração" })),
          "Cola o site (ou a página da categoria Pokémon) e carrega em Detetar.", true),
        field("Títulos sem indicação de idioma", h("select", { onchange: (event) => { draft.unmarkedLanguage = event.target.value; } },
          Object.entries(UNMARKED_LANGUAGE).map(([value, [label]]) => h("option", { value, selected: draft.unmarkedLanguage === value }, label))),
          "“Contar como inglês” só se a loja marca sempre os PT/ES/JP. “Idioma por confirmar” avisa na mesma, com um aviso para confirmares na página.", true)),
      labelledSwitch(draft.enabled, "Loja ativa", (checked) => { draft.enabled = checked; })),
    drawerSection("Classificação",
      h("div", { class: "fields" },
        field("Área", h("select", { onchange: (event) => { draft.country = event.target.value; } },
          Object.entries(COUNTRIES).map(([value, [label]]) => h("option", { value, selected: draft.country === value }, label)))),
        field("Tier", h("select", { onchange: (event) => { draft.tier = event.target.value; } },
          Object.entries(TIERS).map(([value, [label, , description]]) => h("option", { value, selected: (draft.tier || "None") === value }, `${label} — ${description}`)))),
        field("Tipo de loja", h("select", { onchange: (event) => { draft.type = event.target.value; } },
          Object.entries(STORE_TYPES).map(([value, label]) => h("option", { value, selected: (draft.type || "Unknown") === value }, label)))),
        field("Etiquetas", textInput((draft.tags || []).join(", "), (value) => { draft.tags = splitList(value); }, { placeholder: "por testar, envia para PT" }),
          "Separadas por vírgula. Aparecem no cartão e servem de filtro."),
        field("Notas", h("textarea", { rows: 3, oninput: (event) => { draft.notes = event.target.value; } }, draft.notes || ""), null, true))),
    platformFields,
    drawerSection("Testar antes de guardar",
      h("div", { class: "input-with-button" }, testQueryInput, button("Testar", { iconName: "flask", onClick: runTest })),
      testResults),
    errors,
  ];

  const save = async () => {
    errors.replaceChildren();
    try {
      await post("/store/save", storeToSaveRequest(draft, originalName));
      await reloadConfiguration();
      closeDrawer();
      toast(isNew ? "Loja adicionada" : "Loja guardada", "success");
    } catch (error) {
      errors.replaceChildren(errorList(error.errors || [error.message]));
    }
  };

  openDrawer({
    title: isNew ? "Adicionar loja" : `Editar ${store.name}`,
    subtitle: "Testa a configuração antes de guardar.",
    body,
    footer: [
      !isNew ? button("Apagar", { kind: "text-danger", onClick: () => deleteStore(store) }) : null,
      h("span", { class: "spacer" }),
      button("Cancelar", { kind: "ghost", onClick: closeDrawer }),
      button(isNew ? "Adicionar" : "Guardar", { iconName: "check", kind: "primary", onClick: save }),
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
    toast("A detetar a plataforma…", "info");
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
      result.evidence?.length ? h("details", {}, h("summary", {}, "O que foi verificado"),
        h("ul", {}, result.evidence.map((line) => h("li", {}, line)))) : null));
}

/* ----- Stores: test many at once ----- */

const TEST_WORKERS = 4;

function openStoreTestDrawer() {
  const scopes = {
    visible: ["As visíveis com os filtros atuais", () => state.stores.filter(storeMatchesFilters)],
    never: ["Nunca testadas", () => state.stores.filter((store) => !store.lastTest)],
    failed: ["Falharam no último teste", () => state.stores.filter((store) => store.lastTest && TEST_FAILED.has(store.lastTest.status))],
    disabled: ["Desligadas", () => state.stores.filter((store) => !store.enabled)],
    all: ["Todas", () => state.stores],
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
  const refreshCount = () => { scopeCount.textContent = `${scopes[scope][1]().length} loja(s)`; };

  const render = () => {
    const done = [...rows.values()].filter((row) => row.state === "done").length;
    progress.textContent = rows.size ? `${done} de ${rows.size} testadas${running ? "…" : ""}` : "";
    table.replaceChildren(rows.size === 0 ? h("p", { class: "muted" }, "Escolhe as lojas e carrega em Começar.") : h("div", { class: "table-wrap" }, h("table", { class: "table" },
      h("thead", {}, h("tr", {}, h("th", {}, "Loja"), h("th", {}, "Resultado"), h("th", { class: "num" }, "Produtos"), h("th", { class: "num" }, "Reconhecidos"), h("th", {}, "Exemplo / motivo"))),
      h("tbody", {}, list.map((listed) => {
        const store = state.stores.find((candidate) => candidate.name === listed.name) || listed;
        const row = rows.get(store.name);
        const result = row?.result;
        return h("tr", {},
          h("td", { class: "nowrap" }, h("div", { class: "store-line" }, storeAvatar(store.name, "small"), h("strong", {}, store.name)),
            h("div", { class: "muted small" }, `${store.platform}${store.enabled ? " · ativa" : " · desligada"}`)),
          h("td", { class: "nowrap" }, row?.state === "running" ? pill("A testar…", "accent")
            : result ? testPill(result) : pill("À espera")),
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
    list = scopes[scope][1]().slice().sort((a, b) => a.name.localeCompare(b.name, "pt"));
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
    toast(failed ? `${message}: ${stores.length - failed} ok, ${failed} com erro` : `${message}: ${stores.length}`, failed ? "error" : "success");
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
          await post("/store/save", storeToSaveRequest({ ...updated, tags: (store.tags || []).filter((tag) => tag !== "plataforma por identificar") }, store.name));
          row.note = `Detetado ${detection.platform}${detection.shopifyCatalogPaths ? ` (${detection.shopifyCatalogPaths})` : ""} — configuração atualizada.`;
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
      actions.replaceChildren(button("Parar", { iconName: "stop", onClick: () => { stopRequested = true; } }));
      return;
    }
    if (rows.size === 0) { actions.replaceChildren(h("p", { class: "muted small" }, "Depois do teste aparecem aqui as ações (ativar as que funcionam, desligar as que falharam…).")); return; }
    const toEnable = byResult((result, store) => result.status === "Ok" && !store.enabled && store.platform !== "Manual");
    const toDisable = byResult((result, store) => TEST_FAILED.has(result.status) && store.enabled);
    const blocked = byResult((result, store) => result.status === "Blocked" && store.platform !== "Manual");
    const toDetect = byResult((result) => ["Unreachable", "Error", "Empty", "NoIntegration"].includes(result.status));
    const action = (stores, label, onClick, primary) => button(`${label} (${stores.length})`, { kind: primary ? "primary" : "", disabled: stores.length === 0, onClick });
    actions.replaceChildren(
      action(toEnable, "Ativar as que funcionam", () => bulk(toEnable, (store) => post("/store/enable", { name: store.name, enabled: true }), "Lojas ativadas"), true),
      action(toDisable, "Desligar as que falharam", () => bulk(toDisable, (store) => post("/store/enable", { name: store.name, enabled: false }), "Lojas desligadas")),
      action(blocked, "Bloqueadas → Sem integração", () => bulk(blocked, (store) => post("/store/save", storeToSaveRequest({
        ...store, platform: "Manual", enabled: false, tags: [...new Set([...(store.tags || []), "bloqueia bots (403)"])],
      }, store.name)), "Marcadas como Sem integração")),
      action(toDetect, "Voltar a detetar a plataforma", () => redetect(toDetect)),
    );
  }

  const scopeSelect = h("select", { "aria-label": "Lojas a testar", onchange: (event) => { scope = event.target.value; refreshCount(); } },
    Object.entries(scopes).map(([value, [label]]) => h("option", { value, selected: value === scope }, label)));
  refreshCount();
  render();
  renderActions();

  openDrawer({
    title: "Testar várias lojas",
    subtitle: `Faz uma pesquisa em cada loja (${TEST_WORKERS} de cada vez, com os limites por domínio) e guarda o resultado no cartão. Lojas “Sem integração” não são contactadas.`,
    body: [
      drawerSection(null,
        h("div", { class: "fields" },
          field("Lojas", h("div", { class: "input-with-button" }, scopeSelect, scopeCount)),
          field("Texto de pesquisa", textInput(query, (value) => { query = value; }), "“pokemon” apanha quase tudo; usa “elite trainer box” para algo mais fino.")),
        h("div", { class: "button-row" }, button("Começar", { iconName: "play", kind: "primary", onClick: start }), progress)),
      drawerSection(null, table),
      drawerSection("Depois do teste", actions),
    ],
    footer: [button("Fechar", { kind: "ghost", onClick: () => { stopRequested = true; closeDrawer(); } })],
    wide: true,
  });
}

/* ----- Stores: JSON import / export ----- */

function exportStores() {
  const json = JSON.stringify(state.stores.map((store) => ({ ...storeToSaveRequest(store, null), originalName: undefined })), null, 2);
  downloadJson(json, `tcgstockbot-lojas-${new Date().toISOString().slice(0, 10)}.json`);
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
 * with "Atualizar classificação" only its area, tier, type, tags and notes change — its platform setup is kept.
 */
function openStoreImportDrawer() {
  let text = "";
  let updateClassification = true;
  let running = false;
  const results = h("div", {});

  const runImport = async () => {
    if (running) return;
    let items;
    try { items = parseImportList(text, "stores"); } catch (error) { results.replaceChildren(errorList([`JSON inválido: ${error.message}`])); return; }

    running = true;
    const rows = [];
    let created = 0, updated = 0, skipped = 0, failed = 0;
    for (const [index, raw] of items.entries()) {
      results.replaceChildren(h("p", { class: "muted" }, `A importar… ${index + 1}/${items.length}`));
      const label = raw?.name || `#${index + 1}`;
      try {
        if (!raw || typeof raw !== "object" || Array.isArray(raw)) throw new Error("não é um objeto");
        const existing = state.stores.find((store) => sameName(store.name, String(raw.name || "")))
          || (hostOf(raw.baseUrl) && state.stores.find((store) => hostOf(store.baseUrl) === hostOf(raw.baseUrl)));
        if (existing) {
          if (!updateClassification) { skipped++; rows.push([label, "plain", `já existe (${existing.name}) — ignorada`]); continue; }
          const merged = {
            ...existing, country: raw.country || existing.country, tier: raw.tier || existing.tier, type: raw.type || existing.type,
            tags: [...new Set([...(existing.tags || []), ...(Array.isArray(raw.tags) ? raw.tags : [])].filter((tag) => tag !== "por testar"))],
            notes: existing.notes || raw.notes || "",
          };
          await post("/store/save", storeToSaveRequest(merged, existing.name));
          updated++; rows.push([label, "blue", `classificação atualizada (${existing.name})`]);
          continue;
        }
        const draft = {
          name: "", enabled: false, platform: "Manual", baseUrl: "", unmarkedLanguage: "Unknown",
          wooCommerceProductsPath: "/wp-json/wc/store/v1/products", shopifyCatalogPaths: "", html: null,
          awinFeedKey: "", country: "Portugal", tier: "None", type: "Unknown", tags: [], notes: "", ...raw,
        };
        await post("/store/save", storeToSaveRequest(draft, null));
        state.stores.push(draft);
        created++; rows.push([label, "accent", `criada (${draft.platform})`]);
      } catch (error) {
        failed++;
        rows.push([label, "red", errorText(error)]);
      }
    }
    running = false;
    await reloadConfiguration();
    importSummary(results, rows, created, updated, skipped, failed);
    if (failed === 0) toast(`Importação concluída: ${created + updated} loja(s)`, "success");
  };

  openDrawer({
    title: "Importar lojas (JSON)",
    subtitle: "Cola uma lista de lojas ou escolhe um ficheiro. O formato é o mesmo do “Exportar JSON”.",
    body: importDrawerBody({
      placeholder: '[\n  { "name": "Loja", "platform": "Shopify", "baseUrl": "https://loja.pt", "country": "Portugal", "tier": "A", "type": "Specialist", "tags": ["por testar"], "enabled": false }\n]',
      help: "Campos: name, platform (Shopify, WooCommerce, Html, Amazon, Fnac, Worten, ElCorteIngles, Awin, Manual), baseUrl, country (Portugal/Spain/Europe), tier (S/A/B/C), type (Retail/Specialist/Importer/Marketplace/Official), tags, notes, enabled, shopifyCatalogPaths, wooCommerceProductsPath, html, awinFeedKey.",
      switchLabel: "Nas que já existem (mesmo nome ou site), atualizar só área, tier, tipo e etiquetas", switchValue: updateClassification,
      onSwitch: (checked) => { updateClassification = checked; }, onText: (value) => { text = value; }, results,
    }),
    footer: [button("Fechar", { kind: "ghost", onClick: closeDrawer }), button("Importar", { iconName: "check", kind: "primary", onClick: runImport })],
  });
}
