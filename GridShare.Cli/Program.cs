using GridShare.Application;
using GridShare.Domain;
using GridShare.Infrastructure;
using GridShare.Simulation;
using Spectre.Console;

var houseCount = ReadIntArg(args, "--houses", 50);
var tickCount = ReadIntArg(args, "--ticks", 96);
var realDelayMs = ReadIntArg(args, "--delay-ms", 250);
var live = args.Contains("--live", StringComparer.OrdinalIgnoreCase);
var replayPath = ReadStringArg(args, "--replay");
var csvPath = ReadStringArg(args, "--csv");
var jsonlPath = ReadStringArg(args, "--jsonl");
var currency = CurrencyCatalog.Resolve(ReadStringArg(args, "--currency"));

var exporters = new List<ITelemetryExporter>();
if (!string.IsNullOrWhiteSpace(csvPath))
{
    exporters.Add(new CsvTelemetryExporter(csvPath));
}

if (!string.IsNullOrWhiteSpace(jsonlPath))
{
    exporters.Add(new JsonlTelemetryExporter(jsonlPath));
}

var ledger = new InMemoryTradeLedger();
var market = new MarketOperator(ledger, new MatchmakerEngine());
var cancellationToken = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationToken.Cancel();
};

if (!string.IsNullOrWhiteSpace(replayPath))
{
    await RunReplayAsync(replayPath, market, ledger, exporters, currency, cancellationToken.Token);
}
else
{
    await RunSimulationAsync(houseCount, tickCount, realDelayMs, live, market, ledger, exporters, currency, cancellationToken.Token);
}

static async Task RunSimulationAsync(
    int houseCount,
    int tickCount,
    int realDelayMs,
    bool live,
    MarketOperator market,
    ITradeLedger ledger,
    IReadOnlyCollection<ITelemetryExporter> exporters,
    CurrencyProfile currency,
    CancellationToken cancellationToken)
{
    var houses = NeighborhoodFactory.Create(houseCount);
    var clock = new SimulationClock(
        new DateTimeOffset(DateTime.UtcNow.Date.AddHours(5), TimeSpan.Zero),
        simulatedStep: TimeSpan.FromMinutes(15),
        realDelay: TimeSpan.FromMilliseconds(realDelayMs));
    var marketPrice = 0.16m;
    var tick = 0;

    while (!cancellationToken.IsCancellationRequested && (live || tick < tickCount))
    {
        var weather = new WeatherProfile(
            CloudCover: 0.15 + 0.35 * Math.Max(0, Math.Sin(tick / 10.0)),
            TemperatureC: 19 + 8 * Math.Max(0, Math.Sin((clock.Current.Hour - 8) * Math.PI / 12)),
            DayOfYear: clock.Current.DayOfYear);
        var snapshots = houses
            .Select(house => house.Tick(clock.Current, clock.SimulatedStep, weather, marketPrice))
            .OfType<EnergySnapshot>()
            .ToArray();
        var marketSnapshot = market.Operate(snapshots);
        marketPrice = marketSnapshot.MarketPricePerKwh;

        RenderDashboard(marketSnapshot, snapshots, ledger.VerifyIntegrity(), tick + 1, live ? null : tickCount, currency);
        await ExportAsync(exporters, marketSnapshot, snapshots, cancellationToken);

        tick++;
        clock.Advance();
        await Task.Delay(clock.RealDelay, cancellationToken);
    }
}

static async Task RunReplayAsync(
    string replayPath,
    MarketOperator market,
    ITradeLedger ledger,
    IReadOnlyCollection<ITelemetryExporter> exporters,
    CurrencyProfile currency,
    CancellationToken cancellationToken)
{
    var source = new ReplaySnapshotSource(replayPath);
    var tick = 0;

    await foreach (var snapshots in source.ReadTicksAsync(cancellationToken))
    {
        var marketSnapshot = market.Operate(snapshots);
        RenderDashboard(marketSnapshot, snapshots, ledger.VerifyIntegrity(), ++tick, null, currency);
        await ExportAsync(exporters, marketSnapshot, snapshots, cancellationToken);
    }
}

static async Task ExportAsync(
    IReadOnlyCollection<ITelemetryExporter> exporters,
    MarketSnapshot marketSnapshot,
    IReadOnlyCollection<EnergySnapshot> snapshots,
    CancellationToken cancellationToken)
{
    foreach (var exporter in exporters)
    {
        await exporter.ExportAsync(marketSnapshot, snapshots, cancellationToken);
    }
}

static void RenderDashboard(
    MarketSnapshot market,
    IReadOnlyCollection<EnergySnapshot> houses,
    bool ledgerVerified,
    int tick,
    int? tickLimit,
    CurrencyProfile currency)
{
    if (!Console.IsOutputRedirected)
    {
        AnsiConsole.Clear();
    }

    var statusColor = market.Status == GridStatus.BlackoutRisk ? "red" : "green";
    var tickText = tickLimit.HasValue ? $"{tick}/{tickLimit}" : $"{tick}";
    var batteryKwh = houses.Sum(snapshot => snapshot.BatteryChargeKwh);
    var batteryCapacity = Math.Max(1, houses.Sum(snapshot => snapshot.BatteryCapacityKwh));
    var staleCount = houses.Count(snapshot => snapshot.IsStale);

    var grid = new Grid().AddColumn().AddColumn();
    grid.AddRow(
        new Panel(new Markup($"[bold]Tick[/] {tickText}\n[bold]Time[/] {market.SimulationTime:yyyy-MM-dd HH:mm} UTC\n[bold]Houses Online[/] {houses.Count}\n[bold]Stale Packets[/] {staleCount}")).Header("Simulation"),
        new Panel(new Markup($"[bold {statusColor}]{market.Status}[/]\n[bold]Price[/] {currency.Format(market.MarketPricePerKwh)}/kWh\n[bold]Currency[/] {currency.Code} - {currency.Region}\n[bold]Carbon Offset[/] {market.Carbon.CarbonOffsetKg:F2} kg\n[bold]Open Orders[/] {market.OpenOrders.Count}")).Header("Market"));

    var table = new Table().Border(TableBorder.Rounded).Title("Grid Status");
    table.AddColumn("Metric");
    table.AddColumn(new TableColumn("Value").RightAligned());
    table.AddRow("Produced", $"{market.TotalProductionKw:F2} kW");
    table.AddRow("Consumed", $"{market.TotalConsumptionKw:F2} kW");
    table.AddRow("Market Supply", $"{market.MarketSupplyKw:F2} kW");
    table.AddRow("Market Demand", $"{market.MarketDemandKw:F2} kW");
    table.AddRow("Battery", $"{batteryKwh:F2} / {batteryCapacity:F2} kWh");
    table.AddRow("Ledger", ledgerVerified ? "[green]verified[/]" : "[red]failed[/]");

    var trades = new Table().Border(TableBorder.Rounded).Title("Last 5 Transactions");
    trades.AddColumn("Time");
    trades.AddColumn("Route");
    trades.AddColumn(new TableColumn("Delivered").RightAligned());
    trades.AddColumn(new TableColumn("Loss").RightAligned());
    trades.AddColumn(new TableColumn("Amount").RightAligned());

    foreach (var block in market.LastLedgerBlocks)
    {
        trades.AddRow(
            block.Timestamp.ToString("HH:mm:ss"),
            $"{block.ProsumerId} -> {block.ConsumerId}",
            $"{block.DeliveredKwh:F2} kWh",
            $"{block.LineLossKwh:F3} kWh",
            currency.Format(block.SettlementAmount));
    }

    if (market.LastLedgerBlocks.Count == 0)
    {
        trades.AddRow("-", "No trades recorded yet", "-", "-", "-");
    }

    var forecast = new Table().Border(TableBorder.Simple).Title("Next Hour Forecast");
    forecast.AddColumn("Time");
    forecast.AddColumn("Production");
    forecast.AddColumn("Consumption");
    foreach (var point in market.Forecast.Take(4))
    {
        forecast.AddRow(point.Time.ToString("HH:mm"), $"{point.ExpectedProductionKw:F1} kW", $"{point.ExpectedConsumptionKw:F1} kW");
    }

    AnsiConsole.Write(new Rule("[bold yellow]GridShare Simulation Engine[/]"));
    AnsiConsole.Write(grid);
    AnsiConsole.Write(table);
    AnsiConsole.Write(trades);
    AnsiConsole.Write(forecast);

    if (market.Anomalies.Count > 0)
    {
        AnsiConsole.MarkupLine($"[yellow]Anomalies:[/] {string.Join(", ", market.Anomalies.Take(3).Select(flag => $"{flag.NodeId}:{flag.Code}"))}");
    }

    AnsiConsole.MarkupLine("[grey]Ctrl+C to stop. Use --currency EUR/INR/AUD/etc, --csv path.csv, --jsonl path.jsonl, or --replay path.jsonl.[/]");
}

static int ReadIntArg(IReadOnlyList<string> args, string name, int defaultValue)
{
    var value = ReadStringArg(args, name);
    return int.TryParse(value, out var parsed) ? parsed : defaultValue;
}

static string? ReadStringArg(IReadOnlyList<string> args, string name)
{
    var index = args
        .Select((value, position) => new { value, position })
        .FirstOrDefault(item => string.Equals(item.value, name, StringComparison.OrdinalIgnoreCase))
        ?.position;

    return index is not null && index.Value + 1 < args.Count ? args[index.Value + 1] : null;
}
