using System.Globalization;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Validation;

namespace TcgStockBot.WebApi.Extensions.Globalization;

/// <summary>
/// Translates message keys and field identifiers into user-facing text.
/// </summary>
internal interface IGlobalizationHelper
{
    /// <summary>
    /// Translates a key.
    /// </summary>
    /// <param name="key">A <see cref="TranslationKeys"/> name or a field identifier.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The translated text, or the key itself when there is no translation.</returns>
    Task<string> TranslateAsync(string key, params object[] arguments);

    /// <summary>
    /// Builds a validation error "{field label}: {message}".
    /// </summary>
    /// <param name="field">The field identifier.</param>
    /// <param name="messageKey">The message key.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The validation error.</returns>
    Task<ApiValidationError> ValidationErrorAsync(string field, string messageKey, params object[] arguments);
}

/// <summary>
/// Portuguese (pt-PT) translations. The dashboard is Portuguese-only, so the texts live in code
/// rather than in a translation table.
/// </summary>
internal sealed class GlobalizationHelper : IGlobalizationHelper
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-PT");

    private static readonly Dictionary<string, string> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(TranslationKeys.ValidationProblems)] = "Há problemas nos dados enviados.",
        [nameof(TranslationKeys.RequiredField)] = "é obrigatório.",
        [nameof(TranslationKeys.GreaterThen0)] = "tem de ser maior que 0.",
        [nameof(TranslationKeys.InvalidUrl)] = "tem de ser um endereço http(s) completo.",
        [nameof(TranslationKeys.InvalidCharacter)] = "não pode conter o carácter '|'.",
        [nameof(TranslationKeys.AlreadyExists)] = "já existe outro registo com '{0}'.",
        [nameof(TranslationKeys.NoRecordsFound)] = "O registo '{0}' não existe.",
        [nameof(TranslationKeys.PollIntervalTooShort)] = "tem de ser de pelo menos 1 minuto.",
        [nameof(TranslationKeys.NegativeValue)] = "não pode ser negativo.",
        [nameof(TranslationKeys.StoreHttpError)] = "A loja respondeu HTTP {0} ({1}).",
        [nameof(TranslationKeys.StoreTimeout)] = "A loja não respondeu a tempo.",
        [nameof(TranslationKeys.StoreBlocked)] = "A loja recusou o pedido (HTTP 403). Normalmente é a proteção anti-bot da loja; o bot não a contorna.",
        [nameof(TranslationKeys.StoreRateLimited)] = "A loja pediu para abrandar (HTTP 429). Fica em pausa e volta a ser consultada depois.",
        [nameof(TranslationKeys.StoreServerError)] = "Erro do lado da loja (HTTP {0}). Volta a tentar no próximo ciclo.",
        [nameof(TranslationKeys.StoreThrottled)] = "Pedido adiado: {0} está em pausa por mais {1} s (limite de pedidos).",
        [nameof(TranslationKeys.DnsFailed)] = "Não foi possível resolver o endereço (DNS).",
        [nameof(TranslationKeys.ConnectionRefused)] = "A ligação foi recusada.",
        [nameof(TranslationKeys.StoreIntegrationNotConfigured)] = "{0}: integração não configurada — {1}",
        [nameof(TranslationKeys.StoreIntegrationUnauthorized)] = "{0}: as credenciais foram recusadas — {1}",
        [nameof(TranslationKeys.StoreIntegrationApiError)] = "{0}: a API respondeu com erro — {1}",
        [nameof(TranslationKeys.NotificationChannelDisabled)] = "O canal {0} está desligado na configuração.",
        [nameof(TranslationKeys.StoreWithoutIntegration)] = "{0} não tem integração: está na lista só como referência. Escolhe uma plataforma (Shopify, WooCommerce, HTML, Awin…) para a pesquisar.",
        [nameof(TranslationKeys.UnknownProductKind)] = "o tipo de produto '{0}' não existe.",
        [nameof(TranslationKeys.InvalidKey)] = "só pode ter letras, números e '_'.",
        [nameof(TranslationKeys.StoreDetected)] = "Detetado: {0}.",
        [nameof(TranslationKeys.StoreDetectBlocked)] = "O site recusou o bot (403/429 ou proteção anti-bot). Não é contornado: fica \"Sem integração\", ou usa um feed Awin/API se existir.",
        [nameof(TranslationKeys.StoreDetectUnreachable)] = "Não foi possível ligar ao site (DNS, ligação ou tempo esgotado).",
        [nameof(TranslationKeys.StoreDetectUnknown)] = "O site respondeu, mas não tem API Shopify/WooCommerce nem pesquisa PrestaShop reconhecível. Configura HTML à mão (se os termos permitirem) ou deixa \"Sem integração\".",
        [nameof(TranslationKeys.StoresAutoDisabledSubject)] = "[TCG] Loja(s) desligada(s) por erros: {0}",
        [nameof(TranslationKeys.StoreAutoDisabledLine)] = "{0}: {1} ciclos seguidos com erro — {2}",
        [nameof(TranslationKeys.NotificationFailed)] = "Falhou o envio por {0}: {1}",

        // Field labels
        ["Identifier"] = "Id",
        ["Name"] = "Nome",
        ["SearchQuery"] = "Texto de pesquisa",
        ["MaxPrice"] = "Preço máximo",
        ["BaseUrl"] = "URL base",
        ["SearchUrlTemplate"] = "URL de pesquisa",
        ["ProductSelector"] = "Seletor do produto",
        ["Query"] = "Pesquisa",
        ["PollIntervalSeconds"] = "Intervalo entre verificações",
        ["PollJitterSeconds"] = "Variação aleatória",
        ["DelayBetweenRequestsSeconds"] = "Pausa entre pedidos",
        ["UserAgent"] = "User-Agent",
        ["Channel"] = "Canal",
    };

    /// <inheritdoc />
    public Task<string> TranslateAsync(string key, params object[] arguments)
    {
        var text = Translations.TryGetValue(key, out var translation) ? translation : key;
        return Task.FromResult(arguments.Length == 0 ? text : string.Format(Portuguese, text, arguments));
    }

    /// <inheritdoc />
    public async Task<ApiValidationError> ValidationErrorAsync(string field, string messageKey, params object[] arguments) =>
        new(field, $"{await TranslateAsync(field)}: {await TranslateAsync(messageKey, arguments)}");
}
