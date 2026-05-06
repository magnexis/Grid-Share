using GridShare.Api;
using GridShare.Application;
using GridShare.Domain;
using GridShare.Infrastructure;
using GridShare.Simulation;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<GridShareRuntimeOptions>(builder.Configuration.GetSection("GridShare"));
builder.Services.AddSingleton<ITradeLedger>(_ =>
{
    var mongo = builder.Configuration.GetConnectionString("MongoLedger");
    return string.IsNullOrWhiteSpace(mongo)
        ? new InMemoryTradeLedger()
        : new MongoTradeLedger(mongo);
});
builder.Services.AddSingleton<ISettlementService, SettlementService>();
builder.Services.AddSingleton<OrderBook>();
builder.Services.AddSingleton<MatchmakerEngine>();
builder.Services.AddSingleton<ILineLossModel, DistanceLineLossModel>();
builder.Services.AddSingleton<ICarbonAccountingService, CarbonAccountingService>();
builder.Services.AddSingleton<IForecastingEngine, ForecastingEngine>();
builder.Services.AddSingleton<IAnomalyDetector, AnomalyDetector>();
builder.Services.AddSingleton<MarketMetrics>();
builder.Services.AddSingleton<MarketOperator>();
builder.Services.AddSingleton<GridRuntimeState>();
builder.Services.AddSignalR();
builder.Services.AddHostedService<GridSimulationWorker>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services
    .AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(MarketMetrics.MeterName);
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddConsoleExporter();
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/grid/state", (GridRuntimeState state) => state.Current);
app.MapGet("/api/grid/frame", (GridRuntimeState state) => new
{
    market = state.Current,
    nodes = state.Nodes,
    flows = state.Flows
});
app.MapGet("/api/currencies", () => CurrencyCatalog.Supported);
app.MapGet("/api/energy-markets", () => EnergyMarketCatalog.Supported);
app.MapGet("/api/grid/ledger", (ITradeLedger ledger) => ledger.Blocks);
app.MapGet("/api/grid/accounts", (ISettlementService settlements) => settlements.Accounts.Values);
app.MapGet("/api/grid/health", (ITradeLedger ledger, GridRuntimeState state) => new
{
    ledgerVerified = ledger.VerifyIntegrity(),
    state.Current?.Status,
    state.Current?.MarketPricePerKwh,
    state.Current?.Carbon.CarbonOffsetKg
});
app.MapHub<GridFeedHub>("/hubs/grid");

app.Run();
