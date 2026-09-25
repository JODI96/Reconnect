# Reconnect

Habbo-ähnliches 3D-Social-Dating-Spiel. Die Aussenwelt ist ein Nachbau von Zürich aus
swisstopo-Geodaten; Gebäude sind Eingänge zu Räumen, die Nutzer selbst einrichten. In den
Räumen treffen sich Leute als 3D-Avatare, chatten, liken sich und matchen.
Zielplattformen: iOS, Android, später Desktop.

Der Entwickler ist erfahren in .NET/C#, aber neu in Unity. Unity-spezifische Schritte daher
ausführlicher erklären.

## Repositories / Ordner

| Ordner                          | Inhalt                                   | Versionskontrolle |
|---------------------------------|------------------------------------------|-------------------|
| `C:\Users\Joys9\Reconnect.Backend` | Dieses Repo: Backend (.NET)            | Git               |
| `C:\Users\Joys9\Reconnect`      | Unity-Client (ab Phase 2)                | Unity Version Control (Plastic) |

## Gesamtstack

- **Client:** Unity 6 **LTS** (aktuellste LTS-Linie, derzeit 6.3 LTS), URP, neues Input System.
  Das bestehende Unity-Projekt läuft noch auf 6000.1.12f1 (kein LTS) und wird vor Phase 2
  über den Unity Hub auf die LTS-Version gehoben.
- **Echtzeit in Räumen:** Photon Fusion 2 (ab Phase 3)
- **Backend:** ASP.NET Core Minimal APIs auf .NET 10
- **Datenbank:** PostgreSQL + PostGIS, EF Core mit Npgsql + NetTopologySuite
- **Cache/Präsenz:** Redis
- **Chat/Benachrichtigungen:** SignalR (`/hubs/chat`)
- **Auth:** ASP.NET Core Identity + JWT (Access Token + rotierender Refresh Token)
- **Dateien:** Azure Blob Storage (lokal: Azurite)
- **Lokale Orchestrierung:** Aspire 13
- **Tests:** xUnit, Integrationstests mit Testcontainers (postgis/postgis)

## Architektur

```
src/
  Reconnect.AppHost          Aspire: startet API, Postgres+PostGIS, Redis, Azurite
  Reconnect.ServiceDefaults  OpenTelemetry, Health Checks, Resilience (Aspire-Standard)
  Reconnect.Api              Minimal APIs, nach Features geschnitten (Vertical Slices)
    Common/                  Querschnitt: Endpoint-Discovery, Auth-Helfer, Validierung, Fehler
    Features/<Feature>/      Endpoints + Mapping + feature-spezifische Logik
    Hubs/                    SignalR-Hubs
  Reconnect.Domain           Entities + Geschäftsregeln. Keine Abhängigkeit auf EF/ASP.NET
                             (einzige Ausnahme: NetTopologySuite für Geometrien)
  Reconnect.Infrastructure   DbContext, EF-Konfigurationen, Migrations, Seed, Identity-User
  Reconnect.Contracts        DTOs für API + Unity. netstandard2.1, C# 9, KEINE Pakete
tests/
  Reconnect.Api.Tests        Integrationstests (WebApplicationFactory + Testcontainers)
```

Abhängigkeiten: `Api → Infrastructure → Domain`, `Api → Contracts`, `AppHost → Api`.
Domain und Contracts kennen sich nicht; das Mapping passiert in der Api (`*Mappings.cs`).

### Neues Feature hinzufügen

1. Entity in `Reconnect.Domain/<Bereich>/` anlegen (Factory-Methode `Create(...)`, Regeln im Entity).
2. `DbSet` in `ReconnectDbContext` + `IEntityTypeConfiguration<T>` in
   `Reconnect.Infrastructure/Persistence/Configurations/`.
3. Migration erzeugen (siehe unten).
4. DTOs in `Reconnect.Contracts/<Bereich>/`, Routen in `ApiRoutes`.
5. `Reconnect.Api/Features/<Bereich>/<Bereich>Endpoints.cs` mit einer Klasse, die
   `IEndpointModule` implementiert – sie wird automatisch gefunden und gemappt.
6. Integrationstest in `tests/Reconnect.Api.Tests/<Bereich>/`.

## Konventionen

- Sprache im Code: Englisch (Namen, Kommentare nur wo nötig). Doku/Commits dürfen Deutsch sein.
- IDs sind `Guid` (v7, zeitlich sortierbar: `Guid.CreateVersion7()`).
- Zeit immer über `TimeProvider` (testbar), gespeichert als UTC `DateTimeOffset`.
- Geodaten: SRID 4326 (WGS84), Spalten als `geography` → Distanzen in Metern.
  In Contracts nur `Latitude`/`Longitude` als `double`, nie NTS-Typen.
- Endpoints geben `TypedResults` zurück; Fehler als ProblemDetails / ValidationProblem.
- Kein Repository-Layer: Endpoints nutzen `ReconnectDbContext` direkt; wiederverwendete
  Abfragen als Extension-Methoden (z.B. `BlockQueries`).
- Sicherheitsregeln (Blocks!) nie im Client verlassen – jede Abfrage auf Nutzer/Räume muss
  Blocks berücksichtigen.
- Contracts: nur C# 9, keine Pakete, keine `System.Text.Json`-Attribute (Unity nutzt
  Newtonsoft.Json via `com.unity.nuget.newtonsoft-json`).
- Paketversionen nur in `Directory.Packages.props` (Central Package Management).
- **Keine Secrets im Code.** JWT-Signaturschlüssel = Aspire-Parameter `jwt-signing-key`
  (wird generiert und in den AppHost-User-Secrets gespeichert). Connection Strings kommen
  von Aspire.
- Kleine Commits mit klaren Messages. Vor jedem Commit: `dotnet build` und `dotnet test`.

## Befehle

```powershell
dotnet build                                   # alles bauen
dotnet test                                    # Tests (Docker muss laufen)
dotnet run --project src/Reconnect.AppHost     # alles lokal starten (Docker muss laufen)

# Neue Migration
dotnet ef migrations add <Name> --project src/Reconnect.Infrastructure --output-dir Persistence/Migrations
```

Migrationen werden in der Entwicklung beim Start der API automatisch angewendet.

## Roadmap

1. **Backend-Grundgerüst** – Auth, Profile, Räume, Gebäude (PostGIS), Likes/Matches, Blocks/Reports
2. **Unity-Client mit Login und Raumliste** – Upgrade auf Unity 6 LTS, Contracts-DLL einbinden
3. **Multiplayer-Raum mit Photon Fusion 2**
4. **Raum-Editor** (Layout speichern über `PUT /rooms/{id}/layout`)
5. **Likes/Matches/Chat im Client** (SignalR)
6. **Stadtquartier aus swisstopo-Daten**
7. **Face-Tracking**
8. **Videocall (LiveKit)**
