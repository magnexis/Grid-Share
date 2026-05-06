namespace GridShare.Simulation;

public static class DiurnalCycle
{
    public static double SolarProductionKw(DateTimeOffset simulationTime, double panelCapacityKw, WeatherProfile? weatherProfile = null)
    {
        weatherProfile ??= WeatherProfile.ClearSpringDay;
        var hour = simulationTime.TimeOfDay.TotalHours;
        var noonCentered = hour - 12;
        var daylightEnvelope = Math.Exp(-(noonCentered * noonCentered) / (2 * 3.2 * 3.2));
        var seasonalShift = Math.Cos(((weatherProfile.DayOfYear - 172) / 365.0) * 2 * Math.PI);
        var sunrise = 6.0 - seasonalShift;
        var sunset = 18.0 + seasonalShift;
        var nightCutoff = hour < sunrise || hour > sunset ? 0 : 1;
        var cloudFactor = 1 - Math.Clamp(weatherProfile.CloudCover, 0, 1) * 0.78;
        var temperatureFactor = 1 - Math.Max(0, weatherProfile.TemperatureC - 25) * 0.004;

        return panelCapacityKw * daylightEnvelope * nightCutoff * cloudFactor * temperatureFactor;
    }

    public static double ResidentialConsumptionKw(
        DateTimeOffset simulationTime,
        double baseLoadKw,
        double morningPeakKw,
        double eveningPeakKw)
    {
        var hour = simulationTime.TimeOfDay.TotalHours;
        var morning = Peak(hour, centerHour: 7.25, width: 1.3, amplitude: morningPeakKw);
        var evening = Peak(hour, centerHour: 19.0, width: 2.0, amplitude: eveningPeakKw);
        var overnight = Peak(hour, centerHour: 1.0, width: 3.5, amplitude: baseLoadKw * 0.25);

        return baseLoadKw + morning + evening + overnight;
    }

    private static double Peak(double hour, double centerHour, double width, double amplitude)
    {
        var delta = hour - centerHour;
        return amplitude * Math.Exp(-(delta * delta) / (2 * width * width));
    }
}
