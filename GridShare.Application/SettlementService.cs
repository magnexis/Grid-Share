using GridShare.Domain;

namespace GridShare.Application;

public sealed class SettlementService : ISettlementService
{
    private readonly Dictionary<string, SettlementAccount> _accounts = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SettlementAccount> Accounts => new Dictionary<string, SettlementAccount>(_accounts);

    public void EnsureAccount(string nodeId, decimal startingBalance = 25m)
    {
        if (_accounts.ContainsKey(nodeId))
        {
            return;
        }

        _accounts[nodeId] = new SettlementAccount(nodeId, startingBalance, 0, 0, 0);
    }

    public void Settle(MatchedTrade trade)
    {
        EnsureAccount(trade.ProsumerId);
        EnsureAccount(trade.ConsumerId);

        var deliveredKwh = trade.DeliveredKwh > 0 ? trade.DeliveredKwh : trade.KwhAmount;
        var amount = trade.PricePerUnit * (decimal)deliveredKwh;
        var seller = _accounts[trade.ProsumerId];
        var buyer = _accounts[trade.ConsumerId];

        if (trade.ConsumerId == MatchmakerEngine.CommunityBatteryId)
        {
            _accounts[trade.ProsumerId] = seller with
            {
                BatteryCreditsKwh = seller.BatteryCreditsKwh + deliveredKwh
            };
            _accounts[trade.ConsumerId] = buyer with
            {
                BatteryCreditsKwh = buyer.BatteryCreditsKwh + deliveredKwh
            };
            return;
        }

        _accounts[trade.ProsumerId] = seller with
        {
            WalletBalance = seller.WalletBalance + amount,
            LifetimeEarnings = seller.LifetimeEarnings + amount
        };

        _accounts[trade.ConsumerId] = buyer with
        {
            WalletBalance = buyer.WalletBalance - amount,
            UnpaidObligations = buyer.WalletBalance >= amount ? buyer.UnpaidObligations : buyer.UnpaidObligations + (amount - buyer.WalletBalance)
        };
    }
}
