using GridShare.Domain;

namespace GridShare.Application;

public interface ITradeLedger
{
    IReadOnlyList<TradeBlock> Blocks { get; }

    TradeBlock Append(MatchedTrade trade, DateTime timestamp);

    bool VerifyIntegrity();
}

public interface ISettlementService
{
    IReadOnlyDictionary<string, SettlementAccount> Accounts { get; }

    void EnsureAccount(string nodeId, decimal startingBalance = 25m);

    void Settle(MatchedTrade trade);
}

public interface IEnergySnapshotSource
{
    IAsyncEnumerable<IReadOnlyCollection<EnergySnapshot>> ReadTicksAsync(CancellationToken cancellationToken);
}

public interface ISmartMeterGateway
{
    TradeOrder ConvertReading(SmartMeterReading reading, decimal reservationPrice);
}

public sealed record SmartMeterReading(
    string NodeId,
    double ProductionKwh,
    double ConsumptionKwh,
    DateTime Timestamp);
