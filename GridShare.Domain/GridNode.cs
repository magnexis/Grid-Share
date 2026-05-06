namespace GridShare.Domain;

public enum NodeRole
{
    Prosumer,
    Consumer,
    Battery
}

public sealed record GridLocation(double X, double Y)
{
    public double DistanceTo(GridLocation other)
    {
        var xDelta = X - other.X;
        var yDelta = Y - other.Y;
        return Math.Sqrt((xDelta * xDelta) + (yDelta * yDelta));
    }
}

public sealed record GridNode(
    string Id,
    NodeRole Role,
    GridLocation Location,
    double SurplusKwh,
    double DemandKwh)
{
    public string MarketCode { get; init; } = "US-AVG";
}
