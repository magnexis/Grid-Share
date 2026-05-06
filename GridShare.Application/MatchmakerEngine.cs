using GridShare.Domain;

namespace GridShare.Application;

public sealed class MatchmakerEngine
{
    public const string CommunityBatteryId = "COMMUNITY_BATTERY";

    public IReadOnlyList<MatchedTrade> Match(
        IReadOnlyCollection<GridNode> nodes,
        IReadOnlyCollection<TradeOrder> orders)
    {
        return MatchWithBook(nodes, orders, routeUnmatchedSurplusToBattery: true).Trades;
    }

    public MatchResult MatchWithBook(
        IReadOnlyCollection<GridNode> nodes,
        IReadOnlyCollection<TradeOrder> orders,
        bool routeUnmatchedSurplusToBattery)
    {
        var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var asks = orders
            .Where(order => order.Side == OrderSide.Ask && order.KwhAmount > 0)
            .OrderBy(order => order.PricePerUnit)
            .ThenBy(order => order.Timestamp)
            .ToList();
        var bids = orders
            .Where(order => order.Side == OrderSide.Bid && order.KwhAmount > 0)
            .OrderByDescending(order => order.PricePerUnit)
            .ThenBy(order => order.Timestamp)
            .ToList();

        var matches = new List<MatchedTrade>();
        var unmatched = new List<TradeOrder>();

        foreach (var ask in asks)
        {
            var remainingAsk = ask.KwhAmount;

            while (remainingAsk > 0)
            {
                var bid = bids
                    .Where(candidate => candidate.KwhAmount > 0 && candidate.PricePerUnit >= ask.PricePerUnit)
                    .OrderBy(candidate => DistanceBetween(nodesById, ask.NodeId, candidate.NodeId))
                    .ThenByDescending(candidate => candidate.PricePerUnit)
                    .ThenBy(candidate => candidate.Timestamp)
                    .FirstOrDefault();

                if (bid is null)
                {
                    if (routeUnmatchedSurplusToBattery)
                    {
                        matches.Add(new MatchedTrade(ask.NodeId, CommunityBatteryId, remainingAsk, ask.PricePerUnit, 0)
                        {
                            ProsumerMarketCode = MarketCodeFor(nodesById, ask.NodeId),
                            ConsumerMarketCode = MarketCodeFor(nodesById, ask.NodeId)
                        });
                    }
                    else
                    {
                        unmatched.Add(ask with { KwhAmount = remainingAsk });
                    }

                    break;
                }

                var tradedKwh = Math.Min(remainingAsk, bid.KwhAmount);
                var equilibriumPrice = (ask.PricePerUnit + bid.PricePerUnit) / 2;
                matches.Add(new MatchedTrade(
                    ask.NodeId,
                    bid.NodeId,
                    tradedKwh,
                    equilibriumPrice,
                    DistanceBetween(nodesById, ask.NodeId, bid.NodeId))
                {
                    ProsumerMarketCode = MarketCodeFor(nodesById, ask.NodeId),
                    ConsumerMarketCode = MarketCodeFor(nodesById, bid.NodeId)
                });

                remainingAsk -= tradedKwh;
                var bidIndex = bids.IndexOf(bid);
                bids[bidIndex] = bid with { KwhAmount = bid.KwhAmount - tradedKwh };
            }
        }

        unmatched.AddRange(bids.Where(bid => bid.KwhAmount > 0));

        return new MatchResult(matches, unmatched);
    }

    private static double DistanceBetween(IReadOnlyDictionary<string, GridNode> nodesById, string firstNodeId, string secondNodeId)
    {
        if (!nodesById.TryGetValue(firstNodeId, out var firstNode) || !nodesById.TryGetValue(secondNodeId, out var secondNode))
        {
            return double.MaxValue;
        }

        return firstNode.Location.DistanceTo(secondNode.Location);
    }

    private static string MarketCodeFor(IReadOnlyDictionary<string, GridNode> nodesById, string nodeId)
    {
        return nodesById.TryGetValue(nodeId, out var node) ? node.MarketCode : "US-AVG";
    }
}
