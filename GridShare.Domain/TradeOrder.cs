namespace GridShare.Domain;

public enum OrderSide
{
    Ask,
    Bid
}

public sealed record TradeOrder(
    string NodeId,
    OrderSide Side,
    double KwhAmount,
    decimal PricePerUnit,
    DateTime Timestamp)
{
    public Guid OrderId { get; init; } = Guid.NewGuid();

    public bool IsCriticalLoad { get; init; }
}

public sealed record MatchedTrade(
    string ProsumerId,
    string ConsumerId,
    double KwhAmount,
    decimal PricePerUnit,
    double Distance,
    double DeliveredKwh = 0,
    double LineLossKwh = 0)
{
    public string ProsumerMarketCode { get; init; } = "US-AVG";

    public string ConsumerMarketCode { get; init; } = "US-AVG";
}
