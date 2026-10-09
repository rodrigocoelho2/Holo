<p align="center">
  <img src="docs/images/hero.png" alt="TcgStockBot: o Radar no PC e no telemóvel" width="100%">
</p>

<h1 align="center">TcgStockBot</h1>

<p align="center">
  Um bot <b>self-hosted</b> que vigia lojas portuguesas de cartas colecionáveis (Pokémon, One Piece, Lorcana, Magic)<br>
  e te avisa por <b>WhatsApp</b> e <b>email</b> quando um produto aparece <b>em stock, no idioma certo e ao teu preço</b>.
</p>

<p align="center">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white">
  <img alt="ASP.NET Core" src="https://img.shields.io/badge/ASP.NET%20Core-WebApi%20%2B%20MVC-5C2D91">
  <img alt="Lojas" src="https://img.shields.io/badge/lojas-Shopify%20%C2%B7%20WooCommerce%20%C2%B7%20Amazon%20%C2%B7%20Awin-8C96F0">
  <img alt="Avisos" src="https://img.shields.io/badge/avisos-WhatsApp%20%2B%20email-25D366?logo=whatsapp&logoColor=white">
  <img alt="Docker" src="https://img.shields.io/badge/Docker-UGREEN%20NAS-2496ED?logo=docker&logoColor=white">
</p>

<p align="center">
  <a href="#funcionalidades">Funcionalidades</a> ·
  <a href="#screenshots">Screenshots</a> ·
  <a href="#novo-visual-em-desenho">Novo visual</a> ·
  <a href="#arranque-rápido-docker-no-nas">Arranque rápido</a> ·
  <a href="#lojas">Lojas</a> ·
  <a href="#desenvolvimento">Desenvolvimento</a>
</p>

---

## Funcionalidades

**Vigiar produtos**

- Cada **tarefa** é um produto a vigiar: o texto de pesquisa, as palavras obrigatórias e excluídas (com alternativas `a|b` e prefixos `op*`), o **preço máximo**, o **idioma** (inglês ou japonês), as lojas e o **jogo** (Pokémon, One Piece, Lorcana, Magic ou outro).
- Em cada ciclo, o bot pesquisa em todas as lojas e explica cada resultado: *dentro do preço*, *acima do máximo*, *esgotado*, *não é inglês*, *falta a palavra…*.
- Os resultados e os alertas trazem a **imagem do produto** que a loja devolve.
- Importar e exportar as tarefas em JSON.

**Avisos**

- **WhatsApp** (CallMeBot) e **email** (SMTP), com o link direto para a página do produto.
- Um produto só é avisado uma vez enquanto continuar em stock.
- Lojas que não dizem o idioma no título podem **avisar na mesma, com "confirma o idioma"**.

**Dashboard**

- Visão geral com o que está em stock agora, o estado do último ciclo, alertas dos últimos 14 dias e um feed em tempo real.
- Testar uma loja antes de a guardar, e ver o que ela devolve para cada tarefa.
- Atalhos: `1`–`6` mudam de página e `R` verifica logo.

**Feito para correr em casa**

- Três contentores num só `docker-compose.yml`, pensado para um NAS UGREEN: o **bot**, o **dashboard** com password e um **proxy de saída** opcional.
- Cada loja usa só o mecanismo que ela própria autoriza: pesquisa pública da loja, API oficial ou feed de afiliados. Não contorna CAPTCHAs nem proteções anti-bot.
- Limites de pedidos por domínio, respeita o `Retry-After` das lojas e nunca escreve segredos nos logs.

> Só monitoriza e avisa: a compra fazes tu.

## Screenshots

O dashboard atual.

### Visão geral
<img src="docs/screenshots/dashboard-overview.png" alt="Visão geral: alertas enviados, em stock agora, gráfico de 14 dias e feed em tempo real">

### Tarefas
<img src="docs/screenshots/dashboard-tasks.png" alt="Tarefas: produtos vigiados com preço máximo, estado e resultados por loja">

### Lojas
<img src="docs/screenshots/dashboard-stores.png" alt="Lojas: estado de cada loja, plataforma e botões para testar e editar">

## Novo visual (em desenho)

O dashboard vai ser refeito a partir destes mocks. Cada ecrã foi desenhado primeiro como um mock HTML clicável: as fontes estão em [`docs/mocks`](docs/mocks) e abrem no browser (os filtros, a escolha de produtos e os temas funcionam). Os mocks usam dados de exemplo.

<table>
<tr>
<td colspan="2"><b>Radar</b> · <a href="docs/mocks/radar.html">radar.html</a><br>O que está em stock ao teu preço, em cartões com a imagem do produto, e o que ainda está à procura, com o estado em cada loja.<br><img src="docs/images/mock-radar.png" alt="Mock do Radar: produtos em stock com imagem, preço e botão para abrir na loja"></td>
</tr>
<tr>
<td width="50%"><b>A vigiar</b> · <a href="docs/mocks/a-vigiar.html">a-vigiar.html</a><br><img src="docs/images/mock-a-vigiar.png" alt="Mock da lista de produtos vigiados, com filtros por jogo e por estado"></td>
<td width="50%"><b>Detalhe do produto</b> · <a href="docs/mocks/produto.html">produto.html</a><br><img src="docs/images/mock-produto.png" alt="Mock do detalhe de um produto: regras e o que cada loja encontrou, com o motivo"></td>
</tr>
<tr>
<td width="50%"><b>Vigiar um produto</b> · <a href="docs/mocks/vigiar-produto.html">vigiar-produto.html</a><br><img src="docs/images/mock-vigiar-produto.png" alt="Mock de vigiar um produto novo: pesquisar, escolher os resultados certos e regras sugeridas"></td>
<td width="50%"><b>Lojas</b> · <a href="docs/mocks/lojas.html">lojas.html</a><br><img src="docs/images/mock-lojas.png" alt="Mock das lojas com o estado das últimas 24 horas"></td>
</tr>
<tr>
<td width="50%"><b>Alertas</b> · <a href="docs/mocks/alertas.html">alertas.html</a><br><img src="docs/images/mock-alertas.png" alt="Mock do histórico de alertas, com o gráfico de 14 dias"></td>
<td width="50%"><b>Definições</b> · <a href="docs/mocks/definicoes.html">definicoes.html</a><br><img src="docs/images/mock-definicoes.png" alt="Mock das definições: tema, verificação e avisos"></td>
</tr>
<tr>
<td width="50%"><b>No telemóvel</b> · <a href="docs/mocks/telemovel.html">telemovel.html</a><br><img src="docs/images/mock-telemovel.png" alt="Mock do Radar no telemóvel, com a barra de navegação em baixo" width="320"></td>
<td width="50%" valign="top"><b>O que muda</b><br><br>
<ul>
<li>Seis páginas passam a cinco: o <b>Radar</b> substitui a Visão geral e o "Testar loja" passa para o detalhe do produto e para as Lojas.</li>
<li>Imagens dos produtos em todo o lado.</li>
<li>Cada resultado diz em português simples porque é que avisou ou não.</li>
<li>Vigiar um produto novo começa por pesquisar nas lojas, e as regras são sugeridas a partir do que escolhes.</li>
<li>Filtros por jogo.</li>
<li>Temas, com o modo escuro por omissão.</li>
</ul></td>
</tr>
</table>

### Temas

Quatro cores (Azul, Coral, Lavanda e Oceano), cada uma em modo escuro (por omissão), claro ou automático, a seguir o sistema.

<img src="docs/images/temas.png" alt="Os temas Azul e Coral em modo escuro, e Lavanda e Oceano em modo claro">

## Arranque rápido (Docker no NAS)

```bash
cp .env.example .env          # preenche DASHBOARD_PASSWORD, TCG_API_KEY e o WhatsApp e/ou o email
docker network create nas-net # só na primeira vez
docker compose up -d --build --remove-orphans
# abre http://<IP-do-NAS>:5080
```

São três contentores:

| Contentor | Porta | O que faz |
| --- | --- | --- |
| `tcgstockbot-webapi` | nenhuma | o bot: monitor em segundo plano e API JSON (`x-api-key`) |
| `tcgstockbot-website` | `5080` | o dashboard, com password |
| `price-proxy` | nenhuma | proxy de saída opcional (Squid), só na rede `nas-net` e fechado por omissão (ver [Pedidos de saída](#pedidos-de-saída-proxy-opcional-e-limites-por-domínio)) |

1. Copia a pasta da solução para o NAS (por exemplo `docker/tcgstockbot`).
2. Copia `.env.example` para `.env` e preenche `DASHBOARD_PASSWORD`, `TCG_API_KEY` (uma chave longa e aleatória) e o WhatsApp e/ou o email.
3. A rede `nas-net` tem de existir. Se ainda não a tens, cria-a uma vez (por SSH): `docker network create nas-net`.
4. Arranca pela app Docker do UGOS (um *Projeto* a apontar para a pasta) ou por SSH, com o comando acima.
5. Abre `http://<IP-do-NAS>:5080`.

A configuração, as tarefas e o histórico ficam no volume `tcgstockbot-data`. Nunca corras `docker compose down -v`, que o apaga.

> [!WARNING]
> Não abras a porta 5080 no router, porque a password passa sem HTTPS. Para aceder de fora de casa, usa uma VPN (por exemplo Tailscale).

<details>
<summary><b>Vindo da v2 (um só contentor <code>tcgstockbot</code>)?</b></summary>

Usa o mesmo nome de pasta. O volume `tcgstockbot-data` é o mesmo e o formato dos ficheiros não mudou, por isso as tarefas, as lojas e o histórico mantêm-se.

- O `--remove-orphans` apaga o contentor antigo.
- No `.env`, a antiga `Dashboard__Password` passa a chamar-se `DASHBOARD_PASSWORD`.
- Acrescenta também `TCG_API_KEY`.
</details>

> [!IMPORTANT]
> **Nunca faças commit de segredos.** O `.env` e os `appsettings.Development.json` estão no `.gitignore`. Para correr no PC, copia `TcgStockBot.WebApi/appsettings.Development.json.example` para `appsettings.Development.json` (e o mesmo no Website, se precisares).

### WhatsApp (CallMeBot)

1. Guarda nos contactos o número publicado em https://www.callmebot.com/blog/free-api-whatsapp-messages/
2. Envia-lhe por WhatsApp: `I allow callmebot to send me messages`. Recebes a API key numa resposta.
3. No `.env`: `WhatsApp__Enabled=true`, `WhatsApp__Phone=+351…` (o teu número) e `WhatsApp__ApiKey=…`.
4. `docker compose up -d`, e depois, em **Definições → WhatsApp**, carrega em *Enviar mensagem de teste*.

O CallMeBot é um serviço gratuito de terceiros, só para uso pessoal, e sem garantias. Mantém o email ligado como segundo canal.

## Lojas

| Plataforma | Como pesquisa | Imagem do produto |
| --- | --- | --- |
| `Shopify` | pesquisa pública da loja (`/search/suggest.json`) | `featured_image` ou `image` |
| `WooCommerce` | Store API pública (`/wp-json/wc/store/…`) | a primeira de `images` |
| `Html` | lê a página de pesquisa com seletores CSS | a primeira `<img>` do cartão (também `data-src` de imagens com *lazy loading*) |
| `Amazon` | API oficial **Creators API** | `images.primary.large` |
| `ElCorteIngles`, `Worten`, `Fnac` | feed de produtos de afiliados **Awin** | `merchant_image_url`, `large_image`, `aw_image_url` ou `aw_thumb_url` |

### Idioma sem marcador (por loja)

| Opção | Comportamento |
| --- | --- |
| Não avisar | título sem "(EN)/Inglês/(PT)…" → não avisa |
| Contar como inglês | a loja marca sempre os PT/ES/JP, por isso sem marcador = inglês |
| **Avisar com idioma por confirmar** | avisa na mesma, e o WhatsApp, o email e o dashboard pedem para confirmares na página (usado na CentroXogo) |

### Lojas grandes: Amazon, FNAC, Worten e El Corte Inglés

Cada loja usa só o mecanismo que ela própria autoriza. O bot não contorna CAPTCHAs, Cloudflare/Akamai, deteção de bots, bloqueios de IP nem limites de pedidos, e não faz scraping da Amazon.

| Loja | Plataforma no dashboard | Mecanismo | Estado |
| --- | --- | --- | --- |
| Amazon.es | `Amazon` | API oficial **Creators API** (sucessora da PA-API 5) | funciona com credenciais de Associates |
| El Corte Inglés (ES) | `ElCorteIngles` | feed de produtos do programa de afiliados **Awin 13075** | funciona depois de o programa te aprovar e configurares o feed |
| Worten | `Worten` | feed de produtos do programa **Awin 99897** ("Worten PT") | o programa existe; **não foi possível confirmar que tem feed** |
| FNAC Portugal | `Fnac` | nenhum mecanismo autorizado confirmado | **não suportada**: só funciona se obtiveres um feed Awin autorizado da FNAC PT |

Nenhuma das três tem API pública de produtos. As APIs de marketplace (Mirakl, e a API XML da FNAC) são só para vendedores com conta aprovada, por isso não servem para acompanhar preços. Os sites respondem 403 ao bot, e os termos de uso restringem a reprodução do conteúdo. Por isso **não há scraper** para estas lojas: os providers leem só o feed Awin e nunca contactam o site da loja.

<details>
<summary><b>Amazon.es (Creators API)</b></summary>

1. Precisas de uma conta **Amazon Associates** (programa de afiliados) aprovada. A Amazon só dá acesso à Creators API a contas com **10 vendas qualificadas nos últimos 30 dias**.
2. Em Associates Central → *Tools → Creators API*: cria uma aplicação e uma credencial. Anota o **Credential ID**, o **Credential Secret** (só aparece uma vez) e a **versão** (amazon.es = `3.2`).
3. No `.env`: `Amazon__CredentialId`, `Amazon__CredentialSecret`, `Amazon__CredentialVersion=3.2` e `Amazon__PartnerTag` (a tua tag, ex.: `atua-tag-21`). Nunca no código nem no `appsettings.json`.
4. `docker compose up -d`. No dashboard, adiciona uma loja com a plataforma **Amazon** e URL base `https://www.amazon.es`, e carrega em *Testar*.

Numa tarefa, o texto de pesquisa pode ser palavras-chave (usa `SearchItems`) ou um ASIN, por exemplo `B0XXXXXXXX` ou `asin:B0XXXXXXXX` (usa `GetItems`, que é mais exato). O preço vem da oferta *buy box*, e o vendedor (ex.: *Amazon.es* ou um revendedor) aparece no resultado. Contam como disponível: `IN_STOCK`, `IN_STOCK_SCARCE`, `PREORDER`, `LEADTIME` e `AVAILABLE_DATE`.

O token OAuth dura 1 hora e fica em cache. A API permite ~1 pedido/segundo e 8640/dia, por isso o limite por omissão para `amazon.es` é de 2 segundos entre pedidos.

Sem credenciais, a loja mostra *"Amazon: integração não configurada"* e a Amazon não é contactada.
</details>

<details>
<summary><b>FNAC, Worten e El Corte Inglés (feeds Awin)</b></summary>

1. Cria uma conta de *publisher* na [Awin](https://www.awin.com) e candidata-te ao programa: El Corte Ingles ES (13075) ou Worten PT (99897). Só depois da aprovação tens acesso ao feed. A FNAC PT não tem programa confirmado: se encontrares um, o processo é o mesmo.
2. Em *Toolbox → Create-a-Feed*, escolhe o feed do anunciante, **CSV**, delimitador **vírgula** (pipe e tab também são aceites), compressão **gzip** ou nenhuma, e as colunas `product_name`, `search_price`, `currency`, `in_stock`, `stock_status`, `merchant_deep_link`, `aw_deep_link`, `merchant_product_id`, `merchant_name` e `merchant_image_url` (ou `aw_image_url`, para a imagem).
3. Copia o URL de download para o `.env`: `AwinFeeds__Feeds__ElCorteIngles=…` (ou `…__Worten`, `…__Fnac`). Esse URL tem a tua chave de feed, por isso é **secreto**. Nos logs aparece só como `datafeed.api.productserve.com/… (Awin feed ElCorteIngles)`.
4. No dashboard, adiciona a loja com a plataforma correspondente e o URL base (ex.: `https://www.elcorteingles.es`).

Como funciona:
- O feed é descarregado **no máximo uma vez a cada `AwinFeeds:RefreshInterval`** (6 h por omissão, mínimo 1 h) e é partilhado por todas as tarefas. As pesquisas filtram-no localmente (todas as palavras do texto de pesquisa no título, sem acentos).
- Um download falhado só volta a ser tentado passados `RetryAfterFailure` (30 min). Entretanto, usa-se o último feed bom, se houver.
- Só ficam em memória os produtos com `KeepKeywords` no título (por omissão `pokemon`, `one piece`, `lorcana` e `gathering`), até `MaxProducts` (5000). Os feeds dos grandes retalhistas têm centenas de milhares de produtos.
- O stock segue as regras da Awin: `in_stock` 1/0 (vazio = sem informação); senão `stock_quantity`, e senão `stock_status` (in stock / out of stock / preorder…). O link é o da loja (`merchant_deep_link`); se faltar, usa-se o `aw_deep_link`.
- Os preços do feed estão atrasados até à última importação da Awin. Confirma sempre na loja antes de comprar.
</details>

## Pedidos de saída: proxy opcional e limites por domínio

Configuração na secção `OutboundHttp` (no Docker, `OutboundHttp__…` no `.env`):

| Chave | Por omissão | Significado |
| --- | --- | --- |
| `Proxy:Enabled` | `false` | `false` = ligação direta; `true` = pedidos de preços pelo `Proxy:Url` |
| `Proxy:Url` | `http://price-proxy:3128` | proxy HTTP (aceita `http://user:pass@host:porta`, as credenciais nunca vão para os logs) |
| `ConnectTimeout` | `00:00:10` | tempo máximo para abrir a ligação TCP (ao proxy, ou à loja se direto) |
| `RateLimits:<domínio>` | amazon.es 2, fnac.pt 10, worten.pt 10, elcorteingles.es 10 (os feeds Awin contam no host da Awin, com o valor por omissão) | **mínimo de segundos entre dois pedidos** ao domínio, incluindo subdomínios (`amazon.es` cobre `www.amazon.es`); a regra mais específica ganha |
| `DefaultRateLimitSeconds` | `2` | o mesmo, para domínios sem regra |
| `MaxThrottleWait` | `00:00:30` | se um pedido tiver de esperar mais do que isto pela sua vez, é adiado para o ciclo seguinte em vez de ficar pendurado |
| `MaxBackoff` | `01:00:00` | teto para o `Retry-After` de uma loja |

- **429/503 com `Retry-After`**: o domínio fica em pausa pelo tempo pedido (um 429 sem `Retry-After` pausa 5 minutos). Não há retries imediatos: o bot tenta de novo no ciclo seguinte.
- **Só os pedidos de preços** (lojas e Amazon) usam o proxy. O **CallMeBot e o email nunca** passam por ele.
- Cada pedido fica no log numa linha como `Outbound [Amazon] POST creatorsapi.amazon/catalog/v1/searchItems via direct -> 200 in 312 ms`, com host e caminho mas **sem** query string, headers, cookies, tokens ou chaves.
- As imagens dos produtos são carregadas pelo browser diretamente das lojas; o bot só guarda o endereço.

<details>
<summary><b>Ligar o proxy (Squid <code>price-proxy</code>)</b></summary>

O proxy **não desbloqueia** lojas que recusam o bot (403). Passa os pedidos tal como são e só serve para teres um ponto único de saída e um log de tudo o que o bot contacta.

1. A rede `nas-net` tem de existir (uma vez): `docker network create nas-net`.
2. `docker compose up -d --build`. O `price-proxy` arranca **fechado**: não tem porta publicada e recusa todos os clientes.
3. Descobre o IP do bot em `nas-net`: `docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}' tcgstockbot-webapi` e põe esse IP (ex.: `172.20.0.5/32`) numa linha de `proxy/allowed-clients.txt`. Para o IP não mudar quando o contentor é recriado, fixa-o no `docker-compose.yml` (`ipv4_address`, ver o comentário).
4. No `.env`: `OutboundHttp__Proxy__Enabled=true`. Depois: `docker compose up -d` e `docker restart price-proxy`.
5. Confirma:
   - `docker logs tcgstockbot-webapi | grep "Outbound HTTP"` → `Outbound HTTP for price providers: proxy price-proxy:3128`
   - `docker exec price-proxy tail -f /var/log/squid/access.log` → uma linha `CONNECT www.amazon.es:443` (ou outra loja) por pedido.

Para desligar: `OutboundHttp__Proxy__Enabled=false` e `docker compose up -d`.
</details>

### Problemas comuns

| No dashboard / log | Causa | O que fazer |
| --- | --- | --- |
| *O proxy … não está disponível* | `price-proxy` parado, nome errado ou fora de `nas-net` | `docker ps`, confirma `Proxy:Url` e as redes |
| *O proxy não conseguiu ligar à loja* | IP do bot não está em `allowed-clients.txt`, ou porta ≠ 443 | corrige a lista e `docker restart price-proxy`; no `access.log` aparece `TCP_DENIED/403` |
| log `...Proxy:Url is not a valid http:// URL; ... DIRECT` | URL inválido com o proxy ligado | usa `http://host:porta` |
| *A loja recusou o pedido (HTTP 403)* | proteção anti-bot da loja | o bot não a contorna, e o proxy não muda isso |
| *A loja pediu para abrandar (HTTP 429)* / *Pedido adiado* | limite da loja ou `RateLimits` | espera; aumenta `PollInterval` ou o `RateLimits` do domínio |
| *Amazon: as credenciais foram recusadas* | ID/secret/versão errados | confirma no Associates Central; amazon.es = `3.2` |
| *Amazon: a API respondeu com erro — … InvalidPartnerTag / AccessDenied* | tag errada, ou conta sem acesso à Creators API | confirma a tag e a elegibilidade (10 vendas em 30 dias) |
| *FNAC/Worten/El Corte Inglés: integração não configurada* | não há feed Awin configurado para a loja | ver "FNAC, Worten e El Corte Inglés"; sem feed autorizado, a loja fica sem dados |
| *…: o Awin recusou o download do feed (HTTP 401/403)* | programa ainda não aprovado, ou URL incompleto | confirma o estado no painel da Awin e gera um URL novo |
| *…: o feed Awin não tem as colunas …* | colunas em falta no Create-a-Feed | gera o feed com as colunas indicadas |

## Desenvolvimento

### Estrutura da solução

```
TcgStockBot.slnx
├── Directory.Build.props / Directory.Packages.props   # net10.0, nullable; versões de pacotes centralizadas
├── TcgStockBot.Library/        # partilhado: Enums (TcgGame, TaskLanguage…), ServiceApiResponse, validação, texto (idioma, preço)
├── TcgStockBot.WebApi/         # o bot: monitor em segundo plano + API JSON (x-api-key)
│   ├── Program.cs
│   ├── TcgStockBotStartup.cs   # registos (AddSingleton, repositório antes do serviço) + pipeline
│   ├── Extensions/             # Data (BotDataContext, RepositoryBase, JsonFile), InternalControllerBase, auth, traduções
│   │   └── Http/               # saída: proxy opcional, limites por domínio, Retry-After, logs sem segredos
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
├── TcgStockBot.UnitTest/       # xUnit, uma pasta por feature (+ teste de contrato Sdk ↔ WebApi)
└── docs/                       # imagens do README, screenshots e os mocks HTML
```

O fluxo é `browser → Website (password + anti-forgery) → Sdk → WebApi (x-api-key) → data/*.json`. Tudo é código deste repositório: não há dependências de bibliotecas externas privadas.

### Correr no PC (Visual Studio)

Abre `TcgStockBot.slnx` e, em *Configure Startup Projects*, escolhe **Multiple** com `TcgStockBot.WebApi` e `TcgStockBot.Website`. Em Development:
- a WebApi corre em `http://127.0.0.1:5081` (Swagger em `/swagger`, chave `devkey`);
- o Website corre em `http://127.0.0.1:5080`.

Os segredos locais vão para os user-secrets:

```bash
dotnet user-secrets --project TcgStockBot.WebApi set "WhatsApp:ApiKey" "<key>"
```

### Testes

```bash
dotnet test TcgStockBot.slnx
```

O `SdkContractTests` falha se um DTO da WebApi ganhar, perder ou mudar uma propriedade sem a cópia no Sdk acompanhar.

---

<sub>Pokémon, One Piece, Disney Lorcana e Magic: The Gathering são marcas dos respetivos donos. Este é um projeto pessoal e não comercial, sem ligação a eles nem às lojas mencionadas.</sub>
