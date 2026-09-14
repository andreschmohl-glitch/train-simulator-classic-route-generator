using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace TscRouteGenerator;

public partial class MainWindow : Window
{
    private RouteProject _project = new();
    private readonly ObservableCollection<RoutePoint> _points = [];
    private string? _currentFile;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MainWindow()
    {
        InitializeComponent();
        PointsGrid.ItemsSource = _points;
        TrackTypeBox.SelectedIndex = 0;
        LoadProjectIntoUi(new RouteProject());
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _currentFile = null;
        LoadProjectIntoUi(new RouteProject());
        StatusText.Text = "Nieuw project aangemaakt";
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "TSC Route-project (*.tscroute.json)|*.tscroute.json|JSON (*.json)|*.json",
            Title = "Routeproject openen"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var loaded = JsonSerializer.Deserialize<RouteProject>(File.ReadAllText(dialog.FileName), _jsonOptions)
                         ?? throw new InvalidDataException("Het projectbestand is leeg.");
            _currentFile = dialog.FileName;
            LoadProjectIntoUi(loaded);
            StatusText.Text = "Project geopend";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Openen mislukt", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveProject();
    }

    private bool SaveProject()
    {
        ReadSettings();
        if (string.IsNullOrWhiteSpace(_currentFile))
        {
            var dialog = new SaveFileDialog
            {
                Filter = "TSC Route-project (*.tscroute.json)|*.tscroute.json",
                FileName = SafeFileName(_project.Name) + ".tscroute.json",
                Title = "Routeproject opslaan"
            };
            if (dialog.ShowDialog() != true) return false;
            _currentFile = dialog.FileName;
        }

        try
        {
            File.WriteAllText(_currentFile, JsonSerializer.Serialize(_project, _jsonOptions), Encoding.UTF8);
            StatusText.Text = "Project opgeslagen";
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Opslaan mislukt", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void ImportGpx_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "GPX (*.gpx)|*.gpx", Title = "GPX-route importeren" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var document = XDocument.Load(dialog.FileName);
            var gpxPoints = document.Descendants()
                .Where(x => x.Name.LocalName is "trkpt" or "rtept" or "wpt")
                .Select((x, i) => new RoutePoint
                {
                    Name = ChildValue(x, "name") ?? $"Punt {i + 1}",
                    Latitude = ParseDouble(x.Attribute("lat")?.Value),
                    Longitude = ParseDouble(x.Attribute("lon")?.Value),
                    Elevation = ParseDouble(ChildValue(x, "ele")),
                    IsStation = x.Name.LocalName == "wpt"
                }).ToList();

            ReplacePoints(gpxPoints);
            StatusText.Text = $"{gpxPoints.Count} GPX-punten geïmporteerd";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "GPX-import mislukt", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "CSV (*.csv)|*.csv|Tekst (*.txt)|*.txt", Title = "CSV-route importeren" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var lines = File.ReadAllLines(dialog.FileName)
                .Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            if (lines.Count == 0) throw new InvalidDataException("Het CSV-bestand is leeg.");

            var separator = lines[0].Count(c => c == ';') > lines[0].Count(c => c == ',') ? ';' : ',';
            var start = lines[0].Contains("latitude", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var imported = new List<RoutePoint>();

            for (var i = start; i < lines.Count; i++)
            {
                var cells = lines[i].Split(separator);
                if (cells.Length < 4) continue;
                imported.Add(new RoutePoint
                {
                    Name = cells[0].Trim(),
                    Latitude = ParseDouble(cells[1]),
                    Longitude = ParseDouble(cells[2]),
                    Elevation = ParseDouble(cells[3]),
                    IsStation = cells.Length > 4 && bool.TryParse(cells[4].Trim(), out var station) && station
                });
            }

            ReplacePoints(imported);
            StatusText.Text = $"{imported.Count} CSV-punten geïmporteerd";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "CSV-import mislukt", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(LengthBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var lengthKm) ||
            lengthKm is < 1 or > 1000)
        {
            MessageBox.Show("Voer een lengte tussen 1 en 1000 km in.", "Ongeldige lengte");
            return;
        }

        if (!int.TryParse(StationsBox.Text, out var stationCount) || stationCount is < 2 or > 100)
        {
            MessageBox.Show("Voer 2 tot 100 stations in.", "Ongeldig aantal stations");
            return;
        }

        var landscape = (LandscapeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Nederlands vlakland";
        var random = new Random();
        var sampleCount = Math.Max(60, (int)(lengthKm * 5));
        const double startLat = 52.02;
        const double startLon = 4.70;
        var generated = new List<RoutePoint>();

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)(sampleCount - 1);
            var northMeters = t * lengthKm * 1000;
            var eastMeters = Math.Sin(t * Math.PI * 3) * 850 + Math.Sin(t * Math.PI * 9) * 110;
            var elevation = landscape switch
            {
                "Heuvelland" => 45 + 32 * Math.Sin(t * Math.PI * 4) + 10 * Math.Sin(t * Math.PI * 13),
                "Kustgebied" => 4 + 2 * Math.Sin(t * Math.PI * 5),
                "Stedelijk" => 8 + 3 * Math.Sin(t * Math.PI * 2),
                _ => 2 + 1.5 * Math.Sin(t * Math.PI * 4)
            };

            var stationIndex = (int)Math.Round(t * (stationCount - 1));
            var stationT = stationIndex / (double)(stationCount - 1);
            var isStation = Math.Abs(t - stationT) < 0.5 / sampleCount;

            generated.Add(new RoutePoint
            {
                Name = isStation ? $"Station {stationIndex + 1}" : $"Punt {i + 1}",
                Latitude = startLat + northMeters / 111_320.0,
                Longitude = startLon + eastMeters / (111_320.0 * Math.Cos(startLat * Math.PI / 180.0)),
                Elevation = Math.Round(elevation + random.NextDouble() * 0.15, 2),
                IsStation = isStation
            });
        }

        ReplacePoints(generated);
        StatusText.Text = $"Fictieve route van {lengthKm:F1} km gegenereerd";
        ValidateRoute();
    }

    private void Validate_Click(object sender, RoutedEventArgs e) => ValidateRoute();

    private List<ValidationMessage> ValidateRoute()
    {
        ReadSettings();
        var messages = new List<ValidationMessage>();
        if (_points.Count < 2)
            messages.Add(new("FOUT", "De route heeft minimaal twee punten nodig."));

        for (var i = 1; i < _points.Count; i++)
        {
            var distance = DistanceMeters(_points[i - 1], _points[i]);
            if (distance < 0.05)
                messages.Add(new("FOUT", $"Punten {i} en {i + 1} liggen op dezelfde plaats."));
            else
            {
                var gradient = Math.Abs(_points[i].Elevation - _points[i - 1].Elevation) / distance * 1000;
                if (gradient > _project.MaxGradientPermille)
                    messages.Add(new("WAARSCHUWING", $"Helling {gradient:F1}‰ bij punt {i + 1} overschrijdt {_project.MaxGradientPermille:F1}‰."));
            }
        }

        for (var i = 1; i < _points.Count - 1; i++)
        {
            var radius = ApproximateRadius(_points[i - 1], _points[i], _points[i + 1]);
            if (double.IsFinite(radius) && radius < _project.MinimumRadiusMeters)
                messages.Add(new("WAARSCHUWING", $"Boog rond punt {i + 1} is circa {radius:F0} m; minimum is {_project.MinimumRadiusMeters:F0} m."));
        }

        if (_points.All(p => !p.IsStation))
            messages.Add(new("INFO", "De route bevat nog geen stations."));
        if (messages.Count == 0)
            messages.Add(new("OK", "Geen problemen gevonden. De route is gereed voor staging-export."));

        ValidationList.ItemsSource = messages.Select(m => $"[{m.Severity}] {m.Text}");
        StatusText.Text = $"{messages.Count(m => m.Severity is "FOUT" or "WAARSCHUWING")} aandachtspunt(en)";
        return messages;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var messages = ValidateRoute();
        if (messages.Any(m => m.Severity == "FOUT"))
        {
            MessageBox.Show("Los eerst de fouten uit de routecontrole op.", "Export geblokkeerd");
            return;
        }

        var dialog = new OpenFolderDialog { Title = "Kies een map voor de TSC-staging-export" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            ReadSettings();
            var exportRoot = Path.Combine(dialog.FolderName, SafeFileName(_project.Name));
            Directory.CreateDirectory(exportRoot);

            var manifest = new TscExportManifest
            {
                RouteName = _project.Name,
                TrackType = _project.TrackType,
                LengthMeters = TotalDistance(),
                PointCount = _points.Count,
                StationCount = _points.Count(p => p.IsStation)
            };
            File.WriteAllText(Path.Combine(exportRoot, "manifest.json"),
                JsonSerializer.Serialize(manifest, _jsonOptions), Encoding.UTF8);

            var csv = new StringBuilder("name,latitude,longitude,elevation,isStation\r\n");
            foreach (var p in _points)
                csv.AppendLine($"{Csv(p.Name)},{F(p.Latitude)},{F(p.Longitude)},{F(p.Elevation)},{p.IsStation.ToString().ToLowerInvariant()}");
            File.WriteAllText(Path.Combine(exportRoot, "route.csv"), csv.ToString(), Encoding.UTF8);

            var geoJson = BuildGeoJson();
            File.WriteAllText(Path.Combine(exportRoot, "route.geojson"), geoJson, Encoding.UTF8);

            var xml = new XDocument(
                new XElement("TscRouteGeneratorSource",
                    new XAttribute("version", "1"),
                    new XElement("RouteName", _project.Name),
                    new XElement("TrackType", _project.TrackType),
                    new XElement("Points", _points.Select((p, i) =>
                        new XElement("Point",
                            new XAttribute("index", i),
                            new XAttribute("station", p.IsStation),
                            new XElement("Name", p.Name),
                            new XElement("Latitude", F(p.Latitude)),
                            new XElement("Longitude", F(p.Longitude)),
                            new XElement("Elevation", F(p.Elevation)))))));
            xml.Save(Path.Combine(exportRoot, "route-source.xml"));

            var script = """
param(
  [string]$SerzPath = "C:\Program Files (x86)\Steam\steamapps\common\RailWorks\Serz.exe",
  [string]$BlueprintXml
)
if (-not (Test-Path $SerzPath)) { throw "Serz.exe niet gevonden: $SerzPath" }
if (-not $BlueprintXml) {
  Write-Host "Deze stagingmap bevat neutrale routebrondata."
  Write-Host "Geef een gevalideerde TSC Blueprint XML op met -BlueprintXml om Serz uit te voeren."
  exit 0
}
& $SerzPath $BlueprintXml
if ($LASTEXITCODE -ne 0) { throw "Serz.exe eindigde met foutcode $LASTEXITCODE" }
""";
            File.WriteAllText(Path.Combine(exportRoot, "Run-Serz.ps1"), script, Encoding.UTF8);

            StatusText.Text = "TSC-staging-export voltooid";
            MessageBox.Show($"Export gereed:\n{exportRoot}", "TSC Route Generator");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Export mislukt", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string BuildGeoJson()
    {
        var coordinates = _points.Select(p => new[] { p.Longitude, p.Latitude, p.Elevation }).ToArray();
        var data = new
        {
            type = "FeatureCollection",
            features = new object[]
            {
                new
                {
                    type = "Feature",
                    properties = new { name = _project.Name, trackType = _project.TrackType },
                    geometry = new { type = "LineString", coordinates }
                }
            }
        };
        return JsonSerializer.Serialize(data, _jsonOptions);
    }

    private void LoadProjectIntoUi(RouteProject project)
    {
        _project = project;
        NameBox.Text = project.Name;
        DescriptionBox.Text = project.Description;
        GradientBox.Text = project.MaxGradientPermille.ToString(CultureInfo.CurrentCulture);
        RadiusBox.Text = project.MinimumRadiusMeters.ToString(CultureInfo.CurrentCulture);
        var item = TrackTypeBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(x => x.Content?.ToString() == project.TrackType);
        TrackTypeBox.SelectedItem = item ?? TrackTypeBox.Items[0];
        ReplacePoints(project.Points);
    }

    private void ReadSettings()
    {
        _project.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Nieuwe route" : NameBox.Text.Trim();
        _project.Description = DescriptionBox.Text.Trim();
        _project.TrackType = (TrackTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Geëlektrificeerd dubbelspoor";
        if (double.TryParse(GradientBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var gradient))
            _project.MaxGradientPermille = gradient;
        if (double.TryParse(RadiusBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var radius))
            _project.MinimumRadiusMeters = radius;
        _project.Points = _points.ToList();
    }

    private void ReplacePoints(IEnumerable<RoutePoint> points)
    {
        _points.Clear();
        foreach (var point in points) _points.Add(point);
        _project.Points = _points.ToList();
        Redraw();
    }

    private void SettingsChanged(object sender, EventArgs e)
    {
        if (!IsLoaded) return;
        ReadSettings();
        UpdateSummary();
    }

    private void PointsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ReadSettings();
            Redraw();
        });
    }

    private void RouteCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        if (RouteCanvas is null) return;
        RouteCanvas.Children.Clear();
        if (_points.Count < 2)
        {
            UpdateSummary();
            return;
        }

        var width = Math.Max(100, RouteCanvas.ActualWidth - 50);
        var height = Math.Max(100, RouteCanvas.ActualHeight - 50);
        var minLat = _points.Min(p => p.Latitude);
        var maxLat = _points.Max(p => p.Latitude);
        var minLon = _points.Min(p => p.Longitude);
        var maxLon = _points.Max(p => p.Longitude);
        var latRange = Math.Max(0.000001, maxLat - minLat);
        var lonRange = Math.Max(0.000001, maxLon - minLon);

        Point Map(RoutePoint p) => new(
            25 + (p.Longitude - minLon) / lonRange * width,
            25 + (maxLat - p.Latitude) / latRange * height);

        for (var i = 0; i < 10; i++)
        {
            var x = 25 + width * i / 9;
            var y = 25 + height * i / 9;
            RouteCanvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = 25, Y2 = 25 + height, Stroke = new SolidColorBrush(Color.FromRgb(24, 42, 56)), StrokeThickness = 1 });
            RouteCanvas.Children.Add(new Line { X1 = 25, X2 = 25 + width, Y1 = y, Y2 = y, Stroke = new SolidColorBrush(Color.FromRgb(24, 42, 56)), StrokeThickness = 1 });
        }

        var line = new Polyline { Stroke = Brushes.Cyan, StrokeThickness = 4, StrokeLineJoin = PenLineJoin.Round };
        foreach (var p in _points) line.Points.Add(Map(p));
        RouteCanvas.Children.Add(line);

        foreach (var point in _points.Where(p => p.IsStation))
        {
            var pos = Map(point);
            var marker = new Ellipse { Width = 13, Height = 13, Fill = Brushes.Gold, Stroke = Brushes.Black, StrokeThickness = 2 };
            Canvas.SetLeft(marker, pos.X - 6.5);
            Canvas.SetTop(marker, pos.Y - 6.5);
            RouteCanvas.Children.Add(marker);

            var label = new TextBlock { Text = point.Name, Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromArgb(180, 15, 25, 35)), Padding = new Thickness(3) };
            Canvas.SetLeft(label, pos.X + 8);
            Canvas.SetTop(label, pos.Y - 10);
            RouteCanvas.Children.Add(label);
        }
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        if (SummaryText is null) return;
        SummaryText.Text = $"{TotalDistance() / 1000:F1} km • {_points.Count} punten • {_points.Count(p => p.IsStation)} stations";
    }

    private double TotalDistance() => Enumerable.Range(1, _points.Count > 1 ? _points.Count - 1 : 0)
        .Sum(i => DistanceMeters(_points[i - 1], _points[i]));

    private static double DistanceMeters(RoutePoint a, RoutePoint b)
    {
        const double earth = 6_371_000;
        var p1 = a.Latitude * Math.PI / 180;
        var p2 = b.Latitude * Math.PI / 180;
        var dp = (b.Latitude - a.Latitude) * Math.PI / 180;
        var dl = (b.Longitude - a.Longitude) * Math.PI / 180;
        var h = Math.Sin(dp / 2) * Math.Sin(dp / 2) +
                Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
        return earth * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }

    private static double ApproximateRadius(RoutePoint a, RoutePoint b, RoutePoint c)
    {
        var lat0 = b.Latitude * Math.PI / 180;
        (double x, double y) P(RoutePoint p) =>
            ((p.Longitude - b.Longitude) * 111_320 * Math.Cos(lat0), (p.Latitude - b.Latitude) * 111_320);
        var pa = P(a); var pb = P(b); var pc = P(c);
        var ab = Math.Hypot(pa.x - pb.x, pa.y - pb.y);
        var bc = Math.Hypot(pb.x - pc.x, pb.y - pc.y);
        var ca = Math.Hypot(pc.x - pa.x, pc.y - pa.y);
        var twiceArea = Math.Abs((pb.x - pa.x) * (pc.y - pa.y) - (pb.y - pa.y) * (pc.x - pa.x));
        return twiceArea < 0.001 ? double.PositiveInfinity : ab * bc * ca / (2 * twiceArea);
    }

    private static string? ChildValue(XElement element, string localName) =>
        element.Elements().FirstOrDefault(x => x.Name.LocalName == localName)?.Value;

    private static double ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result :
        double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result) ? result : 0;

    private static string F(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string SafeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return string.IsNullOrWhiteSpace(value) ? "route" : value.Trim();
    }
}
