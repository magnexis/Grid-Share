using GridShare.Domain;

namespace GridShare.Simulation;

public sealed class SmartHouse
{
    private readonly Random _random;
    private EnergySnapshot? _lastSnapshot;

    public SmartHouse(
        string id,
        HouseHardwareProfile hardwareProfile,
        GridLocation location,
        string marketCode,
        ReliabilityProfile? reliabilityProfile = null,
        int? seed = null)
    {
        Id = id;
        HardwareProfile = hardwareProfile;
        Location = location;
        MarketCode = marketCode;
        ReliabilityProfile = reliabilityProfile ?? ReliabilityProfile.Normal;
        BatteryChargeKwh = hardwareProfile.BatteryStorageKwh * 0.35;
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
    }

    public string Id { get; }

    public HouseHardwareProfile HardwareProfile { get; }

    public GridLocation Location { get; }

    public string MarketCode { get; }

    public ReliabilityProfile ReliabilityProfile { get; }

    public double BatteryChargeKwh { get; private set; }

    public EnergySnapshot? Tick(DateTimeOffset simulationTime, TimeSpan simulatedStep, WeatherProfile? weatherProfile = null, decimal marketPricePerKwh = 0.16m)
    {
        if (_random.NextDouble() < ReliabilityProfile.DropPacketProbability)
        {
            return null;
        }

        if (_lastSnapshot is not null && _random.NextDouble() < ReliabilityProfile.StaleReadingProbability)
        {
            return _lastSnapshot with { SimulationTime = simulationTime, IsStale = true };
        }

        var productionKw = DiurnalCycle.SolarProductionKw(simulationTime, HardwareProfile.PanelCapacityKw, weatherProfile);
        var consumptionKw = DiurnalCycle.ResidentialConsumptionKw(
            simulationTime,
            HardwareProfile.BaseLoadKw,
            HardwareProfile.MorningPeakKw,
            HardwareProfile.EveningPeakKw);

        consumptionKw += HardwareProfile.Archetype == HouseholdArchetype.EvOwner && simulationTime.Hour is >= 18 and <= 22 ? 3.2 : 0;
        consumptionKw += HardwareProfile.Archetype == HouseholdArchetype.WorkFromHome && simulationTime.Hour is >= 9 and <= 16 ? 0.55 : 0;
        consumptionKw *= marketPricePerKwh > 0.38m && HardwareProfile.CriticalLoadKw <= 0 ? 0.88 : 1;
        consumptionKw *= 1 + ((_random.NextDouble() - 0.5) * HardwareProfile.ConsumptionVariance);

        var stepHours = simulatedStep.TotalHours;
        var netKwhForStep = (productionKw - consumptionKw) * stepHours;
        var gridExchangeKwh = netKwhForStep;

        if (netKwhForStep > 0)
        {
            var chargeAccepted = Math.Min(netKwhForStep, HardwareProfile.BatteryStorageKwh - BatteryChargeKwh);
            BatteryChargeKwh += chargeAccepted;
            gridExchangeKwh -= chargeAccepted;
        }
        else if (netKwhForStep < 0)
        {
            var discharge = Math.Min(Math.Abs(netKwhForStep), BatteryChargeKwh);
            BatteryChargeKwh -= discharge;
            gridExchangeKwh += discharge;
        }

        var snapshot = new EnergySnapshot(
            Id,
            Location,
            productionKw,
            consumptionKw,
            gridExchangeKwh / stepHours,
            BatteryChargeKwh,
            HardwareProfile.BatteryStorageKwh,
            simulatedStep,
            simulationTime)
        {
            CriticalLoadKw = HardwareProfile.CriticalLoadKw,
            IsStale = false,
            MarketCode = MarketCode
        };

        _lastSnapshot = snapshot;

        if (_random.NextDouble() < ReliabilityProfile.DelayedReadingProbability)
        {
            return snapshot with { SimulationTime = simulationTime.Add(-simulatedStep), IsStale = true };
        }

        return snapshot;
    }
}
