using System.Globalization;

namespace GridShare.Domain;

public sealed record CurrencyProfile(
    string Code,
    string DisplayName,
    string Region,
    decimal UnitsPerUsd,
    string Locale)
{
    public decimal FromUsd(decimal amount)
    {
        return amount * UnitsPerUsd;
    }

    public string Format(decimal usdAmount)
    {
        var culture = CultureInfo.GetCultureInfo(Locale);
        return string.Create(culture, $"{FromUsd(usdAmount):C}");
    }
}

public static class CurrencyCatalog
{
    private static readonly CurrencyProfile[] Profiles =
    [
        new("USD", "US Dollar", "United States solar markets", 1.00m, "en-US"),
        new("EUR", "Euro", "European Union solar markets", 0.93m, "de-DE"),
        new("GBP", "Pound Sterling", "United Kingdom solar markets", 0.80m, "en-GB"),
        new("AUD", "Australian Dollar", "Australia rooftop solar", 1.52m, "en-AU"),
        new("INR", "Indian Rupee", "India solar expansion", 83.30m, "en-IN"),
        new("JPY", "Japanese Yen", "Japan distributed solar", 151.00m, "ja-JP"),
        new("CNY", "Chinese Yuan", "China solar manufacturing and deployment", 7.24m, "zh-CN"),
        new("BRL", "Brazilian Real", "Brazil distributed generation", 5.05m, "pt-BR"),
        new("ZAR", "South African Rand", "South Africa solar and backup power", 18.60m, "en-ZA"),
        new("MXN", "Mexican Peso", "Mexico solar markets", 16.80m, "es-MX"),
        new("AED", "UAE Dirham", "United Arab Emirates utility solar", 3.67m, "ar-AE"),
        new("KES", "Kenyan Shilling", "Kenya off-grid and mini-grid solar", 132.00m, "en-KE")
    ];

    public static IReadOnlyList<CurrencyProfile> Supported => Profiles;

    public static CurrencyProfile Resolve(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Profiles[0];
        }

        return Profiles.FirstOrDefault(profile => string.Equals(profile.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? Profiles[0];
    }
}

public sealed record EnergyMarketProfile(
    string Code,
    string Name,
    string CountryCode,
    string CurrencyCode,
    decimal ResidentialRetailPricePerKwh,
    decimal SolarExportPricePerKwh,
    double GridEmissionFactorKgPerKwh,
    string SourceNote)
{
    public decimal RetailUsdPerKwh => ResidentialRetailPricePerKwh / CurrencyCatalog.Resolve(CurrencyCode).UnitsPerUsd;

    public decimal ExportUsdPerKwh => SolarExportPricePerKwh / CurrencyCatalog.Resolve(CurrencyCode).UnitsPerUsd;
}

public static class EnergyMarketCatalog
{
    private static readonly EnergyMarketProfile[] Profiles =
    [
        new("US-AVG", "United States average", "US", "USD", 0.168m, 0.075m, 0.386, "EIA 2025 US residential average forecast and representative export credit."),
        new("DE-DE", "Germany household market", "DE", "EUR", 0.3869m, 0.082m, 0.351, "Eurostat 2025-S2 Germany household electricity price and German EEG-style export baseline."),
        new("GB-UK", "United Kingdom price-cap market", "GB", "GBP", 0.2573m, 0.12m, 0.207, "Ofgem 2025 electricity unit-rate cap and representative smart export tariff."),
        new("AU-NSW", "Australia NSW rooftop solar", "AU", "AUD", 0.34m, 0.075m, 0.65, "Representative NSW residential tariff and rooftop solar feed-in range."),
        new("IN-DL", "India Delhi solar", "IN", "INR", 7.20m, 3.00m, 0.71, "Representative Indian residential slab and net-metering export baseline."),
        new("JP-TEPCO", "Japan TEPCO residential", "JP", "JPY", 31.00m, 16.00m, 0.44, "Representative Japanese residential unit rate and post-FIT export price."),
        new("BR-SP", "Brazil Sao Paulo distributed generation", "BR", "BRL", 0.92m, 0.42m, 0.08, "Representative Brazil residential tariff and distributed generation credit."),
        new("ZA-CPT", "South Africa Cape Town solar", "ZA", "ZAR", 3.70m, 1.25m, 0.86, "Representative municipal residential tariff and small-scale embedded generation credit."),
        new("MX-CFE", "Mexico CFE residential", "MX", "MXN", 1.10m, 0.78m, 0.39, "Representative CFE residential energy charge and net-metering credit."),
        new("KE-NAI", "Kenya mini-grid solar", "KE", "KES", 31.75m, 12.00m, 0.17, "Representative Kenya residential tariff and mini-grid solar export credit.")
    ];

    public static IReadOnlyList<EnergyMarketProfile> Supported => Profiles;

    public static EnergyMarketProfile Resolve(string? code)
    {
        return Profiles.FirstOrDefault(profile => string.Equals(profile.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? Profiles[0];
    }
}
