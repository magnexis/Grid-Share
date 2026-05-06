using GridShare.Domain;

namespace GridShare.Application;

public sealed class InMemoryTradeLedger : ITradeLedger
{
    private const string GenesisHash = "GENESIS";
    private readonly List<TradeBlock> _blocks = [];
    private readonly object _syncRoot = new();

    public IReadOnlyList<TradeBlock> Blocks
    {
        get
        {
            lock (_syncRoot)
            {
                return _blocks.ToArray();
            }
        }
    }

    public TradeBlock Append(MatchedTrade trade, DateTime timestamp)
    {
        if (trade.KwhAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trade), "Trade amount must be greater than zero.");
        }

        lock (_syncRoot)
        {
            var previousHash = _blocks.LastOrDefault()?.Hash ?? GenesisHash;
            var block = TradeBlock.Create(
                trade.ProsumerId,
                trade.ConsumerId,
                trade.ProsumerMarketCode,
                trade.ConsumerMarketCode,
                trade.KwhAmount,
                trade.DeliveredKwh > 0 ? trade.DeliveredKwh : trade.KwhAmount,
                trade.LineLossKwh,
                trade.PricePerUnit,
                (trade.PricePerUnit * (decimal)(trade.DeliveredKwh > 0 ? trade.DeliveredKwh : trade.KwhAmount)),
                (trade.DeliveredKwh > 0 ? trade.DeliveredKwh : trade.KwhAmount) * EnergyMarketCatalog.Resolve(trade.ConsumerMarketCode).GridEmissionFactorKgPerKwh,
                timestamp,
                previousHash);

            _blocks.Add(block);
            return block;
        }
    }

    public bool VerifyIntegrity()
    {
        lock (_syncRoot)
        {
            for (var index = 0; index < _blocks.Count; index++)
            {
                var expectedPreviousHash = index == 0 ? GenesisHash : _blocks[index - 1].Hash;
                if (_blocks[index].PreviousHash != expectedPreviousHash || !_blocks[index].HasValidHash())
                {
                    return false;
                }
            }

            return true;
        }
    }
}
