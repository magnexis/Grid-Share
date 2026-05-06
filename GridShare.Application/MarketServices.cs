using System.Diagnostics.Metrics;
using GridShare.Domain;

namespace GridShare.Application;

public interface ILineLossModel
{
    MatchedTrade Apply(MatchedTrade trade);
}

public interface ICarbonAccountingService
{
    CarbonAccountingSummary Calculate(IEnumerable<MatchedTrade> trades);
}

public interface IForecastingEngine
{
    IReadOnlyList<ForecastPoint> ForecastNextHour(IReadOnlyCollection<EnergySnapshot> snapshots);
}

public interface IAnomalyDetector
{
    IReadOnlyList<AnomalyFlag> Detect(IReadOnlyCollection<EnergySnapshot> snapshots);
}

public sealed class DistanceLineLossModel : ILineLossModel
{
    private readonly double _lossRatePerGridUnit;

    public DistanceLineLossModel(double lossRatePerGridUnit = 0.004)
    {
        _lossRatePerGridUnit = lossRatePerGridUnit;
    }

    public MatchedTrade Apply(MatchedTrade trade)
    {
        if (trade.ConsumerId == MatchmakerEngine.CommunityBatteryId)
        {
            return trade with { DeliveredKwh = trade.KwhAmount, LineLossKwh = 0 };
        }

        var lossRatio = Math.Clamp(trade.Distance * _lossRatePerGridUnit, 0, 0.18);
        var loss = trade.KwhAmount * lossRatio;
        return trade with { DeliveredKwh = trade.KwhAmount - loss, LineLossKwh = loss };
    }
}

public sealed class CarbonAccountingService : ICarbonAccountingService
{
    private readonly double _gridEmissionFactorKgPerKwh;

    public CarbonAccountingService(double gridEmissionFactorKgPerKwh = 0.386)
    {
        _gridEmissionFactorKgPerKwh = gridEmissionFactorKgPerKwh;
    }

    public CarbonAccountingSummary Calculate(IEnumerable<MatchedTrade> trades)
    {
        var offset = trades
            .Where(trade => trade.ConsumerId != MatchmakerEngine.CommunityBatteryId)
            .Sum(trade => trade.DeliveredKwh * EnergyMarketCatalog.Resolve(trade.ConsumerMarketCode).GridEmissionFactorKgPerKwh);

        return new CarbonAccountingSummary(offset, _gridEmissionFactorKgPerKwh);
    }
}

public sealed class ForecastingEngine : IForecastingEngine
{
    public IReadOnlyList<ForecastPoint> ForecastNextHour(IReadOnlyCollection<EnergySnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return [];
        }

        var currentTime = snapshots.Max(snapshot => snapshot.SimulationTime);
        var production = snapshots.Sum(snapshot => snapshot.ProducedKw);
        var consumption = snapshots.Sum(snapshot => snapshot.ConsumedKw);
        var exchange = snapshots.Sum(snapshot => snapshot.NetKw);
        var points = new List<ForecastPoint>();

        for (var step = 1; step <= 4; step++)
        {
            var time = currentTime.AddMinutes(15 * step);
            var daylightBias = Math.Max(0.15, Math.Sin(Math.PI * time.TimeOfDay.TotalHours / 24));
            var demandBias = 1 + (Math.Cos((time.TimeOfDay.TotalHours - 19) * Math.PI / 12) * 0.12);
            points.Add(new ForecastPoint(time, production * daylightBias, consumption * demandBias, exchange));
        }

        return points;
    }
}

public sealed class AnomalyDetector : IAnomalyDetector
{
    public IReadOnlyList<AnomalyFlag> Detect(IReadOnlyCollection<EnergySnapshot> snapshots)
    {
        var flags = new List<AnomalyFlag>();

        foreach (var snapshot in snapshots)
        {
            if (snapshot.ProducedKw < -0.01 || snapshot.ConsumedKw < -0.01)
            {
                flags.Add(new AnomalyFlag(snapshot.NodeId, "NEGATIVE_METER", "Meter reported negative production or consumption.", 0.95, snapshot.SimulationTime));
            }

            if (snapshot.ProducedKw > 25)
            {
                flags.Add(new AnomalyFlag(snapshot.NodeId, "SOLAR_SPIKE", "Solar production exceeds expected residential inverter range.", 0.75, snapshot.SimulationTime));
            }

            if (snapshot.ConsumedKw > 18)
            {
                flags.Add(new AnomalyFlag(snapshot.NodeId, "LOAD_SPIKE", "Consumption spike may indicate EV fast charge, fault, or bad meter sample.", 0.65, snapshot.SimulationTime));
            }
        }

        return flags;
    }
}

public sealed class MarketMetrics
{
    public const string MeterName = "GridShare.Market";
    private readonly Counter<long> _tradeCounter;
    private readonly Histogram<double> _priceHistogram;
    private readonly Histogram<double> _demandPressureHistogram;

    public MarketMetrics(IMeterFactory? meterFactory = null)
    {
        var meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);
        _tradeCounter = meter.CreateCounter<long>("gridshare.trades");
        _priceHistogram = meter.CreateHistogram<double>("gridshare.price_per_kwh");
        _demandPressureHistogram = meter.CreateHistogram<double>("gridshare.demand_pressure");
    }

    public void Record(MarketSnapshot snapshot)
    {
        _tradeCounter.Add(snapshot.Trades.Count);
        _priceHistogram.Record((double)snapshot.MarketPricePerKwh);
        _demandPressureHistogram.Record(snapshot.MarketDemandKw / Math.Max(snapshot.MarketSupplyKw, 0.1));
    }
}
