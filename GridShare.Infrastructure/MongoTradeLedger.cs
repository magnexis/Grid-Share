using GridShare.Application;
using GridShare.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace GridShare.Infrastructure;

public sealed class MongoTradeLedger : ITradeLedger
{
    private const string GenesisHash = "GENESIS";
    private readonly IMongoCollection<TradeBlockDocument> _collection;

    public MongoTradeLedger(string connectionString, string databaseName = "gridshare", string collectionName = "trade_blocks")
    {
        var client = new MongoClient(connectionString);
        _collection = client.GetDatabase(databaseName).GetCollection<TradeBlockDocument>(collectionName);
        _collection.Indexes.CreateOne(new CreateIndexModel<TradeBlockDocument>(
            Builders<TradeBlockDocument>.IndexKeys.Ascending(block => block.Sequence),
            new CreateIndexOptions { Unique = true }));
    }

    public IReadOnlyList<TradeBlock> Blocks => _collection
        .Find(Builders<TradeBlockDocument>.Filter.Empty)
        .SortBy(block => block.Sequence)
        .ToList()
        .Select(block => block.ToDomain())
        .ToArray();

    public TradeBlock Append(MatchedTrade trade, DateTime timestamp)
    {
        var latest = _collection
            .Find(Builders<TradeBlockDocument>.Filter.Empty)
            .SortByDescending(block => block.Sequence)
            .Limit(1)
            .FirstOrDefault();

        var block = TradeBlock.Create(
            trade.ProsumerId,
            trade.ConsumerId,
            trade.ProsumerMarketCode,
            trade.ConsumerMarketCode,
            trade.KwhAmount,
            trade.DeliveredKwh > 0 ? trade.DeliveredKwh : trade.KwhAmount,
            trade.LineLossKwh,
            trade.PricePerUnit,
            trade.PricePerUnit * (decimal)(trade.DeliveredKwh > 0 ? trade.DeliveredKwh : trade.KwhAmount),
            (trade.DeliveredKwh > 0 ? trade.DeliveredKwh : trade.KwhAmount) * EnergyMarketCatalog.Resolve(trade.ConsumerMarketCode).GridEmissionFactorKgPerKwh,
            timestamp,
            latest?.Hash ?? GenesisHash);

        _collection.InsertOne(TradeBlockDocument.FromDomain(block, (latest?.Sequence ?? 0) + 1));
        return block;
    }

    public bool VerifyIntegrity()
    {
        var blocks = Blocks;

        for (var index = 0; index < blocks.Count; index++)
        {
            var expectedPreviousHash = index == 0 ? GenesisHash : blocks[index - 1].Hash;
            if (blocks[index].PreviousHash != expectedPreviousHash || !blocks[index].HasValidHash())
            {
                return false;
            }
        }

        return true;
    }

    private sealed class TradeBlockDocument
    {
        [BsonId]
        public ObjectId Id { get; init; }

        public long Sequence { get; init; }

        public Guid TransactionId { get; init; }

        public string ProsumerId { get; init; } = "";

        public string ConsumerId { get; init; } = "";

        public string ProsumerMarketCode { get; init; } = "US-AVG";

        public string ConsumerMarketCode { get; init; } = "US-AVG";

        public double KwhAmount { get; init; }

        public double DeliveredKwh { get; init; }

        public double LineLossKwh { get; init; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal PricePerUnit { get; init; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal SettlementAmount { get; init; }

        public double CarbonOffsetKg { get; init; }

        public DateTime Timestamp { get; init; }

        public string PreviousHash { get; init; } = "";

        public string Hash { get; init; } = "";

        public static TradeBlockDocument FromDomain(TradeBlock block, long sequence)
        {
            return new TradeBlockDocument
            {
                Sequence = sequence,
                TransactionId = block.TransactionId,
                ProsumerId = block.ProsumerId,
                ConsumerId = block.ConsumerId,
                ProsumerMarketCode = block.ProsumerMarketCode,
                ConsumerMarketCode = block.ConsumerMarketCode,
                KwhAmount = block.KwhAmount,
                DeliveredKwh = block.DeliveredKwh,
                LineLossKwh = block.LineLossKwh,
                PricePerUnit = block.PricePerUnit,
                SettlementAmount = block.SettlementAmount,
                CarbonOffsetKg = block.CarbonOffsetKg,
                Timestamp = block.Timestamp,
                PreviousHash = block.PreviousHash,
                Hash = block.Hash
            };
        }

        public TradeBlock ToDomain()
        {
            return new TradeBlock(TransactionId, ProsumerId, ConsumerId, ProsumerMarketCode, ConsumerMarketCode, KwhAmount, DeliveredKwh, LineLossKwh, PricePerUnit, SettlementAmount, CarbonOffsetKg, Timestamp, PreviousHash, Hash);
        }
    }
}
