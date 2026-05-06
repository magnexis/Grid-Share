using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GridShare.Domain;

public sealed record TradeBlock(
    Guid TransactionId,
    string ProsumerId,
    string ConsumerId,
    string ProsumerMarketCode,
    string ConsumerMarketCode,
    double KwhAmount,
    double DeliveredKwh,
    double LineLossKwh,
    decimal PricePerUnit,
    decimal SettlementAmount,
    double CarbonOffsetKg,
    DateTime Timestamp,
    string PreviousHash,
    string Hash)
{
    public static TradeBlock Create(
        string prosumerId,
        string consumerId,
        string prosumerMarketCode,
        string consumerMarketCode,
        double kwhAmount,
        double deliveredKwh,
        double lineLossKwh,
        decimal pricePerUnit,
        decimal settlementAmount,
        double carbonOffsetKg,
        DateTime timestamp,
        string previousHash)
    {
        var transactionId = Guid.NewGuid();
        var hash = ComputeHash(
            transactionId,
            prosumerId,
            consumerId,
            prosumerMarketCode,
            consumerMarketCode,
            kwhAmount,
            deliveredKwh,
            lineLossKwh,
            pricePerUnit,
            settlementAmount,
            carbonOffsetKg,
            timestamp,
            previousHash);

        return new TradeBlock(
            transactionId,
            prosumerId,
            consumerId,
            prosumerMarketCode,
            consumerMarketCode,
            kwhAmount,
            deliveredKwh,
            lineLossKwh,
            pricePerUnit,
            settlementAmount,
            carbonOffsetKg,
            timestamp,
            previousHash,
            hash);
    }

    public bool HasValidHash()
    {
        return Hash == ComputeHash(
            TransactionId,
            ProsumerId,
            ConsumerId,
            ProsumerMarketCode,
            ConsumerMarketCode,
            KwhAmount,
            DeliveredKwh,
            LineLossKwh,
            PricePerUnit,
            SettlementAmount,
            CarbonOffsetKg,
            Timestamp,
            PreviousHash);
    }

    private static string ComputeHash(
        Guid transactionId,
        string prosumerId,
        string consumerId,
        string prosumerMarketCode,
        string consumerMarketCode,
        double kwhAmount,
        double deliveredKwh,
        double lineLossKwh,
        decimal pricePerUnit,
        decimal settlementAmount,
        double carbonOffsetKg,
        DateTime timestamp,
        string previousHash)
    {
        var payload = string.Join(
            "|",
            transactionId,
            prosumerId,
            consumerId,
            prosumerMarketCode,
            consumerMarketCode,
            kwhAmount.ToString("F6", CultureInfo.InvariantCulture),
            deliveredKwh.ToString("F6", CultureInfo.InvariantCulture),
            lineLossKwh.ToString("F6", CultureInfo.InvariantCulture),
            pricePerUnit.ToString("F6", CultureInfo.InvariantCulture),
            settlementAmount.ToString("F6", CultureInfo.InvariantCulture),
            carbonOffsetKg.ToString("F6", CultureInfo.InvariantCulture),
            timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            previousHash);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}
