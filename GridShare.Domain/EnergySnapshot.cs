namespace GridShare.Domain;

public sealed record EnergySnapshot(
    string NodeId,
    GridLocation Location,
    double ProducedKw,
    double ConsumedKw,
    double GridExchangeKw,
    double BatteryChargeKwh,
    double BatteryCapacityKwh,
    TimeSpan SamplePeriod,
    DateTimeOffset SimulationTime)
{
    public double NetKw => GridExchangeKw;

    public bool HasSurplus => NetKw > 0;

    public bool HasDemand => NetKw < 0;

    public double CriticalLoadKw { get; init; }

    public bool IsStale { get; init; }

    public string MarketCode { get; init; } = "US-AVG";
}
