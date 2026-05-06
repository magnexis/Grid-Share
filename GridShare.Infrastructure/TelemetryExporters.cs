using System.Globalization;
using System.Text.Json;
using GridShare.Application;
using GridShare.Domain;

namespace GridShare.Infrastructure;

public interface ITelemetryExporter
{
    Task ExportAsync(MarketSnapshot marketSnapshot, IReadOnlyCollection<EnergySnapshot> energySnapshots, CancellationToken cancellationToken = default);
}

public sealed class JsonlTelemetryExporter : ITelemetryExporter
{
    private readonly string _path;
    private readonly string _runId = $"run-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    private long _sequence;

    public JsonlTelemetryExporter(string path)
    {
        _path = path;
    }

    public async Task ExportAsync(MarketSnapshot marketSnapshot, IReadOnlyCollection<EnergySnapshot> energySnapshots, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path)) ?? ".");
        var payload = JsonSerializer.Serialize(new
        {
            schemaVersion = "2.0",
            runId = _runId,
            sequence = ++_sequence,
            generatedAt = DateTimeOffset.UtcNow,
            aggregate = TelemetryMath.Aggregate(marketSnapshot),
            marketSnapshot,
            energySnapshots,
            trades = marketSnapshot.RecordedBlocks,
            openOrders = marketSnapshot.OpenOrders,
            accounts = marketSnapshot.Accounts.Values,
            anomalies = marketSnapshot.Anomalies,
            forecast = marketSnapshot.Forecast
        });
        await File.AppendAllTextAsync(_path, payload + Environment.NewLine, cancellationToken);
    }
}

public sealed class CsvTelemetryExporter : ITelemetryExporter
{
    private const string NodeHeader = "run_id,sequence,time,node_id,market_code,market_name,country,currency,local_retail_price_per_kwh,local_solar_export_price_per_kwh,produced_kw,consumed_kw,grid_exchange_kw,sample_minutes,battery_kwh,battery_capacity_kwh,battery_state_of_charge,critical_load_kw,is_stale,has_surplus,has_demand,market_price_usd_per_kwh,market_price_local_per_kwh,status,total_production_kw,total_consumption_kw,market_supply_kw,market_demand_kw,demand_pressure,open_orders,trade_count,delivered_kwh,line_loss_kwh,settlement_usd,carbon_offset_kg,forecast_next_hour_production_kw,forecast_next_hour_consumption_kw,anomaly_count";
    private const string MarketHeader = "run_id,sequence,time,status,node_count,stale_nodes,total_production_kw,total_consumption_kw,market_supply_kw,market_demand_kw,demand_pressure,market_price_usd_per_kwh,open_orders,trade_count,delivered_kwh,line_loss_kwh,loss_ratio,settlement_usd,carbon_offset_kg,unpaid_obligations_usd,battery_credits_kwh,forecast_next_hour_production_kw,forecast_next_hour_consumption_kw,anomaly_count";
    private const string TradeHeader = "run_id,sequence,time,transaction_id,prosumer_id,consumer_id,prosumer_market,consumer_market,kwh_amount,delivered_kwh,line_loss_kwh,price_usd_per_kwh,settlement_usd,carbon_offset_kg,previous_hash,hash";
    private const string AccountHeader = "run_id,sequence,time,node_id,wallet_balance_usd,battery_credits_kwh,lifetime_earnings_usd,unpaid_obligations_usd";
    private const string AnomalyHeader = "run_id,sequence,time,node_id,code,message,severity";
    private readonly string _path;
    private readonly string _marketPath;
    private readonly string _tradesPath;
    private readonly string _accountsPath;
    private readonly string _anomaliesPath;
    private readonly string _runId = $"run-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    private long _sequence;

    public CsvTelemetryExporter(string path)
    {
        _path = path;
        var directory = Path.GetDirectoryName(path) ?? "";
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        _marketPath = Path.Combine(directory, $"{fileName}.market{extension}");
        _tradesPath = Path.Combine(directory, $"{fileName}.trades{extension}");
        _accountsPath = Path.Combine(directory, $"{fileName}.accounts{extension}");
        _anomaliesPath = Path.Combine(directory, $"{fileName}.anomalies{extension}");
    }

    public async Task ExportAsync(MarketSnapshot marketSnapshot, IReadOnlyCollection<EnergySnapshot> energySnapshots, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path)) ?? ".");
        var sequence = ++_sequence;
        var aggregate = TelemetryMath.Aggregate(marketSnapshot);

        await WriteNodeRowsAsync(sequence, aggregate, marketSnapshot, energySnapshots, cancellationToken);
        await WriteMarketRowAsync(sequence, aggregate, marketSnapshot, energySnapshots, cancellationToken);
        await WriteTradeRowsAsync(sequence, marketSnapshot, cancellationToken);
        await WriteAccountRowsAsync(sequence, marketSnapshot, cancellationToken);
        await WriteAnomalyRowsAsync(sequence, marketSnapshot, cancellationToken);
    }

    private async Task WriteNodeRowsAsync(
        long sequence,
        TelemetryAggregate aggregate,
        MarketSnapshot marketSnapshot,
        IReadOnlyCollection<EnergySnapshot> energySnapshots,
        CancellationToken cancellationToken)
    {
        EnsureHeaderCompatible(_path, NodeHeader);
        var exists = File.Exists(_path);
        await using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);

        if (!exists)
        {
            await writer.WriteLineAsync(NodeHeader);
        }

        foreach (var snapshot in energySnapshots)
        {
            var localMarket = EnergyMarketCatalog.Resolve(snapshot.MarketCode);
            var localCurrency = CurrencyCatalog.Resolve(localMarket.CurrencyCode);
            var line = Csv.Join(
                _runId,
                sequence,
                marketSnapshot.SimulationTime.ToString("O", CultureInfo.InvariantCulture),
                snapshot.NodeId,
                localMarket.Code,
                localMarket.Name,
                localMarket.CountryCode,
                localMarket.CurrencyCode,
                localMarket.ResidentialRetailPricePerKwh.ToString("F6", CultureInfo.InvariantCulture),
                localMarket.SolarExportPricePerKwh.ToString("F6", CultureInfo.InvariantCulture),
                snapshot.ProducedKw.ToString("F3", CultureInfo.InvariantCulture),
                snapshot.ConsumedKw.ToString("F3", CultureInfo.InvariantCulture),
                snapshot.GridExchangeKw.ToString("F3", CultureInfo.InvariantCulture),
                snapshot.SamplePeriod.TotalMinutes.ToString("F0", CultureInfo.InvariantCulture),
                snapshot.BatteryChargeKwh.ToString("F3", CultureInfo.InvariantCulture),
                snapshot.BatteryCapacityKwh.ToString("F3", CultureInfo.InvariantCulture),
                TelemetryMath.BatteryStateOfCharge(snapshot).ToString("F4", CultureInfo.InvariantCulture),
                snapshot.CriticalLoadKw.ToString("F3", CultureInfo.InvariantCulture),
                snapshot.IsStale,
                snapshot.HasSurplus,
                snapshot.HasDemand,
                marketSnapshot.MarketPricePerKwh.ToString("F6", CultureInfo.InvariantCulture),
                localCurrency.FromUsd(marketSnapshot.MarketPricePerKwh).ToString("F6", CultureInfo.InvariantCulture),
                marketSnapshot.Status,
                marketSnapshot.TotalProductionKw.ToString("F3", CultureInfo.InvariantCulture),
                marketSnapshot.TotalConsumptionKw.ToString("F3", CultureInfo.InvariantCulture),
                marketSnapshot.MarketSupplyKw.ToString("F3", CultureInfo.InvariantCulture),
                marketSnapshot.MarketDemandKw.ToString("F3", CultureInfo.InvariantCulture),
                aggregate.DemandPressure.ToString("F4", CultureInfo.InvariantCulture),
                marketSnapshot.OpenOrders.Count,
                marketSnapshot.RecordedBlocks.Count,
                aggregate.DeliveredKwh.ToString("F6", CultureInfo.InvariantCulture),
                aggregate.LineLossKwh.ToString("F6", CultureInfo.InvariantCulture),
                aggregate.SettlementUsd.ToString("F6", CultureInfo.InvariantCulture),
                marketSnapshot.Carbon.CarbonOffsetKg.ToString("F6", CultureInfo.InvariantCulture),
                aggregate.ForecastNextHourProductionKw.ToString("F3", CultureInfo.InvariantCulture),
                aggregate.ForecastNextHourConsumptionKw.ToString("F3", CultureInfo.InvariantCulture),
                marketSnapshot.Anomalies.Count);

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }

    private async Task WriteMarketRowAsync(
        long sequence,
        TelemetryAggregate aggregate,
        MarketSnapshot marketSnapshot,
        IReadOnlyCollection<EnergySnapshot> energySnapshots,
        CancellationToken cancellationToken)
    {
        EnsureHeaderCompatible(_marketPath, MarketHeader);
        var exists = File.Exists(_marketPath);
        await using var stream = new FileStream(_marketPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);

        if (!exists)
        {
            await writer.WriteLineAsync(MarketHeader);
        }

        var line = Csv.Join(
            _runId,
            sequence,
            marketSnapshot.SimulationTime.ToString("O", CultureInfo.InvariantCulture),
            marketSnapshot.Status,
            energySnapshots.Count,
            energySnapshots.Count(snapshot => snapshot.IsStale),
            marketSnapshot.TotalProductionKw.ToString("F3", CultureInfo.InvariantCulture),
            marketSnapshot.TotalConsumptionKw.ToString("F3", CultureInfo.InvariantCulture),
            marketSnapshot.MarketSupplyKw.ToString("F3", CultureInfo.InvariantCulture),
            marketSnapshot.MarketDemandKw.ToString("F3", CultureInfo.InvariantCulture),
            aggregate.DemandPressure.ToString("F4", CultureInfo.InvariantCulture),
            marketSnapshot.MarketPricePerKwh.ToString("F6", CultureInfo.InvariantCulture),
            marketSnapshot.OpenOrders.Count,
            marketSnapshot.RecordedBlocks.Count,
            aggregate.DeliveredKwh.ToString("F6", CultureInfo.InvariantCulture),
            aggregate.LineLossKwh.ToString("F6", CultureInfo.InvariantCulture),
            aggregate.LineLossRatio.ToString("F6", CultureInfo.InvariantCulture),
            aggregate.SettlementUsd.ToString("F6", CultureInfo.InvariantCulture),
            marketSnapshot.Carbon.CarbonOffsetKg.ToString("F6", CultureInfo.InvariantCulture),
            aggregate.UnpaidObligationsUsd.ToString("F6", CultureInfo.InvariantCulture),
            aggregate.BatteryCreditsKwh.ToString("F6", CultureInfo.InvariantCulture),
            aggregate.ForecastNextHourProductionKw.ToString("F3", CultureInfo.InvariantCulture),
            aggregate.ForecastNextHourConsumptionKw.ToString("F3", CultureInfo.InvariantCulture),
            marketSnapshot.Anomalies.Count);

        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
    }

    private async Task WriteTradeRowsAsync(long sequence, MarketSnapshot marketSnapshot, CancellationToken cancellationToken)
    {
        EnsureHeaderCompatible(_tradesPath, TradeHeader);
        var exists = File.Exists(_tradesPath);
        await using var stream = new FileStream(_tradesPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);

        if (!exists)
        {
            await writer.WriteLineAsync(TradeHeader);
        }

        foreach (var block in marketSnapshot.RecordedBlocks)
        {
            var line = Csv.Join(
                _runId,
                sequence,
                marketSnapshot.SimulationTime.ToString("O", CultureInfo.InvariantCulture),
                block.TransactionId,
                block.ProsumerId,
                block.ConsumerId,
                block.ProsumerMarketCode,
                block.ConsumerMarketCode,
                block.KwhAmount.ToString("F6", CultureInfo.InvariantCulture),
                block.DeliveredKwh.ToString("F6", CultureInfo.InvariantCulture),
                block.LineLossKwh.ToString("F6", CultureInfo.InvariantCulture),
                block.PricePerUnit.ToString("F6", CultureInfo.InvariantCulture),
                block.SettlementAmount.ToString("F6", CultureInfo.InvariantCulture),
                block.CarbonOffsetKg.ToString("F6", CultureInfo.InvariantCulture),
                block.PreviousHash,
                block.Hash);

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }

    private async Task WriteAccountRowsAsync(long sequence, MarketSnapshot marketSnapshot, CancellationToken cancellationToken)
    {
        EnsureHeaderCompatible(_accountsPath, AccountHeader);
        var exists = File.Exists(_accountsPath);
        await using var stream = new FileStream(_accountsPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);

        if (!exists)
        {
            await writer.WriteLineAsync(AccountHeader);
        }

        foreach (var account in marketSnapshot.Accounts.Values.OrderBy(account => account.NodeId, StringComparer.OrdinalIgnoreCase))
        {
            var line = Csv.Join(
                _runId,
                sequence,
                marketSnapshot.SimulationTime.ToString("O", CultureInfo.InvariantCulture),
                account.NodeId,
                account.WalletBalance.ToString("F6", CultureInfo.InvariantCulture),
                account.BatteryCreditsKwh.ToString("F6", CultureInfo.InvariantCulture),
                account.LifetimeEarnings.ToString("F6", CultureInfo.InvariantCulture),
                account.UnpaidObligations.ToString("F6", CultureInfo.InvariantCulture));

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }

    private async Task WriteAnomalyRowsAsync(long sequence, MarketSnapshot marketSnapshot, CancellationToken cancellationToken)
    {
        EnsureHeaderCompatible(_anomaliesPath, AnomalyHeader);
        var exists = File.Exists(_anomaliesPath);
        await using var stream = new FileStream(_anomaliesPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);

        if (!exists)
        {
            await writer.WriteLineAsync(AnomalyHeader);
        }

        foreach (var anomaly in marketSnapshot.Anomalies)
        {
            var line = Csv.Join(
                _runId,
                sequence,
                anomaly.Timestamp.ToString("O", CultureInfo.InvariantCulture),
                anomaly.NodeId,
                anomaly.Code,
                anomaly.Message,
                anomaly.Severity.ToString("F4", CultureInfo.InvariantCulture));

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }

    private static void EnsureHeaderCompatible(string path, string expectedHeader)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var firstLine = File.ReadLines(path).FirstOrDefault();
        if (string.Equals(firstLine, expectedHeader, StringComparison.Ordinal))
        {
            return;
        }

        var directory = Path.GetDirectoryName(path) ?? "";
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var archivedPath = Path.Combine(directory, $"{fileName}.legacy-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}{extension}");
        File.Move(path, archivedPath, overwrite: true);
    }
}

public sealed record TelemetryAggregate(
    double DemandPressure,
    double DeliveredKwh,
    double LineLossKwh,
    double LineLossRatio,
    decimal SettlementUsd,
    decimal UnpaidObligationsUsd,
    double BatteryCreditsKwh,
    double ForecastNextHourProductionKw,
    double ForecastNextHourConsumptionKw);

internal static class TelemetryMath
{
    public static TelemetryAggregate Aggregate(MarketSnapshot marketSnapshot)
    {
        var delivered = marketSnapshot.RecordedBlocks.Sum(block => block.DeliveredKwh);
        var lineLoss = marketSnapshot.RecordedBlocks.Sum(block => block.LineLossKwh);
        var settlement = marketSnapshot.RecordedBlocks.Sum(block => block.SettlementAmount);
        var obligations = marketSnapshot.Accounts.Values.Sum(account => account.UnpaidObligations);
        var batteryCredits = marketSnapshot.Accounts.Values.Sum(account => account.BatteryCreditsKwh);

        return new TelemetryAggregate(
            DemandPressure: marketSnapshot.MarketDemandKw / Math.Max(marketSnapshot.MarketSupplyKw, 0.1),
            DeliveredKwh: delivered,
            LineLossKwh: lineLoss,
            LineLossRatio: delivered + lineLoss <= 0 ? 0 : lineLoss / (delivered + lineLoss),
            SettlementUsd: settlement,
            UnpaidObligationsUsd: obligations,
            BatteryCreditsKwh: batteryCredits,
            ForecastNextHourProductionKw: marketSnapshot.Forecast.Sum(point => point.ExpectedProductionKw) / Math.Max(1, marketSnapshot.Forecast.Count),
            ForecastNextHourConsumptionKw: marketSnapshot.Forecast.Sum(point => point.ExpectedConsumptionKw) / Math.Max(1, marketSnapshot.Forecast.Count));
    }

    public static double BatteryStateOfCharge(EnergySnapshot snapshot)
    {
        return snapshot.BatteryCapacityKwh <= 0 ? 0 : snapshot.BatteryChargeKwh / snapshot.BatteryCapacityKwh;
    }
}

internal static class Csv
{
    public static string Join(params object?[] values)
    {
        return string.Join(",", values.Select(Escape));
    }

    private static string Escape(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
