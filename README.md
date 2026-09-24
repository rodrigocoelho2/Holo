# TcgStockBot

Monitoriza lojas portuguesas de Pokémon TCG e avisa-te (WhatsApp e/ou email) quando um produto está
**em stock, na versão inglesa e ao preço máximo que definiste ou abaixo**. Tem um dashboard web para
veres o que se passa e configurares tarefas e lojas. Só monitoriza e avisa: a compra fazes tu.

## ⚠️ Configuração de Segredos

**IMPORTANTE:** Nunca commites segredos para o Git! 

Os seguintes ficheiros NÃO devem ser incluídos no repositório:
- `.env` - Variáveis de ambiente (passwords, API keys)
- `**/appsettings.Development.json` - Configuração local de desenvolvimento

**Como configurar localmente:**
1. Copia `.env.example` para `.env` e preenche com os teus valores
2. Copia `TcgStockBot.WebApi/appsettings.Development.json.example` para `TcgStockBot.WebApi/appsettings.Development.json`
3. Faz o mesmo para `TcgStockBot.Website/appsettings.Development.json` (se necessário)

Estes ficheiros estão em `.gitignore` e **nunca serão committed**.

## Estrutura da solução

```
TcgStockBot.slnx
├── Directory.Build.props / Directory.Packages.props   # net10.0, nullable; versões de pacotes centralizadas
├── TcgStockBot.Library/        # partilhado: Enums, ServiceApiResponse, validação, texto (idioma, preço)
├── TcgStockBot.WebApi/         # o bot: monitor em segundo plano + API JSON (x-api-key)
│   ├── Program.cs
│   ├── TcgStockBotStartup.cs   # registos (AddSingleton, repositório antes do serviço) + pipeline
│   ├── Extensions/             # dados (BotDataContext, RepositoryBase), InternalControllerBase, auth, traduções
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

## 📸 Exemplos do Dashboard

### Visão Geral
Página de dashboard com resumo de alertas, gráfico de 14 dias e estatísticas por loja.
- **Alertas enviados**: 68 alertas
- **Em stock agora**: 0 produtos
- **Preparo de alertas**: 16/16 lojas prontas
- **Gráfico de evolução**: mostra a tendência de alertas ao longo do tempo
- **Alertas por loja**: breakdown de Trap Card TCG, Versus Gamecenter, CentroXogo, Panda Collecting, etc.
- **Feed em tempo real**: últimas verificações e alertas com timestamps

*[Screenshot será adicionada em breve]*

### Tarefas
Página de gestão de tarefas de monitorização. Cada tarefa vigia um produto específico em várias lojas.
- **Filtros**: Todas, A comer 1, Em stock 2, Pausadas 1
- **Produtos monitorados**:
  - 30th ETB (Trap Card TCG) - há 5 h
  - 30th en Box (Versus Gamecenter) - há 5 h
  - 30th Ultra Premium (CentroXogo) - há 3 h
  - 30th Booster Bundle (Panda Collecting) - há 5 h
- **Cada tarefa mostra**: preço máximo, estado (✅ online), última verificação
- **Ações**: Testar (Teste), Editar (Editar), Duplicar, Apagar

*[Screenshot será adicionada em breve]*

### Lojas
Página com todas as lojas configuradas (9 lojas no exemplo).
- **Trap Card TCG** (Shopify) - ✅ Online
- **Versus Gamecenter** (Shopify) - ✅ Online - confirmar idioma
- **Panda Collecting** (WooCommerce) - ✅ Online
- **Notel** (HTML) - ⚠️ Offline
- **CentroXogo** (Shopify) - ✅ Online
- **Areias Porto** (WooCommerce) - ✅ Online
- **Continente** (Shopify) - ✅ Online
- **Cada loja tem**: status, tipo de integração, última verificação e botões de Testar e Editar

*[Screenshot será adicionada em breve]*

## Docker no NAS UGREEN

São três contentores:
- `tcgstockbot-webapi`, o bot, sem porta exposta;
- `tcgstockbot-website`, o dashboard, na porta 5080;
- `price-proxy`, o proxy de saída opcional, sem porta exposta, só na rede `nas-net` e fechado por omissão
  (ver "Pedidos de saída").

1. Copia a pasta da solução para o NAS (por exemplo `docker/tcgstockbot`).
2. Copia `.env.example` para `.env` e preenche `DASHBOARD_PASSWORD`, `TCG_API_KEY`
   (uma chave longa e aleatória) e o WhatsApp e/ou o email.
3. A rede `nas-net` tem de existir. Se ainda não a tens, cria-a uma vez (por SSH): `docker network create nas-net`.
4. Arranca, pela app Docker do UGOS (Projeto a apontar para a pasta) ou por SSH:

   ```bash
   docker compose up -d --build --remove-orphans
   ```

5. Abre `http://<IP-do-NAS>:5080`.

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

- **429/503 com `Retry-After`**: o domínio fica em pausa pelo tempo pedido (um 429 sem `Retry-After`
  pausa 5 minutos). Não há retries imediatos: o bot tenta de novo no ciclo seguinte.
- **Só os pedidos de preços** (lojas e Amazon) usam o proxy. O **CallMeBot e o email nunca** passam por ele.
- Cada pedido fica no log numa linha como `Outbound [Amazon] POST creatorsapi.amazon/catalog/v1/searchItems via direct -> 200 in 312 ms`,
  com host e caminho mas **sem** query string, headers, cookies, tokens ou chaves.

### Ligar o proxy (Squid `price-proxy`)

O proxy **não desbloqueia** lojas que recusam o bot (403). Passa os pedidos tal como são e só serve para
teres um ponto único de saída e um log de tudo o que o bot contacta.

1. A rede `nas-net` tem de existir (uma vez): `docker network create nas-net`.
2. `docker compose up -d --build`. O `price-proxy` arranca **fechado**: não tem porta publicada e recusa
   todos os clientes.
3. Descobre o IP do bot em `nas-net`:
   `docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}' tcgstockbot-webapi`
   e põe esse IP (ex.: `172.20.0.5/32`) numa linha de `proxy/allowed-clients.txt`. Para o IP não mudar
   quando o contentor é recriado, fixa-o no `docker-compose.yml` (`ipv4_address`, ver o comentário).
4. No `.env`: `OutboundHttp__Proxy__Enabled=true`. Depois: `docker compose up -d` e `docker restart price-proxy`.
5. Confirma:
   - `docker logs tcgstockbot-webapi | grep "Outbound HTTP"` → `Outbound HTTP for price providers: proxy price-proxy:3128`
   - `docker exec price-proxy tail -f /var/log/squid/access.log` → uma linha `CONNECT www.amazon.es:443` (ou outra loja) por pedido.

Para desligar: `OutboundHttp__Proxy__Enabled=false` e `docker compose up -d`.

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
