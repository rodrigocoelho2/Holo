# TcgStockBot

Monitoriza lojas portuguesas de Pokémon TCG e avisa-te (WhatsApp e/ou email) quando um produto está
**em stock, na versão inglesa e ao preço máximo que definiste ou abaixo**. Tem um dashboard web para
veres o que se passa e configurares tarefas e lojas. Só monitoriza e avisa: a compra fazes tu.

## Estrutura da solução

```
TcgStockBot.slnx
├── Directory.Build.props / Directory.Packages.props   # net10.0, nullable; versões de pacotes centralizadas
├── TcgStockBot.Library/        # partilhado: Enums, ServiceApiResponse, validação, texto (idioma, preço)
├── TcgStockBot.WebApi/         # o bot: monitor em segundo plano + API JSON (x-api-key)
│   ├── Program.cs
│   ├── TcgStockBotStartup.cs   # registos (AddSingleton, repositório antes do serviço) + pipeline
│   ├── Extensions/             # dados (BotDataContext, RepositoryBase), InternalControllerBase, auth, traduções
│   │   └── Http/               # saída: limites por domínio, Retry-After, logs sem segredos
│   └── Features/
│       ├── TcgStockBotOptions.cs
│       ├── StockTasks/  Stores/  Alerts/  Monitors/  Settings/  Notifications/
│       │   ├── Controllers/    # internal, [Route("stocktask")], {Feature}{Operation}Async, ToActionResult()
│       │   ├── Services/       # validação (ValidateXxxAsync privados) + regras; devolvem ServiceApiResponse<T>
│       │   ├── Repositories/   # só acesso a dados (ficheiros JSON em data/)
│       │   ├── Models/Entities # o formato guardado; nunca sai do serviço
│       │   ├── Models/DTOs     # contrato da API (DTO, SaveRequest, SaveResponse, …)
│       │   └── Http/*.http     # pedidos de teste para cada endpoint
│       │   (Stores/Services/Adapters: Shopify, WooCommerce, Html, Amazon/ (Creators API), Awin/ (FNAC, Worten, El Corte Inglés))
├── TcgStockBot.Sdk/            # cliente tipado da WebApi (TcgStockBotClient) + cópias dos DTOs
├── TcgStockBot.Website/        # o dashboard (MVC)
│   ├── Controllers/            # finos: criam o model, chamam-no, devolvem Json(AjaxDataResult)
│   ├── Model/{Feature}/        # "fat models": chamam a WebApi através do Sdk
│   ├── Views/Dashboard/Index.cshtml, Views/Shared/_Layout.cshtml
│   └── wwwroot/css/dashboard.css, wwwroot/js/dashboard.js
└── TcgStockBot.UnitTest/       # xUnit, uma pasta por feature (+ teste de contrato Sdk ↔ WebApi)
```

O fluxo é `browser → Website (password + anti-forgery) → Sdk → WebApi (x-api-key) → data/*.json`.
Tudo é código deste repositório: não há dependências de bibliotecas externas privadas.

## Dashboard

| Página | O que tem |
| --- | --- |
| **Visão geral** | alertas enviados, o que está em stock agora, estado do último ciclo, gráfico de alertas (14 dias), alertas/resultados por loja, alertas recentes, feed em tempo real |
| **Tarefas** | um produto a vigiar (pesquisa, palavras obrigatórias/excluídas, preço máximo, lojas). Iniciar/parar, editar, duplicar, apagar. Ao clicar numa linha vês o que cada loja devolveu e porquê |
| **Lojas** | ativar/desligar, estado, adicionar/editar com **teste antes de guardar** |
| **Alertas** | histórico, com link para o produto e a marca "confirmar idioma" |
| **Testar loja** | pesquisa numa loja e explica, por tarefa, cada resultado |
| **Definições** | intervalo, pausas, User-Agent, estado do WhatsApp/email + mensagem de teste |

O layout é fluido: ocupa a largura toda e os widgets, gráficos e letra crescem e encolhem com a janela.
Atalhos: `1`–`6` mudam de página e `R` verifica logo.

### Idioma sem marcador (por loja)

| Opção | Comportamento |
| --- | --- |
| Não avisar | título sem "(EN)/Inglês/(PT)…" → não avisa |
| Contar como inglês | a loja marca sempre os PT/ES/JP, por isso sem marcador = inglês |
| **Avisar com idioma por confirmar** | avisa na mesma, e o WhatsApp, o email e o dashboard pedem para confirmares na página (usado na CentroXogo) |

## Docker no NAS UGREEN

São dois contentores: `tcgstockbot-webapi`, o bot, sem porta exposta, e `tcgstockbot-website`, o dashboard, na porta 5080.

1. Copia a pasta da solução para o NAS (por exemplo `docker/tcgstockbot`).
2. Copia `.env.example` para `.env` e preenche `DASHBOARD_PASSWORD`, `TCG_API_KEY`
   (uma chave longa e aleatória) e o WhatsApp e/ou o email.
3. Arranca, pela app Docker do UGOS (Projeto a apontar para a pasta) ou por SSH:

   ```bash
   docker compose up -d --build --remove-orphans
   ```

4. Abre `http://<IP-do-NAS>:5080`.

**Ao atualizar**, copia os ficheiros novos por cima e **não apagues o `.env`** (não faz parte da solução, porque tem
segredos). Se vens de uma versão com o proxy (`price-proxy`), o `--remove-orphans` apaga esse contentor, e as linhas
`OutboundHttp__Proxy__…` que ficarem no `.env` são ignoradas.

**Vindo da v2 (um só contentor `tcgstockbot`)?** Usa o mesmo nome de pasta. O volume `tcgstockbot-data` é o mesmo e o formato dos ficheiros não mudou, por isso as tarefas, as lojas e o histórico mantêm-se.
- O `--remove-orphans` apaga o contentor antigo.
- No `.env`, a antiga `Dashboard__Password` passa a chamar-se `DASHBOARD_PASSWORD`.
- Acrescenta também `TCG_API_KEY`.

Não abras a porta 5080 no router, porque a password passa sem HTTPS. Para aceder de fora de casa, usa uma VPN (por exemplo Tailscale).

### WhatsApp (CallMeBot)

1. Guarda nos contactos o número publicado em https://www.callmebot.com/blog/free-api-whatsapp-messages/
2. Envia-lhe por WhatsApp: `I allow callmebot to send me messages`. Recebes a API key numa resposta.
3. No `.env`: `WhatsApp__Enabled=true`, `WhatsApp__Phone=+351…` (o teu número) e `WhatsApp__ApiKey=…`.
4. `docker compose up -d`, e depois, em **Definições → WhatsApp**, carrega em *Enviar mensagem de teste*.

O CallMeBot é um serviço gratuito de terceiros, só para uso pessoal, e sem garantias. Mantém o email ligado como segundo canal.

## Lojas Shopify: pesquisa ou catálogo

Por omissão, o bot usa a pesquisa da loja (`/search/suggest.json`), que devolve **no máximo 10 produtos** por
pesquisa. Numa loja com muitos produtos do mesmo set, alguns ficam de fora.

Em **Lojas → editar → Catálogo**, podes indicar uma ou mais coleções, separadas por vírgula:

| Loja | Catálogo |
| --- | --- |
| Versus Gamecenter | `/collections/pokemon-tcg-1, /collections/riftbound-league-of-legends-tcg` |
| Note! | `/collections/pokemon` |
| Trap Card TCG | `/collections/pokemon-booster-boxes, /collections/pokemon-elite-trainer-boxes, /collections/pokemon-collection-boxes, /collections/pokemon-tins, /collections/pokemon-booster-packs` |

- Com catálogo, o bot lê as coleções (páginas de 250 produtos) **uma vez por ciclo** e filtra-as localmente
  para todas as tarefas dessa loja. Não há limite de 10, e são menos pedidos do que uma pesquisa por tarefa.
- O texto de pesquisa da tarefa tem de aparecer no título, no tipo, no fabricante ou nas tags do produto
  (palavras inteiras, sem acentos).
- `/products.json` lê a loja inteira. Funciona, mas numa loja grande são muitas páginas por ciclo, por isso
  prefere as coleções.
- Para descobrires as coleções de uma loja, abre `https://<loja>/collections.json` no browser.
- Uma coleção vazia ou errada não dá erro, só não devolve produtos. Confirma em **Testar loja**.

## Alertas, descidas de preço e histórico

- **Em stock:** um produto que passa a tarefa (palavras, idioma, stock e preço máximo) avisa uma vez. Não volta a
  avisar enquanto continuar em stock, a não ser que o preço desça.
- **Baixou de preço:** um produto já avisado volta a avisar quando o preço desce pelo menos 1 % e 0,50 € abaixo do
  último preço avisado (ex.: 130 € → 115 €). Se o preço subir e depois voltar ao mesmo valor, não avisa outra vez.
  A mensagem mostra o preço antigo: "🔻 115,00 € (antes 130,00 €)".
- **Pré-venda:** marcada quando o título o diz ("Pré-venda", "Pre-order", "Reserva", "Preventa"…) ou quando a Amazon
  (`PREORDER`) ou o feed Awin (`stock_status`) o indicam. Aparece no aviso e no dashboard.
- **Histórico de preços** (`data/price-history.json`): para cada produto que uma tarefa
  reconhece (palavras e idioma certos, esteja ou não em stock ou acima do preço), guarda um ponto sempre que o preço
  ou o stock mudam. São até 500 alterações por produto, e os produtos que não aparecem há 180 dias são apagados. O
  mesmo produto em várias tarefas conta como um só (loja + URL). Nas tabelas das tarefas, o ícone de gráfico abre o
  histórico e "mín." mostra o preço mais baixo já visto. Para comparar lojas, usa o **Comparar** (com "Só numa loja"
  ligado vês também os produtos que só uma loja tem).
- A `alert-state.json` da v3.0 continua a ser lida; a partir do primeiro ciclo passa a guardar também o preço.

## Comparar (o mesmo produto em várias lojas)

A página **Comparar** junta, numa linha, o mesmo produto em todas as lojas: quem tem o preço mais baixo com stock,
a diferença para a segunda loja, o mínimo de sempre e a última descida de preço. O detalhe tem um gráfico com uma linha
por loja. Quando a loja está esgotada, a linha fica tracejada e mais clara.

- **É automático.** Cada produto que uma tarefa reconhece é agrupado por **set** (a tarefa que o encontrou) e
  **tipo de produto**, detetado no título: ETB, Booster Bundle, Booster Display, Half Display, Booster, Blister,
  Checklane, Tin, Mini Tin, Premium/Ultra/Super Premium Collection, e assim por diante. As palavras que sobram do título
  (um Pokémon num blister, o set numa tarefa que não é de um set) distinguem variantes.
- Por omissão só aparecem os produtos em **duas ou mais lojas**. O interruptor "Só numa loja" mostra os restantes.
- **Correção manual**, no detalhe:
  - **Separar deste produto** tira um anúncio do grupo, para um produto à parte ou para outro grupo;
  - **Juntar anúncio…** mete no grupo um anúncio de outra loja;
  - **Repor automático** desfaz a correção.

  As correções ficam em `data/compare-overrides.json` e valem para os ciclos seguintes.
- Os produtos gravados no histórico antes desta versão não sabem de que tarefa vieram. Entram no Comparar depois do
  primeiro ciclo com a versão nova.

## Lojas: áreas, tiers e etiquetas

O separador **Lojas** agrupa as lojas por **área** (Portugal, Espanha, Europa/online) e, dentro de cada área, por
**tier**:

| Tier | Para quê |
| --- | --- |
| **S** | MSRP / grande retalho (Amazon, Worten, FNAC, Continente, El Corte Inglés, Toys R Us…) |
| **A** | Especialistas TCG |
| **B** | Importadores e lojas online mais pequenas |
| **C** | Marketplaces (Cardmarket, eBay, Wallapop, Vinted): só referência, não comparável com o MSRP |

Cada loja tem também um **tipo** (Retalho, Especialista TCG, Importador, Marketplace, Oficial), **etiquetas** livres
(ex.: `por testar`, `confirmar envio para PT`) e **notas**. Tudo isto é só classificação: não muda a forma como a loja é
pesquisada. Filtra por área, tier, tipo ou etiqueta no topo da página (clicar numa etiqueta de um cartão também filtra).

Plataformas novas:
- **Sem integração** (`Manual`): a loja fica na lista (link, tier, etiquetas) mas o bot **nunca a pesquisa**. Serve
  para lojas que ainda não sabes como ler, ou que não autorizam leitura automática.
- **Awin (genérico)** (`Awin`): qualquer outra loja com programa de afiliados Awin e feed de produtos (MediaMarkt,
  FNAC ES…). Dás-lhe uma **chave** (ex.: `MediaMarktES`) e pões o URL do feed em `AwinFeeds__Feeds__MediaMarktES` no
  `.env`. Funciona como os feeds da secção seguinte.

**Importar / Exportar JSON** (mesmo formato nos dois sentidos). Numa loja que já existe (mesmo nome **ou** mesmo site),
a importação não mexe na configuração: com *Atualizar classificação* ligado, muda só área, tier, tipo e etiquetas.

### Detetar, testar e ativar lojas

- **Detetar** (no formulário da loja, ao lado do URL): cola o site ou a página da categoria Pokémon. O bot faz uns
  poucos pedidos normais (com o mesmo User-Agent e os mesmos limites por domínio) e preenche a configuração:
  - **Shopify**: `/products.json` responde. Propõe a coleção para o modo catálogo (a do URL que colaste, ou
    `pokemon`, `pokemon-tcg`, `pokemon-tcg-1`, `all-pokemon`);
  - **WooCommerce**: a Store API `/wp-json/wc/store/v1/products` (ou sem `/v1`) responde;
  - **PrestaShop**: a pesquisa `index.php?controller=search` tem cartões `product-miniature`. Preenche os seletores
    HTML genéricos (confirma o esgotado no Testar);
  - **bloqueia o bot** (403/429 ou página anti-bot): para aí e não contorna nada. Fica "Sem integração".
- **Testar lojas** (botão no topo da página Lojas): pesquisa cada loja (4 de cada vez) com um texto à escolha
  ("pokemon" por omissão), mostra o resultado de cada uma e guarda-o no cartão ("Último teste"). Escolhe quais: as
  visíveis com os filtros, as nunca testadas, as que falharam, as desligadas ou todas. No fim:
  - **Ativar as que funcionam**;
  - **Desligar as que falharam**;
  - **Bloqueadas → Sem integração** (com a etiqueta `bloqueia bots (403)`);
  - **Voltar a detetar a plataforma** das que deram erro, 0 produtos ou não têm integração. Se a deteção encontrar
    uma plataforma, a loja é atualizada e testada de novo.

  Um teste com produtos tira a etiqueta `por testar`. Há um filtro "Último teste" (nunca testadas, OK, falhou,
  desligadas pelo bot).
- **Desligar automático** (Definições → "Desligar loja após N ciclos com erro", 6 por omissão, 0 = nunca): quando
  todas as pesquisas de uma loja ativa falham N ciclos seguidos, o bot desliga-a, põe-lhe a etiqueta `a falhar` e
  avisa-te uma vez (WhatsApp/email). Não contam os pedidos adiados pelo limite por domínio do próprio bot, nem as
  lojas por configurar (Amazon sem credenciais, feed Awin em falta). Um ciclo com uma pesquisa bem-sucedida põe o
  contador a zero; ligar a loja de novo também, e tira a etiqueta.

## MSRP (preços de referência)

O bot guarda um **MSRP por tipo de produto** (ETB, Booster Bundle, Display, Blister, Tin…) de Pokémon, igual para
todos os sets, em `TcgStockBot.WebApi/Features/Msrps/Data/msrp-defaults.json`. Um `data/msrp.json` com o mesmo
formato substitui esse ficheiro, se quiseres mudar um valor sem nova build. O tipo sai do título do produto, e o
preço só se aplica quando o título ou a tarefa diz "Pokémon", por isso uma caixa de One Piece nunca apanha um preço
de Pokémon.

Onde aparece:
- **Comparar**: coluna MSRP, "−8% vs MSRP" do mais barato com stock, e uma linha tracejada no gráfico.
- **Alertas**: "… · MSRP 59,99 € (−8%)" no WhatsApp/email e "vs MSRP" na lista de alertas.

**Não existe um MSRP oficial em euros para Portugal/Espanha.** Os valores são estimativas do preço de lançamento
habitual na Europa, com o MSRP dos EUA (que a Pokémon publica) e a fonte ao lado de cada um. Confirma e ajusta.

## Lojas grandes: Amazon, FNAC, Worten e El Corte Inglés

Cada loja usa só o mecanismo que ela própria autoriza. O bot não contorna CAPTCHAs, Cloudflare/Akamai,
deteção de bots, bloqueios de IP nem limites de pedidos, e não faz scraping da Amazon.

| Loja | Plataforma no dashboard | Mecanismo | Estado |
| --- | --- | --- | --- |
| Amazon.es | `Amazon` | API oficial **Creators API** (sucessora da PA-API 5) | funciona com credenciais de Associates |
| El Corte Inglés (ES) | `ElCorteIngles` | feed de produtos do programa de afiliados **Awin 13075** | funciona depois de o programa te aprovar e configurares o feed |
| Worten | `Worten` | feed de produtos do programa **Awin 99897** ("Worten PT") | o programa existe; **não foi possível confirmar que tem feed** |
| FNAC Portugal | `Fnac` | nenhum mecanismo autorizado confirmado | **não suportada**: só funciona se obtiveres um feed Awin autorizado da FNAC PT |

Nenhuma das três tem API pública de produtos. As APIs de marketplace (Mirakl, e a API XML da FNAC) são só
para vendedores com conta aprovada, por isso não servem para acompanhar preços. Os sites respondem 403 ao
bot, e os termos de uso restringem a reprodução do conteúdo. Por isso **não há scraper** para estas lojas:
os providers leem só o feed Awin e nunca contactam o site da loja.

### Amazon.es (Creators API)

1. Precisas de uma conta **Amazon Associates** (programa de afiliados) aprovada. A Amazon só dá acesso à
   Creators API a contas com **10 vendas qualificadas nos últimos 30 dias**.
2. Em Associates Central → *Tools → Creators API*: cria uma aplicação e uma credencial. Anota o
   **Credential ID**, o **Credential Secret** (só aparece uma vez) e a **versão** (amazon.es = `3.2`).
3. No `.env`: `Amazon__CredentialId`, `Amazon__CredentialSecret`, `Amazon__CredentialVersion=3.2` e
   `Amazon__PartnerTag` (a tua tag, ex.: `atua-tag-21`). Nunca no código nem no `appsettings.json`.
4. `docker compose up -d`. No dashboard, adiciona uma loja com a plataforma **Amazon** e URL base
   `https://www.amazon.es`, e carrega em *Testar*.

Numa tarefa, o texto de pesquisa pode ser palavras-chave (usa `SearchItems`) ou um ASIN, por exemplo
`B0XXXXXXXX` ou `asin:B0XXXXXXXX` (usa `GetItems`, que é mais exato). O preço vem da oferta *buy box*,
e o vendedor (ex.: *Amazon.es* ou um revendedor) aparece no resultado. Contam como disponível: `IN_STOCK`,
`IN_STOCK_SCARCE`, `PREORDER`, `LEADTIME` e `AVAILABLE_DATE`.

O token OAuth dura 1 hora e fica em cache. A API permite ~1 pedido/segundo e 8640/dia, por isso o limite
por omissão para `amazon.es` é de 2 segundos entre pedidos.

Sem credenciais, a loja mostra *"Amazon: integração não configurada"* e a Amazon não é contactada.

### FNAC, Worten e El Corte Inglés (feeds Awin)

1. Cria uma conta de *publisher* na [Awin](https://www.awin.com) e candidata-te ao programa: El Corte Ingles ES
   (13075) ou Worten PT (99897). Só depois da aprovação tens acesso ao feed. A FNAC PT não tem programa confirmado:
   se encontrares um, o processo é o mesmo.
2. Em *Toolbox → Create-a-Feed*, escolhe o feed do anunciante, **CSV**, delimitador **vírgula** (pipe e tab também
   são aceites), compressão **gzip** ou nenhuma, e as colunas `product_name`, `search_price`, `currency`, `in_stock`,
   `stock_status`, `merchant_deep_link`, `aw_deep_link`, `merchant_product_id` e `merchant_name`.
3. Copia o URL de download para o `.env`: `AwinFeeds__Feeds__ElCorteIngles=…` (ou `…__Worten`, `…__Fnac`).
   Esse URL tem a tua chave de feed, por isso é **secreto**. Nos logs aparece só como
   `datafeed.api.productserve.com/… (Awin feed ElCorteIngles)`.
4. No dashboard, adiciona a loja com a plataforma correspondente e o URL base (ex.: `https://www.elcorteingles.es`).

Como funciona:
- O feed é descarregado **no máximo uma vez a cada `AwinFeeds:RefreshInterval`** (6 h por omissão, mínimo 1 h) e é
  partilhado por todas as tarefas. As pesquisas filtram-no localmente (todas as palavras do texto de pesquisa no
  título, sem acentos).
- Um download falhado só volta a ser tentado passados `RetryAfterFailure` (30 min). Entretanto, usa-se o último
  feed bom, se houver.
- Só ficam em memória os produtos com `KeepKeywords` no título (por omissão `pokemon`), até `MaxProducts` (5000).
  Os feeds dos grandes retalhistas têm centenas de milhares de produtos.
- O stock segue as regras da Awin: `in_stock` 1/0 (vazio = sem informação); senão `stock_quantity`, e senão
  `stock_status` (in stock / out of stock / preorder…). O link é o da loja (`merchant_deep_link`); se faltar,
  usa-se o `aw_deep_link`.
- Os preços do feed estão atrasados até à última importação da Awin. Confirma sempre na loja antes de comprar.


## Pedidos de saída: limites por domínio

Configuração na secção `OutboundHttp` (no Docker, `OutboundHttp__…` no `.env`):

| Chave | Por omissão | Significado |
| --- | --- | --- |
| `ConnectTimeout` | `00:00:10` | tempo máximo para abrir a ligação TCP à loja |
| `RateLimits:<domínio>` | amazon.es 2, fnac.pt 10, worten.pt 10, elcorteingles.es 10 (os feeds Awin contam no host da Awin, com o valor por omissão) | **mínimo de segundos entre dois pedidos** ao domínio, incluindo subdomínios (`amazon.es` cobre `www.amazon.es`); a regra mais específica ganha |
| `DefaultRateLimitSeconds` | `2` | o mesmo, para domínios sem regra |
| `MaxThrottleWait` | `00:00:30` | se um pedido tiver de esperar mais do que isto pela sua vez, é adiado para o ciclo seguinte em vez de ficar pendurado |
| `MaxBackoff` | `01:00:00` | teto para o `Retry-After` de uma loja |

- **429/503 com `Retry-After`**: o domínio fica em pausa pelo tempo pedido (um 429 sem `Retry-After`
  pausa 5 minutos). Não há retries imediatos: o bot tenta de novo no ciclo seguinte.
- Cada pedido fica no log numa linha como `Outbound [Amazon] POST creatorsapi.amazon/catalog/v1/searchItems -> 200 in 312 ms`,
  com host e caminho mas **sem** query string, headers, cookies, tokens ou chaves.

### Problemas comuns

| No dashboard / log | Causa | O que fazer |
| --- | --- | --- |
| *A loja recusou o pedido (HTTP 403)* | proteção anti-bot da loja | o bot não a contorna |
| *A loja pediu para abrandar (HTTP 429)* / *Pedido adiado* | limite da loja ou `RateLimits` | espera; aumenta `PollInterval` ou o `RateLimits` do domínio |
| *Amazon: as credenciais foram recusadas* | ID/secret/versão errados | confirma no Associates Central; amazon.es = `3.2` |
| *Amazon: a API respondeu com erro — … InvalidPartnerTag / AccessDenied* | tag errada, ou conta sem acesso à Creators API | confirma a tag e a elegibilidade (10 vendas em 30 dias) |
| *FNAC/Worten/El Corte Inglés: integração não configurada* | não há feed Awin configurado para a loja | ver "FNAC, Worten e El Corte Inglés"; sem feed autorizado, a loja fica sem dados |
| *…: o Awin recusou o download do feed (HTTP 401/403)* | programa ainda não aprovado, ou URL incompleto | confirma o estado no painel da Awin e gera um URL novo |
| *…: o feed Awin não tem as colunas …* | colunas em falta no Create-a-Feed | gera o feed com as colunas indicadas |

## Correr no PC (Visual Studio)

Abre `TcgStockBot.slnx` e, em *Configure Startup Projects*, escolhe **Multiple** com
`TcgStockBot.WebApi` e `TcgStockBot.Website`. Em Development:
- a WebApi corre em `http://127.0.0.1:5081` (Swagger em `/swagger`, chave `devkey`);
- o Website corre em `http://127.0.0.1:5080`.

Os segredos locais vão para os user-secrets:

```bash
dotnet user-secrets --project TcgStockBot.WebApi set "WhatsApp:ApiKey" "<key>"
```

## Testes

```bash
dotnet test TcgStockBot.slnx
```
