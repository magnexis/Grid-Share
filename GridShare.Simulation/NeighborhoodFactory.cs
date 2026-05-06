using GridShare.Domain;

namespace GridShare.Simulation;

public static class NeighborhoodFactory
{
    public static IReadOnlyList<SmartHouse> Create(int count, int seed = 412)
    {
        var random = new Random(seed);
        var houses = new List<SmartHouse>(count);

        for (var index = 0; index < count; index++)
        {
            var isConsumerHeavy = index % 5 == 0;
            var archetype = PickArchetype(index);
            var profile = new HouseHardwareProfile(
                PanelCapacityKw: archetype == HouseholdArchetype.SolarOnly ? 7.5 + random.NextDouble() * 6 : isConsumerHeavy ? random.NextDouble() * 1.5 : 3.5 + random.NextDouble() * 6.5,
                BatteryStorageKwh: archetype == HouseholdArchetype.BatteryHeavy ? 16 + random.NextDouble() * 16 : isConsumerHeavy ? random.NextDouble() * 2.5 : 4 + random.NextDouble() * 12,
                BaseLoadKw: isConsumerHeavy ? 0.9 + random.NextDouble() * 0.8 : 0.35 + random.NextDouble() * 0.55,
                MorningPeakKw: isConsumerHeavy ? 1.6 + random.NextDouble() * 2.1 : 0.8 + random.NextDouble() * 1.8,
                EveningPeakKw: isConsumerHeavy ? 2.2 + random.NextDouble() * 2.8 : 1.1 + random.NextDouble() * 2.3,
                ConsumptionVariance: 0.18,
                Archetype: archetype,
                CriticalLoadKw: archetype == HouseholdArchetype.LowIncomeCriticalLoad ? 0.65 : 0);

            houses.Add(new SmartHouse(
                $"House_{index + 1:00}",
                profile,
                new GridLocation(random.NextDouble() * 10, random.NextDouble() * 10),
                PickMarket(index),
                ReliabilityProfile.Normal,
                seed + index));
        }

        return houses;
    }

    private static HouseholdArchetype PickArchetype(int index)
    {
        return (index % 12) switch
        {
            0 => HouseholdArchetype.EvOwner,
            1 => HouseholdArchetype.WorkFromHome,
            2 => HouseholdArchetype.SolarOnly,
            3 => HouseholdArchetype.BatteryHeavy,
            4 => HouseholdArchetype.LowIncomeCriticalLoad,
            _ => HouseholdArchetype.Standard
        };
    }

    private static string PickMarket(int index)
    {
        var markets = EnergyMarketCatalog.Supported;
        return markets[index % markets.Count].Code;
    }
}
