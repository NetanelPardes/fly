namespace Airport.Api.DTOs;

public record CurrencyRatesDto(
    string BaseCurrency,
    DateTime FetchedAt,
    IReadOnlyDictionary<string, decimal> Rates);
