using GridShare.Domain;

namespace GridShare.Application;

public sealed class OrderBook
{
    private readonly List<TradeOrder> _orders = [];
    private readonly TimeSpan _timeToLive;

    public OrderBook(TimeSpan? timeToLive = null)
    {
        _timeToLive = timeToLive ?? TimeSpan.FromHours(2);
    }

    public IReadOnlyList<TradeOrder> OpenOrders => _orders.ToArray();

    public void AddRange(IEnumerable<TradeOrder> orders, DateTime now)
    {
        _orders.RemoveAll(order => now - order.Timestamp > _timeToLive || order.KwhAmount <= 0);
        _orders.AddRange(orders);
    }

    public void ReplaceOpenOrders(IEnumerable<TradeOrder> orders, DateTime now)
    {
        _orders.Clear();
        _orders.AddRange(orders.Where(order => now - order.Timestamp <= _timeToLive && order.KwhAmount > 0));
    }
}

public sealed record MatchResult(
    IReadOnlyList<MatchedTrade> Trades,
    IReadOnlyList<TradeOrder> UnmatchedOrders);
