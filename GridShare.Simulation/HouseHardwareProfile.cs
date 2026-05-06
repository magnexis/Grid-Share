namespace GridShare.Simulation;

public enum HouseholdArchetype
{
    Standard,
    EvOwner,
    WorkFromHome,
    SolarOnly,
    BatteryHeavy,
    LowIncomeCriticalLoad
}

public sealed record HouseHardwareProfile(
    double PanelCapacityKw,
    double BatteryStorageKwh,
    double BaseLoadKw,
    double MorningPeakKw,
    double EveningPeakKw,
    double ConsumptionVariance,
    HouseholdArchetype Archetype,
    double CriticalLoadKw = 0);

public sealed record WeatherProfile(
    double CloudCover,
    double TemperatureC,
    int DayOfYear)
{
    public static WeatherProfile ClearSpringDay => new(0.18, 22, 125);
}

public sealed record ReliabilityProfile(
    double DropPacketProbability,
    double StaleReadingProbability,
    double DelayedReadingProbability)
{
    public static ReliabilityProfile Normal => new(0.015, 0.02, 0.02);
}
