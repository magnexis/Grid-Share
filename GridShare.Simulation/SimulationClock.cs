namespace GridShare.Simulation;

public sealed class SimulationClock
{
    public SimulationClock(DateTimeOffset startTime, TimeSpan simulatedStep, TimeSpan realDelay)
    {
        Current = startTime;
        SimulatedStep = simulatedStep;
        RealDelay = realDelay;
    }

    public DateTimeOffset Current { get; private set; }

    public TimeSpan SimulatedStep { get; }

    public TimeSpan RealDelay { get; }

    public DateTimeOffset Advance()
    {
        Current = Current.Add(SimulatedStep);
        return Current;
    }
}
