using System.Collections.Concurrent;
using System.Text.Json;
using GridShare.Application;
using GridShare.Domain;

namespace GridShare.Infrastructure;

public sealed class IotJsonSnapshotSource : IEnergySnapshotSource
{
    private readonly ConcurrentDictionary<string, EnergySnapshot> _latest = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _tickPeriod;

    public IotJsonSnapshotSource(TimeSpan? tickPeriod = null)
    {
        _tickPeriod = tickPeriod ?? TimeSpan.FromSeconds(1);
    }

    public void EnqueuePayload(string payload)
    {
        var reading = JsonSerializer.Deserialize<IotReading>(payload);
        if (reading is null)
        {
            return;
        }

        var produced = reading.ProductionKw;
        var consumed = reading.ConsumptionKw;
        _latest[reading.NodeId] = new EnergySnapshot(
            reading.NodeId,
            new GridLocation(reading.X, reading.Y),
            produced,
            consumed,
            produced - consumed,
            reading.BatteryKwh,
            reading.BatteryCapacityKwh,
            _tickPeriod,
            reading.Timestamp)
        {
            MarketCode = string.IsNullOrWhiteSpace(reading.MarketCode) ? "US-AVG" : reading.MarketCode
        };
    }

    public async IAsyncEnumerable<IReadOnlyCollection<EnergySnapshot>> ReadTicksAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            yield return _latest.Values.ToArray();
            await Task.Delay(_tickPeriod, cancellationToken);
        }
    }

    private sealed record IotReading(
        string NodeId,
        double ProductionKw,
        double ConsumptionKw,
        double BatteryKwh,
        double BatteryCapacityKwh,
        double X,
        double Y,
        DateTimeOffset Timestamp,
        string? MarketCode = null);
}
