namespace GridShare.Api;

public sealed record GridShareRuntimeOptions
{
    public int HouseCount { get; init; } = 50;

    public int TickDelayMs { get; init; } = 1000;

    public int SimulatedMinutesPerTick { get; init; } = 15;
}
