using Airport.Api.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Airport.Api.Services;

public interface ICurrencyExchangeService
{
    Task<CurrencyRatesDto> GetUsdRatesAsync(CancellationToken cancellationToken);
}

public class CurrencyExchangeService(HttpClient httpClient, IMemoryCache cache, ILogger<CurrencyExchangeService> logger) : ICurrencyExchangeService
{
    private static readonly string[] SupportedCurrencies = ["USD", "ILS", "EUR", "GBP", "JPY", "RUB"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string CacheKey = "currency-rates-usd";

    public async Task<CurrencyRatesDto> GetUsdRatesAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out CurrencyRatesDto? cached) && cached is not null)
        {
            return cached;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://open.er-api.com/v6/latest/USD");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Currency rate provider returned HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        CurrencyProviderResponse? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<CurrencyProviderResponse>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException error)
        {
            logger.LogWarning(error, "Currency provider response JSON could not be parsed.");
            throw new InvalidOperationException("Currency rate provider returned an unreadable response.");
        }

        if (payload is null || !string.Equals(payload.Result, "success", StringComparison.OrdinalIgnoreCase) || payload.Rates is null)
        {
            throw new InvalidOperationException("Currency rate provider returned an error.");
        }

        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var currency in SupportedCurrencies)
        {
            if (currency == "USD")
            {
                rates[currency] = 1m;
                continue;
            }

            if (!payload.Rates.TryGetValue(currency, out var rate) || rate <= 0)
            {
                throw new InvalidOperationException($"Currency rate provider did not return {currency}.");
            }

            rates[currency] = rate;
        }

        var result = new CurrencyRatesDto("USD", DateTime.UtcNow, rates);
        cache.Set(CacheKey, result, TimeSpan.FromMinutes(10));
        return result;
    }

    private sealed record CurrencyProviderResponse(
        string? Result,
        [property: JsonPropertyName("base_code")] string? BaseCode,
        Dictionary<string, decimal>? Rates);
}
