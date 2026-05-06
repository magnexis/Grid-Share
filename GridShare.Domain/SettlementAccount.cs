namespace GridShare.Domain;

public sealed record SettlementAccount(
    string NodeId,
    decimal WalletBalance,
    double BatteryCreditsKwh,
    decimal LifetimeEarnings,
    decimal UnpaidObligations);

public sealed record CarbonAccountingSummary(
    double CarbonOffsetKg,
    double GridEmissionFactorKgPerKwh);

public sealed record ForecastPoint(
    DateTimeOffset Time,
    double ExpectedProductionKw,
    double ExpectedConsumptionKw,
    double ExpectedGridExchangeKw);

public sealed record AnomalyFlag(
    string NodeId,
    string Code,
    string Message,
    double Severity,
    DateTimeOffset Timestamp);
