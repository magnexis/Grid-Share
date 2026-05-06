using GridShare.Application;
using GridShare.Domain;

namespace GridShare.Api;

public sealed class GridRuntimeState
{
    public MarketSnapshot? Current { get; private set; }

    public IReadOnlyList<EnergySnapshot> Nodes { get; private set; } = [];

    public IReadOnlyList<TradeFlow> Flows { get; private set; } = [];

    public void Publish(MarketSnapshot snapshot, IReadOnlyList<EnergySnapshot> nodes, IReadOnlyList<TradeFlow> flows)
    {
        Current = snapshot;
        Nodes = nodes;
        Flows = flows;
    }
}

public sealed record TradeFlow(
    string From,
    string To,
    double DeliveredKwh,
    double LineLossKwh,
    decimal PricePerUnit);
