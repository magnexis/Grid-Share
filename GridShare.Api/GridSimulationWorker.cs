using GridShare.Application;
using GridShare.Domain;
using GridShare.Simulation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace GridShare.Api;

public sealed class GridSimulationWorker : BackgroundService
{
    private readonly GridRuntimeState _state;
    private readonly MarketOperator _marketOperator;
    private readonly IHubContext<GridFeedHub> _hub;
    private readonly GridShareRuntimeOptions _options;
    private readonly IReadOnlyList<SmartHouse> _houses;
    private DateTimeOffset _simulationTime;
    private decimal _marketPrice = 0.16m;

    public GridSimulationWorker(
        GridRuntimeState state,
        MarketOperator marketOperator,
        IHubContext<GridFeedHub> hub,
        IOptions<GridShareRuntimeOptions> options)
    {
        _state = state;
        _marketOperator = marketOperator;
        _hub = hub;
        _options = options.Value;
        _houses = NeighborhoodFactory.Create(_options.HouseCount);
        _simulationTime = new DateTimeOffset(DateTime.UtcNow.Date.AddHours(5), TimeSpan.Zero);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var step = TimeSpan.FromMinutes(_options.SimulatedMinutesPerTick);

        while (!stoppingToken.IsCancellationRequested)
        {
            var weather = new WeatherProfile(0.25, 22, _simulationTime.DayOfYear);
            var snapshots = _houses
                .Select(house => house.Tick(_simulationTime, step, weather, _marketPrice))
                .OfType<EnergySnapshot>()
                .ToArray();
            var market = _marketOperator.Operate(snapshots);
            _marketPrice = market.MarketPricePerKwh;
            var flows = market.Trades.Select(trade => new TradeFlow(
                trade.ProsumerId,
                trade.ConsumerId,
                trade.DeliveredKwh,
                trade.LineLossKwh,
                trade.PricePerUnit)).ToArray();
            _state.Publish(market, snapshots, flows);

            await _hub.Clients.All.SendAsync("grid.tick", new
            {
                market,
                nodes = snapshots,
                flows
            }, stoppingToken);

            _simulationTime = _simulationTime.Add(step);
            await Task.Delay(_options.TickDelayMs, stoppingToken);
        }
    }
}
