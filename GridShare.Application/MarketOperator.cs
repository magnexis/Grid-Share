using GridShare.Domain;

namespace GridShare.Application;

public sealed class MarketOperator
{
    private readonly ITradeLedger _ledger;
    private readonly ISettlementService _settlementService;
    private readonly MatchmakerEngine _matchmaker;
    private readonly MarketPricingOptions _pricingOptions;
    private readonly OrderBook _orderBook;
    private readonly ILineLossModel _lineLossModel;
    private readonly ICarbonAccountingService _carbonAccountingService;
    private readonly IForecastingEngine _forecastingEngine;
    private readonly IAnomalyDetector _anomalyDetector;
    private readonly MarketMetrics _metrics;

    public MarketOperator(
        ITradeLedger ledger,
        MatchmakerEngine matchmaker,
        MarketPricingOptions? pricingOptions = null,
        ISettlementService? settlementService = null,
        OrderBook? orderBook = null,
        ILineLossModel? lineLossModel = null,
        ICarbonAccountingService? carbonAccountingService = null,
        IForecastingEngine? forecastingEngine = null,
        IAnomalyDetector? anomalyDetector = null,
        MarketMetrics? metrics = null)
    {
        _ledger = ledger;
        _matchmaker = matchmaker;
        _pricingOptions = pricingOptions ?? new MarketPricingOptions();
        _settlementService = settlementService ?? new SettlementService();
        _orderBook = orderBook ?? new OrderBook();
        _lineLossModel = lineLossModel ?? new DistanceLineLossModel();
        _carbonAccountingService = carbonAccountingService ?? new CarbonAccountingService();
        _forecastingEngine = forecastingEngine ?? new ForecastingEngine();
        _anomalyDetector = anomalyDetector ?? new AnomalyDetector();
        _metrics = metrics ?? new MarketMetrics();
    }

    public MarketSnapshot Operate(IReadOnlyCollection<EnergySnapshot> energySnapshots)
    {
        var totalProductionKw = energySnapshots.Sum(snapshot => snapshot.ProducedKw);
        var totalConsumptionKw = energySnapshots.Sum(snapshot => snapshot.ConsumedKw);
        var marketSupplyKw = energySnapshots.Where(snapshot => snapshot.NetKw > 0).Sum(snapshot => snapshot.NetKw);
        var marketDemandKw = energySnapshots.Where(snapshot => snapshot.NetKw < 0).Sum(snapshot => Math.Abs(snapshot.NetKw));
        var marketPrice = CalculateLocationAwarePrice(energySnapshots, marketSupplyKw, marketDemandKw);
        var simulationTime = energySnapshots.Count == 0
            ? DateTimeOffset.UtcNow
            : energySnapshots.Max(snapshot => snapshot.SimulationTime);

        var nodes = energySnapshots.Select(snapshot => new GridNode(
            snapshot.NodeId,
            snapshot.HasSurplus ? NodeRole.Prosumer : NodeRole.Consumer,
            snapshot.Location,
            Math.Max(0, snapshot.NetKw),
            Math.Max(0, -snapshot.NetKw))
        {
            MarketCode = snapshot.MarketCode
        }).ToArray();

        foreach (var snapshot in energySnapshots)
        {
            _settlementService.EnsureAccount(snapshot.NodeId);
        }

        _settlementService.EnsureAccount(MatchmakerEngine.CommunityBatteryId, 0);

        var newOrders = energySnapshots
            .Where(snapshot => Math.Abs(snapshot.NetKw) >= _pricingOptions.MinimumTradeKw)
            .Select(snapshot =>
            {
                var side = snapshot.HasSurplus ? OrderSide.Ask : OrderSide.Bid;
                var localMarket = EnergyMarketCatalog.Resolve(snapshot.MarketCode);
                var pressure = CalculatePressureMultiplier(marketSupplyKw, marketDemandKw);
                var basePrice = side == OrderSide.Ask
                    ? localMarket.ExportUsdPerKwh * _pricingOptions.AskDiscount * (decimal)Math.Max(0.8, pressure * 0.75)
                    : localMarket.RetailUsdPerKwh * _pricingOptions.BidPremium * (decimal)pressure;

                var price = side == OrderSide.Bid
                    ? ApplyPriceElasticity(basePrice, snapshot)
                    : basePrice;
                var tradeableKwh = Math.Abs(snapshot.NetKw) * snapshot.SamplePeriod.TotalHours;
                return new TradeOrder(snapshot.NodeId, side, tradeableKwh, price, simulationTime.UtcDateTime)
                {
                    IsCriticalLoad = snapshot.CriticalLoadKw > 0
                };
            })
            .ToArray();

        _orderBook.AddRange(newOrders, simulationTime.UtcDateTime);
        var hasActiveDemand = newOrders.Any(order => order.Side == OrderSide.Bid);
        var matchResult = _matchmaker.MatchWithBook(nodes, _orderBook.OpenOrders, routeUnmatchedSurplusToBattery: !hasActiveDemand);
        _orderBook.ReplaceOpenOrders(matchResult.UnmatchedOrders, simulationTime.UtcDateTime);
        var trades = ApplyBlackoutMitigation(matchResult.Trades, matchResult.UnmatchedOrders, marketSupplyKw, marketDemandKw)
            .Select(_lineLossModel.Apply)
            .ToArray();

        foreach (var trade in trades)
        {
            _settlementService.Settle(trade);
        }

        var recordedBlocks = trades
            .Select(trade => _ledger.Append(trade, simulationTime.UtcDateTime))
            .ToArray();
        var carbon = _carbonAccountingService.Calculate(trades);
        var forecast = _forecastingEngine.ForecastNextHour(energySnapshots);
        var anomalies = _anomalyDetector.Detect(energySnapshots);

        var status = marketDemandKw > Math.Max(marketSupplyKw, 0.1) * _pricingOptions.BlackoutDemandRatio
            ? GridStatus.BlackoutRisk
            : GridStatus.Nominal;

        return new MarketSnapshot(
            simulationTime,
            totalProductionKw,
            totalConsumptionKw,
            marketSupplyKw,
            marketDemandKw,
            marketPrice,
            status,
            trades,
            recordedBlocks,
            _ledger.Blocks.TakeLast(5).Reverse().ToArray(),
            _orderBook.OpenOrders,
            _settlementService.Accounts,
            forecast,
            anomalies,
            carbon);
    }

    private decimal CalculateLocationAwarePrice(IReadOnlyCollection<EnergySnapshot> snapshots, double marketSupplyKw, double marketDemandKw)
    {
        if (snapshots.Count == 0)
        {
            return _pricingOptions.FloorPricePerKwh;
        }

        var pressure = CalculatePressureMultiplier(marketSupplyKw, marketDemandKw);
        var demandValue = snapshots
            .Where(snapshot => snapshot.HasDemand)
            .Sum(snapshot => EnergyMarketCatalog.Resolve(snapshot.MarketCode).RetailUsdPerKwh * (decimal)Math.Abs(snapshot.NetKw));
        var supplyValue = snapshots
            .Where(snapshot => snapshot.HasSurplus)
            .Sum(snapshot => EnergyMarketCatalog.Resolve(snapshot.MarketCode).ExportUsdPerKwh * (decimal)Math.Abs(snapshot.NetKw));
        var weightedDemand = marketDemandKw > 0 ? demandValue / (decimal)marketDemandKw : 0;
        var weightedSupply = marketSupplyKw > 0 ? supplyValue / (decimal)marketSupplyKw : 0;
        var localReference = weightedDemand > 0 && weightedSupply > 0
            ? (weightedDemand + weightedSupply) / 2
            : weightedDemand > 0 ? weightedDemand : weightedSupply;

        if (localReference <= 0)
        {
            return _pricingOptions.FloorPricePerKwh;
        }

        return Math.Clamp(localReference * (decimal)pressure, _pricingOptions.FloorPricePerKwh, _pricingOptions.CeilingPricePerKwh);
    }

    private double CalculatePressureMultiplier(double totalProductionKw, double totalConsumptionKw)
    {
        if (totalProductionKw <= 0 && totalConsumptionKw <= 0)
        {
            return _pricingOptions.MinimumPriceMultiplier;
        }

        var demandPressure = totalConsumptionKw / Math.Max(totalProductionKw, 0.1);
        return Math.Clamp(demandPressure, _pricingOptions.MinimumPriceMultiplier, _pricingOptions.MaximumPriceMultiplier);
    }

    private decimal ApplyPriceElasticity(decimal bidPrice, EnergySnapshot snapshot)
    {
        if (bidPrice <= _pricingOptions.ElasticityActivationPricePerKwh || snapshot.CriticalLoadKw > 0)
        {
            return bidPrice;
        }

        return bidPrice * (1 - _pricingOptions.PriceElasticityDemandReduction);
    }

    private IReadOnlyList<MatchedTrade> ApplyBlackoutMitigation(
        IReadOnlyList<MatchedTrade> trades,
        IReadOnlyList<TradeOrder> unmatchedOrders,
        double marketSupplyKw,
        double marketDemandKw)
    {
        if (marketDemandKw <= Math.Max(marketSupplyKw, 0.1) * _pricingOptions.BlackoutDemandRatio)
        {
            return trades;
        }

        var rationingRatio = Math.Clamp(marketSupplyKw / Math.Max(marketDemandKw, 0.1), 0.15, 1);
        var criticalConsumers = unmatchedOrders
            .Where(order => order.Side == OrderSide.Bid && order.IsCriticalLoad)
            .Select(order => order.NodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return trades
            .Select(trade => criticalConsumers.Contains(trade.ConsumerId)
                ? trade
                : trade with { KwhAmount = trade.KwhAmount * rationingRatio })
            .ToArray();
    }
}

public sealed record MarketPricingOptions
{
    public decimal BaselinePricePerKwh { get; init; } = 0.16m;

    public decimal FloorPricePerKwh { get; init; } = 0.05m;

    public decimal CeilingPricePerKwh { get; init; } = 0.65m;

    public double MinimumPriceMultiplier { get; init; } = 0.35;

    public double MaximumPriceMultiplier { get; init; } = 4.0;

    public decimal AskDiscount { get; init; } = 0.97m;

    public decimal BidPremium { get; init; } = 1.03m;

    public double MinimumTradeKw { get; init; } = 0.05;

    public double BlackoutDemandRatio { get; init; } = 1.8;

    public decimal ElasticityActivationPricePerKwh { get; init; } = 0.38m;

    public decimal PriceElasticityDemandReduction { get; init; } = 0.12m;
}

public sealed record MarketSnapshot(
    DateTimeOffset SimulationTime,
    double TotalProductionKw,
    double TotalConsumptionKw,
    double MarketSupplyKw,
    double MarketDemandKw,
    decimal MarketPricePerKwh,
    GridStatus Status,
    IReadOnlyList<MatchedTrade> Trades,
    IReadOnlyList<TradeBlock> RecordedBlocks,
    IReadOnlyList<TradeBlock> LastLedgerBlocks,
    IReadOnlyList<TradeOrder> OpenOrders,
    IReadOnlyDictionary<string, SettlementAccount> Accounts,
    IReadOnlyList<ForecastPoint> Forecast,
    IReadOnlyList<AnomalyFlag> Anomalies,
    CarbonAccountingSummary Carbon)
{
    public decimal TotalSettlementAmount => RecordedBlocks.Sum(block => block.SettlementAmount);
}

public enum GridStatus
{
    Nominal,
    BlackoutRisk
}
