namespace TscRouteGenerator;

public sealed class RouteProject
{
    public string Name { get; set; } = "Nieuwe route";
    public string Description { get; set; } = "";
    public string TrackType { get; set; } = "Geëlektrificeerd dubbelspoor";
    public double MaxGradientPermille { get; set; } = 25;
    public double MinimumRadiusMeters { get; set; } = 250;
    public List<RoutePoint> Points { get; set; } = [];
}

public sealed class RoutePoint
{
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Elevation { get; set; }
    public bool IsStation { get; set; }
}

public sealed record ValidationMessage(string Severity, string Text);

public sealed class TscExportManifest
{
    public string Format { get; set; } = "TSC Route Generator staging v1";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string RouteName { get; set; } = "";
    public string TrackType { get; set; } = "";
    public double LengthMeters { get; set; }
    public int PointCount { get; set; }
    public int StationCount { get; set; }
    public string Status { get; set; } = "Staging data; blueprint conversion required";
}
