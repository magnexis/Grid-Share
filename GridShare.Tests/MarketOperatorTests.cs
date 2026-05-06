using GridShare.Application;
using GridShare.Domain;
using GridShare.Simulation;

namespace GridShare.Tests;

public sealed class MarketOperatorTests
{
    [Fact]
    public void LedgerRejectsTamperedHashChain()
    {
        var ledger = new InMemoryTradeLedger();
        ledger.Append(new MatchedTrade("A", "B", 1, 0.12m, 1, 0.99, 0.01), DateTime.UtcNow);

        Assert.True(ledger.VerifyIntegrity());
        var block = ledger.Blocks[0] with { PricePerUnit = 99m };

        Assert.False(block.HasValidHash());
    }

    [Fact]
    public void PricingDropsWhenMarketSupplyDominates()
    {
        var market = new MarketOperator(new InMemoryTradeLedger(), new MatchmakerEngine());
        var snapshots = new[]
        {
            Snapshot("Solar_A", produced: 10, consumed: 1),
            Snapshot("Solar_B", produced: 9, consumed: 1),
            Snapshot("Load_A", produced: 0, consumed: 2)
        };

        var result = market.Operate(snapshots);

        Assert.True(result.MarketPricePerKwh < 0.16m);
    }

    [Fact]
    public void PricingRisesAndFlagsBlackoutWhenDemandDominates()
    {
        var market = new MarketOperator(new InMemoryTradeLedger(), new MatchmakerEngine());
        var snapshots = new[]
        {
            Snapshot("Load_A", produced: 0, consumed: 8),
            Snapshot("Load_B", produced: 0, consumed: 7),
            Snapshot("TinySolar", produced: 1, consumed: 0.5)
        };

        var result = market.Operate(snapshots);

        Assert.Equal(GridStatus.BlackoutRisk, result.Status);
        Assert.True(result.MarketPricePerKwh > 0.16m);
    }

    [Fact]
    public void OrderBookKeepsUnmatchedDemandAcrossTicks()
    {
        var market = new MarketOperator(new InMemoryTradeLedger(), new MatchmakerEngine());

        var first = market.Operate(new[] { Snapshot("Buyer", produced: 0, consumed: 4) });
        var second = market.Operate(new[] { Snapshot("Seller", produced: 5, consumed: 0) });

        Assert.NotEmpty(first.OpenOrders);
        Assert.Contains(second.Trades, trade => trade.ConsumerId == "Buyer");
    }

    [Fact]
    public void LineLossReducesDeliveredEnergy()
    {
        var model = new DistanceLineLossModel(lossRatePerGridUnit: 0.01);
        var trade = model.Apply(new MatchedTrade("A", "B", 10, 0.1m, 5));

        Assert.True(trade.DeliveredKwh < 10);
        Assert.True(trade.LineLossKwh > 0);
    }

    [Fact]
    public void DiurnalCycleProducesMoreSolarNearNoonThanNight()
    {
        var noon = DiurnalCycle.SolarProductionKw(new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero), 8);
        var night = DiurnalCycle.SolarProductionKw(new DateTimeOffset(2026, 5, 5, 1, 0, 0, TimeSpan.Zero), 8);

        Assert.True(noon > night);
    }

    [Fact]
    public void LocalEnergyMarketsInfluenceBidPrices()
    {
        var market = new MarketOperator(new InMemoryTradeLedger(), new MatchmakerEngine());
        var result = market.Operate(new[]
        {
            Snapshot("GermanBuyer", produced: 0, consumed: 4) with { MarketCode = "DE-DE" },
            Snapshot("UsSeller", produced: 8, consumed: 0) with { MarketCode = "US-AVG" }
        });

        Assert.Contains(result.Trades, trade => trade.ConsumerMarketCode == "DE-DE");
        Assert.True(result.Trades[0].PricePerUnit > EnergyMarketCatalog.Resolve("US-AVG").ExportUsdPerKwh);
    }

    private static EnergySnapshot Snapshot(string nodeId, double produced, double consumed)
    {
        return new EnergySnapshot(
            nodeId,
            new GridLocation(0, 0),
            produced,
            consumed,
            produced - consumed,
            0,
            10,
            TimeSpan.FromMinutes(15),
            DateTimeOffset.UtcNow);
    }
}
