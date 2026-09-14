# TSC Route Generator

Een native Windows 11-routegenerator voor **Train Simulator Classic**, gebouwd met C#/.NET 8 en WPF.

## Eerste werkende versie

- echte routes importeren uit GPX en CSV;
- fictieve spoortrajecten procedureel genereren;
- stations en routepunten bewerken;
- route-afstand, hellingen en boogstralen controleren;
- interactieve 2D-voorvertoning;
- projecten opslaan en openen als `.tscroute.json`;
- exporteren naar een controleerbare TSC-stagingmap;
- meegeleverd PowerShell-startpunt voor de lokale `Serz.exe`.

> TSC gebruikt complexe blueprint- en binaire routebestanden. Deze versie schrijft bewust geen verzonnen `.bin`-bestand. De staging-export maakt betrouwbare JSON-, CSV-, GeoJSON- en XML-brondata. Een volgende exportadapter vertaalt die data naar gevalideerde TSC-blueprints en laat daarna de officiële lokale `Serz.exe` converteren.

## Vereisten

- Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Train Simulator Classic voor uiteindelijke spelintegratie

## Starten

```powershell
git clone https://github.com/andreschmohl-glitch/train-simulator-classic-route-generator.git
cd train-simulator-classic-route-generator
dotnet restore src/TscRouteGenerator/TscRouteGenerator.csproj
dotnet run --project src/TscRouteGenerator/TscRouteGenerator.csproj
```

## Een Windows-programma bouwen

```powershell
dotnet publish src/TscRouteGenerator/TscRouteGenerator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

De uitvoer komt in:
`src\TscRouteGenerator\bin\Release\net8.0-windows\win-x64\publish`

## CSV-formaat

Komma of puntkomma is toegestaan:

```csv
name,latitude,longitude,elevation,isStation
Gouda,52.0170,4.7055,0,true
Waddinxveen,52.0452,4.6517,0,true
Alphen aan den Rijn,52.1242,4.6572,0,true
```

## Roadmap

1. Wissels, parallelsporen en emplacementen
2. Hoogtemodel-import
3. TSC provider/product-assetselectie
4. Blueprint XML-adapter met Serz-validatie
5. Scenario- en dienstregelinggenerator
