# MeteoMesh5.0

Ein verteiltes meteorologisches Datenerfassungssystem, das mit .NET 9 entwickelt wurde und mehrere Sensorstationen umfasst, die über lokale Knoten und einen zentralen Server koordiniert werden.

## Architektur

- **Zentraler Server**: Blazor-Anwendung für Datenaggregation und Visualisierung
- **Lokale Knoten**: Koordinieren mehrere Sensorstationen in einem geografischen Gebiet
- **Sensorstationen**: Temperatur-, Feuchtigkeits-, Druck- und Lidar-Messstationen
- **Kommunikation**: gRPC-basiertes Telemetriesystem mit Echtzeit-Datenstreaming

## Entwicklung

### Voraussetzungen
- .NET 9 SDK
- Visual Studio 2024 oder Visual Studio Code
- Docker (für Produktionsdeployment)

### Lokale Entwicklung mit Aspire

Dieses Projekt verwendet .NET Aspire für die lokale Entwicklungsorchestration:

```bash
# Alle Services mit Aspire starten
dotnet run --project MeteoMesh5.Host
```

Aspire wird automatisch:
- Den zentralen Server starten
- Alle konfigurierten lokalen Knoten starten
- Sensorstationen für jeden Knoten starten
- Service Discovery und Konfiguration bereitstellen
- Das Aspire-Dashboard zur Überwachung öffnen

### Tests ausführen

```bash
# Alle Tests ausführen
dotnet test

# Tests für ein spezifisches Projekt ausführen
dotnet test MeteoMesh5.CentralServer.Tests
```

## Konfiguration (appsettings.json)

Die Hauptkonfiguration erfolgt in `MeteoMesh5.Host/appsettings.json`:

```json
{
  "AppConfig": {
    "CentralServers": [
      {
        "Id": "CENTRAL_001",
        "Name": "MeteoMesh5 Central Server",
        "Port": 8000
      }
    ],
    "LocalNodes": [
      {
        "Id": 1,
        "Name": "Knoten Steinen",
        "Port": 7000,
        "CentralUrl": "https://localhost:8000",
        "Coordinates": {
          "Latitude": 47.05109629873727,
          "Longitude": 8.614277548861644,
          "Altitude": 37.0
        },
        "StationInactiveMinutes": 30,
        "Stations": [
          {
            "Id": 1,
            "Type": 0  // 0=Temperatur, 1=Feuchtigkeit, 2=Druck, 3=Lidar
          }
        ]
      }
    ],
    "Simulation": {
      "StartTime": "2024-01-01T00:00:00Z",
      "SpeedMultiplier": 60.0,
      "UseSimulation": true
    }
  }
}
```

### MeteoMesh5.Host spezifische Konfiguration

#### AppConfig

- `CentralServers`: Liste der zentralen Server
  - `Id`: Eindeutige Kennung des zentralen Servers
  - `Name`: Anzeigename des Servers
  - `Port`: HTTPS-Port für den Server

- `LocalNodes`: Liste der lokalen Knoten
  - `Id`: Eindeutige numerische ID des Knotens
  - `Name`: Anzeigename des Knotens
  - `Port`: Basis-Port für den Knoten (Stationen verwenden Port+1000, Port+2000, etc.)
  - `CentralUrl`: URL des zentralen Servers
  - `Coordinates`: GPS-Koordinaten (Latitude/Longitude/Altitude)
  - `StationInactiveMinutes`: Timeout für inaktive Stationen in Minuten
  - `Stations`: Array von Stationen mit ID und Typ

- `Simulation`: Simulationskonfiguration
  - `StartTime`: Startzeitpunkt der Simulation (ISO 8601 Format)
  - `SpeedMultiplier`: Geschwindigkeitsmultiplikator (60.0 = 1 Minute = 1 Stunde)
  - `UseSimulation`: true für Simulationsmodus, false für echte Sensoren

#### Verfügbare Stationstypen:

- `0` = Temperaturstation
- `1` = Feuchtigkeitsstation  
- `2` = Druckstation
- `3` = Lidar-Station

### Einzelne Service-Konfiguration

**LocalNode appsettings.json:**
```json
{
  "Node": {
    "Id": 1,
    "Name": "Knoten Steinen",
    "Port": 7001,
    "CentralUrl": "https://localhost:8000",
    "IgnoreSslErrors": true,
    "UseMockClient": false,
    "StationInactiveMinutes": 1,
    "Coordinates": {
      "Latitude": 47.05109629873727,
      "Longitude": 8.614277548861644,
      "Altitude": 37.0
    }
  },
  "ConnectionStrings": {
    "SqliteConnection": "Data Source=database.db"
  }
}
```

**Logging-Konfiguration (Serilog):**
```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/app-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

## Deployment

Das Projekt verwendet GitHub Actions für CI/CD mit automatischen Docker-Image-Builds und Multi-Environment-Deployments.

### Produktions-Deployment
- Docker-Images werden automatisch für jeden Service erstellt
- GitHub Container Registry wird für Image-Speicherung verwendet
- Kubernetes-Manifeste verfügbar für Produktionsorchestration

## Projektstruktur

- `MeteoMesh5.CentralServer/` - Hauptanwendung (Blazor Server)
- `MeteoMesh5.LocalNode/` - Lokaler Knoten-Koordinationsservice
- `MeteoMesh5.*Station/` - Individuelle Sensorstations-Services
- `MeteoMesh5.Shared/` - Gemeinsame Modelle und gRPC-Definitionen
- `MeteoMesh5.Host/` - Aspire AppHost für lokale Entwicklung
- `*.Tests/` - Unit- und Integrationstests

## Erste Schritte

1. Repository klonen
2. .NET 9 SDK installieren
3. Konfiguration in `MeteoMesh5.Host/appsettings.json` anpassen
4. Projekt starten: `dotnet run --project MeteoMesh5.Host`
5. Aspire-Dashboard im Browser öffnen (URL wird in der Konsole angezeigt)