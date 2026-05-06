using GridShare.Domain;

namespace GridShare.Application;

public sealed class SmartMeterGateway : ISmartMeterGateway
{
    public TradeOrder ConvertReading(SmartMeterReading reading, decimal reservationPrice)
    {
        var netKwh = reading.ProductionKwh - reading.ConsumptionKwh;

        return netKwh >= 0
            ? new TradeOrder(reading.NodeId, OrderSide.Ask, netKwh, reservationPrice, reading.Timestamp)
            : new TradeOrder(reading.NodeId, OrderSide.Bid, Math.Abs(netKwh), reservationPrice, reading.Timestamp);
    }
}
