using System.Text.Json;
using GridShare.Application;
using GridShare.Domain;

namespace GridShare.Infrastructure;

public sealed class ReplaySnapshotSource : IEnergySnapshotSource
{
    private readonly string _path;
    private readonly TimeSpan _delay;

    public ReplaySnapshotSource(string path, TimeSpan? delay = null)
    {
        _path = path;
        _delay = delay ?? TimeSpan.FromMilliseconds(250);
    }

    public async IAsyncEnumerable<IReadOnlyCollection<EnergySnapshot>> ReadTicksAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(_path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tick = JsonSerializer.Deserialize<ReplayTick>(line, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (tick?.EnergySnapshots is { Count: > 0 } snapshots)
            {
                yield return snapshots;
                await Task.Delay(_delay, cancellationToken);
            }
        }
    }

    private sealed record ReplayTick(List<EnergySnapshot> EnergySnapshots);
}
