using Airport.Api.DTOs;
using System.Globalization;

namespace Airport.Api.Services;

public class AirportLocationCatalog(IConfiguration configuration, IHostEnvironment environment)
{
    private AirportLocationCatalogDto? cachedCatalog;

    public AirportLocationCatalogDto GetCatalog()
    {
        cachedCatalog ??= LoadCatalog();
        return cachedCatalog;
    }

    private AirportLocationCatalogDto LoadCatalog()
    {
        var path = Path.Combine(ResolveDataDirectory(), "airports.csv");
        var locations = ReadCsv(path)
            .Select(ToLocation)
            .Where(location => location is not null)
            .Select(location => location!)
            .DistinctBy(location => $"{location.City}|{location.Country}|{location.IataCode}|{location.AirportName}")
            .OrderBy(location => location.Country)
            .ThenBy(location => location.City)
            .ThenBy(location => location.AirportName)
            .ToList();

        var countries = AllKnownCountries()
            .Concat(locations.Select(location => location.Country))
            .Where(country => !string.IsNullOrWhiteSpace(country))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(country => country)
            .ToList();

        return new AirportLocationCatalogDto(countries, locations);
    }

    private AirportLocationDto? ToLocation(Dictionary<string, string> row)
    {
        var city = Clean(row.GetValueOrDefault("municipality"));
        var airportName = Clean(row.GetValueOrDefault("name"));
        var countryCode = Clean(row.GetValueOrDefault("iso_country"));
        var country = CountryName(countryCode);
        var iata = Clean(row.GetValueOrDefault("iata_code"));
        var icao = Clean(row.GetValueOrDefault("icao_code"));

        if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(country))
        {
            return null;
        }

        var codeText = !string.IsNullOrWhiteSpace(iata) ? $" ({iata})" : string.Empty;
        var label = $"{city}, {country}{codeText} - {airportName}";
        return new AirportLocationDto(city, country, countryCode, airportName, EmptyToNull(iata), EmptyToNull(icao), label);
    }

    private string ResolveDataDirectory()
    {
        var configured = configuration["AIRPORT_DATA_DIRECTORY"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var publishedData = Path.Combine(AppContext.BaseDirectory, "Data");
        if (Directory.Exists(publishedData))
        {
            return publishedData;
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "Data"));
    }

    private static string CountryName(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Equals("ZZ", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        try
        {
            return new RegionInfo(countryCode).EnglishName;
        }
        catch (ArgumentException)
        {
            return countryCode;
        }
    }

    private static IEnumerable<string> AllKnownCountries()
    {
        return CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(culture =>
            {
                try
                {
                    return new RegionInfo(culture.Name).EnglishName;
                }
                catch (ArgumentException)
                {
                    return string.Empty;
                }
            })
            .Where(country => !string.IsNullOrWhiteSpace(country));
    }

    private static string Clean(string? value)
    {
        var cleaned = (value ?? string.Empty).Trim();
        return cleaned is @"\N" or "N/A" ? string.Empty : cleaned;
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static IEnumerable<Dictionary<string, string>> ReadCsv(string path)
    {
        using var reader = new StreamReader(path);
        var headers = ParseCsvLine(reader.ReadLine() ?? string.Empty);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < values.Count && index < headers.Count; index++)
            {
                row[headers[index]] = values[index];
            }

            yield return row;
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new List<char>();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Add('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(new string(current.ToArray()));
                current.Clear();
            }
            else
            {
                current.Add(character);
            }
        }

        values.Add(new string(current.ToArray()));
        return values;
    }
}
